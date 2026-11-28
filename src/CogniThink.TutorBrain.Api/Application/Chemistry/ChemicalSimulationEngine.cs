namespace CogniThink.TutorBrain.Application.Chemistry;

using CogniThink.TutorBrain.Domain.Chemistry;

/// <summary>参与模拟的初始物质</summary>
public sealed class SimulationSpeciesInput
{
    /// <summary>物质 Id、名称、化学式或别名</summary>
    public string Substance { get; set; } = string.Empty;

    /// <summary>物质的量（mol），默认 1；水作溶剂时可不填</summary>
    public double Moles { get; set; } = 1;
}

/// <summary>分阶段加料：同一容器内先加一批、反应完后再加下一批</summary>
public sealed class SimulationStage
{
    public string Name { get; set; } = string.Empty;
    public List<SimulationSpeciesInput> Species { get; set; } = new();
}

/// <summary>模拟环境。未显式给出的条件按"不满足"处理，避免引擎凭空加热或点燃。</summary>
public sealed class SimulationRequest
{
    /// <summary>初始物质（第 0 阶段）</summary>
    public List<SimulationSpeciesInput> Species { get; set; } = new();

    /// <summary>后续阶段依次加入的物质，适合"先溶解、后沉淀"这类顺序实验</summary>
    public List<SimulationStage> Stages { get; set; } = new();

    /// <summary>水溶液体系；为 true 时自动补充足量水作溶剂</summary>
    public bool Aqueous { get; set; } = true;

    public bool Heating { get; set; }
    public bool Ignition { get; set; }
    public bool Catalyst { get; set; }
    public bool Electricity { get; set; }
    public bool Light { get; set; }
    public bool Concentrated { get; set; }
    public bool Dilute { get; set; }

    /// <summary>空气环境；为 true 时自动补充足量氧气</summary>
    public bool Air { get; set; } = true;

    public bool Acidic { get; set; }
    public bool Alkaline { get; set; }

    /// <summary>高温（区别于普通加热）；需高温的反应开 Heating 不满足</summary>
    public bool HighTemperature { get; set; }

    /// <summary>醇溶液体系（如卤代烃消去的 NaOH 乙醇溶液）</summary>
    public bool Alcoholic { get; set; }

    /// <summary>加压/高压环境（如合成氨）</summary>
    public bool Pressurized { get; set; }

    /// <summary>是否包含长期缓慢过程（默认 false：只展示快速过程）</summary>
    public bool IncludeSlow { get; set; }

    /// <summary>最大反应步数，防止病态数据导致死循环</summary>
    public int MaxSteps { get; set; } = 24;
}

/// <summary>反应后某物质的状态快照</summary>
public sealed record SimulationSpeciesState(
    string Id,
    string Name,
    string Formula,
    double InitialMoles,
    double FinalMoles,
    string AmountText,
    string State,
    string? Color,
    string? SolutionColor,
    string Solubility,
    string Role);

/// <summary>一次反应事件（可追溯到反应条目与计量依据）</summary>
public sealed record SimulationEvent(
    int Step,
    int StageIndex,
    string StageName,
    string ReactionId,
    string Equation,
    string Type,
    List<string> Conditions,
    List<string> Phenomena,
    Dictionary<string, double> Consumed,
    Dictionary<string, double> Produced,
    string SolutionColorAfter,
    List<string> Precipitates,
    List<string> Gases,
    string Evidence);

/// <summary>未发生但值得解释的反应（条件不满足等）</summary>
public sealed record SimulationSkippedReaction(
    string ReactionId,
    string Equation,
    string Reason);

/// <summary>状态推演结果</summary>
public sealed class SimulationResult
{
    public bool Converged { get; set; }
    public int Steps { get; set; }
    public List<SimulationEvent> Events { get; set; } = new();
    public List<SimulationSpeciesState> InitialSpecies { get; set; } = new();
    public List<SimulationSpeciesState> FinalSpecies { get; set; } = new();
    public string FinalSolutionColor { get; set; } = "无色";
    public List<string> FinalPrecipitates { get; set; } = new();
    public List<string> FinalGases { get; set; } = new();
    public List<string> FinalResidualGases { get; set; } = new();
    public List<string> FinalSolids { get; set; } = new();
    public string FinalStateText { get; set; } = string.Empty;
    public List<string> Phenomena { get; set; } = new();
    public List<string> Evidence { get; set; } = new();
    public List<SimulationSkippedReaction> SkippedReactions { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// 兼容适配器（保留旧签名）。新代码请使用 <see cref="ChemicalSimulator"/>。
/// 内部把 <see cref="SimulationRequest"/> 映射为 <see cref="ChemicalSystemInput"/>，
/// 委托 <see cref="ChemicalSimulator"/>，再把 <see cref="SimulationTrace"/> 映射回 <see cref="SimulationResult"/>。
/// 映射为纯字段转换，不改变任何数值，确保现有 Controller / 测试 / 反向引擎零改动、行为等价。
/// </summary>
public sealed class ChemicalSimulationEngine
{
    private readonly ChemicalKnowledgeBase _kb;
    private readonly ChemicalSimulator _simulator;

    public ChemicalSimulationEngine(ChemicalKnowledgeBase kb, ReactionEquationParser parser)
    {
        _kb = kb;
        _simulator = new ChemicalSimulator(kb, parser);
    }

    public SimulationResult Simulate(SimulationRequest request)
    {
        var input = MapToInput(request);
        var trace = _simulator.Simulate(input);
        return MapToLegacyResult(trace);
    }

    private static ChemicalSystemInput MapToInput(SimulationRequest request)
    {
        var input = new ChemicalSystemInput
        {
            Conditions = new ChemicalConditions
            {
                Aqueous = request.Aqueous,
                Heating = request.Heating,
                Ignition = request.Ignition,
                Catalyst = request.Catalyst,
                Electricity = request.Electricity,
                Light = request.Light,
                Concentrated = request.Concentrated,
                Dilute = request.Dilute,
                Air = request.Air,
                Acidic = request.Acidic,
                Alkaline = request.Alkaline,
                HighTemperature = request.HighTemperature,
                Alcoholic = request.Alcoholic,
                Pressurized = request.Pressurized,
                IncludeSlow = request.IncludeSlow,
                MaxSteps = request.MaxSteps,
                ReturnConsidered = false
            }
        };

        foreach (var s in request.Species)
            input.Species.Add(new SpeciesInput { Substance = s.Substance, Moles = s.Moles });

        foreach (var st in request.Stages)
        {
            var stage = new SystemStage { Name = st.Name };
            foreach (var s in st.Species)
                stage.Species.Add(new SpeciesInput { Substance = s.Substance, Moles = s.Moles });
            input.Stages.Add(stage);
        }

        return input;
    }

    private SimulationResult MapToLegacyResult(SimulationTrace trace)
    {
        var result = new SimulationResult
        {
            Converged = trace.Converged,
            Steps = trace.Steps,
            InitialSpecies = trace.InitialSpecies.Select(MapSpecies).ToList(),
            FinalSpecies = trace.FinalState.Species.Select(MapSpecies).ToList(),
            FinalSolutionColor = trace.FinalState.SolutionColor,
            FinalPrecipitates = trace.FinalState.Precipitates.ToList(),
            FinalGases = trace.FinalState.Gases.ToList(),
            FinalResidualGases = trace.FinalState.ResidualGases.ToList(),
            FinalSolids = trace.FinalState.Solids.ToList(),
            FinalStateText = trace.FinalState.Summary,
            Phenomena = trace.FinalState.Phenomena.ToList(),
            Evidence = trace.Evidence.ToList(),
            Warnings = trace.Warnings.ToList()
        };

        foreach (var e in trace.Events)
            result.Events.Add(MapEvent(e));

        foreach (var pair in trace.SkippedReasons)
        {
            var equation = _kb.Reactions.FirstOrDefault(r => r.Id == pair.Key)?.Equation ?? string.Empty;
            result.SkippedReactions.Add(new SimulationSkippedReaction(pair.Key, equation, pair.Value));
        }

        return result;
    }

    private static SimulationEvent MapEvent(ReactionEvent e) =>
        new(
            e.Step, e.StageIndex, e.StageName, e.ReactionId, e.Equation, e.Type,
            e.Conditions, e.Phenomena, e.Consumed, e.Produced,
            e.SolutionColorAfter, e.Precipitates, e.Gases,
            ChemicalSimulator.BuildEventEvidence(e));

    private static SimulationSpeciesState MapSpecies(SpeciesState s) =>
        new(s.Id, s.Name, s.Formula, s.InitialMoles, s.FinalMoles, s.AmountText,
            s.State, s.Color, s.SolutionColor, s.Solubility, s.Role);
}
