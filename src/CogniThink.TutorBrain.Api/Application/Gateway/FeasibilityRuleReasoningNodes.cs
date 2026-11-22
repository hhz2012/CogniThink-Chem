using System.Collections.Generic;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 反应可行性判定中两条「非活动性」规则的可溯源推理节点（父 spec R4，B-LlmMain 批 T5 审计补线程）。
/// <para>这两处原 Task 4 因「无 activity 锚点」留空，T5 审计发现各自存在其他干净 JSON 锚点：</para>
/// <list type="bullet">
/// <item>氧化性酸与金属反应不生 H2 → <c>data/knowledge/redox-rules.json</c> 的 <c>reductionProducts</c>
/// （<c>redox-no3-dilute→NO</c> / <c>redox-h2so4-concentrated→SO2</c>）。</item>
/// <item>Fe 与稀酸只能到 +2 价 → <c>data/knowledge/knowledge.json</c> 条目 <c>KH-IN-06</c>
/// （pitfall：「Fe 与弱氧化剂（H+、Cu2+、S、I2）生成 Fe2+」，此处氧化剂正是 H+）。</item>
/// </list>
/// <para>仅加性提供节点；判决字段由调用方原样保留（AC7）。</para>
/// </summary>
internal static class FeasibilityRuleReasoningNodes
{
    /// <summary>强氧化性酸（硝酸/浓硫酸）与金属反应酸根被还原、不生成 H2 → 单节点（KnowledgeId=redox-rules 条目 id）。</summary>
    internal static List<ReasoningNode> OxidizingAcidNoH2(string acidName)
    {
        // 硝酸：代码结论文本引 NO（稀硝酸还原产物）→ redox-no3-dilute；浓硫酸 → redox-h2so4-concentrated。
        var redoxId = acidName == "硝酸" ? "redox-no3-dilute" : "redox-h2so4-concentrated";
        return new List<ReasoningNode>
        {
            new()
            {
                Step = 1,
                Description = "强氧化性酸还原产物（取自 redox-rules.json）",
                Premises = new List<string>
                {
                    $"{acidName}是强氧化性酸，与金属反应时酸根被还原、不生成 H2（redox-rules.json 条目：{redoxId}）"
                },
                KnowledgeId = redoxId,
                Conclusion = $"{acidName}与金属反应不生成 H2，酸根被还原",
                EvidenceLevel = EvidenceLevel.KnowledgeFact,
            }
        };
    }

    /// <summary>Fe 与稀硫酸/稀盐酸（弱氧化剂 H+）只能氧化为 +2 价 → 单节点（KnowledgeId=KH-IN-06）。</summary>
    internal static List<ReasoningNode> FeDiluteAcidValence()
    {
        return new List<ReasoningNode>
        {
            new()
            {
                Step = 1,
                Description = "Fe 的价态规则（取自 knowledge.json 条目 KH-IN-06）",
                Premises = new List<string>
                {
                    "Fe 与弱氧化剂（H+、Cu2+、S、I2）生成 Fe2+（knowledge.json 条目 KH-IN-06 pitfall）"
                },
                KnowledgeId = "KH-IN-06",
                Conclusion = "稀硫酸/稀盐酸为弱氧化剂（H+），只能将 Fe 氧化为 Fe2+",
                EvidenceLevel = EvidenceLevel.KnowledgeFact,
            }
        };
    }
}
