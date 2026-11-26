using System.Text;
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

        if (imageAssets is null || imageAssets.Count == 0) return null;

        // 分析全部 molecule 资产（而非只取第一个），聚合各成功解析结果
        var successes = new List<MoleculeAnalysisResult>();
        foreach (var asset in imageAssets)
        {
            if (asset.Type != "molecule") continue;
            var result = AnalyzeMoleculeAsset(asset);
            if (result is not null) successes.Add(result);
        }
        if (successes.Count == 0) return null; // 全部解析失败 → 如实回退，不臆造特征

        // 单个成功资产 → 与旧行为逐字段一致（不加分块前缀，保证无回归）
        if (successes.Count == 1)
            return BuildMoleculeResponse(successes[0]);

        return BuildAggregatedMoleculeResponse(successes);
    }

    /// <summary>解析单个 molecule 资产的 payload 并调用 RDKit 分析；失败返回 null（不臆造）。</summary>
    private static MoleculeAnalysisResult? AnalyzeMoleculeAsset(ImageAsset asset)
    {
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
        return result.Success ? result : null;
    }

    /// <summary>单分子响应：与历史行为逐字段一致。</summary>
    private static EngineResponse BuildMoleculeResponse(MoleculeAnalysisResult result) => new()
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
        MoleculeName = result.MoleculeName,
    };

    /// <summary>
    /// 多分子聚合响应：官能团取并集（去重保序）、名称拼接、结论按资产顺序分块；
    /// 判分仍走 MatchMolecule OR（任一特征命中答案即正确），聚合只扩大覆盖、不收紧。
    /// 分子式/不饱和度/极性/杂化等数值字段保守取首个成功资产，不为多分子杜撰合并值。
    /// </summary>
    private static EngineResponse BuildAggregatedMoleculeResponse(IReadOnlyList<MoleculeAnalysisResult> results)
    {
        var groups = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in results)
            foreach (var g in r.FunctionalGroups)
                if (!string.IsNullOrEmpty(g) && seen.Add(g))
                    groups.Add(g);

        var names = results.Select(r => r.MoleculeName).Where(n => !string.IsNullOrEmpty(n)).ToList();
        string? name = names.Count > 0 ? string.Join("；", names) : null;

        var sb = new StringBuilder();
        for (int i = 0; i < results.Count; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append('(').Append(i + 1).Append(')').Append(results[i].FinalResult);
        }

        var first = results[0];
        return new EngineResponse
        {
            Ok = true,
            FinalResult = sb.ToString(),
            ConstraintNote = $"【molecule 引擎】{results.Count} 个分子结构特征聚合（RDKit 真值）",
            Confidence = 0.9,
            AssetKind = "molecule",
            MoleculeFormula = first.MolecularFormula,
            MoleculeUnsaturation = first.DegreeOfUnsaturation,
            MoleculeIsPolar = first.IsPolar,
            MoleculeCentralHybridization = first.CentralAtomHybridization,
            MoleculeFunctionalGroups = groups.ToArray(),
            MoleculeName = name,
        };
    }
}
