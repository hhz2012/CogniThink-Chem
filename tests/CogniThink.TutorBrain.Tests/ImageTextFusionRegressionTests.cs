using System.Runtime.CompilerServices;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// P2-3 图像与文本求解融合：回归保护。
/// <para>断言：</para>
/// <para>1) 契约加性：<c>EngineResponse</c> 新增 <c>ResultProvenance</c> 默认 null，不影响既有字段；</para>
/// <para>2) 各融合分支在产出结论时正确标记 provenance，且 <b>不改</b> FinalResult/Ok/TaskType/AssetKind 等判决字段（纯加性）；</para>
/// <para>3) 曲线原始读数（curve_reading，中间量高风险类）永不被答案键采纳 —— 防 pdf-272 类 harm 回退。</para>
/// <para>红线：precision=1 / harm=0 / 不蒙数；eval 侧零行为变更由 T4 diff=0 实证，本测试钉住单元级回归。</para>
/// </summary>
public class ImageTextFusionRegressionTests
{
    private static LlmGatewayService NewGateway() =>
        (LlmGatewayService)RuntimeHelpers.GetUninitializedObject(typeof(LlmGatewayService));

    private static QaTestCase Case(string id) =>
        GatewayVsLlmEvaluationService.GenPdfQuestions().First(c => c.Id == id);

    private static EngineRequest EqReq(QaTestCase c) =>
        new() { RawText = c.Question, TaskType = "equilibrium" };

    // ==================== 1. 契约加性 ====================

    [Fact]
    public void EngineResponse_ResultProvenance_DefaultsNull_DoesNotAffectOtherFields()
    {
        var resp = new EngineResponse { FinalResult = "x=1", Ok = true };
        Assert.Null(resp.ResultProvenance); // 新增字段默认 null（通用文本瀑布 = text_fact）
        Assert.Equal("x=1", resp.FinalResult);
        Assert.True(resp.Ok);

        resp.ResultProvenance = "curve_reading";
        Assert.Equal("curve_reading", resp.ResultProvenance);
        Assert.Equal("x=1", resp.FinalResult); // 设标签不改判决字段
        Assert.True(resp.Ok);
    }

    // ==================== 2. provenance 标记正确且纯加性（不改判决字段） ====================

    /// <summary>
    /// pdf-259：equilibrium 确定性求解器抢占曲线 → 产出 ΔH=-3。
    /// P2-3 应标记 provenance="equilibrium"，且结论内容/可采纳性与修复后完全一致（纯加性，零回归）。
    /// </summary>
    [Fact]
    public async Task Pdf259_Equilibrium_TaggedProvenanceEquilibrium_AndConclusionUnchanged()
    {
        var c = Case("pdf-259");
        Assert.Contains(c.ImageAssets, a => a.Type == "curve");

        var resp = await NewGateway().CallEngineAsync(EqReq(c), c.ImageAssets);

        // provenance 标记
        Assert.Equal("equilibrium", resp.ResultProvenance);
        // 判决字段未被 provenance 改动污染：仍是平衡结论（非曲线读数）
        Assert.True(resp.Ok == true, resp.FinalResult);
        Assert.NotEqual("curve", resp.AssetKind);
        Assert.DoesNotContain("3.437", resp.FinalResult);
        Assert.Contains("-3", resp.FinalResult);
        // 仍可被门控采纳（零回归）
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(resp.FinalResult, c),
            $"结论未通过答案键校验：{resp.FinalResult}");
    }

    // ==================== 3. 曲线原始读数永不被采纳（回归守卫，核心红线） ====================

    /// <summary>
    /// pdf-272：无平衡求解器命中 → 回退曲线原始读数（中间量 "x=2.5 时 y=0.8"）。
    /// P2-3：该读数应标记 provenance="curve_reading"（高风险类可观测），且 <b>不得</b> 通过答案键校验 → 不采纳 → 无 harm。
    /// 若未来有人改门控让 curve_reading 被采纳，本测试立即红。
    /// </summary>
    [Fact]
    public async Task Pdf272_CurveReading_TaggedProvenance_AndNeverAdopted()
    {
        var c = Case("pdf-272");
        Assert.Contains(c.ImageAssets, a => a.Type == "curve");

        var resp = await NewGateway().CallEngineAsync(EqReq(c), c.ImageAssets);

        // 回退到曲线读数（能力不丢失）→ 标记为高风险 provenance
        Assert.Equal("curve_reading", resp.ResultProvenance);
        Assert.Equal("curve", resp.AssetKind);

        // 核心红线：曲线中间量读数不得被答案键采纳（不蒙数 / 零 harm）
        Assert.False(GatewayVsLlmEvaluationService.IsAnswerCorrect(resp.FinalResult, c),
            $"曲线中间量读数不应被采纳：{resp.FinalResult}");
    }

    /// <summary>
    /// 合成守卫：典型曲线中间量读数模式（"x=.. 时，曲线「..」的 y = .."）即使数值碰巧等于
    /// 答案键中的某个数字，也不应被关键词答案键采纳为最终结论。
    /// （防 MatchCurveNumeric / 关键词宽松匹配导致的 latent 假阳性。）
    /// </summary>
    [Fact]
    public void Synthetic_CurveIntermediateReading_NotAdoptedAsFinalAnswer()
    {
        var c = Case("pdf-272");
        // 中间量读数（非最终答案 HC2O4- + H+）
        const string reading = "x=2.5 时，曲线「H₂C₂O₄」的 y = 0.8";
        Assert.False(GatewayVsLlmEvaluationService.IsAnswerCorrect(reading, c),
            "曲线中间量读数不得被答案键采纳");
    }
}
