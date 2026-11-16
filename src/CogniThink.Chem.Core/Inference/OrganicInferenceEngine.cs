using CogniThink.Chem.Core.Reactions;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.Chem.Core.Inference;

/// <summary>
/// 有机推断引擎：把字母标签 A/B/C… 回溯赋值到候选有机物，
/// 使每一步"反应物（起点物种 + 试剂）→ 产物含终点物种"都能在有机反应知识库
/// （RX-OR-* 条目）中找到支撑；现象约束要求起点或终点物种能解释题给现象。
/// 解计数：唯一解 → Solved；多解 → Underdetermined；无解 → NoSolution。
/// 铁律：每一步方程式都来自知识库条目，绝不编造。
/// 约定：
/// <list type="bullet">
/// <item>试剂可以是字母标签（如 ORG-H01 第 4 步的 B），与 from/to 标签一同参与赋值；
/// 单字母试剂若不是已知白名单物种则视为标签。</item>
/// <item>无试剂且带 △/点燃/高温 条件的步骤，若单反应物无匹配条目，
/// 允许补入空气中的 O2 再检索（有机物在空气中燃烧/加热的教材惯例）。</item>
/// <item>推理步骤的证据只写知识条目 id（RX-OR-*），绝不写方程式原文——
/// 引导生成器会把 RX- 前缀证据直接呈现给学生，方程式即答案泄露。</item>
/// </list>
/// </summary>
public static class OrganicInferenceEngine
{
    public static OrganicInferenceResult Infer(OrganicInferenceRequest request, ReactionLibrary? library = null)
    {
        library ??= ReactionLibrary.Default.Value;
        var result = new OrganicInferenceResult();
        var candidates = request.CandidateSpecies.Distinct(StringComparer.Ordinal).ToList();
        var steps = request.Steps;

        if (steps.Count == 0)
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = "未提供任何转化步骤，无法推断有机转化链。";
            return result;
        }

        // 候选预检：全部候选必须是可解析且课内白名单已知的物种（知识驱动铁律）
        foreach (var candidate in candidates)
        {
            bool known;
            try
            {
                known = WhitelistGuard.Check(Chemistry.Species.Parse(candidate)).Known;
            }
            catch (FormatException)
            {
                known = false;
            }

            if (!known)
            {
                result.Status = InferenceStatus.NoSolution;
                result.Message = $"候选物种 \"{candidate}\" 不在课内白名单或无法解析，超出知识库范围，引擎拒绝推断。";
                return result;
            }
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
            result.Message = "步骤中未出现字母标签，无法建立有机转化链赋值。";
            return result;
        }

        result.Steps.Add(new InferenceStep
        {
            Description = $"有机转化链含 {labels.Count} 个字母标签（{string.Join("、", labels)}），候选有机物 {candidates.Count} 种，在有机反应知识库中回溯赋值。",
            Source = "OrganicInferenceEngine"
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
                var matches = VerifyStep(step, assign, library, phenomena);
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
            result.Message = "有机反应知识库支撑不了该转化链：没有任何候选赋值能使每一步都落到已知有机反应。";
            result.Steps.Add(new InferenceStep
            {
                Description = "回溯穷举所有标签赋值，均至少有一步找不到有机知识库反应支撑。",
                Source = "OrganicInferenceEngine"
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
                Source = "OrganicInferenceEngine"
            });
        }

        foreach (var step in steps)
        {
            var matches = VerifyStep(step, solution, library, phenomena);
            if (matches is null || matches.Count == 0)
            {
                // 理论上不会发生（解已通过验证），防御性处理
                result.Status = InferenceStatus.NoSolution;
                result.Message = $"第 {step.Step} 步在最终赋值下失去有机知识库支撑。";
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
                    + $"与 {DescribeReagents(step, solution)} 反应，"
                    + $"依据 {entry.Id}「{entry.Name}」，得到 {step.To}（{solution.GetValueOrDefault(step.To, "?")}）。"
            });
            result.Steps.Add(new InferenceStep
            {
                Description = $"第 {step.Step} 步匹配 {entry.Id}「{entry.Name}」",
                Evidence = entry.Id,
                KnowledgeId = entry.KnowledgeId,
                Source = entry.Source
            });
        }

        if (solutions.Count > 1)
        {
            result.Status = InferenceStatus.Underdetermined;
            result.Message = $"存在至少 {solutions.Count} 组赋值都能解释全部步骤与现象，条件不足，无法唯一确定各字母代表的有机物。";
        }
        else
        {
            result.Status = InferenceStatus.Solved;
            result.Message = "唯一赋值满足全部步骤，每一步均有有机知识库反应支撑。";
        }

        return result;
    }

    /// <summary>
    /// 验证单步：返回有机知识库（RX-OR-* 条目）匹配列表；null 表示赋值尚未完成（剪枝时视为通过）。
    /// </summary>
    private static List<ReactionMatch>? VerifyStep(
        ExperimentStep step,
        Dictionary<string, string> assign,
        ReactionLibrary library,
        List<PhenomenonEntry> phenomena)
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
        var matches = ReactionMatcher.Match(reactants, tokens, library, toSpecies)
            .Where(m => m.Entry.Id.StartsWith("RX-OR-", StringComparison.Ordinal))
            .ToList();

        // 无试剂的加热/点燃/高温步骤：单反应物无匹配时补入空气中的 O2 再试
        if (matches.Count == 0
            && step.Reagents.Count == 0
            && tokens.Any(t => t is "△" or "点燃" or "高温" or "加热"))
        {
            var withOxygen = new List<string>(reactants) { "O2" };
            matches = ReactionMatcher.Match(withOxygen, tokens, library, toSpecies)
                .Where(m => m.Entry.Id.StartsWith("RX-OR-", StringComparison.Ordinal))
                .ToList();
        }

        // 现象约束：题给现象若命中现象库指示物种，则起点或终点物种必须能解释该现象
        // （如"溴水褪色"指示 C2H4/C2H2/SO2，饱和烃赋值即被剪除）。
        if (matches.Count > 0 && step.Phenomenon.Length > 0)
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

    private static string DescribeReagents(ExperimentStep step, Dictionary<string, string> assign)
    {
        if (step.Reagents.Count == 0)
        {
            return step.Conditions.Count > 0 ? $"（{string.Join("、", step.Conditions)}，空气中 O2 参与）" : "（自身转化）";
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

// ---------------------------------------------------------------------------
// 有机推断
// ---------------------------------------------------------------------------

/// <summary>有机推断请求：为有机转化链的每一步找到有机知识库反应支撑。</summary>
public sealed class OrganicInferenceRequest
{
    /// <summary>候选有机物（L2 写法）：字母标签 A/B/C… 的取值域。</summary>
    public List<string> CandidateSpecies { get; set; } = new();

    /// <summary>转化步骤序列。</summary>
    public List<ExperimentStep> Steps { get; set; } = new();

    /// <summary>介质："acidic"/"basic"/空（有机推断一般不限定）。</summary>
    public string Medium { get; set; } = "";
}

/// <summary>有机推断结果。</summary>
public sealed class OrganicInferenceResult
{
    public InferenceStatus Status { get; set; }

    /// <summary>字母标签 → 有机物 的赋值（Solved 时唯一；Underdetermined 时为首个可行解）。</summary>
    public Dictionary<string, string> Assignment { get; set; } = new();

    /// <summary>各步命中的反应与方程式。</summary>
    public List<ProcessStepResult> StepResults { get; set; } = new();

    public List<InferenceStep> Steps { get; set; } = new();

    public string Message { get; set; } = "";
}
