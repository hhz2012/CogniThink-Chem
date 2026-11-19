using System.Collections.Generic;
using System.Linq;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// 网关层原子结构确定性求解器（阶段4 / Tier 1）验收 + 护栏测试。
/// 真值源：PeriodicTable（原子序数 → 基态/离子电子排布、价层、周期族、未成对电子数）。
/// 重点验证：PDF 题库中 4 道干净填空被解出且判正；任何边界外/歧义/坏键情形一律不损害精确率、不帮倒忙。
/// </summary>
public class AtomicStructureSolverTests
{
    private static EngineResponse? Solve(string raw) =>
        AtomicStructureSolver.TrySolve(new EngineRequest { TaskType = "concept", RawText = raw });

    private static bool Correct(string answer, QaTestCase c) =>
        GatewayVsLlmEvaluationService.IsAnswerCorrect(answer, c);

    private static QaTestCase? PdfCase(string id) =>
        GatewayVsLlmEvaluationService.GenPdfQuestions().FirstOrDefault(x => x.Id == id);

    // ============================================================
    // 正例：4 道干净新解（均为 concept 类、答案键正确）
    // ============================================================

    [Fact]
    public void Pdf482_Be2plus_轨道表示式_判正()
    {
        var c = PdfCase("pdf-482");
        Assert.NotNull(c);
        var r = Solve(c!.Question);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("1s²", r.FinalResult);
        Assert.True(Correct(r.FinalResult, c), "Be²⁺ 轨道表示式应通过判分");
    }

    [Fact]
    public void Pdf487_Sb_价层电子排布式_判正()
    {
        var c = PdfCase("pdf-487");
        Assert.NotNull(c);
        var r = Solve(c!.Question);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("5s²5p³", r.FinalResult);
        Assert.True(Correct(r.FinalResult, c), "Sb 价层电子排布应通过判分");
    }

    [Fact]
    public void Pdf490_Mo_价层电子排布图_判正()
    {
        var c = PdfCase("pdf-490");
        Assert.NotNull(c);
        var r = Solve(c!.Question);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("4d⁵5s¹", r.FinalResult);
        Assert.True(Correct(r.FinalResult, c), "Mo 价层电子排布应通过判分");
    }

    [Fact]
    public void Pdf538_Ga_价电子排布式_判正()
    {
        var c = PdfCase("pdf-538");
        Assert.NotNull(c);
        var r = Solve(c!.Question);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("4s²4p¹", r.FinalResult);
        Assert.True(Correct(r.FinalResult, c), "Ga 价电子排布应通过判分");
    }

    // ============================================================
    // 负例：边界外一律返回 null（不蒙数）
    // ============================================================

    [Theory]
    [InlineData("什么是电解质？")]                                          // 无锚点
    [InlineData("基态Ni的价电子排布式是3d⁸4s²")]                          // 锚点 + 元素，但无填空横线
    [InlineData("Fe²⁺与Fe³⁺的未成对电子数之比为______。")]                // 求比值 → 约定不一致，不蒙数
    [InlineData("铜是一种常见的金属元素，请问它位于元素周期表第____周期")]  // 元素距锚点 >14 字
    public void 边界外_一律返回null(string raw) => Assert.Null(Solve(raw));

    [Fact]
    public void 选择题不触发()
    {
        // 生产钩子不介入选择题；且题库中选择题无任何一题会触发求解器。
        var fired = GatewayVsLlmEvaluationService.GenPdfQuestions()
            .Where(c => c.Type == "choice")
            .Count(c => Solve(c.Question) is not null);
        Assert.Equal(0, fired);
    }

    // ============================================================
    // 护栏：遍历 PDF 全题库（与生产完全一致的用例集），
    // 凡求解器返回非 null，必须判正 —— 否则会损害精确率或帮倒忙。
    // 已知坏键（答案键缺失/错位）显式列入允许列表，并锁定触发集合防回归。
    // ============================================================
    [Fact]
    public void Guardrail_全题库无假阳性()
    {
        var cases = GatewayVsLlmEvaluationService.GenPdfQuestions();
        Assert.NotEmpty(cases);

        // 已知答案键缺失/错位（PDF 抽取噪声）：化学结论正确但键不匹配；
        // 且二者分类为 calculation，生产钩子（仅 concept/unknown）不介入，
        // 评估侧也只采纳校验通过的结论 → 精确率与帮倒忙均不受影响。
        var brokenKeys = new HashSet<string> { "pdf-108", "pdf-166" };

        var firedIds = new List<string>();
        foreach (var c in cases)
        {
            if (c.Type == "choice") continue; // 生产钩子不介入选择题
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

        // 锁定当前题库行为：4 个干净新解 + 2 个坏键 = 6 触发；任何新增触发都必须先判明正确性。
        Assert.Equal(
            new[] { "pdf-108", "pdf-166", "pdf-482", "pdf-487", "pdf-490", "pdf-538" },
            firedIds.OrderBy(x => x));
    }
}
