using System.Collections.Generic;
using System.Linq;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 弱酸酸性强弱排序的可溯源推理节点构造器（父 spec R4，B-LlmMain 批 T3）。
/// <para>每个在 <c>acid-constants.json</c> 中命中 Ka1 的酸 → 一个节点（<c>KnowledgeId</c>=该酸
/// 在 JSON 中的稳定字典键，如 <c>CH3COOH:Ka1</c>，<see cref="EvidenceLevel.KnowledgeFact"/>，
/// <c>Conclusion</c>=酸名）；末尾追加 1 个汇总节点（<c>Conclusion</c>=整题 FinalResult，
/// <c>Premises</c>=去重后的非空 KnowledgeId 集合）。<c>Step</c> 从 1 连续无跳步。</para>
/// <para>命中酸全空 / KaKey 全空白 → 返回空列表（不臆造汇总，R4：无真实锚点不发节点）。
/// 镜像 <see cref="HydrolysisPhCalculator"/> 的 kaKey 锚点范式（常数与物种只来自 JSON）。</para>
/// </summary>
internal static class AcidOrderReasoningNodes
{
    /// <summary>单个命中酸的可溯源记录：酸名 + acid-constants.json 稳定键 + Ka 显示串。</summary>
    internal sealed record AcidHit(string Acid, string KaKey, string KaDisp);

    /// <summary>
    /// 由命中的酸构造推理节点；命中全空时返回空列表。
    /// </summary>
    /// <param name="acids">命中的酸（顺序即节点顺序，通常为最终排序方向）。</param>
    /// <param name="finalResult">整题最终结论（用于末尾汇总节点的 Conclusion）。</param>
    internal static List<ReasoningNode> Build(List<AcidHit> acids, string finalResult)
    {
        var nodes = new List<ReasoningNode>();
        if (acids is null || acids.Count == 0)
        {
            return nodes;
        }

        var step = 1;
        foreach (var a in acids)
        {
            if (a is null || string.IsNullOrWhiteSpace(a.KaKey))
            {
                continue;
            }
            nodes.Add(new ReasoningNode
            {
                Step = step++,
                Description = $"{a.Acid} 酸性强弱依据（25 ℃ 电离常数 Ka）",
                Premises = new List<string>
                {
                    $"{a.Acid} 的 Ka = {a.KaDisp}（取自 acid-constants.json：{a.KaKey}）"
                },
                KnowledgeId = a.KaKey,
                Conclusion = a.Acid,
                EvidenceLevel = EvidenceLevel.KnowledgeFact,
            });
        }

        if (nodes.Count > 0)
        {
            var premises = nodes
                .Select(n => n.KnowledgeId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Select(id => id!)
                .Distinct()
                .ToList();
            nodes.Add(new ReasoningNode
            {
                Step = step,
                Description = "依据各酸 Ka 数值比较，给出酸性强弱排序",
                Premises = premises,
                Conclusion = finalResult,
                EvidenceLevel = EvidenceLevel.KnowledgeFact,
            });
        }

        return nodes;
    }
}
