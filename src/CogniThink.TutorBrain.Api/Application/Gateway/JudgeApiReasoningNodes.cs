namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 判官-API 聚合器（<see cref="MultiSubCorrectnessSolver"/> / <see cref="JudgmentStatementAggregator"/>）
/// 共享的可溯源推理节点构造器（父 spec R4）。
/// <para>
/// 每个有<b>真实命中 id</b>的小题 → 每个命中 id 一个节点（<c>KnowledgeId</c>=id，
/// <see cref="EvidenceLevel.KnowledgeFact"/>，<c>Conclusion</c>=该小题判决文本）；
/// 末尾追加一个汇总节点（<c>Conclusion</c>=整题 FinalResult，<c>Premises</c> 列出全部命中 id）。
/// <c>Step</c> 从 1 连续无跳步。
/// </para>
/// <para>
/// engine-only 小题（引擎检查项 / Counting / RedoxElectron 判定，无暴露 id）<b>不传入 hits</b>
/// → 不建节点、不占 Step（R4：无真实锚点不发节点，禁止假可溯源）。hits 全空时返回空列表，
/// 由求解器决定不置 <c>ReasoningNodes</c>（保持留空）。
/// </para>
/// </summary>
internal static class JudgeApiReasoningNodes
{
    /// <summary>小题命中记录：小题编号、判决符号、命中 id 列表（仅含真实 id）。</summary>
    internal sealed record SubHit(int SubIndex, char Symbol, List<string> HitIds);

    /// <summary>构造可溯源节点列表；无命中时返回空列表。</summary>
    internal static List<ReasoningNode> Build(List<SubHit> hits, string finalResult)
    {
        var nodes = new List<ReasoningNode>();
        var step = 1;
        foreach (var hit in hits)
        {
            if (hit.HitIds is null || hit.HitIds.Count == 0) continue;
            foreach (var id in hit.HitIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                nodes.Add(new ReasoningNode
                {
                    Step = step++,
                    Description = $"第({hit.SubIndex})小题判定为「{hit.Symbol}」（依据命中的知识/规则条目）",
                    Premises = new List<string> { $"小题({hit.SubIndex})判决符号：{hit.Symbol}" },
                    KnowledgeId = id,
                    Conclusion = $"({hit.SubIndex}){hit.Symbol}",
                    EvidenceLevel = EvidenceLevel.KnowledgeFact,
                });
            }
        }

        if (nodes.Count > 0)
        {
            nodes.Add(new ReasoningNode
            {
                Step = step,
                Description = "汇总各小题判决，给出整题结论",
                Premises = nodes
                    .Select(n => n.KnowledgeId)
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Distinct()
                    .ToList()!,
                Conclusion = finalResult,
                EvidenceLevel = EvidenceLevel.KnowledgeFact,
            });
        }

        return nodes;
    }
}
