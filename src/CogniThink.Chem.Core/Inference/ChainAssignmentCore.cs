using CogniThink.Chem.Core.Reactions;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.Chem.Core.Inference;

/// <summary>
/// 链式赋值选项：过程/有机两引擎的全部行为差异在此参数化。
/// 文案逐字保留两套（禁止"顺手统一"）：固定串用 string，含运行时数值的用委托。
/// </summary>
internal sealed record ChainOptions
{
    /// <summary>推理步骤的 Source 名（引擎类名）。</summary>
    public required string SourceName { get; init; }

    /// <summary>知识条目 id 前缀过滤（有机="RX-OR-"；null=不过滤，过程引擎用全库）。</summary>
    public string? EntryIdPrefix { get; init; }

    /// <summary>证据形态：true=entry.Equation（过程）；false=entry.Id（有机，防泄露铁律）。</summary>
    public required bool EvidenceUsesEquation { get; init; }

    /// <summary>步骤匹配 Description 是否含"：{方程式}"后缀（过程含、有机不含）。</summary>
    public required bool StepDescriptionIncludesEquation { get; init; }

    /// <summary>是否启用通用现象约束段（有机：题给现象命中指示物种时起点或终点须能解释；过程仅"变为"对称约束）。</summary>
    public required bool FullPhenomenonConstraint { get; init; }

    /// <summary>无试剂无条件步骤的自反应措辞（过程="自身分解"，有机="自身转化"）。</summary>
    public required string SelfTransformText { get; init; }

    /// <summary>无步骤 Message。</summary>
    public required string NoSteps { get; init; }

    /// <summary>无标签 Message。</summary>
    public required string NoLabels { get; init; }

    /// <summary>首步介绍模板：(标签列表, 候选数) → 含标签数/标签串/候选数的插值文案。</summary>
    public required Func<List<string>, int, string> Intro { get; init; }

    /// <summary>回溯穷举无解时的步骤 Description。</summary>
    public required string ExhaustedStep { get; init; }

    /// <summary>无解 Message。</summary>
    public required string NoSolution { get; init; }

    /// <summary>最终赋值下失去支撑 Message 模板：(步号) → 文案。</summary>
    public required Func<int, string> LostSupport { get; init; }

    /// <summary>欠定 Message 模板：(解数) → 文案。</summary>
    public required Func<int, string> Underdetermined { get; init; }

    /// <summary>唯一解 Message。</summary>
    public required string Solved { get; init; }
}

/// <summary>
/// 链式赋值公共核心（过程/有机推断共用）：把字母标签 A/B/C… 回溯赋值到候选物种，
/// 使每一步"反应物（起点物种 + 试剂）→ 产物含终点物种"都能在知识库中找到条目支撑。
/// 流程：标签收集 → 回溯搜索（最多记 2 解）→ 按解计数输出 → 逐步 VerifyStep。
/// 两引擎差异（条目过滤/证据形态/现象约束/文案）全部经 <see cref="ChainOptions"/> 参数化，
/// 输出逐字节保持各引擎原样。
/// </summary>
internal static class ChainAssignmentCore
{
    /// <summary>
    /// 执行链式赋值推断。candidates 须已去重；有机引擎的候选白名单预检由调用方先行完成。
    /// 返回过程推断结果形状（两引擎结果字段同构），有机包装器负责转填自身结果类型。
    /// </summary>
    internal static ProcessInferenceResult Infer(
        ChainOptions options,
        List<string> candidates,
        List<ExperimentStep> steps,
        ReactionLibrary library)
    {
        var result = new ProcessInferenceResult();

        if (steps.Count == 0)
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = options.NoSteps;
            return result;
        }

        // 1. 收集字母标签：from/to 中的单字母 + 仅出现在试剂中的单字母（须非白名单物种）
        var labels = new List<string>();
        foreach (var step in steps)
        {
            foreach (var tag in new[] { step.From, step.To })
            {
                if (IsLabelToken(tag) && !labels.Contains(tag))
                {
                    labels.Add(tag);
                }
            }
        }

        foreach (var step in steps)
        {
            foreach (var reagent in step.Reagents)
            {
                var token = reagent.Trim();
                if (IsLabelToken(token) && !labels.Contains(token) && !IsKnownSpecies(token))
                {
                    labels.Add(token);
                }
            }
        }

        if (labels.Count == 0)
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = options.NoLabels;
            return result;
        }

        result.Steps.Add(new InferenceStep
        {
            Description = options.Intro(labels, candidates.Count),
            Source = options.SourceName
        });

        var phenomena = RuleStore.Load().Query<PhenomenaData>("phenomena").Items;

        // 2. 回溯搜索全部可行赋值（最多记 2 个，用于区分唯一/欠定）
        var solutions = new List<Dictionary<string, string>>();
        var assign = new Dictionary<string, string>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        Search(0);

        void Search(int index)
        {
            if (solutions.Count >= 2)
            {
                return;
            }

            if (index == labels.Count)
            {
                solutions.Add(new Dictionary<string, string>(assign));
                return;
            }

            foreach (var candidate in candidates)
            {
                if (!used.Add(candidate))
                {
                    continue;
                }

                assign[labels[index]] = candidate;
                if (PartiallyConsistent())
                {
                    Search(index + 1);
                }

                assign.Remove(labels[index]);
                used.Remove(candidate);
            }
        }

        bool PartiallyConsistent()
        {
            foreach (var step in steps)
            {
                var matches = VerifyStep(step, assign, library, phenomena, options);
                if (matches is not null && matches.Count == 0)
                {
                    return false;
                }
            }

            return true;
        }

        // 3. 按解计数输出
        if (solutions.Count == 0)
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = options.NoSolution;
            result.Steps.Add(new InferenceStep
            {
                Description = options.ExhaustedStep,
                Source = options.SourceName
            });
            return result;
        }

        var solution = solutions[0];
        result.Assignment = solution;
        foreach (var (label, species) in solution)
        {
            result.Steps.Add(new InferenceStep
            {
                Description = $"赋值 {label} = {species}",
                Source = options.SourceName
            });
        }

        foreach (var step in steps)
        {
            var matches = VerifyStep(step, solution, library, phenomena, options);
            if (matches is null || matches.Count == 0)
            {
                // 理论上不会发生（解已通过验证），防御性处理
                result.Status = InferenceStatus.NoSolution;
                result.Message = options.LostSupport(step.Step);
                return result;
            }

            var match = matches[0];
            var entry = match.Entry;
            result.StepResults.Add(new ProcessStepResult
            {
                Step = step.Step,
                Reaction = entry,
                Equation = entry.Equation,
                Explanation = $"第 {step.Step} 步：{step.From}（{solution.GetValueOrDefault(step.From, "?")}）"
                    + $"与 {DescribeReagents(step, solution, options.SelfTransformText)} 反应，"
                    + $"依据 {entry.Id}「{entry.Name}」，得到 {step.To}（{solution.GetValueOrDefault(step.To, "?")}）。"
            });
            result.Steps.Add(new InferenceStep
            {
                Description = options.StepDescriptionIncludesEquation
                    ? $"第 {step.Step} 步匹配 {entry.Id}「{entry.Name}」：{entry.Equation}"
                    : $"第 {step.Step} 步匹配 {entry.Id}「{entry.Name}」",
                Evidence = options.EvidenceUsesEquation ? entry.Equation : entry.Id,
                KnowledgeId = entry.KnowledgeId,
                Source = entry.Source
            });
        }

        if (solutions.Count > 1)
        {
            result.Status = InferenceStatus.Underdetermined;
            result.Message = options.Underdetermined(solutions.Count);
        }
        else
        {
            result.Status = InferenceStatus.Solved;
            result.Message = options.Solved;
        }

        return result;
    }

    /// <summary>
    /// 验证单步：返回匹配列表（按选项过滤条目 id 前缀）；null 表示赋值尚未完成（剪枝时视为通过）。
    /// </summary>
    private static List<ReactionMatch>? VerifyStep(
        ExperimentStep step,
        Dictionary<string, string> assign,
        ReactionLibrary library,
        List<PhenomenonEntry> phenomena,
        ChainOptions options)
    {
        if (!assign.TryGetValue(step.From, out var fromSpecies))
        {
            return null;
        }

        var reactants = new List<string> { fromSpecies };
        var tokens = new List<string>(step.Conditions);
        var (reagents, reagentTokens) = ReactionMatcher.NormalizeReagents(step.Reagents);
        tokens.AddRange(reagentTokens);

        foreach (var reagent in reagents)
        {
            if (IsLabelToken(reagent))
            {
                if (!assign.TryGetValue(reagent, out var species))
                {
                    return null;
                }

                reactants.Add(species);
            }
            else
            {
                reactants.Add(reagent);
            }
        }

        assign.TryGetValue(step.To, out var toSpecies);
        var matches = FilterByPrefix(ReactionMatcher.Match(reactants, tokens, library, toSpecies), options.EntryIdPrefix);

        // 无试剂的加热/点燃/高温步骤：单反应物无匹配时补入空气中的 O2 再试
        if (matches.Count == 0
            && step.Reagents.Count == 0
            && tokens.Any(t => t is "△" or "点燃" or "高温" or "加热"))
        {
            var withOxygen = new List<string>(reactants) { "O2" };
            matches = FilterByPrefix(ReactionMatcher.Match(withOxygen, tokens, library, toSpecies), options.EntryIdPrefix);
        }

        // 现象约束（有机）：题给现象若命中现象库指示物种，则起点或终点物种必须能解释该现象
        // （如"溴水褪色"指示 C2H4/C2H2/SO2，饱和烃赋值即被剪除）。
        if (options.FullPhenomenonConstraint && matches.Count > 0 && step.Phenomenon.Length > 0)
        {
            var indicated = ReactionMatcher.PhenomenaIndicated(step.Phenomenon, phenomena);
            if (indicated.Count > 0)
            {
                var fromExplains = ReactionMatcher.ExplainsIndication(fromSpecies, indicated);
                // 终点尚未赋值（回溯剪枝阶段）时推迟该约束，完整赋值后复检
                if (!fromExplains && toSpecies is not null
                    && !ReactionMatcher.ExplainsIndication(toSpecies, indicated))
                {
                    return new List<ReactionMatch>();
                }
            }
        }

        // 现象状态变化对称破缺："由 X 变为 Y" 要求起点物种能解释 X、终点物种能解释 Y。
        // 例：PROC-008 "溶液由浅绿色变为黄色" ⇒ 起点含 Fe^2+（FeCl2），终点含 Fe^3+（FeCl3），
        // 排除 A=Cl2/B=FeCl2 的镜像赋值。
        if (matches.Count > 0 && step.Phenomenon.Contains("变为"))
        {
            var index = step.Phenomenon.IndexOf("变为", StringComparison.Ordinal);
            var preIndicated = ReactionMatcher.PhenomenaIndicated(step.Phenomenon[..index], phenomena);
            var postIndicated = ReactionMatcher.PhenomenaIndicated(step.Phenomenon[(index + 2)..], phenomena);
            if (preIndicated.Count > 0 && !ReactionMatcher.ExplainsIndication(fromSpecies, preIndicated))
            {
                return new List<ReactionMatch>();
            }

            if (postIndicated.Count > 0 && toSpecies is not null
                && !ReactionMatcher.ExplainsIndication(toSpecies, postIndicated))
            {
                return new List<ReactionMatch>();
            }
        }

        return matches;
    }

    /// <summary>按条目 id 前缀过滤匹配列表（null=不过滤，过程引擎用全库；有机仅保留 RX-OR-* 条目）。</summary>
    private static List<ReactionMatch> FilterByPrefix(List<ReactionMatch> matches, string? idPrefix) =>
        idPrefix is null
            ? matches
            : matches.Where(m => m.Entry.Id.StartsWith(idPrefix, StringComparison.Ordinal)).ToList();

    private static string DescribeReagents(ExperimentStep step, Dictionary<string, string> assign, string selfTransformText)
    {
        if (step.Reagents.Count == 0)
        {
            return step.Conditions.Count > 0 ? $"（{string.Join("、", step.Conditions)}，空气中 O2 参与）" : $"（{selfTransformText}）";
        }

        var parts = new List<string>();
        foreach (var reagent in step.Reagents)
        {
            var token = reagent.Trim();
            parts.Add(IsLabelToken(token) && assign.TryGetValue(token, out var s) ? $"{token}（{s}）" : token);
        }

        return string.Join("、", parts);
    }

    /// <summary>单一大写字母视为字母标签候选。</summary>
    private static bool IsLabelToken(string text) =>
        text.Length == 1 && text[0] >= 'A' && text[0] <= 'Z';

    /// <summary>白名单中的单字母物种（如 C、S）不作为标签，按真实试剂处理。</summary>
    private static bool IsKnownSpecies(string formula)
    {
        try
        {
            var (known, _) = WhitelistGuard.Check(Chemistry.Species.Parse(formula));
            return known;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
