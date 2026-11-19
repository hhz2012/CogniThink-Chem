using System.Text.Json;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Molecules;
using CogniThink.TutorBrain.Application.Common;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// LlmGatewayService 的 molecule 资产路由分部。
/// 有 molecule 资产（SMILES / molblock）→ 调 MoleculeAnalyzer 输出结构特征作为解题上下文（RDKit 真值，不臆造）；
/// 解析失败 → 返回 null 回退通用分支。选择题不短路（保留原有选项校验路径，避免用特征替换选项答案）。
/// </summary>
public sealed partial class LlmGatewayService
{
    private static readonly Regex ChoiceOptionRe = new(@"(^|\n)\s*[A-D][.．、]\s", RegexOptions.Compiled);

    private EngineResponse? TryRouteMolecule(EngineRequest req, IReadOnlyList<ImageAsset>? imageAssets)
    {
        // 选择题保留原有选项校验路径，不让分子特征文本替换选项答案
        if (ChoiceOptionRe.IsMatch(req.RawText ?? string.Empty)) return null;
        if (req.TaskType == "choice") return null;

        var asset = imageAssets?.FirstOrDefault(a => a.Type == "molecule");
        if (asset is null) return null;

        string? smiles = null, molblock = null;
        try
        {
            var payload = asset.Payload;
            if (payload.HasValue && payload.Value.ValueKind == JsonValueKind.Object)
            {
                var p = payload.Value;
                if (p.TryGetProperty("smiles", out var sp) && sp.ValueKind == JsonValueKind.String)
                    smiles = sp.GetString();
                if (p.TryGetProperty("molblock", out var mp) && mp.ValueKind == JsonValueKind.String)
                    molblock = mp.GetString();
            }
        }
        catch
        {
            // payload 结构异常 → 交由 Analyze 判定失败
        }

        var result = MoleculeAnalyzer.Analyze(smiles, molblock);
        if (!result.Success) return null; // 解析失败如实回退，不臆造特征

        return new EngineResponse
        {
            Ok = true,
            FinalResult = result.FinalResult,
            ConstraintNote = "【molecule 引擎】分子结构特征（RDKit 真值）",
            Confidence = 0.9,
            AssetKind = "molecule",
            MoleculeFormula = result.MolecularFormula,
            MoleculeUnsaturation = result.DegreeOfUnsaturation,
            MoleculeIsPolar = result.IsPolar,
            MoleculeCentralHybridization = result.CentralAtomHybridization,
            MoleculeFunctionalGroups = result.FunctionalGroups.ToArray(),
        };
    }
}
