using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Evaluation;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// Task 11 / AC-7：评估摘要按资产类型拆分（graph/curve/molecule）的聚合逻辑，
/// 不依赖 LLM，直接验证 BuildSummary 产出 coverage / structuredHits 字段且无未捕获异常。
/// </summary>
public class EvaluationSummaryTests
{
    private static QaCaseResult MakeAssetResult(string assetType, bool structuredHit, string? assetKind) =>
        new()
        {
            Case = new QaTestCase { ImageAssets = new List<ImageAsset> { new() { Type = assetType } } },
            StructuredHit = structuredHit,
            StructuredAssetKind = assetKind
        };

    /// <summary>TR-11.1：摘要含每类资产的 cases / structuredHits 字段，且聚合正确。</summary>
    [Fact]
    public void BuildSummary_按资产类型拆分聚合正确()
    {
        var results = new List<QaCaseResult>
        {
            MakeAssetResult("graph", structuredHit: true, assetKind: "graph"),
            MakeAssetResult("curve", structuredHit: true, assetKind: "curve"),
            MakeAssetResult("curve", structuredHit: false, assetKind: null), // 有 curve 资产但未结构化命中
            MakeAssetResult("molecule", structuredHit: false, assetKind: null),
            new QaCaseResult { Case = new QaTestCase { ImageAssets = new List<ImageAsset>() } }, // 无资产
        };

        var summary = GatewayVsLlmEvaluationService.BuildSummary(results, llmAvailable: false);

        Assert.Equal(5, summary.TotalCases);
        Assert.Equal(1, summary.GraphCases);
        Assert.Equal(1, summary.GraphStructuredHits);
        Assert.Equal(2, summary.CurveCases);
        Assert.Equal(1, summary.CurveStructuredHits);
        Assert.Equal(1, summary.MoleculeCases);
        Assert.Equal(0, summary.MoleculeStructuredHits);
    }

    /// <summary>
    /// TR-11.2：混合数据（含 null 条目、Case 为 null、帮倒忙样本）跑汇总无未捕获异常。
    /// 对应真实评估中单个 case 异常时 results[i] 可能为 null 的边界。
    /// </summary>
    [Fact]
    public void BuildSummary_混合边界数据_无未捕获异常且计数正确()
    {
        var results = new List<QaCaseResult>
        {
            null!,                                // 空条目：应被跳过不抛异常
            MakeAssetResult("molecule", structuredHit: true, assetKind: "molecule"),
            new QaCaseResult { Case = null! },    // Case 为空：资产拆分应安全降级
            new QaCaseResult                      // 帮倒忙：引擎采用但答错，纯 LLM 答对
            {
                GatewayTruthAdopted = true,
                GatewayCorrect = false,
                PureLlmCorrect = true
            },
        };

        var summary = GatewayVsLlmEvaluationService.BuildSummary(results, llmAvailable: true);

        Assert.Equal(4, summary.TotalCases);
        Assert.Equal(1, summary.MoleculeCases);
        Assert.Equal(1, summary.MoleculeStructuredHits);
        Assert.Equal(1, summary.HarmCount);
        Assert.True(summary.LlmAvailable);
    }

    /// <summary>同一题拥有多类资产时，每类 Cases 都应计数。</summary>
    [Fact]
    public void BuildSummary_一题多资产_每类均计数()
    {
        var r = new QaCaseResult
        {
            Case = new QaTestCase
            {
                ImageAssets = new List<ImageAsset>
                {
                    new() { Type = "graph" },
                    new() { Type = "curve" }
                }
            },
            StructuredHit = true,
            StructuredAssetKind = "curve"
        };

        var summary = GatewayVsLlmEvaluationService.BuildSummary(new List<QaCaseResult> { r }, llmAvailable: false);

        Assert.Equal(1, summary.GraphCases);
        Assert.Equal(0, summary.GraphStructuredHits); // 命中的是 curve，不是 graph
        Assert.Equal(1, summary.CurveCases);
        Assert.Equal(1, summary.CurveStructuredHits);
    }
}
