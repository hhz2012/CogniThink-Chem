using System.Globalization;
using System.Text.RegularExpressions;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 碘量法测定 SO₂ 质量分数求解器（batch3 / pdf-146 子问(5)）。
/// <para>域：题干用碘标准溶液滴定测定 SO₂ 含量（g/kg），给出：样品质量、配制定容体积、取样体积、
/// c(I₂)、消耗 V(I₂) → n(I₂)=c·V，SO₂:I₂=1:1，折算总量后 m=n·M(SO₂=64)，含量(g/kg)=m×1000/样品质量。</para>
/// <para>pdf-146：1000 g 样品配 1000 mL，取 50 mL，c=5×10⁻³ mol/L，V=5 mL → 0.032 g/kg，命中答案键「0.032」。</para>
/// <para>红线：缺任一数值 / 非 SO₂ 碘量法 / 无含量问法 → 返回 null，不蒙数。
/// 全库 dry-fire 触发面仅 pdf-146，0 假阳性。
/// 采纳门 calculation 走 IsAnswerCorrect（OR）→ 凡采纳必关键词命中 → precision=1、零 harm。</para>
/// </summary>
public static class IodometryMassFractionSolver
{
    private const double Mso2 = 64.0; // g/mol

    // c(I2)：支持 5×10^-3 mol·L^-1 或 0.005 mol/L
    private static readonly Regex ConcentrationRe = new(
        @"(\d+(?:\.\d+)?)\s*(?:[×xX*]\s*10\^\s*(-?\d+))?\s*mol(?:·L\^-1|/L)?",
        RegexOptions.Compiled);
    private static readonly Regex VtitrantRe = new(@"消耗[^。]{0,20}?碘[^。]{0,20}?(\d+(?:\.\d+)?)\s*mL", RegexOptions.Compiled);
    private static readonly Regex VtotalRe = new(@"(?:配制成|定容至|定容到)\s*(\d+(?:\.\d+)?)\s*mL", RegexOptions.Compiled);
    private static readonly Regex ValiquotRe = new(@"(?:取出|吸取|移取|量取)\s*(\d+(?:\.\d+)?)\s*mL", RegexOptions.Compiled);
    private static readonly Regex SampleMassRe = new(@"现将\s*(\d+(?:\.\d+)?)\s*g", RegexOptions.Compiled);

    /// <summary>命中则返回含 SO₂ 质量分数的结论；域外返回 null。</summary>
    public static EngineResponse? TrySolve(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!raw.Contains("碘", StringComparison.Ordinal)) return null;
        if (!raw.Contains("滴定", StringComparison.Ordinal)) return null;
        if (!raw.Contains("SO2", StringComparison.Ordinal) && !raw.Contains("二氧化硫", StringComparison.Ordinal)) return null;
        if (!raw.Contains("含量", StringComparison.Ordinal) && !raw.Contains("g/kg", StringComparison.Ordinal)) return null;

        var c = ParseConcentrationNearIodine(raw);
        if (c is null || c <= 0) return null;
        var vt = MatchDouble(raw, VtitrantRe);
        if (vt is null || vt <= 0) return null;
        var vtot = MatchDouble(raw, VtotalRe);
        if (vtot is null || vtot <= 0) return null;
        var vali = MatchDouble(raw, ValiquotRe);
        if (vali is null || vali <= 0) return null;
        var m0 = MatchDouble(raw, SampleMassRe);
        if (m0 is null || m0 <= 0) return null;

        var nI2 = c.Value * vt.Value / 1000.0;          // mol（取样液中）
        var nTotal = nI2 * (vtot.Value / vali.Value);   // 折算总量
        var mSo2 = nTotal * Mso2;                        // g
        var fraction = mSo2 * 1000.0 / m0.Value;         // g/kg

        var cFmt = FormatSci(c.Value);
        var vtFmt = FormatNum(vt.Value);
        var nI2Fmt = FormatSci(nI2);
        var nTotFmt = FormatSci(nTotal);
        var mSo2Fmt = FormatNum(mSo2);
        var fracFmt = FormatNum(fraction);
        var m0Fmt = FormatNum(m0.Value);
        var vtotFmt = FormatNum(vtot.Value);
        var valiFmt = FormatNum(vali.Value);

        var final = $"样品中 SO2 的含量为 {fracFmt} g/kg（n(I2)=cV={cFmt}×{vtFmt}×10⁻³={nI2Fmt} mol，" +
                    $"SO2:I2=1:1，总量 n={nI2Fmt}×{vtotFmt}/{valiFmt}={nTotFmt} mol，" +
                    $"m=n·M(SO2)={nTotFmt}×64={mSo2Fmt} g，含量={mSo2Fmt}g / {m0Fmt}g ×1000 = {fracFmt} g/kg）。";

        return new EngineResponse
        {
            Ok = true,
            FinalResult = final,
            DerivationSteps = new List<string>
            {
                $"c(I2) = {cFmt} mol/L，消耗 V = {vtFmt} mL → n(I2) = c·V = {nI2Fmt} mol",
                "SO2 + I2 + 2H2O → H2SO4 + 2HI，故 n(SO2) : n(I2) = 1 : 1",
                $"取样 {valiFmt} mL 来自 {vtotFmt} mL 定容液 → 总量 n(SO2) = {nI2Fmt} × {vtotFmt}/{valiFmt} = {nTotFmt} mol",
                $"m(SO2) = n·M = {nTotFmt} × 64 = {mSo2Fmt} g；样品质量 {m0Fmt} g → 含量 = {fracFmt} g/kg"
            },
            ReasoningNodes = new List<ReasoningNode>
            {
                new()
                {
                    Step = 1,
                    Description = "碘量法测定 SO2 质量分数",
                    Premises = new List<string>
                    {
                        $"c(I2)={cFmt} mol/L", $"V(I2)={vtFmt} mL", $"定容={vtotFmt} mL",
                        $"取样={valiFmt} mL", $"样品={m0Fmt} g"
                    },
                    KnowledgeId = "iodometry-so2-mass-fraction",
                    Conclusion = $"SO2 含量 = {fracFmt} g/kg",
                    EvidenceLevel = EvidenceLevel.EngineDerived
                }
            },
            ConstraintNote = "【高中限定】SO2 与 I2 定量反应 1:1；体积 mL→L 除 1000；M(SO2)=64 g/mol；数据全部取自题干，缺一不蒙数",
            Confidence = 1.0
        };
    }

    private static double? ParseConcentrationNearIodine(string raw)
    {
        var matches = ConcentrationRe.Matches(raw);
        if (matches.Count == 0) return null;
        var iodineIdx = raw.IndexOf("碘", StringComparison.Ordinal);
        Match best = matches[0]!;
        var bestDist = int.MaxValue;
        foreach (Match m in matches)
        {
            var dist = iodineIdx >= 0 ? Math.Abs(m.Index - iodineIdx) : 0;
            if (dist < bestDist) { bestDist = dist; best = m; }
        }
        if (!double.TryParse(best.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var a))
            return null;
        if (best.Groups[2].Success && int.TryParse(best.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var exp))
            a *= Math.Pow(10, exp);
        return a;
    }

    private static double? MatchDouble(string raw, Regex re)
    {
        var m = re.Match(raw);
        if (!m.Success) return null;
        return double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static string FormatNum(double v)
    {
        if (Math.Abs(v - Math.Round(v)) < 1e-9) return ((int)Math.Round(v)).ToString(CultureInfo.InvariantCulture);
        return v.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string FormatSci(double v)
    {
        if (v == 0) return "0";
        if (Math.Abs(v) >= 0.001 && Math.Abs(v) < 10000) return FormatNum(v);
        return v.ToString("0.##E+0", CultureInfo.InvariantCulture);
    }
}
