namespace CogniThink.TutorBrain.Application.Chemistry;

/// <summary>体系在某一步结束后的状态快照</summary>
public sealed class StateSnapshot
{
    public int StepIndex { get; set; }
    public string StageName { get; set; } = string.Empty;
    /// <summary>该步结束后的体系状态（物种量/颜色/沉淀/气体）</summary>
    public SystemState State { get; set; } = new();
    /// <summary>该步发生的反应事件（空表示该步无反应）</summary>
    public List<ReactionEvent> Events { get; set; } = new();
    /// <summary>可选：该步结束后体系的平衡态数值（pH/离子浓度/Ksp 残留等），由 ReasoningStep.Numerical 触发（见 §7）</summary>
    public EquilibriumNumericalResult? Numerical { get; set; }
}

public enum ReasoningStepKind { AddSpecies, SetConditions, Assert }

/// <summary>推理计划中的一步</summary>
public sealed class ReasoningStep
{
    public ReasoningStepKind Kind { get; set; }
    /// <summary>AddSpecies：追加的物种（在当前状态基础上加入，再模拟）</summary>
    public List<SpeciesInput>? Species { get; set; }
    /// <summary>SetConditions：全量替换的环境条件（不做增量合并）</summary>
    public ChemicalConditions? Conditions { get; set; }
    /// <summary>Assert：对当前状态的断言描述（不推进反应，仅记录到证据）</summary>
    public string? Assertion { get; set; }
    /// <summary>可选：在该步模拟完成后，对所得状态求哪些平衡态数值（见 EquilibriumQuery）；结果挂到本步 StateSnapshot.Numerical</summary>
    public EquilibriumQuery? Numerical { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class ReasoningPlan
{
    public List<ReasoningStep> Steps { get; set; } = new();
}

/// <summary>搜索目标（可组合，多项为 AND）</summary>
public sealed class ReasoningGoal
{
    public List<string> TargetProducts { get; set; } = new();
    public string? SolutionColor { get; set; }
    public string? PrecipitateColor { get; set; }
    public string? Gas { get; set; }
    public List<string> Phenomena { get; set; } = new();
    public int MaxDepth { get; set; } = 3;
    public int MaxCandidates { get; set; } = 6;
}

/// <summary>推理端点请求体（HTTP / 测试入口）：初始体系 + 推理计划</summary>
public sealed class ReasonRequest
{
    public ChemicalSystemInput Initial { get; set; } = new();
    public ReasoningPlan Plan { get; set; } = new();
}

/// <summary>
/// 化学多步推理模拟器。在 <see cref="ChemicalSimulator"/> 之上：
/// ① 按步骤计划进给，产出状态轨迹；
/// ② 对任一状态做 what-if 分支（纯函数，不污染原轨迹）；
/// ③ 目标导向搜索：从目标产物/现象反查候选路径并正向回放验证。
/// </summary>
public sealed class ChemicalMultiStepReasoningSimulator
{
    private const double Epsilon = 1e-9;

    private readonly ChemicalSimulator _simulator;
    private readonly ChemicalKnowledgeBase _kb;
    private readonly EquilibriumStateSimulator? _equilibrium;
    private ChemicalReverseInferenceEngine? _reverse;

    public ChemicalMultiStepReasoningSimulator(
        ChemicalSimulator simulator,
        ChemicalKnowledgeBase kb,
        EquilibriumStateSimulator? equilibrium = null,
        ChemicalReverseInferenceEngine? reverse = null)
    {
        _simulator = simulator;
        _kb = kb;
        _equilibrium = equilibrium;
        _reverse = reverse;
    }

    private ChemicalReverseInferenceEngine ReverseEngine =>
        _reverse ??= new ChemicalReverseInferenceEngine(_kb, new ChemicalSimulationEngine(_kb, new ReactionEquationParser(_kb)));

    // ==================== Reason — 状态轨迹 ====================

    public ReasoningResult Reason(ReasoningPlan plan, ChemicalSystemInput initial)
    {
        var result = new ReasoningResult();
        var conditions = initial.Conditions ?? new ChemicalConditions();

        // 基线：先模拟初始体系，得到第 0 个快照
        var baselineTrace = _simulator.Simulate(initial);
        var baseline = SnapshotFromTrace(baselineTrace, 0, "初始");
        result.Trajectory.Add(baseline);

        var currentSpecies = baseline.State.Species;
        var currentConditions = CloneConditions(conditions);
        var currentSourceInput = initial;

        if (!baselineTrace.Converged)
            result.Warnings.Add("初始体系模拟未收敛（达到最大步数），结果可能不完整。");

        for (var i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
            var stepIndex = i + 1;
            var stageName = string.IsNullOrWhiteSpace(step.Name) ? $"第 {stepIndex} 步" : step.Name;

            StateSnapshot snapshot;
            ChemicalSystemInput sourceInput;

            switch (step.Kind)
            {
                case ReasoningStepKind.AddSpecies:
                    sourceInput = BuildInput(currentSpecies, step.Species ?? new(), currentConditions);
                    break;
                case ReasoningStepKind.SetConditions:
                    // 全量替换（不增量合并）：未传字段取 ChemicalConditions 默认值
                    currentConditions = CloneConditions(step.Conditions ?? new ChemicalConditions());
                    sourceInput = BuildInput(currentSpecies, new List<SpeciesInput>(), currentConditions);
                    break;
                case ReasoningStepKind.Assert:
                    snapshot = new StateSnapshot
                    {
                        StepIndex = stepIndex,
                        StageName = stageName,
                        State = CloneState(baseline.State), // 占位，下方用 currentSpecies 重算
                        Events = new List<ReactionEvent>()
                    };
                    snapshot.State = CloneState(SnapshotFromSpecies(currentSpecies, currentConditions).State);
                    if (!string.IsNullOrWhiteSpace(step.Assertion))
                        result.Evidence.Add($"[断言·步骤 {stepIndex}] {step.Assertion}");
                    if (step.Numerical is not null)
                        snapshot.Numerical = AttachNumerical(snapshot, currentSourceInput, step.Numerical, result);
                    result.Trajectory.Add(snapshot);
                    continue;
                default:
                    result.Warnings.Add($"未知的推理步骤类型 {step.Kind}，已跳过。");
                    continue;
            }

            var trace = _simulator.Simulate(sourceInput);
            snapshot = SnapshotFromTrace(trace, stepIndex, stageName);
            currentSourceInput = sourceInput;

            if (step.Numerical is not null)
                snapshot.Numerical = AttachNumerical(snapshot, sourceInput, step.Numerical, result);

            if (!trace.Converged)
                result.Warnings.Add($"步骤 {stepIndex}（{stageName}）模拟未收敛（达到最大步数），结果可能不完整。");

            result.Trajectory.Add(snapshot);
            currentSpecies = snapshot.State.Species;
        }

        return result;
    }

    // ==================== QueryState — 状态查询 ====================

    public StateSnapshot? QueryState(IReadOnlyList<StateSnapshot> trajectory, Func<StateSnapshot, bool> predicate)
        => trajectory.FirstOrDefault(predicate);

    // ==================== WhatIf — 纯函数分支 ====================

    /// <summary>
    /// 从 <paramref name="from"/> 状态出发，在 <paramref name="alternative"/> 条件下做独立分支推演。
    /// 纯函数：不修改 <paramref name="from"/> 与原轨迹；环境物种由模拟器按条件自动补充。
    /// 已知限制：分支输入只带 Moles（不带浓度），仅用于反应推演，不挂载平衡数值（见设计 §6.5）。
    /// </summary>
    public SimulationTrace WhatIf(StateSnapshot from, ChemicalConditions alternative)
    {
        var branchInput = new ChemicalSystemInput
        {
            Species = from.State.Species
                .Where(s => s.FinalMoles > Epsilon)
                .Select(s => new SpeciesInput { Substance = s.Id, Moles = s.FinalMoles })
                .ToList(),
            Conditions = CloneConditions(alternative)
        };
        return _simulator.Simulate(branchInput);
    }

    // ==================== Search — 目标导向搜索（委托反向引擎） ====================

    /// <summary>
    /// 从 <paramref name="goal"/> 出发反查候选路径并正向回放验证。委托现有 <see cref="ChemicalReverseInferenceEngine"/>，
    /// 不重新实现反查与验证逻辑。无命中返回空列表；验证不通过的候选如实标注。
    /// </summary>
    public IReadOnlyList<ReasoningCandidate> Search(ChemicalSystemInput initial, ReasoningGoal goal)
    {
        var request = new ReverseInferenceRequest
        {
            Known = initial.Species.Select(s => s.Substance).Distinct(StringComparer.Ordinal).ToList(),
            TargetProducts = goal.TargetProducts,
            SolutionColor = goal.SolutionColor,
            PrecipitateColor = goal.PrecipitateColor,
            Gas = goal.Gas,
            Phenomena = goal.Phenomena,
            MaxDepth = goal.MaxDepth,
            MaxCandidates = goal.MaxCandidates,
            Conditions = MapToSimulationRequest(initial)
        };

        var reverseResult = ReverseEngine.Infer(request);
        var knownIds = initial.Species
            .Select(s => _kb.FindSubstance(s.Substance)?.Id)
            .Where(id => id is not null)
            .Select(id => id!)
            .ToHashSet(StringComparer.Ordinal);

        var candidates = new List<ReasoningCandidate>();
        foreach (var rc in reverseResult.Candidates)
        {
            var steps = DeriveSteps(rc, knownIds);
            var trace = ReplayCandidateTrace(rc, initial, knownIds);
            var verification = rc.Verification ?? new ReverseVerification(
                false, new List<string>(), string.Empty,
                new List<string>(), new List<string>(), new List<string>(), "未验证");
            candidates.Add(new ReasoningCandidate(steps, trace, verification));
        }

        return candidates;
    }

    private List<ReasoningStep> DeriveSteps(ReverseCandidate rc, IReadOnlySet<string> knownIds)
    {
        var reaction = _kb.Reactions.FirstOrDefault(r => r.Id == rc.ReactionId);
        var missing = reaction?.Reactants.Where(id => !knownIds.Contains(id)).ToList()
                      ?? new List<string>();
        var steps = new List<ReasoningStep>();
        if (missing.Count > 0)
        {
            steps.Add(new ReasoningStep
            {
                Kind = ReasoningStepKind.AddSpecies,
                Name = "补加反应物",
                Species = missing.Select(id => new SpeciesInput { Substance = id, Moles = 1 }).ToList()
            });
        }
        return steps;
    }

    private SimulationTrace ReplayCandidateTrace(ReverseCandidate rc, ChemicalSystemInput initial, IReadOnlySet<string> knownIds)
    {
        // 镜像反向引擎 Verify 的构造：已知 + 候选反应缺失反应物，各 1 mol，条件取 initial
        var reaction = _kb.Reactions.FirstOrDefault(r => r.Id == rc.ReactionId);
        var allIds = knownIds.Concat(
                (reaction?.Reactants ?? new List<string>()).Where(id => !knownIds.Contains(id)))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var input = new ChemicalSystemInput { Conditions = CloneConditions(initial.Conditions ?? new ChemicalConditions()) };
        foreach (var id in allIds)
            input.Species.Add(new SpeciesInput { Substance = id, Moles = 1 });
        return _simulator.Simulate(input);
    }

    // ==================== 辅助 ====================

    private static StateSnapshot SnapshotFromTrace(SimulationTrace trace, int stepIndex, string stageName) =>
        new()
        {
            StepIndex = stepIndex,
            StageName = stageName,
            State = trace.FinalState,
            Events = trace.Events.ToList()
        };

    private static StateSnapshot SnapshotFromSpecies(IReadOnlyList<SpeciesState> species, ChemicalConditions conditions)
    {
        // 仅用于 Assert 步骤：克隆当前状态，不重新模拟
        var state = new SystemState
        {
            Species = species.ToList(),
            SolutionColor = "无色"
        };
        return new StateSnapshot { State = state };
    }

    private ChemicalSystemInput BuildInput(
        IReadOnlyList<SpeciesState> currentSpecies,
        IReadOnlyList<SpeciesInput> added,
        ChemicalConditions conditions)
    {
        var input = new ChemicalSystemInput { Conditions = CloneConditions(conditions) };
        foreach (var s in currentSpecies.Where(s => s.FinalMoles > Epsilon))
            input.Species.Add(new SpeciesInput { Substance = s.Id, Moles = s.FinalMoles });
        foreach (var a in added)
            input.Species.Add(new SpeciesInput
            {
                Substance = a.Substance,
                Moles = a.Moles,
                Concentration = a.Concentration,
                Volume = a.Volume,
                Role = a.Role
            });
        return input;
    }

    private EquilibriumNumericalResult? AttachNumerical(
        StateSnapshot snap, ChemicalSystemInput sourceInput, EquilibriumQuery query, ReasoningResult result)
    {
        if (_equilibrium is null)
        {
            result.Warnings.Add($"步骤 {snap.StepIndex} 未注入 EquilibriumStateSimulator，无法计算平衡数值。");
            return null;
        }

        // 把源输入中带浓度的物种按 Id 匹配到终态物种，携带 Concentration/Volume（终态量用 FinalMoles）
        var sourceById = new Dictionary<string, SpeciesInput>(StringComparer.Ordinal);
        foreach (var si in sourceInput.Species)
        {
            var sub = _kb.FindSubstance(si.Substance);
            if (sub is not null) sourceById.TryAdd(sub.Id, si);
        }

        var stateInput = new ChemicalSystemInput { Conditions = CloneConditions(sourceInput.Conditions) };
        foreach (var s in snap.State.Species.Where(x => x.FinalMoles > Epsilon))
        {
            var species = new SpeciesInput { Substance = s.Id, Moles = s.FinalMoles };
            if (sourceById.TryGetValue(s.Id, out var src))
            {
                species.Concentration = src.Concentration;
                species.Volume = src.Volume;
            }
            stateInput.Species.Add(species);
        }

        var numerical = _equilibrium.Compute(stateInput, query);
        if (numerical.Status == EquilibriumDetermination.Underdetermined)
        {
            var reason = numerical.Warnings.Count > 0
                ? string.Join("；", numerical.Warnings)
                : "信息不足";
            result.Warnings.Add($"步骤 {snap.StepIndex} 平衡数值欠定：{reason}");
        }
        return numerical;
    }

    private static SimulationRequest MapToSimulationRequest(ChemicalSystemInput input)
    {
        var c = input.Conditions ?? new ChemicalConditions();
        var req = new SimulationRequest
        {
            Aqueous = c.Aqueous,
            Heating = c.Heating,
            Ignition = c.Ignition,
            Catalyst = c.Catalyst,
            Electricity = c.Electricity,
            Light = c.Light,
            Concentrated = c.Concentrated,
            Dilute = c.Dilute,
            Air = c.Air,
            Acidic = c.Acidic,
            Alkaline = c.Alkaline,
            MaxSteps = c.MaxSteps
        };
        foreach (var s in input.Species)
            req.Species.Add(new SimulationSpeciesInput { Substance = s.Substance, Moles = s.Moles });
        foreach (var st in input.Stages)
        {
            var stage = new SimulationStage { Name = st.Name };
            foreach (var s in st.Species)
                stage.Species.Add(new SimulationSpeciesInput { Substance = s.Substance, Moles = s.Moles });
            req.Stages.Add(stage);
        }
        return req;
    }

    private static ChemicalConditions CloneConditions(ChemicalConditions c) => new()
    {
        Aqueous = c.Aqueous,
        Heating = c.Heating,
        Ignition = c.Ignition,
        Catalyst = c.Catalyst,
        Electricity = c.Electricity,
        Light = c.Light,
        Concentrated = c.Concentrated,
        Dilute = c.Dilute,
        Air = c.Air,
        Acidic = c.Acidic,
        Alkaline = c.Alkaline,
        MaxSteps = c.MaxSteps,
        ReturnConsidered = c.ReturnConsidered
    };

    private static SystemState CloneState(SystemState s) => new()
    {
        Species = s.Species.ToList(),
        SolutionColor = s.SolutionColor,
        Precipitates = s.Precipitates.ToList(),
        Gases = s.Gases.ToList(),
        ResidualGases = s.ResidualGases.ToList(),
        Solids = s.Solids.ToList(),
        Phenomena = s.Phenomena.ToList(),
        Summary = s.Summary
    };
}

/// <summary>一条目标导向搜索候选：补料步骤 + 正向回放轨迹 + 反向引擎验证</summary>
public sealed record ReasoningCandidate(
    List<ReasoningStep> Steps,
    SimulationTrace Trace,
    ReverseVerification Verification);

/// <summary>多步推理结果</summary>
public sealed class ReasoningResult
{
    public List<StateSnapshot> Trajectory { get; set; } = new();
    public SystemState FinalState => Trajectory.LastOrDefault()?.State ?? new SystemState();
    public List<string> Evidence { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
