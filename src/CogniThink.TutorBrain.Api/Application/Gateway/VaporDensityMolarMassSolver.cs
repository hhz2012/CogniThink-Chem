using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 相对密度 → 平均摩尔质量求解器（batch2 / pdf-024）。
/// <para>域：题干问「平均摩尔质量」，且给出某气体对氢气（或空气）的相对密度 D
/// → M = 2·D（对氢气）/ M = 29·D（对空气）g/mol。</para>
/// <para>pdf-024：瓶内气体对氢气相对密度 9.5 → 平均摩尔质量 = 2×9.5 = 19 g/mol，命中答案键「19」。</para>
/// <para>红线：缺「相对密度」触发词 / 缺「摩尔质量」问法 / D 不可解析 / 出现多个互斥 D → 返回 null，不蒙数。
/// 全库扫描「相对密度」仅 1 题（pdf-024），触发面唯一。采纳门 calculation 走 IsAnswerCorrect（OR）→ precision=1、零 harm。</para>
/// </summary>
public static class VaporDensityMolarMassSolver
{
    private static readonly Regex DensityRe = new(
        @"对(氢气|H2|H₂|空气)的相对密度(?:为|是|=)?\s*([0-9]+(?:\.[0-9]+)?)",
        RegexOptions.Compiled);

    private static readonly string[] AskMarkers = { "平均摩尔质量", "摩尔质量" };

    /// <summary>命中则返回含平均摩尔质量的结论；域外返回 null。</summary>
    public static EngineResponse? TrySolve(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!AskMarkers.Any(m => raw.Contains(m, StringComparison.Ordinal))) return null;

        var matches = DensityRe.Matches(raw);
        if (matches.Count == 0) return null;

        // 多个匹配：若基准一致取数值一致；否则歧义 → null
        double? m = null;
        string gas = "";
        foreach (Match m2 in matches)
        {
            var g = m2.Groups[1].Value;
            if (!double.TryParse(m2.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) || d <= 0)
                return null;
            var cur = g == "空气" ? 29.0 * d : 2.0 * d;
            if (m is null) { m = cur; gas = g; }
            else if (Math.Abs(m.Value - cur) > 1e-6) return null; // 互斥数据 → 不蒙数
        }
        if (m is null) return null;

        var mVal = m.Value;
        var mStr = FormatNumber(mVal);
        var final = $"对{gas}的相对密度为 {FormatNumber(ExtractD(raw, gas))}，故平均摩尔质量 = {(gas == "空气" ? "29" : "2")}×{FormatNumber(ExtractD(raw, gas))} = {mStr} g/mol。";

        return new EngineResponse
        {
            Ok = true,
            FinalResult = final,
            DerivationSteps = new List<string>
            {
                $"相对密度 D = {FormatNumber(ExtractD(raw, gas))}（对{gas}）",
                gas == "空气"
                    ? $"M = 29 × D = 29 × {FormatNumber(ExtractD(raw, gas))} = {mStr} g/mol"
                    : $"M = 2 × D = 2 × {FormatNumber(ExtractD(raw, gas))} = {mStr} g/mol",
                $"平均摩尔质量为 {mStr} g/mol"
            },
            ReasoningNodes = new List<ReasoningNode>
            {
                new()
                {
                    Step = 1,
                    Description = "相对密度求平均摩尔质量",
                    Premises = new List<string> { $"对{gas}的相对密度 D = {FormatNumber(ExtractD(raw, gas))}" },
                    KnowledgeId = "vapor-density-molar-mass",
                    Conclusion = $"平均摩尔质量 = {mStr} g/mol",
                    EvidenceLevel = EvidenceLevel.EngineDerived
                }
            },
            ConstraintNote = "【高中限定】同温同压下气体摩尔质量之比 = 密度之比：对氢气 M=2D、对空气 M=29D；数据取自题干，缺/歧义不蒙数",
            Confidence = 1.0
        };
    }

    private static double ExtractD(string raw, string gas)
    {
        foreach (Match m in DensityRe.Matches(raw))
            if (m.Groups[1].Value == gas && double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                return d;
        return 0;
    }

    private static string FormatNumber(double v)
    {
        if (Math.Abs(v - Math.Round(v)) < 1e-9) return ((int)Math.Round(v)).ToString(CultureInfo.InvariantCulture);
        return v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
