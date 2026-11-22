using System.Collections.Generic;
using System.Linq;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// Ksp 沉淀先后顺序的可溯源推理节点构造器（父 spec R4，B-LlmMain 批 T4 组 2）。
/// <para>每个在 <c>precipitate-types.json</c> 中命中的沉淀 → 一个节点（<c>KnowledgeId</c>=该条目
/// 唯一 id，如 <c>ppt:AgCl</c>，<see cref="EvidenceLevel.KnowledgeFact"/>，<c>Conclusion</c>=沉淀化学式）；
/// 末尾追加 1 个汇总节点（<c>Conclusion</c>=整题 FinalResult，<c>Premises</c>=去重后的非空 KnowledgeId 集合）。
/// <c>Step</c> 从 1 连续无跳步。</para>
/// <para>命中全空 / id 全空白 → 返回空列表（不臆造汇总，R4：无真实锚点不发节点）。
/// 镜像 <see cref="AcidOrderReasoningNodes"/> / <see cref="ChoiceReasoningNodes"/> 的纯函数范式。</para>
/// </summary>
internal static class PrecipitateReasoningNodes
{
    /// <summary>单个命中沉淀的可溯源记录：化学式 + precipitate-types.json 唯一 id。</summary>
    internal sealed record PrecipitateHit(string Formula, string Id);

    /// <summary>
    /// 由命中的沉淀构造推理节点；命中全空时返回空列表。
    /// </summary>
    /// <param name="hits">命中的沉淀（顺序即节点顺序，通常为最终所需阳离子浓度升序）。</param>
    /// <param name="finalResult">整题最终结论（用于末尾汇总节点的 Conclusion）。</param>
    internal static List<ReasoningNode> Build(List<PrecipitateHit> hits, string finalResult)
    {
        var nodes = new List<ReasoningNode>();
        if (hits is null || hits.Count == 0)
        {
            return nodes;
        }

        var step = 1;
        foreach (var h in hits)
        {
            if (h is null || string.IsNullOrWhiteSpace(h.Id))
            {
                continue;
            }
            nodes.Add(new ReasoningNode
            {
                Step = step++,
                Description = $"{h.Formula} 沉淀类型依据（阳离子:阴离子解离比例，取自 precipitate-types.json）",
                Premises = new List<string>
                {
                    $"{h.Formula} 的沉淀类型命中 precipitate-types.json 条目：{h.Id}"
                },
                KnowledgeId = h.Id,
                Conclusion = h.Formula,
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
                Description = "依据各沉淀开始沉淀所需阳离子浓度比较，给出先后沉淀顺序",
                Premises = premises,
                Conclusion = finalResult,
                EvidenceLevel = EvidenceLevel.KnowledgeFact,
            });
        }

        return nodes;
    }
}
