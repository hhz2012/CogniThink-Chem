// P3-3 导学与求解融合：诊断检查点链接网关标准推理链节点 的单测（AC-3）。
// 核心：精确 knowledge_id 匹配、排除 Trend、取首个、不修改链、无链接时讲解不变。
using CogniThink.TutorBrain.Application.Diagnostic;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

public class DiagnosticCheckpointLinkerTests
{
    private static ReasoningNode Node(string kid, EvidenceLevel level = EvidenceLevel.EngineDerived, int step = 1, string desc = "d")
        => new() { Step = step, Description = desc, Conclusion = desc, EvidenceLevel = level, KnowledgeId = kid, Premises = { "前提1" } };

    private static ReasoningCheckpoint Cp(string kid) => new() { KnowledgeId = kid, Type = CheckpointType.Reasoning };

    private static ReasoningStructure Structure(params ReasoningCheckpoint[] cps)
    {
        var s = new ReasoningStructure { ProblemId = "P", StepIndex = 1, StepPrompt = "x" };
        s.Checkpoints.AddRange(cps);
        return s;
    }

    // ==================== 匹配（DiagnosticCheckpointLinker）====================

    [Fact]
    public void Match_ByKnowledgeId_SetsLinkedNode_NonTrend()
    {
        var chain = new[] { Node("KP-1"), Node("KP-2", EvidenceLevel.KnowledgeFact) };
        var s = Structure(Cp("KP-1"), Cp("KP-2"));

        DiagnosticCheckpointLinker.Link(s, chain);

        Assert.Same(chain[0], s.Checkpoints[0].LinkedReasoningNode);
        Assert.Same(chain[1], s.Checkpoints[1].LinkedReasoningNode);
    }

    [Fact]
    public void Trend_Excluded_EvenIfKnowledgeIdMatches()
    {
        var chain = new[] { Node("KP-1", EvidenceLevel.Trend) };
        var s = Structure(Cp("KP-1"));

        DiagnosticCheckpointLinker.Link(s, chain);

        Assert.Null(s.Checkpoints[0].LinkedReasoningNode);
    }

    [Fact]
    public void NoMatch_DifferentKnowledgeId_Null()
    {
        var chain = new[] { Node("KP-A") };
        var s = Structure(Cp("KP-B"));

        DiagnosticCheckpointLinker.Link(s, chain);

        Assert.Null(s.Checkpoints[0].LinkedReasoningNode);
    }

    [Fact]
    public void FirstMatch_Wins_WhenMultipleSameKnowledgeId()
    {
        var first = Node("KP-1", EvidenceLevel.EngineDerived, step: 1, desc: "first");
        var second = Node("KP-1", EvidenceLevel.KnowledgeFact, step: 2, desc: "second");
        var chain = new[] { first, second };
        var s = Structure(Cp("KP-1"));

        DiagnosticCheckpointLinker.Link(s, chain);

        Assert.Same(first, s.Checkpoints[0].LinkedReasoningNode); // 首个匹配（确定、不蒙数）
    }

    [Fact]
    public void EmptyChain_AllLinkedNull()
    {
        var s = Structure(Cp("KP-1"), Cp("KP-2"));

        DiagnosticCheckpointLinker.Link(s, Array.Empty<ReasoningNode>());

        Assert.All(s.Checkpoints, cp => Assert.Null(cp.LinkedReasoningNode));
    }

    [Fact]
    public void NullOrEmptyKnowledgeId_NeverMatches()
    {
        var chain = new[] { Node("KP-1") };
        var s = Structure(Cp(""), Cp("KP-1"));

        DiagnosticCheckpointLinker.Link(s, chain);

        Assert.Null(s.Checkpoints[0].LinkedReasoningNode); // 空 knowledgeId 不匹配
        Assert.NotNull(s.Checkpoints[1].LinkedReasoningNode);
    }

    [Fact]
    public void Link_DoesNotMutateChainNodes()
    {
        var chain = new[] { Node("KP-1") };
        var beforeKid = chain[0].KnowledgeId;
        var beforeDesc = chain[0].Description;
        var s = Structure(Cp("KP-1"));

        DiagnosticCheckpointLinker.Link(s, chain);

        Assert.Equal(beforeKid, chain[0].KnowledgeId);
        Assert.Equal(beforeDesc, chain[0].Description);
    }

    // ==================== 锚点（DiagnosticExplanationAnchor）====================

    [Fact]
    public void Append_WithNode_AddsAnchorAtEvidenceIndex0()
    {
        var explanation = new CheckpointExplanation { KnowledgeId = "KP-1" };
        explanation.Evidence.Add("原有证据");
        var node = Node("KP-1", EvidenceLevel.EngineDerived, step: 3, desc: "推结论");
        node.Conclusion = "pH 升高";

        DiagnosticExplanationAnchor.Append(explanation, node);

        Assert.Equal(2, explanation.Evidence.Count);
        Assert.StartsWith("标准推理链对应节点（步骤 3）", explanation.Evidence[0]);
        Assert.Contains("pH 升高", explanation.Evidence[0]);
        Assert.Equal("原有证据", explanation.Evidence[1]); // 原有证据顺序后移，但内容不变
    }

    [Fact]
    public void Append_NullNode_DoesNotChangeEvidence_逐字不变()
    {
        var explanation = new CheckpointExplanation { KnowledgeId = "KP-1" };
        explanation.Evidence.Add("证据A");
        explanation.Evidence.Add("证据B");
        var before = explanation.Evidence.ToList();

        DiagnosticExplanationAnchor.Append(explanation, null);

        Assert.Equal(before, explanation.Evidence); // 无链接 → 讲解逐字不变
    }

    [Fact]
    public void Append_NodeWithNoPremisesOrConclusion_UsesPlaceholder()
    {
        var explanation = new CheckpointExplanation { KnowledgeId = "KP-1" };
        var node = new ReasoningNode { Step = 1, EvidenceLevel = EvidenceLevel.Candidate, KnowledgeId = "KP-1" };
        // Premises 为空、Conclusion 为空

        DiagnosticExplanationAnchor.Append(explanation, node);

        Assert.Single(explanation.Evidence);
        Assert.Contains("（无显式前提）", explanation.Evidence[0]);
        Assert.Contains("（无显式结论）", explanation.Evidence[0]);
    }
}
