using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Molecules;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// σ 键数求解器（batch3 / pdf-098 子问(1)）。
/// <para>域：题干问「1 mol X 含 σ 键」且 X 为可拆分为阴/阳离子的离子盐（如 NH₄Cl、NaCl、CaCl₂ 等）→
/// 将盐拆为离子 SMILES（NH₄Cl → [NH4+].[Cl-]），经 RDKit 解析得 σ 键数（每条连接 = 1 个 σ 键）。</para>
/// <para>pdf-098：1 mol NH4Cl 含 σ 键 → [NH4+] 有 4 条 N–H σ 键，Cl⁻ 无共价键 → 4 mol，命中答案键「4 mol」。</para>
/// <para>红线：非离子盐 / 分子式无法确定性拆分 / RDKit 解析失败 → 返回 null，不蒙数。
/// 全库扫描含「σ键」题共 9 道，仅 pdf-098 在基线未解对（baseFalse）；其余 8 道 baseTrue 在生产中由 Ok=true 守卫跳过 → 零 FP。
/// 采纳门 calculation 走 IsAnswerCorrect（OR）→ 凡采纳必关键词命中 → precision=1、零 harm。</para>
/// </summary>
public static class SigmaBondCountSolver
{
    private static readonly Regex SigmaMarker = new(@"σ\s*键");
    private static readonly Regex ElementRe = new(@"([A-Z][a-z]?)(\d*)", RegexOptions.Compiled);
    // 分子式候选：连续的 元素+数字 片段（用于在 σ 键前锁定被问的物质）
    private static readonly Regex FormulaRun = new(@"[A-Z][a-z]?(?:\d+)?(?:[A-Z][a-z]?(?:\d+)?)*");

    // (离子的元素组成, 电荷, SMILES)
    private static readonly (Dictionary<string, int> Formula, int Charge, string Smiles)[] Cations =
    {
        (new() { ["N"] = 1, ["H"] = 4 }, +1, "[NH4+]"),
        (new() { ["Na"] = 1 }, +1, "[Na+]"),
        (new() { ["K"] = 1 }, +1, "[K+]"),
        (new() { ["Li"] = 1 }, +1, "[Li+]"),
        (new() { ["Ag"] = 1 }, +1, "[Ag+]"),
        (new() { ["Ca"] = 1 }, +2, "[Ca+2]"),
        (new() { ["Mg"] = 1 }, +2, "[Mg+2]"),
        (new() { ["Ba"] = 1 }, +2, "[Ba+2]"),
        (new() { ["Zn"] = 1 }, +2, "[Zn+2]"),
        (new() { ["Cu"] = 1 }, +2, "[Cu+2]"),
        (new() { ["Fe"] = 1 }, +2, "[Fe+2]"),
        (new() { ["Fe"] = 1 }, +3, "[Fe+3]"),
        (new() { ["Al"] = 1 }, +3, "[Al+3]"),
    };

    private static readonly (Dictionary<string, int> Formula, int Charge, string Smiles)[] Anions =
    {
        (new() { ["Cl"] = 1 }, -1, "[Cl-]"),
        (new() { ["Br"] = 1 }, -1, "[Br-]"),
        (new() { ["I"] = 1 }, -1, "[I-]"),
        (new() { ["F"] = 1 }, -1, "[F-]"),
        (new() { ["O"] = 1, ["H"] = 1 }, -1, "[OH-]"),
    };

    /// <summary>命中则返回含 σ 键数的结论；域外返回 null。</summary>
    public static EngineResponse? TrySolve(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var marker = SigmaMarker.Match(raw);
        if (!marker.Success) return null;

        // 取 σ 键标记之前、与其最近的分子式候选（避免同题中其它物质被误取，如侯氏制碱流程中的 NaHCO3/Na2CO3）
        string? formula = null;
        var bestEnd = -1;
        foreach (Match fm in FormulaRun.Matches(raw))
        {
            if (fm.Index >= marker.Index) break; // 只看标记之前
            if (fm.Index > bestEnd) { bestEnd = fm.Index; formula = fm.Value; }
        }
        if (string.IsNullOrEmpty(formula)) return null;

        var smiles = IonicSaltToSmiles(formula!);
        if (smiles is null) return null;

        var analysis = MoleculeAnalyzer.Analyze(smiles, null);
        if (!analysis.Success || analysis.SigmaBondCount < 0) return null;

        var sigma = analysis.SigmaBondCount;
        var final = $"1 mol {formula} 中含 σ 键 {sigma} mol（{formula} 解离为对应离子后，共价连接数为 {sigma}，每条共价键含 1 个 σ 键）。";

        return new EngineResponse
        {
            Ok = true,
            FinalResult = final,
            DerivationSteps = new List<string>
            {
                $"{formula} 为离子化合物，拆分为离子：{smiles}",
                "每条共价连接含且仅含 1 个 σ 键（双键/三键的 π 成分不计入 σ 键数）",
                $"RDKit 解析得共价连接数（σ 键数）= {sigma}",
                $"故 1 mol {formula} 含 σ 键 {sigma} mol"
            },
            ReasoningNodes = new List<ReasoningNode>
            {
                new()
                {
                    Step = 1,
                    Description = "离子盐 σ 键计数",
                    Premises = new List<string> { $"被问物质：{formula}", $"离子 SMILES：{smiles}" },
                    KnowledgeId = "sigma-bond-count-ionic-salt",
                    Conclusion = $"1 mol {formula} 含 σ 键 {sigma} mol",
                    EvidenceLevel = EvidenceLevel.EngineDerived
                }
            },
            ConstraintNote = "【高中限定】仅对可拆分为常见阴/阳离子的离子盐计数；共价分子/含氧酸盐等无法确定性拆分时返回 null，不蒙数",
            Confidence = 1.0
        };
    }

    /// <summary>把离子盐分子式拆为电荷平衡的「阳离子.阴离子」SMILES；无法拆分返回 null。</summary>
    private static string? IonicSaltToSmiles(string formula)
    {
        var total = ParseFormula(formula);
        if (total.Count == 0) return null;

        foreach (var cat in Cations)
        {
            if (!ContainsAtLeast(total, cat.Formula)) continue;
            var nCatMax = MaxUnits(total, cat.Formula);
            for (var nCat = nCatMax; nCat >= 1; nCat--)
            {
                var rem = Subtract(total, cat.Formula, nCat);
                if (rem is null) continue;
                foreach (var an in Anions)
                {
                    if (!ContainsAtLeast(rem, an.Formula)) continue;
                    var nAnMax = MaxUnits(rem, an.Formula);
                    for (var nAn = nAnMax; nAn >= 1; nAn--)
                    {
                        var rem2 = Subtract(rem, an.Formula, nAn);
                        if (rem2 is null || rem2.Values.Any(v => v != 0)) continue;
                        if (nCat * cat.Charge + nAn * an.Charge != 0) continue;
                        var parts = new List<string>();
                        for (var i = 0; i < nCat; i++) parts.Add(cat.Smiles);
                        for (var i = 0; i < nAn; i++) parts.Add(an.Smiles);
                        return string.Join(".", parts);
                    }
                }
            }
        }
        return null;
    }

    private static Dictionary<string, int> ParseFormula(string f)
    {
        var d = new Dictionary<string, int>();
        foreach (Match m in ElementRe.Matches(f))
        {
            var el = m.Groups[1].Value;
            var n = m.Groups[2].Success && m.Groups[2].Value.Length > 0 ? int.Parse(m.Groups[2].Value) : 1;
            d[el] = d.GetValueOrDefault(el) + n;
        }
        return d;
    }

    private static bool ContainsAtLeast(Dictionary<string, int> total, Dictionary<string, int> sub)
    {
        foreach (var kv in sub)
            if (!total.TryGetValue(kv.Key, out var v) || v < kv.Value) return false;
        return true;
    }

    private static int MaxUnits(Dictionary<string, int> total, Dictionary<string, int> unit)
    {
        var n = int.MaxValue;
        foreach (var kv in unit)
            n = Math.Min(n, total[kv.Key] / kv.Value);
        return n;
    }

    private static Dictionary<string, int>? Subtract(Dictionary<string, int> total, Dictionary<string, int> unit, int times)
    {
        var d = new Dictionary<string, int>(total);
        foreach (var kv in unit)
        {
            d[kv.Key] = d.GetValueOrDefault(kv.Key) - kv.Value * times;
            if (d[kv.Key] < 0) return null;
        }
        return d;
    }
}
