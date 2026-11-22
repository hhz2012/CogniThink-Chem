namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 多部分大题子问分发的推理节点聚合器（父 spec R4：纯传播、仅加性元数据）。
/// <para>
/// 把各子问响应的 <c>ReasoningNodes</c> 链进父响应：子节点 <c>Step</c> 重排为从 1 连续、
/// <c>Description</c> 前缀标注子问归属；其余字段（<c>Premises</c>/<c>RuleId</c>/<c>KnowledgeId</c>/
/// <c>Conclusion</c>/<c>EvidenceLevel</c>）原样克隆。末尾追加 1 个汇总节点
/// （<c>Conclusion</c> = 聚合 <c>FinalResult</c>，<c>Premises</c> = 去重后的非空 <c>KnowledgeId</c>）。
/// </para>
/// <para>
/// 全部子问均无节点（<c>ReasoningNodes</c> 为空）→ 返回空列表，<b>不</b>追加汇总节点
/// （R4：无真实锚点不臆造节点，禁止假可溯源）。镜像 <see cref="JudgeApiReasoningNodes.Build"/> 范式。
/// </para>
/// </summary>
internal static class MultiPartReasoningNodes
{
    /// <summary>
    /// 聚合各子问推理节点为父响应节点列表；无任何子节点时返回空列表。
    /// </summary>
    /// <param name="parts">有序子问列表（序号 + 子响应本体）。</param>
    /// <param name="aggregatedFinalResult">父响应聚合结论（用作汇总节点的 Conclusion）。</param>
    internal static List<ReasoningNode> Merge(List<(int Index, EngineResponse SubResp)> parts, string aggregatedFinalResult)
    {
        var nodes = new List<ReasoningNode>();
        var step = 1;

        foreach (var (idx, sub) in parts)
        {
            if (sub?.ReasoningNodes is null || sub.ReasoningNodes.Count == 0)
                continue; // R4：无节点的子问不发节点、不占 Step

            foreach (var n in sub.ReasoningNodes)
            {
                nodes.Add(new ReasoningNode
                {
                    Step = step++,
                    Description = $"({idx}) {n.Description}",
                    Premises = n.Premises?.ToList() ?? new List<string>(),
                    RuleId = n.RuleId,
                    KnowledgeId = n.KnowledgeId,
                    Conclusion = n.Conclusion,
                    EvidenceLevel = n.EvidenceLevel,
                });
            }
        }

        if (nodes.Count > 0)
        {
            nodes.Add(new ReasoningNode
            {
                Step = step,
                Description = "汇总各子问推理链，给出多部分大题聚合结论",
                Premises = nodes
                    .Select(n => n.KnowledgeId)
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Distinct()
                    .ToList()!,
                Conclusion = aggregatedFinalResult,
                EvidenceLevel = EvidenceLevel.KnowledgeFact,
            });
        }

        return nodes;
    }
}
