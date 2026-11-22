// P3-3 导学与求解融合：把诊断检查点链接到网关标准推理链（ReasoningNodes DAG）中同 knowledge_id 的节点。
//
// 纯函数（便于单测，不依赖 DB / 网关）：
// - 精确匹配 checkpoint.KnowledgeId == node.KnowledgeId；
// - 取首个匹配（确定、不蒙数）；
// - 排除 EvidenceLevel=Trend（P2-2 世界模型解释，标注「不作判定依据」，不进入导学诊断锚点）；
// - 不修改链上节点（只写 checkpoint.LinkedReasoningNode 引用）。
namespace CogniThink.TutorBrain.Application.Diagnostic;

using CogniThink.TutorBrain.Application.Gateway;

public static class DiagnosticCheckpointLinker
{
    /// <summary>为结构中每个检查点设置 LinkedReasoningNode（无匹配留 null）。</summary>
    public static void Link(ReasoningStructure structure, IReadOnlyList<ReasoningNode> chain)
    {
        if (structure is null || chain is null) return;
        foreach (var cp in structure.Checkpoints)
            cp.LinkedReasoningNode = FirstMatchingNode(chain, cp.KnowledgeId);
    }

    public static ReasoningNode? FirstMatchingNode(IReadOnlyList<ReasoningNode> chain, string? knowledgeId)
    {
        if (string.IsNullOrEmpty(knowledgeId)) return null;
        return chain.FirstOrDefault(n =>
            n.EvidenceLevel != EvidenceLevel.Trend
            && !string.IsNullOrEmpty(n.KnowledgeId)
            && n.KnowledgeId == knowledgeId);
    }
}
