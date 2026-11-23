using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 键焓（键能）反推求解器（equation_writer / calculation 兜底）。
/// <para>域：题干含「键焓 / 键能」+ 单位 kJ/mol，且设问某分子中某键的键焓为空白待求；
/// 已知若干键的键焓与一个带 ΔH 的反应，由 ΔH = 反应物总键能 − 生成物总键能 解出未知键焓。</para>
/// <para>真值源：键结构（分子→键及数目）来自 <c>data/highschool/bond-structures.json</c>，不硬编码化学真值。</para>
/// <para>红线（不蒙数）：触发严格、所有反应物种必须在键结构表中、未知键必须唯一可解、
/// 解须为正且分母非零；任一不满足返回 null。采纳门必经答案键校验 → precision=1、零 harm。</para>
/// </summary>
public static class BondEnergySolver
{
    private static readonly Lazy<BondStructureData> Data = new(
        () => RuleStore.Load().Query<BondStructureData>("bond-structures"));

    /// <summary>反应箭头候选（═ U+2550 / → U+2192 / ⟶ U+27F6 / ＝ U+FF1D）。</summary>
    private const string ArrowChars = "═→⟶＝";

    private static readonly Regex DeltaHRe = new(
        @"([^。；\n]*?)(?:ΔH|△H|∆H)\s*=\s*([+\-]?\d+(?:\.\d+)?)",
        RegexOptions.Compiled);

    private static readonly Regex BondSpecRe = new(
        @"[A-Z][a-z]?\d*[═≡＝=\-][A-Z][a-z]?\d*", RegexOptions.Compiled);

    private static readonly Regex LeadingCoefRe = new(
        @"^\s*(\d+/\d+|\d+(?:\.\d+)?)\s*", RegexOptions.Compiled);

    private static readonly Regex TargetMolRe = new(
        @"([A-Za-z][A-Za-z0-9()]*)\s*分子中", RegexOptions.Compiled);

    private static readonly Regex CircledPrefixRe = new(
        @"^[①②③④⑤⑥⑦⑧⑨⑩⑪⑫\d\s]+", RegexOptions.Compiled);

    public static EngineResponse? TrySolve(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        // 1. 严格触发：键焓/键能 + kJ/mol 单位 + 设问某键的键焓为空白
        if (!raw.Contains("键焓") && !raw.Contains("键能")) return null;
        if (!raw.Contains("kJ/mol") && !raw.Contains("kJ·mol") && !raw.Contains("千焦每摩")) return null;
        bool asksBond = raw.Contains("键焓为") || raw.Contains("键能为") ||
                        raw.Contains("的键焓") || raw.Contains("的键能");
        if (!asksBond) return null;

        // 归一化负号（U+2212 − / U+FF0D － → ASCII -），便于数值解析
        var t = raw.Replace('−', '-').Replace('－', '-');

        // 2. 已知键焓：优先解析「A、B 的键焓分别为 v1、v2 kJ/mol」结构
        var known = ParseKnownBondEnergies(t);
        if (known.Count == 0) return null;

        // 3. 带 ΔH 的反应
        var rxn = ParseReactionWithDeltaH(t);
        if (rxn is null) return null;
        var (left, right, deltaH) = rxn.Value;

        // 4. 目标分子（设问「X 分子中 … 键的键焓」）
        var targetMol = ExtractTargetMolecule(t);
        if (string.IsNullOrEmpty(targetMol)) return null;
        if (!Data.Value.Molecules.TryGetValue(targetMol, out var targetBonds) || targetBonds.Count == 0)
            return null;

        // 5. 确定唯一未知键：目标分子中、不在已知表里的键
        var unknownBond = targetBonds
            .Select(b => NormalizeBond(b.Bond))
            .FirstOrDefault(b => !known.ContainsKey(b));
        if (string.IsNullOrEmpty(unknownBond)) return null; // 目标键均已知 → 没有待求量
        if (targetBonds.Count(b => NormalizeBond(b.Bond) == unknownBond) != 1) return null; // 仅一种未知键

        // 6. 所有反应物种必须在键结构表中（缺表 → 无法计量 → 不蒙数）
        var map = Data.Value.Molecules;
        if (!left.All(s => map.ContainsKey(s.species)) || !right.All(s => map.ContainsKey(s.species)))
            return null;

        // 7. 计量：ΔH = Σ(反应物键能) − Σ(生成物键能)
        double knownBroken = 0, unknownBroken = 0, knownFormed = 0, unknownFormed = 0;
        if (!Accumulate(left, map, known, unknownBond, ref knownBroken, ref unknownBroken)) return null;
        if (!Accumulate(right, map, known, unknownBond, ref knownFormed, ref unknownFormed)) return null;

        double denom = unknownBroken - unknownFormed;
        if (denom == 0d) return null; // 未知键计量抵消 → 不可解
        double x = (deltaH - knownBroken + knownFormed) / denom;
        if (!double.IsFinite(x) || x <= 0d || x >= 5000d) return null; // 物理合理性

        double ans = Math.Round(x, 2, MidpointRounding.AwayFromZero);
        string ansStr = ans.ToString("F2", CultureInfo.InvariantCulture);

        string final =
            $"由键焓公式 ΔH = 反应物总键能 − 生成物总键能，设 {targetMol} 中 {unknownBond} 键的键焓为 x，" +
            $"则 ΔH = {FormatNum(knownBroken)} − {FormatNum(knownFormed + unknownFormed)} = {FormatNum(deltaH)}，" +
            $"解得 x = {ansStr} kJ/mol。答案：{ansStr}";

        return new EngineResponse
        {
            Ok = true,
            FinalResult = final,
            DerivationSteps = new List<string>
            {
                $"已知键焓：{string.Join("，", known.Select(kv => $"{kv.Key}={kv.Value}"))}",
                $"目标分子 {targetMol} 含未知键 {unknownBond}；反应 ΔH = {FormatNum(deltaH)} kJ/mol",
                $"ΔH = 反应物总键能 − 生成物总键能 → x = {ansStr} kJ/mol"
            },
            ConstraintNote = "【高中限定】键焓反推，键结构取自 bond-structures.json，已校验反应物种计量与解的合理性",
            Confidence = 1.0
        };
    }

    private static bool Accumulate(
        List<(double coef, string species)> side,
        Dictionary<string, List<BondEntry>> map,
        Dictionary<string, double> known,
        string unknownBond,
        ref double knownSum,
        ref double unknownSum)
    {
        foreach (var (coef, species) in side)
        {
            foreach (var entry in map[species])
            {
                var b = NormalizeBond(entry.Bond);
                double contrib = entry.Count * coef;
                if (b == unknownBond) unknownSum += contrib;
                else if (known.TryGetValue(b, out var v)) knownSum += v * contrib;
                else return false; // 出现既非已知也非未知的键 → 欠定
            }
        }
        return true;
    }

    /// <summary>解析「A、B 的键焓分别为 v1、v2 kJ/mol」；失败回退全局键谱/数值等计数配对。</summary>
    private static Dictionary<string, double> ParseKnownBondEnergies(string text)
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);

        // (a) 分别为 结构：标签在前、数值在后
        var m = Regex.Match(text, @"([^。；\n]*?)(?:键焓|键能)[^。；\n]*?分别为([^。；\n]*)");
        if (m.Success)
        {
            var labels = BondSpecRe.Matches(m.Groups[1].Value)
                .Cast<Match>().Select(x => NormalizeBond(x.Value)).Distinct().ToList();
            // 仅取紧跟 kJ/mol 的数值，避免把 CO2 等分子式里的数字误作键焓值
            var values = Regex.Matches(m.Groups[2].Value, @"(\d+(?:\.\d+)?)\s*kJ/mol")
                .Cast<Match>().Select(x => double.Parse(x.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();
            if (labels.Count > 0 && labels.Count == values.Count)
            {
                for (int i = 0; i < labels.Count; i++)
                    result[labels[i]] = values[i];
                return result;
            }
        }

        // (b) 回退：全文键谱与非 ΔH 的 kJ/mol 数值等计数配对
        var allBonds = BondSpecRe.Matches(text).Cast<Match>()
            .Select(x => NormalizeBond(x.Value)).Distinct().ToList();
        // 排除 ΔH=±X kJ/mol 中的反应焓值（其前为 = 或 ± 号），只取独立给出的键焓值
        var allVals = Regex.Matches(text, @"(?<![=+\-Δ△∆])(\d+(?:\.\d+)?)\s*kJ/mol")
            .Cast<Match>().Select(x => double.Parse(x.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();
        if (allBonds.Count > 0 && allBonds.Count == allVals.Count)
        {
            for (int i = 0; i < allBonds.Count; i++)
                result[allBonds[i]] = allVals[i];
        }
        return result;
    }

    private static (List<(double coef, string species)> left, List<(double coef, string species)> right, double deltaH)?
        ParseReactionWithDeltaH(string text)
    {
        var m = DeltaHRe.Match(text);
        if (!m.Success) return null;
        if (!double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var deltaH))
            return null;

        string seg = m.Groups[1].Value;
        // 反应箭头：优先 ═ → ⟶ ＝；回退 =
        int arrowIdx = seg.IndexOfAny(ArrowChars.ToCharArray());
        if (arrowIdx < 0) arrowIdx = seg.IndexOf('=');
        if (arrowIdx <= 0) return null;

        string leftRaw = CircledPrefixRe.Replace(seg.Substring(0, arrowIdx), "");
        string rightRaw = seg.Substring(arrowIdx + 1);

        var left = TokenizeSide(leftRaw);
        var right = TokenizeSide(rightRaw);
        if (left.Count == 0 || right.Count == 0) return null;
        return (left, right, deltaH);
    }

    private static List<(double coef, string species)> TokenizeSide(string side)
    {
        var list = new List<(double, string)>();
        foreach (var raw in side.Split('+'))
        {
            var token = raw.Trim().Replace('　', ' ');
            if (token.Length == 0) continue;
            double coef = 1d;
            var cm = LeadingCoefRe.Match(token);
            string rest = token;
            if (cm.Success)
            {
                coef = ParseCoef(cm.Groups[1].Value);
                rest = token.Substring(cm.Length).Trim();
            }
            // 剥离相态 (g)(l)(s)(aq)
            rest = Regex.Replace(rest, @"\([a-z]+\)\s*$", "").Trim();
            if (rest.Length == 0) continue;
            list.Add((coef, rest));
        }
        return list;
    }

    private static double ParseCoef(string s)
    {
        if (s.Contains('/'))
        {
            var parts = s.Split('/');
            if (parts.Length == 2 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var b) && b != 0)
                return a / b;
        }
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 1d;
    }

    private static string? ExtractTargetMolecule(string text)
    {
        var m = TargetMolRe.Match(text);
        if (!m.Success) return null;
        var mol = Regex.Replace(m.Groups[1].Value, @"\([a-z]+\)\s*$", "").Trim();
        return mol.Length == 0 ? null : mol;
    }

    /// <summary>键名归一化：═(U+2550)、＝(U+FF1D) → =；保留 ≡ 与 -。</summary>
    private static string NormalizeBond(string s) =>
        s.Replace('═', '=').Replace('＝', '=').Trim();

    private static string FormatNum(double v) =>
        v.ToString("0.##", CultureInfo.InvariantCulture);
}

/// <summary>键结构数据模型：data/highschool/bond-structures.json。化学事实全部来自 JSON，不硬编码。</summary>
public sealed class BondStructureData
{
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("source")] public string Source { get; set; } = "";
    [JsonPropertyName("molecules")]
    public Dictionary<string, List<BondEntry>> Molecules { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>单分子中一类键及其数目。</summary>
public sealed class BondEntry
{
    [JsonPropertyName("bond")] public string Bond { get; set; } = "";
    [JsonPropertyName("count")] public int Count { get; set; }
}
