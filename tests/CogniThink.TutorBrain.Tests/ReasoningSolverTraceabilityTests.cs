using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// P2-1 T3：4 个示范 solver 命中链的可溯源断言（AC-4）。
/// 直接调用各 solver 的 public static TrySolve（绕过路由，避免歧义），断言：
/// ReasoningNodes 非空 / Step 连续无跳步 / 至少一个节点 KnowledgeId 或 RuleId 非空 / 末节点 Conclusion==FinalResult。
/// 题干取自 after_p15b checkpoint 中已判正的真实题目，保证命中稳定。
/// </summary>
public class ReasoningSolverTraceabilityTests
{
    private static EngineRequest Req(string taskType, string raw) =>
        new() { TaskType = taskType, RawText = raw };

    private static void AssertTraceable(EngineResponse? resp, string label)
    {
        Assert.NotNull(resp);
        Assert.True(resp!.Ok == true, $"{label}: 期望 Ok=true，实际 FinalResult={resp.FinalResult}");

        var nodes = resp.ReasoningNodes;
        Assert.NotEmpty(nodes); // AC-4: 非空

        // Step 连续 1..N，无跳步
        var steps = nodes.Select(n => n.Step).OrderBy(s => s).ToList();
        Assert.Equal(Enumerable.Range(1, steps.Count).ToList(), steps);

        // 至少一个节点可溯源（KnowledgeId 或 RuleId 非空）
        Assert.True(
            nodes.Any(n => !string.IsNullOrEmpty(n.KnowledgeId) || !string.IsNullOrEmpty(n.RuleId)),
            $"{label}: 没有任何节点携带 KnowledgeId/RuleId（假可溯源）");

        // 末节点 Conclusion == FinalResult
        var last = nodes.OrderBy(n => n.Step).Last();
        Assert.Equal(resp.FinalResult, last.Conclusion);
    }

    [Fact]
    public void ConceptFactSolver_命中概念事实库_可溯源_AC4()
    {
        // checkpoint concept-1，已判正
        var resp = ConceptFactSolver.TrySolve(Req("concept", "什么是电解质？"));
        AssertTraceable(resp, "ConceptFact");
        // knowledge_id 来自 concept-facts.json 条目 id（非空）
        var node = resp!.ReasoningNodes[0];
        Assert.False(string.IsNullOrEmpty(node.KnowledgeId), "ConceptFact 命中节点应带 knowledge_id");
        Assert.Equal(EvidenceLevel.KnowledgeFact, node.EvidenceLevel);
    }

    [Fact]
    public void AtomicStructureSolver_命中电子排布_可溯源_双节点无跳步_AC4()
    {
        // checkpoint pdf-173，已判正：基态 Cu 价层电子排布式
        var resp = AtomicStructureSolver.TrySolve(Req("atomic_structure", "（1）基态 Cu 原子的价层电子排布式为____________________。"));
        AssertTraceable(resp, "AtomicStructure");
        Assert.Equal(2, resp!.ReasoningNodes.Count); // 识别元素 → 构造原理计算
        Assert.Equal("periodic_table", resp.ReasoningNodes[0].KnowledgeId);
        Assert.Equal("rule:aufbau_hund_pauli", resp.ReasoningNodes[1].RuleId);
    }

    [Fact]
    public void VseprSolver_查表命中空间构型_可溯源_AC4()
    {
        // checkpoint d6-1，已判正：NH3 空间构型
        var resp = VseprSolver.TrySolve(Req("vsepr", "NH3 分子的空间构型是什么？"));
        AssertTraceable(resp, "Vsepr");
        var node = resp!.ReasoningNodes[0];
        Assert.Equal("vsepr_table", node.KnowledgeId);
    }

    [Fact]
    public void ReactionKbWriterSolver_离子方程式模板命中_可溯源_AC4()
    {
        // 酸 + 碳酸盐 → 离子方程式模板命中（ReactionKbWriterSolver，指纹 = 与 ^）
        var resp = ReactionKbWriterSolver.TrySolve(Req("equation_writer", "写出碳酸钠与盐酸反应的离子方程式。"));
        AssertTraceable(resp, "ReactionKb");
        var node = resp!.ReasoningNodes[0];
        Assert.Equal("reaction_kb:ionic", node.RuleId);
        Assert.Equal(EvidenceLevel.KnowledgeFact, node.EvidenceLevel);
    }

    [Fact]
    public void 无命中_不臆造节点_R4()
    {
        // 域外题干 → solver 返回 null（不蒙数），不得返回带 Ok=true + 假节点的响应
        var resp = ReactionKbWriterSolver.TrySolve(Req("equation_writer", "今天天气怎么样？"));
        Assert.Null(resp);
    }
}
