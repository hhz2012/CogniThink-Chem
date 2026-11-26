using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// 网关层 VSEPR / 分子构型确定性求解器（阶段5 / Tier 2）验收 + 护栏测试。
/// 真值源：高中常见微粒「分子构型 / VSEPR 模型 / 杂化」对照表。
/// 重点验证：7 道干净填空被解出且判正（d6-1~5 + pdf-507 + pdf-508，pdf-507 原坏键已校正为平面三角形）；
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
    // 已知坏键（化学结论正确但答案键错位）显式列入允许列表，并锁定触发集合防回归。（pdf-507 已校正，当前无坏键）
    // ============================================================
    [Fact]
    public void Guardrail_全题库无假阳性()
    {
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        Assert.NotEmpty(cases);

        // pdf-507 原答案键错位为「四面体形」，化学正确为平面三角形（2 成键+1 孤对，价层 3 对）；
        // P1 已校正为平面三角形，现干净判正。当前 VSEPR 题库无坏键，精确率与 harm 均不受影响。
        // （保留 brokenKeys 机制以备未来抽取噪声。）
        var brokenKeys = new HashSet<string>();

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

        // 锁定当前题库行为：5 手工 + pdf-507 + pdf-508 干净判正 = 7 触发（pdf-507 原坏键已校正）；
        // 任何新增触发都必须先判明正确性。
        Assert.Equal(
            new[] { "d6-1", "d6-2", "d6-3", "d6-4", "d6-5", "pdf-507", "pdf-508" },
            firedIds.OrderBy(x => x));
    }

    // ============================================================
    // 键角比较原因求解器（TrySolveBondAngleReason）：通用孤电子对排斥推理
    // 与 TrySolve 互不干扰（后者拒绝含「键角/原因/比」的题）。
    // ============================================================
    private static EngineResponse? SolveBar(string raw) =>
        VseprSolver.TrySolveBondAngleReason(new EngineRequest { TaskType = "unknown", RawText = raw });

    [Fact]
    public void Pdf509_AsCl3_BCl3_键角比较原因_判正()
    {
        var c = Case("pdf-509");
        Assert.NotNull(c);
        var r = SolveBar(c!.Question);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("孤电子对", r.FinalResult);
        Assert.Contains("排斥", r.FinalResult);
        Assert.True(Correct(r.FinalResult, c), "pdf-509 键角比较原因结论应通过判分");
    }

    [Theory]
    [InlineData("比较 NH3 和 PH3 的沸点并解释原因")]                 // 非键角 → 不介入
    [InlineData("AsCl3 的空间构型是什么？")]                       // 无原因问法 → 走 TrySolve，不走键角原因
    [InlineData("(1) 比较 H2O 与 SO4^2- 的键角并解释原因 (2) ...")]  // 多段（2+ 编号小题）→ 不介入
    [InlineData("下列说法正确的是 A. AsCl3 键角小于 BCl3 B. ...")]  // 选择题块 → 不介入
    [InlineData("XCl3 的键角比 YCl3 小，原因是")]                  // 微粒不在孤对表 → 不臆造
    public void 键角原因_边界外_一律返回null(string raw) => Assert.Null(SolveBar(raw));

    [Fact]
    public void BondAngleReason_全题库无假阳性()
    {
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        Assert.NotEmpty(cases);

        var firedIds = new List<string>();
        foreach (var c in cases)
        {
            if (c.Type == "choice") continue; // 求解器不介入选择题
            var r = SolveBar(c.Question);
            if (r is null) continue;
            firedIds.Add(c.Id);
            Assert.True(Correct(r.FinalResult, c),
                $"{c.Id} 键角原因求解器返回 {r.FinalResult} 但未通过判分 → 损害精确率");
        }

        // 锁定：当前题库仅 pdf-509 命中（AsCl3 vs BCl3）；pdf-506 为多段碎片被多段守卫排除，
        // pdf-501 为选择题被排除。任何新增触发都必须先判明正确性。
        Assert.Equal(new[] { "pdf-509" }, firedIds.OrderBy(x => x));
    }

    // ============================================================
    // 整题兜底变体（TrySolveBondAngleReasonForMultiPart，T5）：
    // 允许多部分整题触发，按编号子问切片，仅在含「键角+原因+比较」的段内求解。
    // 用于分发失败后的整题兜底（OR 采纳门），构造性零 LOST、零假阳性。
    // ============================================================
    private static EngineResponse? SolveBarMp(string raw) =>
        VseprSolver.TrySolveBondAngleReasonForMultiPart(new EngineRequest { TaskType = "unknown", RawText = raw });

    [Fact]
    public void Pdf506_整题多段_键角比较原因_子问切片判正()
    {
        var c = Case("pdf-506");
        Assert.NotNull(c);
        // 原方法（非多段守卫）必须仍拒绝 pdf-506 整题 → 守卫零回归
        Assert.Null(SolveBar(c!.Question));
        // 整题变体：按编号子问切片后，sub-2（H2O vs SO4^2-）唯一解出
        var r = SolveBarMp(c.Question);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("孤电子对斥力大", r.FinalResult);
        Assert.Contains("压缩键角", r.FinalResult);
        Assert.True(Correct(r.FinalResult, c), "pdf-506 整题变体结论应命中答案键（OR 采纳）");
    }

    [Fact]
    public void BondAngleReasonMultiPart_全题库无假阳性()
    {
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        Assert.NotEmpty(cases);

        var firedIds = new List<string>();
        foreach (var c in cases)
        {
            if (c.Type == "choice") continue; // 求解器不介入选择题
            var r = SolveBarMp(c.Question);
            if (r is null) continue;
            firedIds.Add(c.Id);
            Assert.True(Correct(r.FinalResult, c),
                $"{c.Id} 整题变体返回 {r.FinalResult} 但未通过判分 → 损害精确率");
        }

        // 锁定：pdf-509（单段，与原方法一致）+ pdf-506（多段切片 sub-2）。
        // 任何新增触发都必须先判明正确性（全库 dry-fire 已证零 baseFalse 误采纳）。
        Assert.Equal(new[] { "pdf-506", "pdf-509" }, firedIds.OrderBy(x => x));
    }

    // ============================================================
    // 路由回归：vsepr 分支必须兜底到 TrySolveBondAngleReason。
    // 生产中 LLM 把 pdf-509 归为 vsepr（ClassifyTaskDeterministic 对该题判 unknown、不覆盖 LLM 判定），
    // 而 VseprSolver.TrySolve 显式拒绝含「键角/原因」的题 → 若无兜底则 Ok=null 漏解。
    // 修复：case "vsepr" 在 TrySolve 返回 null 后回退 BondAngleReason（其内置守卫保证域外一律 null → 零回归）。
    // ============================================================
    private static LlmGatewayService NewGateway() =>
        (LlmGatewayService)RuntimeHelpers.GetUninitializedObject(typeof(LlmGatewayService));

    [Fact]
    public async Task Vsepr分支_键角原因题_兜底到BondAngleReason_pdf509()
    {
        var c = Case("pdf-509");
        Assert.NotNull(c);
        // 模拟生产路由：LLM 归 vsepr（确定性规则对该题判 unknown，不覆盖）
        var req = new EngineRequest { TaskType = "vsepr", RawText = c!.Question };
        var resp = await NewGateway().CallEngineAsync(req, new List<ImageAsset>());

        Assert.NotNull(resp);
        Assert.True(resp!.Ok == true, $"vsepr 键角原因题应兜底判正，got FinalResult={resp.FinalResult}");
        Assert.Contains("孤电子对", resp.FinalResult);
        Assert.True(Correct(resp.FinalResult, c), "vsepr 兜底结论应通过判分");
    }
}
