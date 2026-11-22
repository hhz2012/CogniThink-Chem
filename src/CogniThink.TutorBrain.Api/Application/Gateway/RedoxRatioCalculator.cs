using System.Globalization;
using System.Text.RegularExpressions;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 氧化还原（歧化）比例计算器（calc 兜底，P1-3）。
/// <para>域：题干含「氧化剂」「还原剂」+ 问比例（质量比/物质的量比），且含一条可读计量数的氧化还原方程式。
/// 从方程式解析各物种计量数与变价元素，确定氧化剂:还原剂的物质的量比（歧化按变价原子数分配）；
/// 若氧化剂与还原剂为同一物质（歧化），质量比 = 物质的量比。</para>
/// <para>红线：比例由题干方程式解析得出，不硬编码具体题目的方程或答案；读不出计量数/无变价/域外 → null（不蒙数）。</para>
/// </summary>
public static class RedoxRatioCalculator
{
    private static readonly Regex Equation =
        new(@"([A-Za-z0-9（）()+\s＋·•]+[=＝→⇌↔][A-Za-z0-9（）()+\s＋·•]+)", RegexOptions.Compiled);

    private static readonly Regex SideSplit = new(@"[=＝→⇌↔]", RegexOptions.Compiled);
    private static readonly Regex PlusSplit = new(@"[+＋]", RegexOptions.Compiled);
    private static readonly Regex Species = new(@"^(\d+)?\s*([A-Za-z0-9（）()·•]+)", RegexOptions.Compiled);

    private static readonly HashSet<string> Group1 = new() { "Na", "K", "Li", "Rb", "Cs" };
    private static readonly HashSet<string> Group2 = new() { "Mg", "Ca", "Ba", "Sr" };

    public static EngineResponse? TrySolve(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!raw.Contains("氧化剂") || !raw.Contains("还原剂")) return null;
        if (!(raw.Contains("质量比") || raw.Contains("物质的量比") || raw.Contains("比是多少")
              || raw.Contains("比值") || raw.Contains("之比")))
            return null;

        var eqMatch = Equation.Match(raw);
        if (!eqMatch.Success) return null;
        var eq = eqMatch.Groups[1].Value;
        var parts = SideSplit.Split(eq);
        if (parts.Length < 2) return null;

        var lhs = ParseSide(parts[0]);
        var rhs = ParseSide(parts[^1]);
        if (lhs.Count == 0 || rhs.Count == 0) return null;

        // 找出在反应物中出现、且在 ≥2 个产物中有不同价态的元素（歧化 / 自身氧化还原）
        var reactElements = new HashSet<char>();
        foreach (var (_, f) in lhs)
            foreach (var ch in f)
                if (char.IsUpper(ch)) reactElements.Add(ch);

        foreach (var el in reactElements)
        {
            var reactStates = StatesFor(lhs, el);
            var prodStates = StatesFor(rhs, el);
            if (reactStates.Count < 1 || prodStates.Count < 2) continue;

            var r0 = reactStates[0].Ox;
            var oxidized = prodStates.Where(p => p.Ox > r0 + 1e-9).ToList();
            var reduced = prodStates.Where(p => p.Ox < r0 - 1e-9).ToList();
            if (oxidized.Count == 0 || reduced.Count == 0) continue;

            var nOxidized = oxidized.Sum(p => p.Coef);   // 被氧化的物质的量 → 还原剂量
            var nReduced = reduced.Sum(p => p.Coef);     // 被还原的物质的量 → 氧化剂量
            if (nOxidized <= 0 || nReduced <= 0) continue;

            var sameSubstance = oxidized.Select(p => p.Formula).Concat(reduced.Select(p => p.Formula))
                .Distinct().Count() == 1 && reactStates[0].Formula == oxidized[0].Formula;
            var ratio = $"{nReduced}:{nOxidized}";
            var ratioKind = sameSubstance ? "质量比（同一物质，等于物质的量比）" : "物质的量比";
            return new EngineResponse
            {
                Ok = true,
                FinalResult = $"反应中元素 {el} 发生歧化：{nReduced} 份被还原（作氧化剂）、{nOxidized} 份被氧化（作还原剂），故氧化剂与还原剂的{ratioKind}为 {ratio}。",
                DerivationSteps = new List<string>
                {
                    $"方程式：{eq.Trim()}",
                    $"元素 {el} 在反应物中为 {FormatOx(r0)} 价",
                    $"产物中升高（被氧化）：{string.Join("、", oxidized.Select(p => $"{p.Formula}({FormatOx(p.Ox)})×{p.Coef}"))}",
                    $"产物中降低（被还原）：{string.Join("、", reduced.Select(p => $"{p.Formula}({FormatOx(p.Ox)})×{p.Coef}"))}",
                    $"氧化剂:还原剂 = 被还原量:被氧化量 = {nReduced}:{nOxidized}"
                },
                ConstraintNote = "【高中限定】由题干配平方程式计量数与变价确定比例",
                Confidence = 1.0
            };
        }

        return null;
    }

    private static List<(int Coef, string Formula)> ParseSide(string side)
    {
        var list = new List<(int, string)>();
        foreach (var tok in PlusSplit.Split(side))
        {
            var t = tok.Trim();
            if (t.Length == 0) continue;
            var m = Species.Match(t);
            if (!m.Success) continue;
            var coef = m.Groups[1].Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 1;
            var formula = m.Groups[2].Value.Trim();
            if (formula.Length > 0) list.Add((coef, formula));
        }
        return list;
    }

    private static List<(int Coef, string Formula, double Ox)> StatesFor(List<(int Coef, string Formula)> side, char el)
    {
        var res = new List<(int, string, double)>();
        foreach (var (coef, f) in side)
        {
            var ox = OxState(f, el);
            if (ox.HasValue) res.Add((coef, f, ox.Value));
        }
        return res;
    }

    // 简单无机物（中性分子）中元素 el 的氧化态：O=-2、H=+1、第Ⅰ主族+1、第Ⅱ主族+2、F=-1；
    // 出现其它未知元素 → 返回 null（不蒙数）。
    private static double? OxState(string formula, char el)
    {
        var counts = new Dictionary<string, int>();
        var i = 0;
        while (i < formula.Length)
        {
            if (char.IsUpper(formula[i]))
            {
                var sym = formula[i].ToString();
                i++;
                while (i < formula.Length && char.IsLower(formula[i])) { sym += formula[i]; i++; }
                var num = "";
                while (i < formula.Length && char.IsDigit(formula[i])) { num += formula[i]; i++; }
                counts[sym] = counts.TryGetValue(sym, out var v) ? v + (num.Length > 0 ? int.Parse(num, CultureInfo.InvariantCulture) : 1) : (num.Length > 0 ? int.Parse(num, CultureInfo.InvariantCulture) : 1);
            }
            else if (formula[i] is '（' or '(' or '）' or ')' or '·' or '•')
            {
                i++;
            }
            else
            {
                i++;
            }
        }

        var elSym = el.ToString();
        if (!counts.TryGetValue(elSym, out var n) || n == 0) return null;

        double others = 0;
        foreach (var kv in counts)
        {
            if (kv.Key == elSym) continue;
            switch (kv.Key)
            {
                case "O": others += -2 * kv.Value; break;
                case "H": others += +1 * kv.Value; break;
                case "F": others += -1 * kv.Value; break;
                default:
                    if (Group1.Contains(kv.Key)) others += 1 * kv.Value;
                    else if (Group2.Contains(kv.Key)) others += 2 * kv.Value;
                    else return null; // 未知元素，不猜
                    break;
            }
        }
        return -others / n;
    }

    private static string FormatOx(double ox)
    {
        var r = Math.Round(ox, 6);
        return r.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
