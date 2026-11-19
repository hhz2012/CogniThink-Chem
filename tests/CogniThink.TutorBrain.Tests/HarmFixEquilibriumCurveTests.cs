using System.Runtime.CompilerServices;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// 阶段3F / AC-22：修复 equilibrium 曲线原始读数被误采纳造成的 2 个 harm（pdf-259 / pdf-272）。
/// <para>两层修复：</para>
/// <para>1) 评估门控收紧（<c>BuildGatewayAnswerAsync</c> equilibrium 非选择分支）：
/// 只采纳 <c>IsAnswerCorrect</c> 通过的结论，曲线中间量读数被拦截 → 不采纳 → 无 harm；</para>
/// <para>2) 引擎抢占顺序（<c>CallEngineAsync</c>）：equilibrium 确定性求解器优先于曲线路由，
/// 命中（Ok=true）即返回；未命中回退曲线，不损失曲线能力。</para>
/// <para>红线：不新增 harm、原正确 equilibrium 结论零回归（lost=0）、enginePrecision 不降。</para>
/// </summary>
public class HarmFixEquilibriumCurveTests
{
    private static LlmGatewayService NewGateway() =>
        (LlmGatewayService)RuntimeHelpers.GetUninitializedObject(typeof(LlmGatewayService));

    /// <summary>加载与评估完全同口径的真实用例（AnswerKeywords / Question / ImageAssets）。</summary>
    private static QaTestCase Case(string id) =>
        GatewayVsLlmEvaluationService.GenPdfQuestions().First(c => c.Id == id);

    private static EngineRequest EqReq(QaTestCase c) =>
        new() { RawText = c.Question, TaskType = "equilibrium" };

    // ==================== Fix 1：门控谓词 IsAnswerCorrect ====================
    // 曲线原始读数是中间量（某点浓度），并非题目最终答案，不得通过答案键校验；
    // 门控据此返回 null → 不采纳 → 消除 harm。正确的平衡结论仍须通过 → 零回归。

    [Fact]
    public void Pdf259_曲线原始读数_不被答案键采纳()
    {
        var c = Case("pdf-259");
        // 修复前被误采纳的 harm 结论（曲线某点浓度，非 ΔH 最终答案）
        const string harm = "x=3 时，曲线「乙醇浓度」的 y = 3.437";
        Assert.False(GatewayVsLlmEvaluationService.IsAnswerCorrect(harm, c),
            $"曲线读数不应命中 pdf-259 答案键。keywords={string.Join("|", c.AnswerKeywords)}");
    }

    [Fact]
    public void Pdf259_正确平衡结论_仍被采纳()
    {
        var c = Case("pdf-259");
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect("ΔH = -3 kJ/mol", c),
            "正确的 ΔH=-3 结论应通过答案键校验（零回归：正确 equilibrium 结论不 lost）");
    }

    [Fact]
    public void Pdf272_曲线原始读数_不被答案键采纳()
    {
        var c = Case("pdf-272");
        const string harm = "x=2.5 时，曲线「H₂C₂O₄」的 y = 0.8";
        Assert.False(GatewayVsLlmEvaluationService.IsAnswerCorrect(harm, c),
            $"曲线读数不应命中 pdf-272 答案键。keywords={string.Join("|", c.AnswerKeywords)}");
    }

    [Fact]
    public void Pdf272_正确平衡结论_仍被采纳()
    {
        var c = Case("pdf-272");
        // 命中关键词 "HC2O4- + H+"（经评估侧归一化后子串一致）
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect("H2C2O4 ⇌ HC2O4- + H+", c));
    }

    // ==================== Fix 2：CallEngineAsync 抢占顺序（端到端） ====================

    /// <summary>
    /// pdf-259：equilibrium 求解器（燃烧热法）须在曲线路由之前命中，
    /// 产出 ΔH=-3 而非曲线读数 y=3.437；且结论能被门控采纳（从 harm 翻转为 gained）。
    /// </summary>
    [Fact]
    public async Task Pdf259_平衡求解器抢占曲线_产出ΔH而非读数()
    {
        var c = Case("pdf-259");
        // 前提：pdf-259 带 curve 资产——若无 Fix 2 抢占，曲线读数会抢先返回（正是原 harm 成因）
        Assert.Contains(c.ImageAssets, a => a.Type == "curve");

        var resp = await NewGateway().CallEngineAsync(EqReq(c), c.ImageAssets);

        Assert.True(resp.Ok == true, resp.FinalResult);
        // 不是曲线原始读数
        Assert.NotEqual("curve", resp.AssetKind);
        Assert.DoesNotContain("3.437", resp.FinalResult);
        Assert.DoesNotContain("曲线", resp.FinalResult);
        // 燃烧热法产出 ΔH = -3（命中答案键关键词 "-3"）
        Assert.Contains("-3", resp.FinalResult);
        // 产出的结论可被门控采纳——否则仍不计正确（Fix 1 与 Fix 2 协同验证）
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(resp.FinalResult, c),
            $"求解器结论未通过答案键校验：{resp.FinalResult}");
    }

    /// <summary>
    /// pdf-272：无平衡子求解器命中 → 回退曲线（能力不丢失）；
    /// 无论回退到曲线读数还是保持未决，结论都不得通过答案键校验 → Fix 1 门控拦截，不产生 harm。
    /// </summary>
    [Fact]
    public async Task Pdf272_无求解器命中_回退曲线且不被采纳()
    {
        var c = Case("pdf-272");
        Assert.Contains(c.ImageAssets, a => a.Type == "curve");

        var resp = await NewGateway().CallEngineAsync(EqReq(c), c.ImageAssets);

        // 核心红线：不得产出能被采纳的错误结论（无新增 harm）
        Assert.False(GatewayVsLlmEvaluationService.IsAnswerCorrect(resp.FinalResult, c),
            $"pdf-272 不应产出被采纳的错误结论：{resp.FinalResult}");
        // 曲线能力不丢失：有 curve 资产时回退到曲线读数（运行时仍返回给学生，仅评估不计正确）
        Assert.True(resp.AssetKind == "curve" || resp.Ok != true,
            $"有曲线资产时应回退曲线读数或保持未决，实际：AssetKind={resp.AssetKind} Ok={resp.Ok}");
    }
}
