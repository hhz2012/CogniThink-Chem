using System.Linq;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// B-judge-API Task 3：MultiSubCorrectnessSolver + JudgmentStatementAggregator 线程化 ReasoningNodes（AC4 / AC5）。
/// 题干取自 BuildTestCases 真实题目（after_b_ext 基线一手捕获的期望值）。
/// 红线：14 例 FinalResult 字节级不变；11 例节点非空且可溯源、3 例 engine-only 留空（R4）；
/// Step 连续无跳步、末节点 Conclusion==FinalResult；MS 不劫持概念题（零回归）。
/// <para>注：基线探测曾误将 pdf-119 判为 engine-only（factId=0）；实现期直接运行时核实
/// pdf-119 真实命中事实库 JS-016（工业制高纯硅 / SiHCl3，verdict=true→√），属可溯源，
/// 故归为 11 例非空之一。R4 仅禁止"无真实 id 仍发节点"，真实命中必须发节点。</para>
/// </summary>
public class JudgeApiTraceabilityTests
{
    private static List<QaTestCase> Cases => GatewayVsLlmEvaluationService.BuildTestCases();

    private static EngineResponse? Solve(string solver, string question)
    {
        var req = new EngineRequest { RawText = question };
        return solver == "MS"
            ? MultiSubCorrectnessSolver.TrySolve(req)
            : JudgmentStatementAggregator.TrySolve(req);
    }

    private static void AssertTraceable(EngineResponse resp, string label)
    {
        Assert.NotNull(resp);
        Assert.True(resp!.Ok == true, $"{label}: 期望 Ok=true，实际 FinalResult={resp.FinalResult}");
        var nodes = resp.ReasoningNodes;
        Assert.NotEmpty(nodes);
        var steps = nodes.Select(n => n.Step).OrderBy(s => s).ToList();
        Assert.Equal(Enumerable.Range(1, steps.Count).ToList(), steps);
        Assert.True(nodes.Any(n => !string.IsNullOrEmpty(n.KnowledgeId) || !string.IsNullOrEmpty(n.RuleId)),
            $"{label}: 没有任何节点携带 KnowledgeId/RuleId（假可溯源）");
        Assert.Equal(resp.FinalResult, nodes.OrderBy(n => n.Step).Last().Conclusion);
    }

    private static void AssertNoNodes(EngineResponse resp, string label)
    {
        Assert.NotNull(resp);
        Assert.True(resp!.Ok == true, $"{label}: 期望 Ok=true");
        Assert.True(resp.ReasoningNodes is null || resp.ReasoningNodes.Count == 0,
            $"{label}: engine-only 案例不得填充推理节点（R4）");
    }

    // 14 例：solver = 网关实际采纳路径（MS 先调，命中即早返回；否则 JA）。
    [Theory]
    [InlineData("pdf-051", "MS", "(1)√ (2)√ (3)× (4)× (5)×", true)]
    [InlineData("pdf-468", "MS", "×", false)]
    [InlineData("pdf-471", "MS", "√", false)]
    [InlineData("pdf-472", "MS", "√", false)]
    [InlineData("pdf-119", "JA", "√", true)]
    [InlineData("pdf-120", "JA", "×", true)]
    [InlineData("pdf-121", "JA", "√", true)]
    [InlineData("pdf-122", "JA", "√", true)]
    [InlineData("pdf-450", "JA", "(1)× (2)× (3)√ (4)× (5)× (6)× (7)× (8)× (9)×", true)]
    [InlineData("pdf-466", "JA", "√", true)]
    [InlineData("pdf-469", "JA", "×", true)]
    [InlineData("pdf-470", "JA", "√", true)]
    [InlineData("pdf-473", "JA", "×", true)]
    [InlineData("pdf-480", "JA", "(1)√ (2)× (3)× (4)√ (5)× (6)× (7)× (8)× (9)× (10)√", true)]
    public void JudgeApi_14Cases_ZeroDrift_AndNodeClassification(
        string caseId, string solver, string expectedFinal, bool expectTraceable)
    {
        var c = Cases.First(x => x.Id == caseId);
        var resp = Solve(solver, c.Question);
        Assert.NotNull(resp);
        Assert.Equal(expectedFinal, resp!.FinalResult); // AC5：字节级相等
        if (expectTraceable) AssertTraceable(resp, caseId);   // 10 例：可溯源
        else AssertNoNodes(resp, caseId);                      // 4 例：R4 留空
    }

    /// <summary>
    /// pdf-051：5 小题中仅 2 个语境规则子（sub3 Rule A / sub5 Rule B）发节点，
    /// 3 个 engine-only 子不发 → 恰好 2 个 ctx-id 节点 + 1 汇总节点 = 3，防臆造（R2 风险项）。
    /// </summary>
    [Fact]
    public void MultiSub_Pdf051_OnlyTwoCtxSubsGetNodes_R4()
    {
        var c = Cases.First(x => x.Id == "pdf-051");
        var resp = MultiSubCorrectnessSolver.TrySolve(new EngineRequest { RawText = c.Question });
        Assert.NotNull(resp);
        Assert.Equal("(1)√ (2)√ (3)× (4)× (5)×", resp!.FinalResult);

        var nodes = resp.ReasoningNodes;
        Assert.Equal(3, nodes.Count); // 2 ctx 子节点 + 1 汇总节点

        var ctxIds = new[] { "ccr-acid-nitrate-sulfite", "ccr-cuprous-ammine-charge" };
        var ctxNodes = nodes.Where(n => ctxIds.Contains(n.KnowledgeId)).ToList();
        Assert.Equal(2, ctxNodes.Count); // 仅 2 个 ctx 子携带 id，3 个 engine-only 子未被臆造节点
        Assert.All(ctxNodes, n => Assert.Equal(EvidenceLevel.KnowledgeFact, n.EvidenceLevel));

        // 末节点（汇总）Conclusion == FinalResult
        var last = nodes.OrderBy(n => n.Step).Last();
        Assert.Equal(resp.FinalResult, last.Conclusion);
        Assert.True(string.IsNullOrEmpty(last.KnowledgeId)); // 汇总节点无独立 id（派生结论，R4 本意不违反）

        // Step 连续 1..3 无跳步
        var steps = nodes.Select(n => n.Step).OrderBy(s => s).ToList();
        Assert.Equal(new[] { 1, 2, 3 }, steps);
    }

    /// <summary>MS 不劫持概念题：pdf-120 无方程 → MS 返回 null，由 JA 接管（零回归）。</summary>
    [Fact]
    public void MultiSub_ConceptCase_ReturnsNull_NoHijack()
    {
        var c = Cases.First(x => x.Id == "pdf-120");
        var resp = MultiSubCorrectnessSolver.TrySolve(new EngineRequest { RawText = c.Question });
        Assert.Null(resp);
    }

    /// <summary>域外（非正误判断题）→ 两求解器均返回 null，不产出带假节点的响应。</summary>
    [Fact]
    public void BothSolvers_OutOfDomain_ReturnNull()
    {
        var req = new EngineRequest { RawText = "今天天气怎么样？" };
        Assert.Null(MultiSubCorrectnessSolver.TrySolve(req));
        Assert.Null(JudgmentStatementAggregator.TrySolve(req));
    }
}
