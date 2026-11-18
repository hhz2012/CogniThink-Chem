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
/// 化学反应状态推演引擎（正向）。
/// 给定初始物质与环境条件，反复在知识库中检索"所有反应物都已存在、条件满足"的反应，
/// 按反应类型优先级与金属活动性择一发生，再按方程式系数计算限量试剂与剩余量，
/// 直到没有可发生的反应为止。输出每一步的现象、颜色变化、沉淀/气体与最终体系状态。
/// 铁律：只使用知识库中已有反应；绝不凭空生成产物。
/// </summary>
public sealed class ChemicalSimulationEngine
{
    private const double Epsilon = 1e-9;
    private const double EnvironmentMoles = 1000;

    private readonly ChemicalKnowledgeBase _kb;
    private readonly ReactionEquationParser _parser;

    public ChemicalSimulationEngine(ChemicalKnowledgeBase kb, ReactionEquationParser parser)
    {
        _kb = kb;
        _parser = parser;
    }

    public SimulationResult Simulate(SimulationRequest request)
    {
        var result = new SimulationResult();
        var state = new Dictionary<string, SpeciesAmount>(StringComparer.Ordinal);
        var order = new List<string>();

        ResolveInputs(request, state, order, result);

        if (request.Aqueous) EnsureEnvironment("H2O", state, order);
        if (request.Air) EnsureEnvironment("O2", state, order);

        var skipped = new Dictionary<string, string>(StringComparer.Ordinal);
        var converged = RunStage(request, state, order, result, 0, "初始", skipped);

        for (var i = 0; i < request.Stages.Count; i++)
        {
            var stage = request.Stages[i];
            var stageIndex = i + 1;
            var stageName = string.IsNullOrWhiteSpace(stage.Name) ? $"第 {stageIndex} 阶段" : stage.Name;

            ResolveStageInputs(stage, state, order, result);
            if (!RunStage(request, state, order, result, stageIndex, stageName, skipped))
                converged = false;
        }

        result.Converged = converged;
        result.Steps = result.Events.Count;
        result.SkippedReactions = skipped
            .Select(pair => new SimulationSkippedReaction(
                pair.Key,
                _kb.Reactions.First(r => r.Id == pair.Key).Equation,
                pair.Value))
            .ToList();

        BuildOutput(result, request, state, order);
        result.Warnings.AddRange(_parser.Warnings.Take(8));
        return result;
    }

    /// <summary>在单一阶段内反复发生反应，直到没有可发生的反应</summary>
    private bool RunStage(
        SimulationRequest request,
        Dictionary<string, SpeciesAmount> state,
        List<string> order,
        SimulationResult result,
        int stageIndex,
        string stageName,
        Dictionary<string, string> skipped)
    {
        // 每个阶段可重新触发的反应集合不同：新加入的物质可能让同一反应再次发生。
        var fired = new HashSet<string>(StringComparer.Ordinal);

        for (var stepInStage = 1; stepInStage <= Math.Max(1, request.MaxSteps); stepInStage++)
        {
            var candidates = new List<Candidate>();
            foreach (var reaction in _kb.Reactions)
            {
                if (fired.Contains(reaction.Id)) continue;

                var stoich = _parser.Get(reaction);
                if (!AllReactantsAvailable(reaction, stoich, state))
                    continue;

                // 可逆且气体被溶液吸收的反应（如 CO2 + H2O ⇌ H2CO3、Cl2 + H2O ⇌ 盐酸/次氯酸）
                // 按平衡处理，不把气体按化学计量完全消耗；气相可逆反应（如 SO2 氧化）仍允许正向进行。
                if (reaction.Reversible &&
                    reaction.Reactants.Any(id => _kb.FindSubstanceById(id)?.State == PhysicalState.Gas) &&
                    reaction.Products.Any(id => _kb.FindSubstanceById(id)?.State == PhysicalState.Aqueous))
                {
                    if (skipped.Count < 16)
                        skipped.TryAdd(reaction.Id, "可逆反应且气体被溶液吸收，按平衡处理，不按完全反应消耗");
                    continue;
                }

                var conditionFailure = CheckConditions(reaction, stoich, request, state);
                if (conditionFailure is not null)
                {
                    if (skipped.Count < 16) skipped.TryAdd(reaction.Id, conditionFailure);
                    continue;
                }

                var extent = ComputeExtent(reaction, stoich, state);
                if (extent <= Epsilon) continue;

                candidates.Add(new Candidate(
                    reaction, stoich, extent,
                    Score(reaction, stoich),
                    BuildRationale(reaction, stoich, extent, state)));
            }

            if (candidates.Count == 0)
                return true;

            var chosen = candidates
                .OrderByDescending(c => c.Score)
                .ThenBy(c => c.Reaction.Id, StringComparer.Ordinal)
                .First();

            result.Events.Add(Apply(chosen, state, order, result.Events.Count + 1, stageIndex, stageName));
            fired.Add(chosen.Reaction.Id);
        }

        return false;
    }

    // ==================== 输入与环境 ====================

    private void ResolveInputs(
        SimulationRequest request,
        Dictionary<string, SpeciesAmount> state,
        List<string> order,
        SimulationResult result)
    {
        foreach (var input in request.Species)
            AddInput(input, state, order, result);
    }

    private void ResolveStageInputs(
        SimulationStage stage,
        Dictionary<string, SpeciesAmount> state,
        List<string> order,
        SimulationResult result)
    {
        foreach (var input in stage.Species)
            AddInput(input, state, order, result);
    }

    private void AddInput(
        SimulationSpeciesInput input,
        Dictionary<string, SpeciesAmount> state,
        List<string> order,
        SimulationResult result)
    {
        var substance = _kb.FindSubstance(input.Substance);
        if (substance is null)
        {
            result.Warnings.Add($"知识库中未找到物质「{input.Substance}」，已忽略。");
            return;
        }

        var moles = input.Moles > 0 ? input.Moles : 1;
        if (state.TryGetValue(substance.Id, out var existing))
        {
            existing.Remaining += moles;
            existing.InitialMoles += moles;
            existing.IsInput = true;
        }
        else
        {
            state[substance.Id] = new SpeciesAmount
            {
                Id = substance.Id,
                InitialMoles = moles,
                Remaining = moles,
                IsInput = true
            };
            order.Add(substance.Id);
        }
    }

    private void EnsureEnvironment(
        string substanceId,
        Dictionary<string, SpeciesAmount> state,
        List<string> order)
    {
        if (_kb.FindSubstanceById(substanceId) is null) return;
        if (state.ContainsKey(substanceId)) return;

        state[substanceId] = new SpeciesAmount
        {
            Id = substanceId,
            InitialMoles = EnvironmentMoles,
            Remaining = EnvironmentMoles,
            IsEnvironment = true
        };
        order.Add(substanceId);
    }

    // ==================== 反应筛选 ====================

    private static bool AllReactantsAvailable(
        Reaction reaction,
        ReactionStoichiometry stoich,
        IReadOnlyDictionary<string, SpeciesAmount> state) =>
        reaction.Reactants.All(id =>
            state.TryGetValue(id, out var amount) && amount.Remaining > Epsilon);

    /// <summary>返回 null 表示条件满足；否则返回未满足原因</summary>
    private string? CheckConditions(
        Reaction reaction,
        ReactionStoichiometry stoich,
        SimulationRequest request,
        IReadOnlyDictionary<string, SpeciesAmount> state)
    {
        foreach (var raw in reaction.Conditions)
        {
            var text = raw.Trim();
            if (text.Length == 0) continue;

            // "稀硫酸或酶" 这类并列条件，满足任意一个分支即可。
            var alternatives = text.Split('或', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => part.Length > 0)
                .ToList();

            if (alternatives.Count > 1)
            {
                string? firstFailure = null;
                var satisfied = false;
                foreach (var alternative in alternatives)
                {
                    var failure = EvaluateCondition(alternative, stoich, request, state);
                    if (failure is null)
                    {
                        satisfied = true;
                        break;
                    }

                    firstFailure ??= failure;
                }

                if (!satisfied) return firstFailure;
                continue;
            }

            var reason = EvaluateCondition(text, stoich, request, state);
            if (reason is not null) return reason;
        }

        return null;
    }

    private string? EvaluateCondition(
        string text,
        ReactionStoichiometry stoich,
        SimulationRequest request,
        IReadOnlyDictionary<string, SpeciesAmount> state)
    {
        if (Has(text, "点燃", "燃烧", "引燃"))
            return request.Ignition ? null : "需要点燃/引燃，当前未提供该条件";

        if (Has(text, "高温", "煅烧", "灼烧", "加热", "△", "℃"))
            return request.Heating ? null : $"需要加热（{text}），当前未加热";

        if (Has(text, "水蒸气"))
            return request.Aqueous ? "需要高温水蒸气环境（非水溶液）" : null;

        if (Has(text, "催化剂", "酶") || IsCatalystSubstance(text))
            return request.Catalyst ? null : $"需要催化剂（{text}）";

        if (Has(text, "通电", "电解"))
            return request.Electricity ? null : "需要通电";

        if (Has(text, "光照", "强光"))
            return request.Light ? null : "需要光照";

        if (Has(text, "浓"))
            return request.Concentrated ? null : $"需要浓溶液（{text}）";

        if (Has(text, "稀"))
            return request.Dilute ? null : $"需要稀溶液（{text}）";

        if (Has(text, "酸性"))
            return request.Acidic ? null : "需要酸性条件";

        if (Has(text, "碱性"))
            return request.Alkaline ? null : "需要碱性条件";

        if (Has(text, "溶液"))
            return request.Aqueous ? null : "需要水溶液体系";

        if (Has(text, "空气"))
            return request.Air ? null : "需要空气（氧气）";

        if (Has(text, "常温")) return null;

        if (TryParseRelativeAmount(text, out var mode, out var substanceId))
        {
            return RelativeAmountSatisfied(mode, substanceId, stoich, state)
                ? null
                : $"{text} 的用量关系不成立";
        }

        return null;
    }

    /// <summary>把 MnO2、V2O5 等条件中出现的催化剂物质识别为催化条件</summary>
    private static bool IsCatalystSubstance(string text) =>
        text is "MnO2" or "V2O5" or "Ni" or "Pt";

    private bool TryParseRelativeAmount(string text, out string mode, out string substanceId)
    {
        mode = string.Empty;
        substanceId = string.Empty;

        foreach (var prefix in new[] { "过量", "足量", "少量" })
        {
            if (!text.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var rest = text[prefix.Length..].Trim();
            var substance = _kb.FindSubstance(rest);
            if (substance is null) return false;

            mode = prefix;
            substanceId = substance.Id;
            return true;
        }

        return false;
    }

    private static bool RelativeAmountSatisfied(
        string mode,
        string substanceId,
        ReactionStoichiometry stoich,
        IReadOnlyDictionary<string, SpeciesAmount> state)
    {
        if (!state.TryGetValue(substanceId, out var target) || target.Remaining <= Epsilon)
            return false;

        var targetRatio = target.Remaining / stoich.CoefficientOfReactant(substanceId);
        var otherMin = double.PositiveInfinity;
        foreach (var otherId in stoich.Reactants.Keys.Where(id => id != substanceId))
        {
            if (!state.TryGetValue(otherId, out var other) || other.Remaining <= Epsilon)
                return false;
            otherMin = Math.Min(otherMin, other.Remaining / stoich.CoefficientOfReactant(otherId));
        }

        if (double.IsPositiveInfinity(otherMin)) return true;

        return mode switch
        {
            // "过量X"：对已经写了过量计量比的反应，X 不少于该反应所需即可触发
            // （例如 AlCl3 + 4NaOH → NaAlO2，n(NaOH)≥4n(AlCl3) 即为过量）。
            "过量" => targetRatio >= otherMin - Epsilon,
            "足量" => targetRatio >= otherMin - Epsilon,
            "少量" => targetRatio <= otherMin + Epsilon,
            _ => true
        };
    }

    private static double ComputeExtent(
        Reaction reaction,
        ReactionStoichiometry stoich,
        IReadOnlyDictionary<string, SpeciesAmount> state)
    {
        var extent = double.PositiveInfinity;
        foreach (var id in reaction.Reactants)
        {
            var available = state[id].Remaining;
            var coefficient = stoich.CoefficientOfReactant(id);
            extent = Math.Min(extent, available / coefficient);
        }

        return double.IsPositiveInfinity(extent) ? 0 : extent;
    }

    /// <summary>反应优先级：类型驱动，再按反应物数量、金属活动性做同类型竞争裁决</summary>
    private static double Score(Reaction reaction, ReactionStoichiometry stoich)
    {
        var score = (double)TypePriority(reaction.Type);
        score += reaction.Reactants.Count * 2;
        score += MetalBonus(reaction);
        if (stoich.IsFallback) score -= 1;
        if (reaction.Reversible) score -= 0.5;
        return score;
    }

    private static int TypePriority(ReactionType type) => type switch
    {
        ReactionType.Neutralization => 100,
        ReactionType.Complexation => 95,
        ReactionType.Precipitation => 90,
        ReactionType.GasEvolution => 85,
        ReactionType.Metathesis => 80,
        ReactionType.Displacement => 75,
        ReactionType.Redox => 70,
        ReactionType.Oxidation => 65,
        ReactionType.Combination => 60,
        ReactionType.Decomposition => 55,
        ReactionType.Esterification => 40,
        ReactionType.Addition => 35,
        ReactionType.Substitution => 35,
        ReactionType.Hydrolysis => 30,
        _ => 20
    };

    private static readonly Dictionary<string, int> MetalActivity = new(StringComparer.Ordinal)
    {
        ["K"] = 100, ["Ca"] = 95, ["Na"] = 90, ["Mg"] = 80, ["Al"] = 75,
        ["Zn"] = 60, ["Fe"] = 50, ["Sn"] = 40, ["Pb"] = 35, ["Cu"] = 20, ["Ag"] = 5
    };

    private static double MetalBonus(Reaction reaction)
    {
        var best = 0;
        foreach (var id in reaction.Reactants)
        {
            if (MetalActivity.TryGetValue(id, out var rank) && rank > best) best = rank;
        }

        return best / 10.0;
    }

    private static string BuildRationale(
        Reaction reaction,
        ReactionStoichiometry stoich,
        double extent,
        IReadOnlyDictionary<string, SpeciesAmount> state)
    {
        var reactantPart = string.Join("、", reaction.Reactants.Select(id =>
        {
            var amount = state[id];
            var coefficient = stoich.CoefficientOfReactant(id);
            return amount.IsEnvironment
                ? $"{id}（环境足量）"
                : $"{id}（余 {FormatMoles(amount.Remaining)} mol / 系数 {coefficient}）";
        }));

        var limiting = reaction.Reactants
            .Select(id => (Id: id, Ratio: state[id].Remaining / stoich.CoefficientOfReactant(id)))
            .Where(x => !state[x.Id].IsEnvironment)
            .OrderBy(x => x.Ratio)
            .Select(x => x.Id)
            .FirstOrDefault();

        return $"反应物齐备：{reactantPart}；按系数比可进行 {FormatMoles(extent)} mol 反应" +
               (limiting is null ? string.Empty : $"，{limiting} 为限量试剂");
    }

    // ==================== 反应执行 ====================

    private SimulationEvent Apply(
        Candidate candidate,
        Dictionary<string, SpeciesAmount> state,
        List<string> order,
        int step,
        int stageIndex,
        string stageName)
    {
        var reaction = candidate.Reaction;
        var extent = candidate.Extent;
        var consumed = new Dictionary<string, double>(StringComparer.Ordinal);
        var produced = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var id in reaction.Reactants)
        {
            var coefficient = candidate.Stoich.CoefficientOfReactant(id);
            var moles = extent * coefficient;
            state[id].Remaining = Math.Max(0, state[id].Remaining - moles);
            state[id].ConsumedMoles += moles;
            consumed[id] = moles;
        }

        foreach (var id in reaction.Products)
        {
            var coefficient = candidate.Stoich.Products.TryGetValue(id, out var c) ? c : 1;
            var moles = extent * coefficient;
            produced[id] = moles;

            if (state.TryGetValue(id, out var existing))
            {
                existing.Remaining += moles;
                existing.ProducedMoles += moles;
            }
            else
            {
                state[id] = new SpeciesAmount
                {
                    Id = id,
                    InitialMoles = 0,
                    Remaining = moles,
                    ProducedMoles = moles
                };
                order.Add(id);
            }
        }

        var precipitates = reaction.Products
            .Select(_kb.FindSubstanceById)
            .Where(s => s is not null && IsPrecipitate(s))
            .Select(s => $"{s!.Color ?? "未知颜色"}{s.Name}")
            .Distinct()
            .ToList();

        var gases = reaction.Products
            .Select(_kb.FindSubstanceById)
            .Where(s => s is not null && s.State == PhysicalState.Gas)
            .Select(s => s!.Name)
            .Distinct()
            .ToList();

        var colorAfter = ComputeSolutionColor(state);

        var evidence =
            $"[{stageName} · 步骤 {step}] {reaction.Id} {reaction.Equation}；" +
            (reaction.Conditions.Count == 0 ? "无条件（常温可进行）" : $"条件：{string.Join("、", reaction.Conditions)}") +
            $"；{candidate.Rationale}；现象：{string.Join("、", reaction.Phenomena)}";

        return new SimulationEvent(
            step,
            stageIndex,
            stageName,
            reaction.Id,
            reaction.Equation,
            reaction.Type.ToString(),
            reaction.Conditions.ToList(),
            reaction.Phenomena.ToList(),
            consumed,
            produced,
            colorAfter,
            precipitates,
            gases,
            evidence);
    }

    // ==================== 结果汇总 ====================

    private void BuildOutput(
        SimulationResult result,
        SimulationRequest request,
        Dictionary<string, SpeciesAmount> state,
        List<string> order)
    {
        foreach (var id in order)
        {
            var amount = state[id];
            var substance = _kb.FindSubstanceById(id);
            if (substance is null) continue;

            double displayInitial;
            double displayRemaining;
            string role;
            string amountText;

            if (amount.IsEnvironment)
            {
                // 环境中的水/氧气若被反应生成，按"净生成量"展示，避免产物被溶剂基线吞掉。
                var netProduced = amount.ProducedMoles - amount.ConsumedMoles;
                if (netProduced <= Epsilon) continue;

                displayInitial = 0;
                displayRemaining = Round(netProduced);
                role = "生成";
                amountText = $"生成 {FormatMoles(netProduced)} mol";
            }
            else
            {
                var isConsumed = amount.Remaining <= Epsilon;
                displayInitial = Round(amount.InitialMoles);
                displayRemaining = Round(amount.Remaining);
                role = amount.InitialMoles <= Epsilon ? "生成"
                    : isConsumed ? "已消耗"
                    : "剩余";
                amountText = amount.InitialMoles <= Epsilon
                    ? $"生成 {FormatMoles(amount.Remaining)} mol"
                    : isConsumed
                        ? "已完全消耗"
                        : $"剩余 {FormatMoles(amount.Remaining)} mol";
            }

            var item = new SimulationSpeciesState(
                substance.Id,
                substance.Name,
                substance.Formula,
                displayInitial,
                displayRemaining,
                amountText,
                substance.State.ToString(),
                substance.Color,
                substance.SolutionColor,
                ChemicalInferenceService.SolubilityText(substance.Solubility),
                role);

            if (!amount.IsEnvironment && amount.InitialMoles > Epsilon)
                result.InitialSpecies.Add(item);
            if (displayRemaining > Epsilon)
                result.FinalSpecies.Add(item);
        }

        result.FinalSolutionColor = request.Aqueous ? ComputeSolutionColor(state) : "非溶液体系";

        foreach (var item in result.FinalSpecies)
        {
            var substance = _kb.FindSubstanceById(item.Id);
            if (substance is null) continue;

            if (substance.State == PhysicalState.Gas)
            {
                var gasText = $"{substance.Color ?? "无色"}{substance.Name}";
                if (item.InitialMoles <= Epsilon)
                    result.FinalGases.Add(gasText);
                else
                    result.FinalResidualGases.Add(gasText);
            }
            else if (IsPrecipitate(substance) && item.InitialMoles <= Epsilon)
            {
                result.FinalPrecipitates.Add($"{substance.Color ?? "未知颜色"}{substance.Name}");
            }
            else if (substance.State == PhysicalState.Solid &&
                     substance.Solubility is SolubilityLevel.Insoluble or SolubilityLevel.SlightlySoluble)
            {
                result.FinalSolids.Add($"{substance.Color ?? "未知颜色"}{substance.Name}");
            }
        }

        result.Phenomena = result.Events
            .SelectMany(e => e.Phenomena)
            .Distinct()
            .ToList();

        result.FinalStateText = ComposeFinalState(request, result);
        result.Evidence.AddRange(result.Events.Select(e => e.Evidence));
        result.Evidence.Add($"[结论] {result.FinalStateText}");
    }

    private static string ComposeFinalState(SimulationRequest request, SimulationResult result)
    {
        if (result.Events.Count == 0)
            return "各物质混合后未检索到可发生的反应，体系保持混合状态。";

        var parts = new List<string>();
        if (request.Aqueous) parts.Add($"溶液呈{result.FinalSolutionColor}");
        if (result.FinalPrecipitates.Count > 0)
            parts.Add($"生成{string.Join("、", result.FinalPrecipitates)}沉淀");
        if (result.FinalGases.Count > 0)
            parts.Add($"有{string.Join("、", result.FinalGases)}逸出");
        if (result.FinalResidualGases.Count > 0)
            parts.Add($"剩余气体：{string.Join("、", result.FinalResidualGases)}");
        if (result.FinalSolids.Count > 0)
            parts.Add($"剩余固体：{string.Join("、", result.FinalSolids)}");

        return parts.Count == 0
            ? "反应后体系无明显颜色或状态变化。"
            : string.Join("；", parts) + "。";
    }

    private string ComputeSolutionColor(IReadOnlyDictionary<string, SpeciesAmount> state)
    {
        var colors = state.Values
            .Where(a => a.Remaining > Epsilon)
            .Select(a => _kb.FindSubstanceById(a.Id))
            .Where(s => s is not null &&
                        s.State != PhysicalState.Gas &&
                        !string.IsNullOrWhiteSpace(s.SolutionColor))
            .Select(s => s!.SolutionColor!)
            .Distinct()
            .ToList();

        return colors.Count == 0 ? "无色" : string.Join("、", colors);
    }

    // 沉淀 = 反应生成的不溶性固体（不限于盐/碱，还包括金属、氧化物、有机物等）
    private static bool IsPrecipitate(Substance substance) =>
        substance.Solubility == SolubilityLevel.Insoluble &&
        substance.State == PhysicalState.Solid;

    private static bool Has(string text, params string[] keywords) =>
        keywords.Any(k => text.Contains(k, StringComparison.Ordinal));

    private static double Round(double value) => Math.Round(value, 4);

    private static string FormatMoles(double moles) =>
        moles >= 100 ? moles.ToString("0") : moles.ToString("0.###");

    private sealed class SpeciesAmount
    {
        public string Id { get; init; } = string.Empty;
        public double InitialMoles { get; set; }
        public double Remaining { get; set; }
        public double ConsumedMoles { get; set; }
        public double ProducedMoles { get; set; }
        public bool IsInput { get; set; }
        public bool IsEnvironment { get; set; }
    }

    private sealed record Candidate(
        Reaction Reaction,
        ReactionStoichiometry Stoich,
        double Extent,
        double Score,
        string Rationale);
}
