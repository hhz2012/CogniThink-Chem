using System.Globalization;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 盐水解 pH 计算器（calc 兜底，P1-3）。
/// <para>域：题干含「pH」+ 数值浓度（mol/L 等）+ 弱酸盐（其共轭酸 Ka 取自
/// <c>acid-constants.json</c> 的 AcidLadders/Ka）。由 Kb=Kw/Ka、[OH⁻]=√(Kb·c) 算 pH。</para>
/// <para>红线：常数与物种映射只来自 JSON，不硬编码化学真值；缺常数/缺浓度/域外 → 返回 null（不蒙数）。
/// 采纳门（评估侧）必经答案键校验，故诚实但与答案键冲突的结论不会被采纳（precision=1、零 harm）。</para>
/// </summary>
public static class HydrolysisPhCalculator
{
    private static readonly Lazy<AcidConstantsData> Constants =
        new(() => RuleStore.Load().Query<AcidConstantsData>("acid-constants"));

    // 浓度：捕捉 0.01 mol·L⁻¹ / 0.1 mol/L / 0.2 mol / 1.0 M 等
    private static readonly Regex Concentration =
        new(@"(\d+(?:\.\d+)?)\s*(?:mol·L⁻¹|mol·L\^-1|mol/L|mol·l-1|mol·L|mol|M)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>命中则返回含数值 pH 的结论；域外/缺数据返回 null。</summary>
    public static EngineResponse? TrySolve(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!raw.Contains("pH", StringComparison.OrdinalIgnoreCase)) return null;

        var cMatch = Concentration.Match(raw);
        if (!cMatch.Success) return null;
        if (!double.TryParse(cMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var c) || c <= 0)
            return null;

        var data = Constants.Value;
        var (kaKey, acidName, baseName) = FindSaltAcid(raw, data);
        if (kaKey is null || !data.Ka.TryGetValue(kaKey, out var kaSpec)) return null;

        var ka = ToDouble(kaSpec);
        if (ka <= 0) return null;
        var kw = ToDouble(data.Kw);
        if (kw <= 0) return null;

        var kb = kw / ka;
        var oh = Math.Sqrt(kb * c);
        if (oh <= 0) return null;
        var pH = 14 + Math.Log10(oh);
        if (double.IsNaN(pH) || pH <= 0 || pH >= 14) return null;

        var phText = pH.ToString("0.00", CultureInfo.InvariantCulture);
        var finalResult = $"{baseName} 水解使溶液呈碱性，pH ≈ {phText}（{acidName} 的 Ka={FormatConst(kaSpec)}，Kb=Kw/Ka，[OH⁻]=√(Kb·c)）。";
        // 加性推理链（spec R4）：kaKey 是 acid-constants.json 的稳定字典键，
        // 到达此处必然非 null（L37 已对缺常数/域外返回 null）→ 命中即有真实知识锚点；
        // pH 由常数经公式推导 → EvidenceLevel.EngineDerived；末节点 Conclusion == FinalResult。
        return new EngineResponse
        {
            Ok = true,
            FinalResult = finalResult,
            DerivationSteps = new List<string>
            {
                $"{acidName} ⇌ H⁺ + {baseName}，Ka = {FormatConst(kaSpec)}",
                $"Kb({baseName}) = Kw / Ka = {kw:E2} / {ka:E2} = {kb:E3}",
                $"[OH⁻] = √(Kb·c) = √({kb:E3} × {c}) = {oh:E3}",
                $"pH = 14 + lg[OH⁻] ≈ {phText}"
            },
            ReasoningNodes = new List<ReasoningNode>
            {
                new()
                {
                    Step = 1,
                    Description = "盐水解 pH 计算（常数取自 acid-constants.json）",
                    Premises = new List<string> { $"常数取自 data/highschool/acid-constants.json：{kaKey}" },
                    KnowledgeId = kaKey,
                    Conclusion = finalResult,
                    EvidenceLevel = EvidenceLevel.EngineDerived
                }
            },
            ConstraintNote = "【高中限定】盐水解 pH（25℃），常数取自 acid-constants.json",
            Confidence = 1.0
        };
    }

    // 从 AcidLadders 派生（酸 → 共轭碱）的盐/阴离子文本形式，全部源自 JSON。
    private static (string? KaKey, string AcidName, string BaseName) FindSaltAcid(string raw, AcidConstantsData data)
    {
        foreach (var kv in data.AcidLadders)
        {
            var acidKey = kv.Key;
            var ladder = kv.Value;
            for (var i = 1; i < ladder.Count; i++)
            {
                var baseSpecies = ladder[i];
                var forms = BuildForms(baseSpecies);
                if (forms.Any(f => !string.IsNullOrEmpty(f) && raw.Contains(f, StringComparison.Ordinal)))
                {
                    var kaKey = ladder.Count > 2 ? $"{acidKey}:Ka{i}" : acidKey;
                    return (kaKey, acidKey, baseSpecies);
                }
            }
        }
        return (null, "", "");
    }

    private static List<string> BuildForms(string baseSpecies)
    {
        var core = baseSpecies
            .Replace("^2-", "").Replace("^3-", "").Replace("^-", "")
            .Replace("⁻", "").Replace("²", "").Replace("³", "")
            .TrimEnd('-');
        return new List<string>
        {
            baseSpecies,
            baseSpecies.Replace("^-", "⁻").Replace("^2-", "²⁻").Replace("^3-", "³⁻"),
            "Na" + core,
            "K" + core,
            core + "-",
            core + "⁻"
        };
    }

    private static double ToDouble(ConstantSpec s) =>
        double.Parse(s.Mantissa, NumberStyles.Float, CultureInfo.InvariantCulture) * Math.Pow(10, s.Exponent);

    private static string FormatConst(ConstantSpec s) => $"{s.Mantissa}×10^{s.Exponent}";
}
