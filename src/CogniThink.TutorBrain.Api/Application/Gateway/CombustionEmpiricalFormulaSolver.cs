using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 燃烧法 → 实验式（最简式）求解器（P2-2 / pdf-328）。
/// <para>域：题干问「分子式」，且给出燃烧数据：样品质量（g）+ CO₂ 与 H₂O 的物质的量（或质量）
/// → 计算 n(C):n(H):n(O) 最简整数比，输出实验式（如 C9H8O2）。</para>
/// <para>pdf-328：2.96 g 肉桂酸完全燃烧 → 0.18 mol CO₂ + 0.08 mol H₂O；仅凭文本即可算
/// n(C):n(H):n(O)=0.18:0.16:0.04=9:8:2 → C9H8O2（恰等于答案，因 n=1），无需读质谱图。</para>
/// <para>红线：触发词「分子式」缺失 / 燃烧数据缺一 / 氧质量为负（数据矛盾）/ 最简比无法化整
/// （超容差）→ 返回 null，绝不蒙数。仅在能唯一确定整数比时输出；采纳门必经答案键校验（calculation，OR）→ precision=1、零 harm。</para>
/// </summary>
public static class CombustionEmpiricalFormulaSolver
{
    private const double MolarC = 12.0;
    private const double MolarH = 1.0;
    private const double MolarO = 16.0;
    private const double MolarCO2 = 44.0;
    private const double MolarH2O = 18.0;

    /// <summary>命中则返回含实验式的结论；域外/缺数据/化整失败返回 null。</summary>
    public static EngineResponse? TrySolve(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!raw.Contains("分子式")) return null;

        var text = NormalizeSuperscripts(raw);

        // 1) 样品质量（g）：<num> g ... 完全燃烧 / 燃烧
        var mSample = ParseSampleMass(text);
        if (mSample is null || mSample <= 0) return null;

        // 2) CO2 / H2O 的量：优先 mol，其次 g（转换）
        var nCO2 = ParseAmount(text, "CO2", MolarCO2);
        var nH2O = ParseAmount(text, "H2O", MolarH2O);
        if (nCO2 is null || nH2O is null) return null;
        if (nCO2 <= 0 || nH2O <= 0) return null;

        // 3) 元素物质的量
        var nC = nCO2.Value;
        var nH = 2.0 * nH2O.Value;
        var massC = MolarC * nC;
        var massH = MolarH * nH;
        var massO = mSample.Value - massC - massH;

        // 氧质量为负（数据矛盾，超出舍入容差）→ 不蒙数
        if (massO < -1e-6) return null;
        var hasO = massO > 1e-6;
        var nO = hasO ? massO / MolarO : 0.0;

        // 4) 化最简整数比
        var counts = ReduceToIntegers(nC, nH, hasO ? nO : (double?)null);
        if (counts is null) return null; // 无法在容差内化整 → 不蒙数
        var (c, h, o) = counts.Value;

        var formula = FormatFormula(c, h, o);
        var ratioText = hasO ? $"{c}:{h}:{o}" : $"{c}:{h}";
        var oNote = hasO
            ? $"，n(O)=({mSample:0.###}−{massC:0.###}−{massH:0.###})/16={nO:0.###} mol"
            : "（不含氧）";
        var final = $"由燃烧产物计算：n(C)={nC:0.###} mol，n(H)={nH:0.###} mol{oNote}，" +
                    $"n(C):n(H):n(O)={ratioText}，分子式为 {formula}。";

        return new EngineResponse
        {
            Ok = true,
            FinalResult = final,
            DerivationSteps = new List<string>
            {
                $"n(C) = n(CO₂) = {nC:0.###} mol，n(H) = 2·n(H₂O) = {nH:0.###} mol",
                hasO
                    ? $"m(O) = {mSample:0.###} − 12×{nC:0.###} − 1×{nH:0.###} = {massO:0.###} g，n(O) = {massO:0.###}/16 = {nO:0.###} mol"
                    : $"m(C)+m(H) = {massC + massH:0.###} g ≈ 样品质量 {mSample:0.###} g → 不含氧",
                $"n(C):n(H):n(O) = {ratioText} → 实验式 {formula}"
            },
            ReasoningNodes = new List<ReasoningNode>
            {
                new()
                {
                    Step = 1,
                    Description = "燃烧法求实验式",
                    Premises = new List<string> { $"样品 {mSample:0.###} g 完全燃烧生成 {nCO2:0.###} mol CO₂、{nH2O:0.###} mol H₂O" },
                    KnowledgeId = "combustion-empirical-formula",
                    Conclusion = final,
                    EvidenceLevel = EvidenceLevel.EngineDerived
                }
            },
            ConstraintNote = "【高中限定】燃烧法求实验式：n(C)=n(CO₂)、n(H)=2n(H₂O)、n(O) 由质量差求；数据取自题干，缺数据/矛盾/无法化整不蒙数",
            Confidence = 1.0
        };
    }

    private static double? ParseSampleMass(string text)
    {
        // <num> g ... 燃烧：取燃烧语境前最近的样品质量
        foreach (Match m in Regex.Matches(text,
                     @"(\d+(?:\.\d+)?)\s*g(?:的)?[^。；;\n]{0,30}?(?:完全燃烧|燃烧|充分燃烧|氧化)"))
        {
            if (double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return v;
        }
        // 兜底：任意 "数字 g" 且后文含燃烧
        var burnIdx = text.IndexOf("燃烧", StringComparison.Ordinal);
        if (burnIdx < 0) return null;
        var prefix = text.Substring(0, burnIdx);
        foreach (Match m in Regex.Matches(prefix, @"(\d+(?:\.\d+)?)\s*g"))
        {
            if (double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return v;
        }
        return null;
    }

    // 解析指定物种（CO2/H2O）的物质的量：优先 "<num> mol <species>"，其次 "<num> g <species>" 换算
    private static double? ParseAmount(string text, string species, double molarMass)
    {
        foreach (Match m in Regex.Matches(text,
                     $@"(\d+(?:\.\d+)?)\s*mol(?:的)?\s*{Regex.Escape(species)}"))
        {
            if (double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return v;
        }
        foreach (Match m in Regex.Matches(text,
                     $@"(\d+(?:\.\d+)?)\s*g(?:的)?\s*{Regex.Escape(species)}"))
        {
            if (double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return v / molarMass;
        }
        return null;
    }

    // n(C), n(H), n(O?) → 最简整数比 (c,h,o)；无法在容差内化整返回 null
    private static (int c, int h, int o)? ReduceToIntegers(double nC, double nH, double? nO)
    {
        var vals = new List<double> { nC, nH };
        if (nO.HasValue) vals.Add(nO.Value);
        if (vals.Any(v => v <= 0)) return null;
        var min = vals.Min();
        var ratios = vals.Select(v => v / min).ToList();
        const double tol = 0.06;
        for (int k = 1; k <= 8; k++)
        {
            var rounded = ratios.Select(r => Math.Round(r * k)).ToList();
            if (ratios.Select((r, i) => Math.Abs(r * k - rounded[i])).All(d => d <= tol))
            {
                int c = (int)rounded[0], h = (int)rounded[1], o = nO.HasValue ? (int)rounded[2] : 0;
                if (c <= 0 || h <= 0) return null;
                return (c, h, o);
            }
        }
        return null;
    }

    private static string FormatFormula(int c, int h, int o)
    {
        var sb = new StringBuilder();
        sb.Append('C');
        if (c > 1) sb.Append(c);
        sb.Append('H');
        if (h > 1) sb.Append(h);
        if (o > 0)
        {
            sb.Append('O');
            if (o > 1) sb.Append(o);
        }
        return sb.ToString();
    }

    private static string NormalizeSuperscripts(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            sb.Append(ch switch
            {
                '₀' => '0', '₁' => '1', '₂' => '2', '₃' => '3', '₄' => '4',
                '₅' => '5', '₆' => '6', '₇' => '7', '₈' => '8', '₉' => '9',
                '⁰' => '0', '¹' => '1', '²' => '2', '³' => '3', '⁴' => '4',
                '⁵' => '5', '⁶' => '6', '⁷' => '7', '⁸' => '8', '⁹' => '9',
                _ => ch,
            });
        }
        return sb.ToString();
    }
}
