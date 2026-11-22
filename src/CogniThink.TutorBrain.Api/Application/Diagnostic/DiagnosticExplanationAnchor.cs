// P3-3 导学与求解融合：当诊断检查点链接到网关标准推理链节点时，在讲解证据前插入锚点行。
// 纯函数（便于单测，不依赖 DB）：node 为 null 时不改 explanation（既有讲解逐字不变）。
namespace CogniThink.TutorBrain.Application.Diagnostic;

using CogniThink.TutorBrain.Application.Gateway;

public static class DiagnosticExplanationAnchor
{
    public static void Append(CheckpointExplanation explanation, ReasoningNode? node)
    {
        if (explanation is null || node is null) return;

        var premises = node.Premises is { Count: > 0 } ? string.Join("；", node.Premises) : "（无显式前提）";
        var conclusion = string.IsNullOrEmpty(node.Conclusion) ? "（无显式结论）" : node.Conclusion;
        explanation.Evidence.Insert(0,
            $"标准推理链对应节点（步骤 {node.Step}）：依据「{premises}」推出「{conclusion}」（证据强度：{node.EvidenceLevel}）");
    }
}
