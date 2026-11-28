namespace CogniThink.TutorBrain.Application.Chemistry;

using System.Text.RegularExpressions;
using CogniThink.TutorBrain.Domain.Chemistry;

/// <summary>
/// 化学模拟器（正向，唯一主体类）。
/// 给定初始物种与环境条件，反复在知识库中检索"所有反应物都已存在、条件满足"的反应，
/// 按反应类型优先级与金属活动性择一发生，再按方程式系数计算限量试剂与剩余量，
/// 直到没有可发生的反应为止。输出每一步的反应事件（含参与物质、化学依据、优先级、定量）与最终体系状态。
/// 铁律：只使用知识库中已有反应；绝不凭空生成产物。
/// </summary>
public sealed class ChemicalSimulator
{
    private const double Epsilon = 1e-9;
    private const double EnvironmentMoles = 1000;
    private const int ConsideredCapPerStep = 50;

    private readonly ChemicalKnowledgeBase _kb;
    private readonly ReactionEquationParser _parser;

    public ChemicalSimulator(ChemicalKnowledgeBase kb, ReactionEquationParser parser)
    {
        _kb = kb;
        _parser = parser;
    }

    public SimulationTrace Simulate(ChemicalSystemInput input)
    {
        var trace = new SimulationTrace();
        var conditions = input.Conditions ?? new ChemicalConditions();
        var state = new Dictionary<string, SpeciesAmount>(StringComparer.Ordinal);
        var order = new List<string>();

        ResolveInputs(input, state, order, trace);

        if (conditions.Aqueous) EnsureEnvironment("H2O", state, order);
        if (conditions.Air) EnsureEnvironment("O2", state, order);

        var skipped = new Dictionary<string, string>(StringComparer.Ordinal);
        var converged = RunStage(conditions, state, order, trace, 0, "初始", skipped);

        for (var i = 0; i < input.Stages.Count; i++)
        {
            var stage = input.Stages[i];
            var stageIndex = i + 1;
            var stageName = string.IsNullOrWhiteSpace(stage.Name) ? $"第 {stageIndex} 阶段" : stage.Name;

            ResolveStageInputs(stage, state, order, trace);
            if (!RunStage(conditions, state, order, trace, stageIndex, stageName, skipped))
                converged = false;
        }

        trace.Converged = converged;
        trace.Steps = trace.Events.Count;
        trace.SkippedReactions = skipped.Keys.ToList();
        trace.SkippedReasons = new Dictionary<string, string>(skipped, StringComparer.Ordinal);
        trace.SkippedDetails = skipped
            .Select(pair =>
            {
                var equation = _kb.Reactions.FirstOrDefault(r => r.Id == pair.Key)?.Equation ?? string.Empty;
                return new SkippedReactionDetail(pair.Key, equation, pair.Value);
            })
            .ToList();

        BuildOutput(trace, conditions, state, order);
        trace.Warnings.AddRange(_parser.Warnings.Take(8));
        return trace;
    }

    /// <summary>在单一阶段内反复发生反应，直到没有可发生的反应</summary>
    private bool RunStage(
        ChemicalConditions conditions,
        Dictionary<string, SpeciesAmount> state,
        List<string> order,
        SimulationTrace trace,
        int stageIndex,
        string stageName,
        Dictionary<string, string> skipped)
    {
        // 每个阶段可重新触发的反应集合不同：新加入的物质可能让同一反应再次发生。
        var fired = new HashSet<string>(StringComparer.Ordinal);
        var returnConsidered = conditions.ReturnConsidered;

        for (var stepInStage = 1; stepInStage <= Math.Max(1, conditions.MaxSteps); stepInStage++)
        {
            var candidates = new List<Candidate>();
            var stepConsidered = returnConsidered ? new List<ConsideredCandidate>() : null;

            foreach (var reaction in _kb.Reactions)
            {
                if (fired.Contains(reaction.Id)) continue;

                var stoich = _parser.Get(reaction);

                if (!AllReactantsAvailable(reaction, stoich, state))
                {
                    if (returnConsidered && stepConsidered!.Count < ConsideredCapPerStep)
                        stepConsidered.Add(new ConsideredCandidate(reaction.Id, reaction.Equation, Score(reaction, stoich), "反应物不足"));
                    continue;
                }

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

                var conditionFailure = CheckConditions(reaction, stoich, conditions, state);
                if (conditionFailure is not null)
                {
                    if (skipped.Count < 16) skipped.TryAdd(reaction.Id, conditionFailure);
                    if (returnConsidered && stepConsidered!.Count < ConsideredCapPerStep)
                        stepConsidered.Add(new ConsideredCandidate(reaction.Id, reaction.Equation, Score(reaction, stoich), conditionFailure));
                    continue;
                }

                // 速率约束：长期缓慢过程默认不进入快速 trace，除非显式开启 includeSlow
                if (!conditions.IncludeSlow && IsSlowProcess(reaction))
                {
                    const string slowReason = "该反应为长期缓慢过程，默认不在快速模拟中展示（开启 includeSlow 可查看）";
                    if (skipped.Count < 16) skipped.TryAdd(reaction.Id, slowReason);
                    if (returnConsidered && stepConsidered!.Count < ConsideredCapPerStep)
                        stepConsidered.Add(new ConsideredCandidate(reaction.Id, reaction.Equation, Score(reaction, stoich), slowReason));
                    continue;
                }

                var extent = ComputeExtent(reaction, stoich, state);
                if (extent <= Epsilon)
                {
                    if (returnConsidered && stepConsidered!.Count < ConsideredCapPerStep)
                        stepConsidered.Add(new ConsideredCandidate(reaction.Id, reaction.Equation, Score(reaction, stoich), "反应进度≈0"));
                    continue;
                }

                candidates.Add(new Candidate(
                    reaction, stoich, extent,
                    Score(reaction, stoich),
                    BuildRationale(reaction, stoich, extent, state)));
            }

            if (candidates.Count == 0)
            {
                if (returnConsidered) trace.Considered.Add(stepConsidered!);
                return true;
            }

            var ranked = candidates
                .OrderByDescending(c => c.Score)
                .ThenBy(c => c.Reaction.Id, StringComparer.Ordinal)
                .ToList();
            var chosen = ranked[0];

            trace.Events.Add(Apply(chosen, state, order, trace.Events.Count + 1, stageIndex, stageName));

            if (returnConsidered)
            {
                foreach (var c in ranked.Skip(1))
                {
                    if (stepConsidered!.Count >= ConsideredCapPerStep) break;
                    stepConsidered.Add(new ConsideredCandidate(c.Reaction.Id, c.Reaction.Equation, c.Score, "被更高优先级反应胜出"));
                }
                trace.Considered.Add(stepConsidered!);
            }

            fired.Add(chosen.Reaction.Id);
        }

        return false;
    }

    // ==================== 输入与环境 ====================

    private void ResolveInputs(
        ChemicalSystemInput input,
        Dictionary<string, SpeciesAmount> state,
        List<string> order,
        SimulationTrace trace)
    {
        foreach (var species in input.Species)
            AddInput(species, state, order, trace);
    }

    private void ResolveStageInputs(
        SystemStage stage,
        Dictionary<string, SpeciesAmount> state,
        List<string> order,
        SimulationTrace trace)
    {
        foreach (var species in stage.Species)
            AddInput(species, state, order, trace);
    }

    private void AddInput(
        SpeciesInput input,
        Dictionary<string, SpeciesAmount> state,
        List<string> order,
        SimulationTrace trace)
    {
        var substance = _kb.FindSubstance(input.Substance);
        if (substance is null)
        {
            trace.Warnings.Add($"知识库中未找到物质「{input.Substance}」，已忽略。");
            return;
        }

        var moles = input.Moles > 0 ? input.Moles : 1;
        if (state.TryGetValue(substance.Id, out var existing))
        {
            existing.Remaining += moles;
            existing.InitialMoles += moles;
            existing.IsInput = true;
            existing.Role = input.Role ?? existing.Role;
        }
        else
        {
            state[substance.Id] = new SpeciesAmount
            {
                Id = substance.Id,
                InitialMoles = moles,
                Remaining = moles,
                IsInput = true,
                Role = input.Role
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
        ChemicalConditions conditions,
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
                    var failure = EvaluateCondition(alternative, stoich, conditions, state);
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

            var reason = EvaluateCondition(text, stoich, conditions, state);
            if (reason is not null) return reason;
        }

        return null;
    }

    private string? EvaluateCondition(
        string text,
        ReactionStoichiometry stoich,
        ChemicalConditions conditions,
        IReadOnlyDictionary<string, SpeciesAmount> state)
    {
        if (Has(text, "点燃", "燃烧", "引燃"))
            return conditions.Ignition ? null : "需要点燃/引燃，当前未提供该条件";

        // 高温（煅烧/灼烧）必须显式开启，普通加热不满足
        if (Has(text, "高温", "煅烧", "灼烧"))
            return conditions.HighTemperature ? null : $"需要高温（{text}），当前未提供高温条件";

        // 普通加热 / 水浴 / 指定温度（如 170℃）
        if (Has(text, "加热", "△", "水浴", "℃"))
            return conditions.Heating ? null : $"需要加热（{text}），当前未加热";

        if (Has(text, "水蒸气"))
            return conditions.Aqueous ? "需要高温水蒸气环境（非水溶液）" : null;

        // 催化剂：先判具体物质（要求真实存在于体系），再判通用催化剂
        if (TryResolveCatalystSubstance(text, out var catalystId))
        {
            if (!conditions.Catalyst)
                return $"需要催化剂（{text}），当前未提供催化条件";
            if (!state.TryGetValue(catalystId, out var cat) || cat.Remaining <= Epsilon)
                return $"需要催化剂 {text}，但体系中未加入该物质";
            return null;
        }
        if (Has(text, "催化剂", "酶"))
            return conditions.Catalyst ? null : $"需要催化剂（{text}）";

        if (Has(text, "通电", "电解", "放电"))
            return conditions.Electricity ? null : "需要通电/放电";

        if (Has(text, "光照", "强光"))
            return conditions.Light ? null : "需要光照";

        if (Has(text, "浓"))
            return conditions.Concentrated ? null : $"需要浓溶液（{text}）";

        if (Has(text, "稀"))
            return conditions.Dilute ? null : $"需要稀溶液（{text}）";

        if (Has(text, "酸性"))
            return conditions.Acidic ? null : "需要酸性条件";

        if (Has(text, "碱性"))
            return conditions.Alkaline ? null : "需要碱性条件";

        // 溶剂：醇溶液 vs 水溶液（先具体后宽泛，避免“醇溶液”被“溶液”分支吞掉）
        if (Has(text, "醇溶液"))
            return conditions.Alcoholic ? null : "需要醇溶液体系（当前为水溶液）";
        if (Has(text, "水溶液"))
            return conditions.Aqueous ? null : "需要水溶液体系";
        if (Has(text, "溶液"))
            return conditions.Aqueous ? null : "需要水溶液体系";

        if (Has(text, "空气"))
            return conditions.Air ? null : "需要空气（氧气）";

        if (Has(text, "加压", "高压"))
            return conditions.Pressurized ? null : $"需要加压/高压（{text}）";

        // 冰晶石（电解铝助熔剂）：要求体系中存在
        if (Has(text, "冰晶石") && TryResolveSubstanceId("冰晶石", out var cryoliteId))
        {
            if (!state.TryGetValue(cryoliteId, out var c) || c.Remaining <= Epsilon)
                return "需要冰晶石作助熔剂，体系中未加入";
            return null;
        }

        if (Has(text, "常温")) return null;

        if (TryParseRelativeAmount(text, out var mode, out var substanceId))
        {
            return RelativeAmountSatisfied(mode, substanceId, stoich, state)
                ? null
                : $"{text} 的用量关系不成立";
        }

        return null;
    }

    /// <summary>已知可作催化剂的物质（条件中出现时要求真实存在于体系）</summary>
    private static readonly HashSet<string> CatalystSubstances = new(StringComparer.Ordinal)
    {
        "MnO2", "V2O5", "Ni", "Pt", "Cu", "FeBr3", "H2SO4"
    };

    /// <summary>把条件中的催化剂物质 token（如 MnO2、浓H2SO4）解析为知识库物质 id；非催化剂返回 false</summary>
    private bool TryResolveCatalystSubstance(string text, out string substanceId)
    {
        substanceId = string.Empty;
        var core = text.Trim();
        if (core.StartsWith("浓", StringComparison.Ordinal)) core = core[1..].Trim();
        if (!CatalystSubstances.Contains(core)) return false;
        var substance = _kb.FindSubstance(core);
        if (substance is null) return false;
        substanceId = substance.Id;
        return true;
    }

    /// <summary>用知识库把名称解析为物质 id（冰晶石等助熔剂条件用）</summary>
    private bool TryResolveSubstanceId(string name, out string substanceId)
    {
        substanceId = string.Empty;
        var substance = _kb.FindSubstance(name);
        if (substance is null) return false;
        substanceId = substance.Id;
        return true;
    }

    private bool TryParseRelativeAmount(string text, out string mode, out string substanceId)
    {
        mode = string.Empty;
        substanceId = string.Empty;
        var trimmed = text.Trim();

        // 前缀：过量X / 足量X / 少量X
        foreach (var prefix in new[] { "过量", "足量", "少量" })
        {
            if (!trimmed.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var rest = trimmed[prefix.Length..].Trim();
            if (rest.Length == 0) return false;
            var substance = _kb.FindSubstance(rest);
            if (substance is null) return false;

            mode = prefix;
            substanceId = substance.Id;
            return true;
        }

        // 后缀：X过量 / X足量 / X少量 / X不足（数据中 CO2过量、SO2过量、O2不足 均为此形式）
        foreach (var suffix in new[] { "过量", "足量", "少量", "不足" })
        {
            if (!trimmed.EndsWith(suffix, StringComparison.Ordinal)) continue;
            var rest = trimmed[..^suffix.Length].Trim();
            if (rest.Length == 0) return false;
            var substance = _kb.FindSubstance(rest);
            if (substance is null) return false;

            mode = suffix;
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
            "不足" => targetRatio <= otherMin + Epsilon,
            _ => true
        };
    }

    // ==================== 反应速率：长期缓慢过程过滤 ====================

    /// <summary>金属单质（常温下与 O2 发生缓慢氧化）</summary>
    private static readonly HashSet<string> MetalElements = new(StringComparer.Ordinal)
    {
        "K", "Ca", "Na", "Mg", "Al", "Zn", "Fe", "Sn", "Pb", "Cu", "Ag", "Hg", "Au", "Pt"
    };

    /// <summary>明确指示长期腐蚀/生锈/风化过程的关键词（不出现在快速反应现象中）</summary>
    private static readonly Regex SlowCorrosionKeywords = new(
        @"生锈|锈蚀|腐蚀|风化|吸氧腐蚀", RegexOptions.Compiled);

    /// <summary>方程式中出现游离 O2 分子（O2 前不得为字母，避免误匹配 CO2/SO2；系数数字允许）</summary>
    private static bool FreeOxygenInEquation(string? equation) =>
        Regex.IsMatch(equation ?? string.Empty, @"(^|[^A-Za-z])O2\b");

    /// <summary>
    /// 判定是否为长期缓慢过程（默认不在快速 trace 中展示）：
    /// 1) 现象/方程式明确为腐蚀/生锈/风化；或
    /// 2) 常温（无条件或仅"常温"）下金属单质与 O2 的缓慢氧化（Na/Fe/Cu 等表面氧化）。
    /// 带"点燃/加热"等条件的燃烧、快速氧化不命中，避免误杀（如 2NO+O2、Na2O2 与水）。
    /// </summary>
    private static bool IsSlowProcess(Reaction reaction)
    {
        var blob = string.Join(' ', reaction.Phenomena ?? Enumerable.Empty<string>()) + " " + reaction.Equation;
        if (SlowCorrosionKeywords.IsMatch(blob)) return true;

        var onlyRoom = reaction.Conditions is null || reaction.Conditions.Count == 0 ||
                       (reaction.Conditions.Count == 1 && reaction.Conditions[0] == "常温");
        if (onlyRoom &&
            FreeOxygenInEquation(reaction.Equation) &&
            reaction.Reactants.Any(id => MetalElements.Contains(id)))
        {
            return true;
        }

        return false;
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

    /// <summary>限量试剂判定文本（与旧引擎 BuildRationale 保持一致，用于 Basis.SelectionReason）</summary>
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

    /// <summary>构造结构化化学依据（ReactionBasis）。KnowledgeIds 取自 Reaction.KnowledgeIds，不臆造。</summary>
    private static ReactionBasis BuildBasis(Reaction reaction, ReactionStoichiometry stoich, double extent,
        IReadOnlyDictionary<string, SpeciesAmount> state)
    {
        var selectionReason =
            $"类型 {reaction.Type}（优先级 {TypePriority(reaction.Type)}）；" +
            BuildRationale(reaction, stoich, extent, state);
        return new ReactionBasis(reaction.Id, reaction.KnowledgeIds, selectionReason);
    }

    // ==================== 反应执行 ====================

    private ReactionEvent Apply(
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

        var basis = BuildBasis(reaction, candidate.Stoich, extent, state);
        var priority = new ReactionPriority(candidate.Score, Rank: 1);

        return new ReactionEvent(
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
            extent,
            basis,
            priority,
            colorAfter,
            precipitates,
            gases);
    }

    /// <summary>把 ReactionEvent 还原为旧引擎风格的 evidence 字符串（供适配器与 trace.Evidence 共用，保持文本一致）</summary>
    internal static string BuildEventEvidence(ReactionEvent e)
    {
        var condText = e.Conditions.Count == 0
            ? "无条件（常温可进行）"
            : $"条件：{string.Join("、", e.Conditions)}";
        return $"[{e.StageName} · 步骤 {e.Step}] {e.ReactionId} {e.Equation}；{condText}；{e.Basis.SelectionReason}；现象：{string.Join("、", e.Phenomena)}";
    }

    // ==================== 结果汇总 ====================

    private void BuildOutput(
        SimulationTrace trace,
        ChemicalConditions conditions,
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

            var item = new SpeciesState(
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
                amount.Role ?? role);

            if (!amount.IsEnvironment && amount.InitialMoles > Epsilon)
                trace.InitialSpecies.Add(item);
            if (displayRemaining > Epsilon)
                trace.FinalState.Species.Add(item);
        }

        trace.FinalState.SolutionColor = conditions.Aqueous ? ComputeSolutionColor(state) : "非溶液体系";

        foreach (var item in trace.FinalState.Species)
        {
            var substance = _kb.FindSubstanceById(item.Id);
            if (substance is null) continue;

            if (substance.State == PhysicalState.Gas)
            {
                var gasText = $"{substance.Color ?? "无色"}{substance.Name}";
                if (item.InitialMoles <= Epsilon)
                    trace.FinalState.Gases.Add(gasText);
                else
                    trace.FinalState.ResidualGases.Add(gasText);
            }
            else if (IsPrecipitate(substance) && item.InitialMoles <= Epsilon)
            {
                trace.FinalState.Precipitates.Add($"{substance.Color ?? "未知颜色"}{substance.Name}");
            }
            else if (substance.State == PhysicalState.Solid &&
                     substance.Solubility is SolubilityLevel.Insoluble or SolubilityLevel.SlightlySoluble)
            {
                trace.FinalState.Solids.Add($"{substance.Color ?? "未知颜色"}{substance.Name}");
            }
        }

        trace.FinalState.Phenomena = trace.Events
            .SelectMany(e => e.Phenomena)
            .Distinct()
            .ToList();

        trace.FinalState.Summary = ComposeFinalState(conditions, trace.FinalState, trace.Events.Count);
        trace.Evidence.AddRange(trace.Events.Select(BuildEventEvidence));
        trace.Evidence.Add($"[结论] {trace.FinalState.Summary}");
    }

    private static string ComposeFinalState(ChemicalConditions conditions, SystemState final, int eventCount)
    {
        if (eventCount == 0)
            return "各物质混合后未检索到可发生的反应，体系保持混合状态。";

        var parts = new List<string>();
        if (conditions.Aqueous) parts.Add($"溶液呈{final.SolutionColor}");
        if (final.Precipitates.Count > 0)
            parts.Add($"生成{string.Join("、", final.Precipitates)}沉淀");
        if (final.Gases.Count > 0)
            parts.Add($"有{string.Join("、", final.Gases)}逸出");
        if (final.ResidualGases.Count > 0)
            parts.Add($"剩余气体：{string.Join("、", final.ResidualGases)}");
        if (final.Solids.Count > 0)
            parts.Add($"剩余固体：{string.Join("、", final.Solids)}");

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
        /// <summary>输入时携带的角色标签（可选），用于讲解归因；不影响推演</summary>
        public string? Role { get; set; }
    }

    private sealed record Candidate(
        Reaction Reaction,
        ReactionStoichiometry Stoich,
        double Extent,
        double Score,
        string Rationale);
}
