using System.Collections.Generic;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 网关层推理节点的共享纯函数工具（父 spec R4，B-LlmMain 批）。
/// <para>当前提供 <see cref="Clone"/>：把子响应的 <c>ReasoningNodes</c> 深拷贝进父响应，
/// 避免父子共享可变引用；空源 / 空列表 → 空列表（<b>不</b>赋 null，规避 CS8601）。</para>
/// <para>克隆时 <c>Step</c> 等字段原样保留（单一来源，无需重排序号；多来源合并由
/// <see cref="MultiPartReasoningNodes"/> 负责）。</para>
/// </summary>
internal static class GatewayReasoningNodes
{
    /// <summary>
    /// 深拷贝推理节点列表；源为 null 或空时返回空列表（绝不返回 null）。
    /// </summary>
    internal static List<ReasoningNode> Clone(IReadOnlyList<ReasoningNode>? source)
    {
        var nodes = new List<ReasoningNode>();
        if (source is null || source.Count == 0)
        {
            return nodes;
        }

        foreach (var n in source)
        {
            if (n is null)
            {
                continue;
            }
            nodes.Add(new ReasoningNode
            {
                Step = n.Step,
                Description = n.Description,
                Premises = n.Premises?.ToList() ?? new List<string>(),
                RuleId = n.RuleId,
                KnowledgeId = n.KnowledgeId,
                Conclusion = n.Conclusion,
                EvidenceLevel = n.EvidenceLevel,
            });
        }

        return nodes;
    }
}
