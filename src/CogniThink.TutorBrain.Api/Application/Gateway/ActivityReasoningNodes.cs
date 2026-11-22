using System.Collections.Generic;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 金属活动性顺序接地的推理节点构造器（父 spec R4，B-LlmMain 批 T4 组 3）。
/// <para>所有节点 <c>KnowledgeId</c> = <c>"activity-series"</c>（真实文件锚点，data/highschool/activity-series.json，
/// R4 不臆造）；<c>RuleId</c> 为运行时派生的稳定序位 id（<c>activity-series:{'{A}>{B}'}</c>，序固定故稳定）。
/// 金属 + 盐置换分支额外挂 <c>RuleId="RED-001"</c>（rules.json 已声明，knowledgeId=KH-IN-06，
/// display=「活动性强的金属能把活动性弱的金属从其盐溶液中置换出来」）。</para>
/// <para>仅加性提供节点列表；判决字段（Ok/FinalResult/...）由调用方原样保留，不得在此改动（AC7）。</para>
/// </summary>
internal static class ActivityReasoningNodes
{
    private const string ActivitySeriesId = "activity-series";

    /// <summary>金属 M 相对 H 的位置比较（氢前/氢后）→ 单节点。</summary>
    internal static List<ReasoningNode> HydrogenPosition(string metal, bool beforeH)
    {
        var conclusion = beforeH
            ? $"{metal} 在金属活动性顺序中排在 H 之前"
            : $"{metal} 在金属活动性顺序中排在 H 之后";
        var ruleId = beforeH ? $"activity-series:{metal}>H" : $"activity-series:H>{metal}";
        return Single(ruleId, conclusion);
    }

    /// <summary>金属 A 活动性强于 B（A 能从盐溶液置换出 B）→ 序位比较节点 + RED-001 置换规则节点。</summary>
    internal static List<ReasoningNode> MetalDisplacement(string metalA, string metalB)
    {
        return new List<ReasoningNode>
        {
            new()
            {
                Step = 1,
                Description = "金属活动性顺序比较（取自 activity-series.json）",
                Premises = new List<string> { $"金属活动性顺序中 {metalA} 位于 {metalB} 之前，故 {metalA} > {metalB}" },
                RuleId = $"activity-series:{metalA}>{metalB}",
                KnowledgeId = ActivitySeriesId,
                Conclusion = $"金属活动性：{metalA} > {metalB}",
                EvidenceLevel = EvidenceLevel.KnowledgeFact,
            },
            new()
            {
                Step = 2,
                Description = "金属活动性顺序置换规则（rules.json: RED-001）",
                Premises = new List<string> { "活动性强的金属能把活动性弱的金属从其盐溶液中置换出来" },
                RuleId = "RED-001",
                KnowledgeId = "KH-IN-06",
                Conclusion = "活动性强的金属能把活动性弱的金属从其盐溶液中置换出来",
                EvidenceLevel = EvidenceLevel.KnowledgeFact,
            },
        };
    }

    /// <summary>极活泼金属（Na/K/Ca）与盐溶液反应时先与水反应 → grounded in 活动性序位（很靠前）。</summary>
    internal static List<ReasoningNode> HighlyActiveWaterFirst(string metal)
        => Single($"activity-series:{metal}>H", $"{metal} 活动性极强（在金属活动性顺序中排在 H 之前很靠前）");

    /// <summary>惰性金属（Pt/Au）不与硝酸/浓硫酸单独反应 → grounded in 活动性序位（最末）。</summary>
    internal static List<ReasoningNode> InertMetalOxidizingAcid(string metal)
        => Single($"activity-series:H>{metal}", $"{metal} 是极不活泼金属（在金属活动性顺序中排在 H 之后、序列末位）");

    /// <summary>非 Pt/Au 金属能被氧化性酸氧化 → grounded in 活动性序位（位于 Pt/Au 之前）。</summary>
    internal static List<ReasoningNode> ActiveMetalOxidizingAcid(string metal)
        => Single($"activity-series:{metal}>Pt", $"{metal} 不是 Pt/Au，在金属活动性顺序中位于 Pt/Au 之前");

    private static List<ReasoningNode> Single(string ruleId, string conclusion)
    {
        return new List<ReasoningNode>
        {
            new()
            {
                Step = 1,
                Description = "金属活动性顺序事实（取自 activity-series.json）",
                Premises = new List<string> { conclusion + "（activity-series.json 序位）" },
                RuleId = ruleId,
                KnowledgeId = ActivitySeriesId,
                Conclusion = conclusion,
                EvidenceLevel = EvidenceLevel.KnowledgeFact,
            }
        };
    }
}
