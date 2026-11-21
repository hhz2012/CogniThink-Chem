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

    [Fact]
    public void Pdf488_Mn_周期族_回退扫描判正()
    {
        // Mn 与锚点「位于第」之间隔有「在元素周期表中」，符号不在窗口末尾；
        // 靠 FindElement 回退扫描命中 Mn → 周期族变体命中 answer「四；VIIB」子串。
        var c = PdfCase("pdf-488");
        Assert.NotNull(c);
        var r = Solve(c!.Question);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("VIIB", r.FinalResult);
        Assert.True(Correct(r.FinalResult, c), "Mn 周期族应通过判分");
    }

    [Fact]
    public void Pdf489_Sn_最外层轨道表示式_回退扫描判正()
    {
        // Sn 与锚点「轨道表示式」之间隔有「的基态原子最外层」；回退扫描命中 Sn →
        // 全配置变体命中 answer「² ²」子串。
        var c = PdfCase("pdf-489");
        Assert.NotNull(c);
        var r = Solve(c!.Question);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("5s²5p²", r.FinalResult);
        Assert.True(Correct(r.FinalResult, c), "Sn 最外层轨道表示式应通过判分");
    }

    [Fact]
    public void Pdf533_Te_简化电子排布_锚点重排不回归()
    {
        // 锚点重排后「简化电子排布」须先于通用「电子排布式」命中，返回 [Kr] 简写而非全配置。
        var c = PdfCase("pdf-533");
        Assert.NotNull(c);
        var r = Solve(c!.Question);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("[Kr]", r.FinalResult);
        Assert.DoesNotContain("1s²", r.FinalResult); // 不能退化为全配置
        Assert.True(Correct(r.FinalResult, c), "Te 简化电子排布应通过判分");
    }

    [Fact]
    public void Pdf485_S_价层两种自旋电子数之比_判正()
    {
        // 官方源 page_064：S(Z=16) 价层 3s²3p⁴ → 轨道表示式 3s↑↓ 3p↑↓↑↑ → 两种自旋电子数之比 2:1（或1:2）。
        // 原存储键 1:1 及其解析（p⁴ 必有 2↑2↓）违反洪特规则，已订正为 2:1（或1:2）；
        // 引擎新增「两种自旋状态的电子数之比」锚点 + PeriodicTable.ValenceSpinCounts 解出。
        var c = PdfCase("pdf-485");
        Assert.NotNull(c);
        var r = Solve(c!.Question);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("2:1", r.FinalResult);
        Assert.Contains("1:2", r.FinalResult);
        Assert.True(Correct(r.FinalResult, c), "S 价层自旋比应通过判分");
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
        // 生产钩子不介入选择题（CallEngineAsync 仅 concept/unknown/atomic_structure 调 AtomicStructureSolver）。
        // 题库中选择题除 pdf-486 外无任何一题触发求解器。pdf-486 是「填空+双选」复合题（type=choice），
        // 其题干前缀含填空锚点（基态N原子价层电子排布式），但生产按 choice 路由不经原子求解器，零 harm；
        // 锁定触发集合恰好为 {pdf-486}，防止其它选择题意外过触发。
        var fired = GatewayVsLlmEvaluationService.GenPdfQuestions()
            .Where(c => c.Type == "choice" && Solve(c.Question) is not null)
            .Select(c => c.Id)
            .OrderBy(x => x)
            .ToList();
        Assert.Equal(new[] { "pdf-486" }, fired);
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
        // pdf-166 / pdf-190 仍错位（不被采纳，零 harm）；pdf-108 / pdf-173 已校正键，现干净判正。
        // pdf-188：题干为「基态Fe2+的3d电子轨道表示式」，存储 answer 为 "sp3"（串题/错位），
        //   求解器输出 Fe²⁺ 全配置（化学正确）但无法匹配坏键 → 不被采纳，零 harm，列入白名单。
        // 评估侧只采纳校验通过的结论 → 精确率与帮倒忙均不受影响。
        var brokenKeys = new HashSet<string> { "pdf-166", "pdf-188", "pdf-190" };

        var firedIds = new List<string>();
        var notAdoptedButFired = new List<string>(); // 触发但未通过判分（非坏键）→ 会损害精确率
        var brokenButAdopted = new List<string>();   // 坏键却意外判正 → 需复核数据/允许列表
        foreach (var c in cases)
        {
            if (c.Type == "choice") continue; // 生产钩子不介入选择题
            var r = Solve(c.Question);
            if (r is null) continue;
            firedIds.Add(c.Id);

            var correct = Correct(r.FinalResult, c);
            if (brokenKeys.Contains(c.Id))
            {
                if (correct) brokenButAdopted.Add($"{c.Id}({r.FinalResult})");
            }
            else if (!correct)
            {
                notAdoptedButFired.Add($"{c.Id}({r.FinalResult})");
            }
        }

        // 一次性报告全部违规，避免逐次只暴露首个（加速发现新增触发）。
        Assert.Empty(notAdoptedButFired);
        Assert.Empty(brokenButAdopted);

        // 锁定当前题库行为：pdf-108 / pdf-173 已校正答案键（原错位，现干净判正）；
        // pdf-166 / pdf-188 / pdf-190 仍为答案键错位（不被采纳，零 harm）；其余为干净判正。
        // 新增触发：pdf-488（Mn 周期族）/ pdf-489（Sn 最外层轨道表示式）由 FindElement 回退扫描解出；
        // 锚点重排（简化电子排布先于通用电子排布式）保证 pdf-533 不回归。任何新增触发都必须先判明正确性。
        // 注意：pdf-486 原为 fill（前缀含「基态N原子价层电子排布式」填空锚点），现改为 choice 以走双选判分；
        // 本护栏跳过 choice 题（见上方 continue），故 pdf-486 不再出现在触发集合中（生产 choice 路由不经原子求解器，零 harm）。
        Assert.Equal(
            new[] { "pdf-108", "pdf-166", "pdf-173", "pdf-188", "pdf-190", "pdf-482", "pdf-485", "pdf-487", "pdf-488", "pdf-489", "pdf-490", "pdf-533", "pdf-538" },
            firedIds.OrderBy(x => x));
    }
}
