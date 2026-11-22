using System.Collections.Generic;
using System.Linq;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 选择题事实命中的可溯源推理节点构造器（父 spec R4，B-LlmMain 批）。
/// <para>每个有真实命中 id 的选项 → 每个命中 id 一个节点（KnowledgeId=id, EvidenceLevel=KnowledgeFact,
/// Conclusion=该选项字母）；末尾追加 1 个汇总节点（Conclusion=整题 FinalResult,
/// Premises=去重后的非空 KnowledgeId 集合）。Step 从 1 连续无跳步。</para>
/// <para>命中 id 全空 → 返回空列表（调用方保持 ReasoningNodes 留空，R4：不臆造汇总节点）。</para>
/// <para>镜像 <see cref="JudgeApiReasoningNodes"/> 的纯函数范式，internal 可直接单测（InternalsVisibleTo 已设）。</para>
/// </summary>
internal static class ChoiceReasoningNodes
{
    /// <summary>单个选项的命中事实 id 列表（无命中为空，不臆造）。</summary>
    internal sealed record OptionHit(string Letter, List<string> HitIds);

    /// <summary>
    /// 由各选项命中 id 构造推理节点。
    /// </summary>
    /// <param name="hits">各选项命中（Letter + 非空 id 列表）；顺序即节点顺序。</param>
    /// <param name="finalResult">整题最终结论（用于末尾汇总节点的 Conclusion）。</param>
    /// <returns>节点列表；命中全空时为空列表。</returns>
    internal static List<ReasoningNode> Build(List<OptionHit> hits, string finalResult)
    {
        var nodes = new List<ReasoningNode>();
        if (hits is null || hits.Count == 0)
        {
            return nodes;
        }

        var step = 1;
        foreach (var hit in hits)
        {
            if (hit?.HitIds is null || hit.HitIds.Count == 0)
            {
                continue;
            }
            foreach (var id in hit.HitIds)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }
                nodes.Add(new ReasoningNode
                {
                    Step = step++,
                    Description = $"选项 {hit.Letter} 的判定依据（命中的知识/规则条目）",
                    Premises = new List<string> { $"选项 {hit.Letter} 命中事实：{id}" },
                    KnowledgeId = id,
                    Conclusion = hit.Letter,
                    EvidenceLevel = EvidenceLevel.KnowledgeFact,
                });
            }
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
                Description = "汇总各选项判定，给出整题结论",
                Premises = premises,
                Conclusion = finalResult,
                EvidenceLevel = EvidenceLevel.KnowledgeFact,
            });
        }

        return nodes;
    }
}
