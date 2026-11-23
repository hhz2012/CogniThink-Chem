using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 多溶液 pH 排序求解器（calculation 兜底，pdf-275）。
/// <para>域：题干含 pH，设问「由大到小 / 由小到大 / 顺序 / 排序」，并列 ≥2 个带圈编号的溶液，
/// 且浓度可解析为单一共享值（带 mol 单位）。</para>
/// <para>真值源：物种分类（强酸/强碱/盐的类别与元数）来自 <c>data/highschool/ph-species.json</c>，
/// 酸碱常数取自 <c>data/highschool/acid-constants.json</c>（由 ph-species 的 acidKey/baseKey 引用），
/// 不硬编码化学真值。</para>
/// <para>计算：强酸 pH=−lg(h·c)；强碱 pH=14+lg(oh·c)；弱酸强碱盐 [OH⁻]=√(Kb·c)（Kb=Kw/Ka）；
/// 强酸弱碱盐 [H⁺]=√(Ka·c)（Ka=Kw/Kb）；弱酸弱碱盐 pH≈7+½(pKa−pKb)。</para>
/// <para>红线（不蒙数）：触发严格、所有带圈物种必须在 ph-species 表中、引用的 Ka/Kb 键必须存在、
/// 浓度必须单一可解析；任一不满足返回 null。采纳门必经答案键校验 → precision=1、零 harm。</para>
/// </summary>
public static class PhOrderingSolver
{
    private static readonly Lazy<PhSpeciesData> SpeciesData = new(
        () => RuleStore.Load().Query<PhSpeciesData>("ph-species"));

    private static readonly Lazy<AcidConstantsData> AcidData = new(
        () => RuleStore.Load().Query<AcidConstantsData>("acid-constants"));

    private const string Circled = "①②③④⑤⑥⑦⑧⑨⑩⑪⑫⑬⑭⑮⑯⑰⑱⑲⑳";

    private static readonly Regex CircledSpeciesRe = new(
        "([" + Circled + @"])\s*([^\s" + Circled + @",，；;、]+)", RegexOptions.Compiled);

    private static readonly Regex ConcentrationRe = new(
        @"(\d+(?:\.\d+)?)\s*(?:mol·L⁻¹|mol·L\^-1|mol/L|mol·l-1|mol·L|mol|M)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static EngineResponse? TrySolve(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (raw.IndexOf("pH", StringComparison.OrdinalIgnoreCase) < 0) return null;

        // 排序方向：由大到小→pH 降序；由小到大→升序；仅有「顺序/排序」时默认升序。
        bool desc = raw.Contains("由大到小");
        bool asc = raw.Contains("由小到大");
        if (!desc && !asc && !raw.Contains("顺序") && !raw.Contains("排序")) return null;

        // 浓度：仅取带 mol 单位的数值；全部相同才采用（多浓度无法逐物种分配 → 不蒙数）。
        var concs = ConcentrationRe.Matches(raw)
            .Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
            .Where(x => x > 0)
            .ToList();
        if (concs.Count == 0) return null;
        var uniq = concs.Select(x => Math.Round(x, 12)).Distinct().ToList();
        if (uniq.Count != 1) return null;
        double c = uniq[0];

        // 解析带圈物种（按出现顺序，同号去重）
        var items = new List<(char ch, string name)>();
        var seen = new HashSet<char>();
        foreach (Match m in CircledSpeciesRe.Matches(raw))
        {
            char ch = m.Groups[1].Value[0];
            if (!seen.Add(ch)) continue;
            items.Add((ch, m.Groups[2].Value));
        }
        if (items.Count < 2) return null;

        var acid = AcidData.Value;
        double kw = ToDouble(acid.Kw);
        var species = SpeciesData.Value.Species;

        // 名称 → 物种键
        var name2key = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in species)
            foreach (var nm in kv.Value.Names)
                name2key[nm] = kv.Key;

        var phList = new List<(char ch, double ph)>();
        var deriv = new List<string>();
        foreach (var (ch, name) in items)
        {
            if (!name2key.TryGetValue(name, out var key)) return null; // 物种不在表 → 不蒙数
            var entry = species[key];
            double? ph = ComputePh(entry, c, acid, kw);
            if (ph is null) return null;
            phList.Add((ch, ph.Value));
            deriv.Add($"{ch} {name}：pH ≈ {ph.Value:0.###}");
        }

        var ordered = desc
            ? phList.OrderByDescending(x => x.ph).ToList()
            : phList.OrderBy(x => x.ph).ToList();
        string order = string.Join(">", ordered.Select(x => x.ch));
        string dirWord = desc ? "由大到小" : "由小到大";

        return new EngineResponse
        {
            Ok = true,
            FinalResult = $"浓度均为 {c.ToString(CultureInfo.InvariantCulture)} mol/L 时，各溶液 pH 计算得：{string.Join("；", deriv)}。pH {dirWord} 的顺序为：{order}。答案：{order}",
            DerivationSteps = deriv,
            ConstraintNote = "【高中限定】强酸/强碱完全电离；盐类水解 [OH⁻]=√(Kb·c)、[H⁺]=√(Ka·c)，Ka/Kb 取自 acid-constants.json；弱酸弱碱盐 pH≈7+½(pKa−pKb)。仅排序，不蒙数。",
            Confidence = 1.0
        };
    }

    private static double? ComputePh(PhSpeciesEntry e, double c, AcidConstantsData acid, double kw)
    {
        switch (e.Kind)
        {
            case "strongBase":
                if (e.Oh is null or <= 0) return null;
                double oh = e.Oh.Value * c;
                return oh <= 0 ? null : 14 + Math.Log10(oh);
            case "strongAcid":
                if (e.H is null or <= 0) return null;
                double h = e.H.Value * c;
                return h <= 0 ? null : -Math.Log10(h);
            case "saltBase":
                if (string.IsNullOrEmpty(e.AcidKey) || !acid.Ka.TryGetValue(e.AcidKey, out var kaSpec)) return null;
                double ka = ToDouble(kaSpec);
                if (ka <= 0) return null;
                double kb = kw / ka;
                double oh2 = Math.Sqrt(kb * c);
                return 14 + Math.Log10(oh2);
            case "saltAcid":
                if (string.IsNullOrEmpty(e.BaseKey) || !acid.Kb.TryGetValue(e.BaseKey, out var kbSpec)) return null;
                double kb2 = ToDouble(kbSpec);
                if (kb2 <= 0) return null;
                double ka2 = kw / kb2;
                double h2 = Math.Sqrt(ka2 * c);
                return -Math.Log10(h2);
            case "saltBoth":
                if (string.IsNullOrEmpty(e.AcidKey) || !acid.Ka.TryGetValue(e.AcidKey, out var kaS)) return null;
                if (string.IsNullOrEmpty(e.BaseKey) || !acid.Kb.TryGetValue(e.BaseKey, out var kbS)) return null;
                double ka3 = ToDouble(kaS);
                double kb3 = ToDouble(kbS);
                if (ka3 <= 0 || kb3 <= 0) return null;
                return 7 + 0.5 * (-Math.Log10(ka3) - (-Math.Log10(kb3))); // 7 + ½(pKa − pKb)
            default:
                return null;
        }
    }

    private static double ToDouble(ConstantSpec s) =>
        double.Parse(s.Mantissa, CultureInfo.InvariantCulture) * Math.Pow(10, s.Exponent);
}

/// <summary>pH 排序物种表（ph-species.json）：物种键 → 类别与常数引用。</summary>
public sealed class PhSpeciesData
{
    public string Version { get; set; } = "";

    public string Source { get; set; } = "";

    [JsonPropertyName("species")]
    public Dictionary<string, PhSpeciesEntry> Species { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>单条 pH 物种描述。</summary>
public sealed class PhSpeciesEntry
{
    [JsonPropertyName("names")]
    public List<string> Names { get; set; } = new();

    /// <summary>strongAcid / strongBase / saltBase / saltAcid / saltBoth。</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "";

    /// <summary>strongAcid：可电离 H 数。</summary>
    [JsonPropertyName("h")]
    public int? H { get; set; }

    /// <summary>strongBase：可电离 OH⁻ 数。</summary>
    [JsonPropertyName("oh")]
    public int? Oh { get; set; }

    /// <summary>盐：阴离子共轭酸在 acid-constants.json 中的 Ka 键。</summary>
    [JsonPropertyName("acidKey")]
    public string? AcidKey { get; set; }

    /// <summary>盐：阳离子共轭碱在 acid-constants.json 中的 Kb 键。</summary>
    [JsonPropertyName("baseKey")]
    public string? BaseKey { get; set; }
}
