using CogniThink.Chem.Core.Data;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.Chem.Core.Inference;

/// <summary>
/// 实验探究推断引擎（知识驱动，高考实验探究题型）。三种探究模式（<see cref="ExperimentInferenceRequest.Mode"/>）：
/// <list type="bullet">
/// <item>"gas_test" / "gas_full_test"：气体验验/验满——对目标气体逐条匹配检验规则（EXP-001..008），
/// 输出"操作—现象—结论"完整表述。</item>
/// <item>"operation_purpose"：基本操作目的/判据与滴定误差分析——对操作代号逐条匹配规则（EXP-009..020）。</item>
/// <item>"phenomenon_conclusion"：现象→结论——现象文本对特征现象索引（PH-*）做关键词匹配，
/// 指示物种取并集后与候选物种求交集；唯一候选才下结论，多义如实报欠定（绝不猜）。</item>
/// </list>
/// 铁律：全部结论溯源到规则条目（Evidence=EXP-*/PH-*）；单条询问匹配到 0 条或多条规则 → Underdetermined，不猜。
/// </summary>
public static class ExperimentEngine
{
    /// <summary>引擎自述步骤的来源标记（无条目证据的流程说明步）。</summary>
    private const string EngineSource = "ExperimentEngine";

    /// <summary>实验探究规则缓存（rules.json 中 EXP-* 前缀条目）。</summary>
    private static readonly Lazy<IReadOnlyList<Rule>> ExpRules = new(() =>
        ChemDataLoader.LoadRules(DataRoot.Resolve())
            .Where(r => r.Id.StartsWith("EXP-", StringComparison.Ordinal))
            .ToList());

    /// <summary>特征现象索引缓存（phenomena.json，PH-* 条目）。</summary>
    private static readonly Lazy<IReadOnlyList<PhenomenonEntry>> PhenomenonEntries = new(() =>
        RuleStore.Load().Query<PhenomenaData>("phenomena").Items);

    /// <summary>按探究模式推断实验结论。</summary>
    public static ExperimentInferenceResult Infer(ExperimentInferenceRequest request)
    {
        var result = new ExperimentInferenceResult();
        switch (request.Mode)
        {
            case "gas_test":
            case "gas_full_test":
                InferByRules(result, request.Mode, request.Targets, "gas",
                    request.Mode == "gas_test" ? "气体验验" : "气体验满");
                break;
            case "operation_purpose":
                InferByRules(result, "operation_purpose", request.Operations, "operation",
                    "操作目的与误差分析");
                break;
            case "phenomenon_conclusion":
                InferPhenomenonConclusion(result, request);
                break;
            default:
                result.Status = InferenceStatus.Underdetermined;
                result.Message = $"不支持的实验探究模式 \"{request.Mode}\"" +
                                 "（支持 gas_test / gas_full_test / operation_purpose / phenomenon_conclusion）";
                break;
        }

        return result;
    }

    // -----------------------------------------------------------------------
    // 模式一/二：逐询问对象匹配唯一规则条目（检验、验满、操作目的、滴定误差）
    // -----------------------------------------------------------------------

    private static void InferByRules(
        ExperimentInferenceResult result, string task, IReadOnlyList<string> items,
        string factKey, string modeLabel)
    {
        result.Steps.Add(new InferenceStep
        {
            Description = $"实验探究模式 \"{task}\"（{modeLabel}）：逐询问对象匹配规则条目",
            Source = EngineSource,
        });

        if (items.Count == 0)
        {
            result.Status = InferenceStatus.Underdetermined;
            result.Message = $"{modeLabel}模式未给出任何询问对象，无法匹配规则条目";
            return;
        }

        var unresolved = new List<string>();
        foreach (var item in items)
        {
            var facts = new Dictionary<string, string> { ["task"] = task, [factKey] = item };
            var matched = ExpRules.Value.Where(r => ActivatedBy(r, facts)).ToList();
            if (matched.Count == 1)
            {
                var rule = matched[0];
                var display = string.IsNullOrEmpty(rule.Conclusion.Display)
                    ? rule.Conclusion.ToString()
                    : rule.Conclusion.Display!;
                result.Conclusions.Add(display);
                result.Steps.Add(new InferenceStep
                {
                    Description = display,
                    Evidence = rule.Id,
                    KnowledgeId = rule.KnowledgeId,
                    Source = rule.Source,
                });
            }
            else
            {
                unresolved.Add(item);
                result.Steps.Add(new InferenceStep
                {
                    Description = matched.Count == 0
                        ? $"\"{item}\" 未匹配到任何{modeLabel}规则条目（知识库未收录，引擎不猜）"
                        : $"\"{item}\" 同时匹配到 {matched.Count} 条规则条目（{string.Join("、", matched.Select(m => m.Id))}），条件不足以唯一确定",
                    Source = EngineSource,
                });
            }
        }

        if (unresolved.Count > 0)
        {
            result.Status = InferenceStatus.Underdetermined;
            result.Conclusions.Clear();
            result.Message = $"以下询问对象无法唯一匹配规则条目，不能给出实验结论：{string.Join("、", unresolved)}";
            return;
        }

        result.Status = InferenceStatus.Solved;
        result.Steps.Add(new InferenceStep
        {
            Description = $"汇总：{items.Count} 个询问对象全部唯一匹配规则条目，实验结论逐条成立",
            Source = EngineSource,
        });
    }

    /// <summary>规则触发：全部前提的 key 出现在事实中且取值命中（本引擎不用否定前提）。</summary>
    private static bool ActivatedBy(Rule rule, IReadOnlyDictionary<string, string> facts) =>
        rule.Preconditions.Count > 0 &&
        rule.Preconditions.All(p => !p.Negate && facts.TryGetValue(p.Key, out var v) && p.Values.Contains(v));

    // -----------------------------------------------------------------------
    // 模式三：现象→结论（特征现象索引 PH-* 关键词匹配 + 候选交集）
    // -----------------------------------------------------------------------

    private static void InferPhenomenonConclusion(ExperimentInferenceResult result, ExperimentInferenceRequest request)
    {
        result.Steps.Add(new InferenceStep
        {
            Description = "实验探究模式 \"phenomenon_conclusion\"：现象文本匹配特征现象索引，指示物种与候选求交集",
            Source = EngineSource,
        });

        if (request.Phenomena.Count == 0 || request.CandidateSpecies.Count == 0)
        {
            result.Status = InferenceStatus.Underdetermined;
            result.Message = "现象→结论模式须同时给出 phenomena（现象）与 candidateSpecies（候选物种）";
            return;
        }

        var indicated = new List<string>();
        var anyEntryMatched = false;
        foreach (var text in request.Phenomena)
        {
            var hits = PhenomenonEntries.Value
                .Where(e => text.Contains(e.Keyword, StringComparison.Ordinal))
                .ToList();
            if (hits.Count == 0)
            {
                result.Steps.Add(new InferenceStep
                {
                    Description = $"现象 \"{text}\" 未匹配到任何特征现象索引条目（PH-*），引擎不猜",
                    Source = EngineSource,
                });
                continue;
            }

            foreach (var entry in hits)
            {
                anyEntryMatched = true;
                result.Steps.Add(new InferenceStep
                {
                    Description = $"现象 \"{text}\" 匹配特征现象条目 {entry.Id}（{entry.Keyword}）：{entry.Description}，指示物种 {string.Join("、", entry.Species)}",
                    Evidence = entry.Id,
                    KnowledgeId = entry.KnowledgeId,
                    Source = entry.Source,
                });
                foreach (var s in entry.Species)
                {
                    if (!indicated.Contains(s, StringComparer.Ordinal))
                    {
                        indicated.Add(s);
                    }
                }
            }
        }

        if (!anyEntryMatched)
        {
            result.Status = InferenceStatus.Underdetermined;
            result.Message = "全部现象均未匹配到特征现象索引条目，无法给出实验结论";
            return;
        }

        var feasible = indicated.Where(s => request.CandidateSpecies.Contains(s, StringComparer.Ordinal)).ToList();
        if (feasible.Count == 0)
        {
            result.Status = InferenceStatus.Underdetermined;
            result.Message = $"现象指示物种（{string.Join("、", indicated)}）与候选（{string.Join("、", request.CandidateSpecies)}）无交集，无法给出实验结论";
            return;
        }

        if (feasible.Count > 1)
        {
            result.Steps.Add(new InferenceStep
            {
                Description = $"汇总：现象多义，候选交集中有 {feasible.Count} 个可能物种（{string.Join("、", feasible)}），现有条件无法唯一确定，引擎不猜",
                Source = EngineSource,
            });
            result.Status = InferenceStatus.Underdetermined;
            result.Message = $"现象多义，无法唯一确定：{string.Join("、", feasible)}";
            return;
        }

        var species = feasible[0];
        result.Status = InferenceStatus.Solved;
        result.Conclusions.Add($"该气体为 {species}");
        result.Steps.Add(new InferenceStep
        {
            Description = $"汇总：候选交集唯一含 {species}，结论成立",
            Source = EngineSource,
        });
    }
}

/// <summary>实验探究推断请求：按 <see cref="Mode"/> 启用对应字段。</summary>
public sealed class ExperimentInferenceRequest
{
    /// <summary>探究模式："gas_test" / "gas_full_test" / "operation_purpose" / "phenomenon_conclusion"。</summary>
    public string Mode { get; set; } = "";

    /// <summary>气体验验/验满的目标气体清单（L2 写法，如 "O2"、"CO2"）。</summary>
    public List<string> Targets { get; set; } = new();

    /// <summary>操作代号清单（operation_purpose 模式，如 "airtightness_check"、"buret_not_rinsed_with_standard"）。</summary>
    public List<string> Operations { get; set; } = new();

    /// <summary>现象文本清单（phenomenon_conclusion 模式，与教材表述一致）。</summary>
    public List<string> Phenomena { get; set; } = new();

    /// <summary>候选物种（phenomenon_conclusion 模式，结论只允许落在候选交集内）。</summary>
    public List<string> CandidateSpecies { get; set; } = new();
}

/// <summary>实验探究推断结果：结论逐条对应询问对象，全部可溯源到 EXP-*/PH-* 条目。</summary>
public sealed class ExperimentInferenceResult
{
    public InferenceStatus Status { get; set; } = InferenceStatus.Underdetermined;

    /// <summary>实验结论（Solved 时逐条与询问对象对应；欠定时为空）。</summary>
    public List<string> Conclusions { get; set; } = new();

    /// <summary>推理步骤链（首步模式说明、逐对象条目步、末步汇总）。</summary>
    public List<InferenceStep> Steps { get; set; } = new();

    /// <summary>状态说明（欠定时给出原因）。</summary>
    public string Message { get; set; } = "";
}
