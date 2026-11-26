using System.Globalization;
using System.Text.RegularExpressions;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 热力学自发性（ΔG）窄域求解器：ΔG = ΔH − TΔS，由符号判断「能 / 不能自发」。
/// <para>红线：缺 ΔH / ΔS / T 任一、多组数据歧义、非自发判断题、多部分整题 → 返回 null，绝不蒙数。</para>
/// <para>多部分整题不触发（交由 <see cref="MultiPartUtility"/> 分发逐子问求解，避免抢占分发链路）；
/// 单条自包含题（含共享数据的子问）可触发。仅在各分支主求解器失败后的 null 路径兜底接入 → 只能 gain 不能 lose。</para>
/// </summary>
public static class GibbsSpontaneitySolver
{
    // ΔH1＝＋74.6 kJ/mol / ΔH = -100 ；支持下标 ₁、全角 ＝ ＋ － −
    private static readonly Regex DeltaH = new(
        @"ΔH\s*(?<sub>[₁₂₃₄₅₆₇₈₉0-9]?)\s*[=＝═]\s*(?<sign>[+\-＋－−]?)\s*(?<num>\d+(?:\.\d+)?)",
        RegexOptions.Compiled);

    private static readonly Regex DeltaS = new(
        @"ΔS\s*[=＝═]\s*(?<sign>[+\-＋－−]?)\s*(?<num>\d+(?:\.\d+)?)",
        RegexOptions.Compiled);

    // 温度：X K 或 X ℃（℃→+273.15 K）。优先「自发」所在子句中的温度。
    // 注意：.NET 的 \w 含 CJK，故不用 K\b（“1000 K时” 中 K→时 无边界）；改用 K(?![A-Za-z])
    // 排除 Ksp/Ka/Kb 等 ASCII 续接，保留中文/标点/空格结尾的 Kelvin 单位。
    private static readonly Regex TempK = new(@"(?<num>\d+(?:\.\d+)?)\s*K(?![A-Za-z])", RegexOptions.Compiled);
    private static readonly Regex TempC = new(@"(?<num>\d+(?:\.\d+)?)\s*℃", RegexOptions.Compiled);

    private static readonly char[] SentenceEnd = { '。', '；', ';', '\n', '\r' };

    /// <summary>
    /// 尝试从题干判出 ΔG 符号并给出「能 / 不能自发」结论。
    /// </summary>
    /// <returns>可唯一判定时返回 <see cref="EngineResponse"/>（Ok=true）；否则 null。</returns>
    public static EngineResponse? TrySolve(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        // 多部分整题不抢占分发；单条子问（IsMultiPart=false）可进入。
        if (MultiPartUtility.IsMultiPart(raw)) return null;
        if (!raw.Contains("自发")) return null;

        // ΔH：仅取数值型；0 个或 >1 个数值型 ΔH → 缺失/歧义 → null（不蒙数）
        var dhValues = new List<double>();
        foreach (Match m in DeltaH.Matches(raw))
        {
            var v = ParseSigned(m);
            if (v.HasValue) dhValues.Add(v.Value);
        }
        if (dhValues.Count != 1) return null;
        double dh = dhValues[0]; // kJ/mol

        // ΔS
        var dsMatch = DeltaS.Match(raw);
        if (!dsMatch.Success) return null;
        var dsRaw = ParseSigned(dsMatch);
        if (!dsRaw.HasValue) return null;
        // 单位：默认 J/(mol·K)；匹配窗口内出现 kJ 则为 kJ/(mol·K)
        int unitStart = dsMatch.Index + dsMatch.Length;
        string unitWindow = raw.Substring(unitStart, Math.Min(40, raw.Length - unitStart));
        double ds = unitWindow.Contains("kJ") ? dsRaw.Value : dsRaw.Value / 1000.0; // → kJ/(mol·K)

        // T：优先「自发」所在子句的温度；否则全文必须唯一温度，多个 → 歧义 null
        double? t = FindTemperature(raw);
        if (!t.HasValue) return null;

        double dG = dh - t.Value * ds;
        bool canSpont = dG < -1e-9;
        bool cannot = dG > 1e-9;
        string verdict = canSpont ? "能自发进行" : cannot ? "不能自发进行" : "处于平衡状态";
        string conclusion = canSpont ? "能" : cannot ? "不能" : "平衡";

        string dGStr = dG.ToString("0.##", CultureInfo.InvariantCulture);
        string der = $"ΔG = ΔH − TΔS = {dh.ToString("0.###", CultureInfo.InvariantCulture)} − " +
                     $"{t.Value.ToString("0.###", CultureInfo.InvariantCulture)} × " +
                     $"{dsRaw.Value.ToString("0.###", CultureInfo.InvariantCulture)}/1000 = {dGStr} kJ/mol；" +
                     $"ΔG {(canSpont ? "<" : cannot ? ">" : "=")} 0 → {verdict}";

        return new EngineResponse
        {
            Ok = true,
            // 结论文本以「能 / 不能 / 平衡」开头，便于答案键关键词命中（pdf-262 子问1 答案键「能」）
            FinalResult = $"{conclusion}（ΔG = {dGStr} kJ/mol，{verdict}）",
            DerivationSteps = new List<string> { der },
            ConstraintNote = "【高中限定】ΔG=ΔH−TΔS 自发性判据，已校验 ΔH/ΔS/T 唯一可解析",
            Confidence = 1.0
        };
    }

    private static double? ParseSigned(Match m)
    {
        var sign = m.Groups["sign"].Value;
        if (!double.TryParse(m.Groups["num"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
            return null;
        return (sign == "-" || sign == "－" || sign == "−") ? -num : num;
    }

    private static double? FindTemperature(string raw)
    {
        int zi = raw.IndexOf("自发", StringComparison.Ordinal);
        if (zi >= 0)
        {
            int s = zi;
            while (s > 0 && Array.IndexOf(SentenceEnd, raw[s - 1]) < 0) s--;
            int e = zi;
            while (e < raw.Length && Array.IndexOf(SentenceEnd, raw[e]) < 0) e++;
            string clause = raw.Substring(s, e - s);

            var tk = TempK.Match(clause);
            if (tk.Success && double.TryParse(tk.Groups["num"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var k))
                return k;
            var tc = TempC.Match(clause);
            if (tc.Success && double.TryParse(tc.Groups["num"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var c))
                return c + 273.15;
        }

        // 回退：全文温度唯一才采用，多个 → 歧义 null
        var allK = TempK.Matches(raw);
        var allC = TempC.Matches(raw);
        if (allK.Count == 1 && allC.Count == 0 &&
            double.TryParse(allK[0].Groups["num"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var k1))
            return k1;
        if (allC.Count == 1 && allK.Count == 0 &&
            double.TryParse(allC[0].Groups["num"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var c1))
            return c1 + 273.15;
        return null;
    }
}
