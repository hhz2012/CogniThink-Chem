using CogniThink.TutorBrain.Application.Common;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// LlmGatewayService 的 curve 资产路由分部。
/// 有 curve 资产且能从题干识别出明确查询意图 → 调 CurveQuery 返回数值读数（图上真值，不臆造）；
/// 识别不出意图 / 查询失败 → 返回 null 回退通用 task_type 分支，不损失原有能力、不蒙数。
/// </summary>
public sealed partial class LlmGatewayService
{
    private EngineResponse? TryRouteCurve(EngineRequest req, IReadOnlyList<ImageAsset>? imageAssets)
    {
        var asset = imageAssets?.FirstOrDefault(a => a.Type == "curve");
        if (asset is null) return null;

        var chart = CurveQuery.Parse(asset);
        if (chart is null) return null;

        var intent = CurveIntentExtractor.Extract(req.RawText ?? string.Empty, chart);
        if (intent is null) return null; // 无明确查询意图 → 回退，绝不猜

        var result = CurveQuery.Query(asset, intent);
        if (!result.Ok) return null; // 查询失败（超范围 / 无交点 / 点不足）→ 回退，不蒙数

        return new EngineResponse
        {
            Ok = true,
            FinalResult = result.FinalResult,
            ConstraintNote = $"【curve 引擎】曲线数值查询（{intent.Type}）：x 轴「{chart.XLabel}」/ y 轴「{chart.YLabel}」",
            Confidence = 0.9,
            AssetKind = "curve",
            NumericValue = result.Value,
            NumericValue2 = result.Value2,
        };
    }
}
