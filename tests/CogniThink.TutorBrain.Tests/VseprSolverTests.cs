using System.Collections.Generic;
using System.Linq;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// 网关层 VSEPR / 分子构型确定性求解器（阶段5 / Tier 2）验收 + 护栏测试。
/// 真值源：高中常见微粒「分子构型 / VSEPR 模型 / 杂化」对照表。
/// 重点验证：6 道干净填空被解出且判正（d6-1~5 + pdf-508）；pdf-507 为已知坏键（化学结论正确但键错位）不采纳；
/// 任何边界外/歧义/选择题一律不触发，不蒙数、不损害精确率。
/// </summary>
public class VseprSolverTests
{
    private static EngineResponse? Solve(string raw) =>
        VseprSolver.TrySolve(new EngineRequest { TaskType = "vsepr", RawText = raw });

    private static bool Correct(string answer, QaTestCase c) =>
        GatewayVsLlmEvaluationService.IsAnswerCorrect(answer, c);

    private static QaTestCase? Case(string id) =>
        GatewayVsLlmEvaluationService.BuildTestCases().FirstOrDefault(x => x.Id == id);

    // ============================================================
    // 正例：干净构型填空（均判正，答案键正确）
    // ============================================================

    [Theory]
    [InlineData("d6-1", "NH3", "三角锥形")]
    [InlineData("d6-2", "H2O", "V形")]
    [InlineData("d6-3", "SO3", "平面三角形")]
    [InlineData("d6-4", "SO2", "V形")]
    [InlineData("d6-5", "ClO3-", "三角锥形")]
    public void 手工构型题_判正(string id, string species, string expectedSubstr)
    {
        var c = Case(id);
        Assert.NotNull(c);
        var r = Solve(c!.Question);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains(expectedSubstr, r.FinalResult);
        Assert.True(Correct(r.FinalResult, c), $"{id}（{species}）构型结论应通过判分");
    }

    [Fact]
    public void Pdf508_AsO43_空间结构_判正()
    {
        var c = Case("pdf-508");
        Assert.NotNull(c);
        var r = Solve(c!.Question);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("正四面体", r.FinalResult);
        Assert.True(Correct(r.FinalResult, c), "pdf-508 AsO4^3- 空间结构结论应通过判分");
    }

    // ============================================================
    // 负例：边界外一律返回 null（不蒙数）
    // ============================================================

    [Theory]
    [InlineData("比较 NH3 和 PH3 的键角大小")]               // 比较/键角 → 不蒙数
    [InlineData("为什么 NH3 分子是三角锥形？")]             // 推理开放 → 不蒙数
    [InlineData("NH3、H2O、CH4 的空间构型分别是什么？")]    // 分别 → 约定不一致
    [InlineData("XCl3 分子的空间构型是什么？")]            // 微粒不在表 → 不臆造
    [InlineData("NH3 的沸点是多少？")]                    // 无构型锚点
    [InlineData("下列说法正确的是 A. NH3 是三角锥形 B. ...")] // 选择题块 → 不介入
    public void 边界外_一律返回null(string raw) => Assert.Null(Solve(raw));

    [Fact]
    public void 选择题不触发()
    {
        var fired = GatewayVsLlmEvaluationService.BuildTestCases()
            .Count(c => c.Type == "choice" && Solve(c.Question) is not null);
        Assert.Equal(0, fired);
    }

    // ============================================================
    // 护栏：遍历全量用例集（手工题 + PDF 真题，与生产完全一致），
    // 凡求解器返回非 null，必须判正 —— 否则会损害精确率或帮倒忙。
    // 已知坏键（化学结论正确但答案键错位）显式列入允许列表，并锁定触发集合防回归。
    // ============================================================
    [Fact]
    public void Guardrail_全题库无假阳性()
    {
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        Assert.NotEmpty(cases);

        // 已知坏键：pdf-507 SnCl2 的 VSEPR 模型，化学上应为平面三角形（3 价层电子对：2 成键+1 孤对），
        // 但 PDF 抽取答案键错位为「四面体形」。结论正确但键不匹配 → 评估侧不采纳（不计引擎解出），
        // 精确率与 harm 均不受影响。
        var brokenKeys = new HashSet<string> { "pdf-507" };

        var firedIds = new List<string>();
        foreach (var c in cases)
        {
            if (c.Type == "choice") continue; // 求解器不介入选择题
            var r = Solve(c.Question);
            if (r is null) continue;
            firedIds.Add(c.Id);

            if (brokenKeys.Contains(c.Id))
            {
                Assert.False(Correct(r.FinalResult, c),
                    $"{c.Id} 在坏键允许列表中却意外判正，需复核数据或允许列表");
                continue;
            }
            Assert.True(Correct(r.FinalResult, c),
                $"{c.Id} 求解器返回 {r.FinalResult} 但未通过判分 → 会损害精确率或帮倒忙");
        }

        // 锁定当前题库行为：5 手工 + pdf-508 干净判正 + pdf-507 坏键 = 7 触发；
        // 任何新增触发都必须先判明正确性。
        Assert.Equal(
            new[] { "d6-1", "d6-2", "d6-3", "d6-4", "d6-5", "pdf-507", "pdf-508" },
            firedIds.OrderBy(x => x));
    }
}
