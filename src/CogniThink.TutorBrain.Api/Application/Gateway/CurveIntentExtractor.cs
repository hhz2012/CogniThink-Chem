using System.Text;
using System.Text.RegularExpressions;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 从题干+选项文本中识别曲线查询意图（拐点 / 交点 / 给 x 查 y / 极值），并绑定到图中真实存在的曲线。
/// 红线：关键词 / 参数 / 曲线名任一缺失 → 返回 null，由网关回退通用分支，绝不猜一条曲线或一个数值。
/// </summary>
public static class CurveIntentExtractor
{
    private static readonly Regex KneeRe = new(@"拐点|最陡|陡变|转折点", RegexOptions.Compiled);
    private static readonly Regex IntersectRe = new(@"交点|相交|交于", RegexOptions.Compiled);
    private static readonly Regex MaxRe = new(@"最大|最高|峰值|极大", RegexOptions.Compiled);
    private static readonly Regex MinRe = new(@"最小|最低|谷值|极小", RegexOptions.Compiled);

    // n(Cl₂)=0.12 或 n(Cl2) = 0.12（捕获轴符号与数值）
    private static readonly Regex ValueAtXRe = new(
        @"n\s*\(\s*(?<sym>[^)=]+?)\s*\)\s*=\s*(?<num>\d+(?:\.\d+)?)",
        RegexOptions.Compiled);

    // x=0.12 时 / pH=7.0 时 这类显式轴赋值（符号限字母+下标上标，且须带"时"避免误命中比例式）
    private static readonly Regex AxisAtXRe = new(
        @"(?<![A-Za-z0-9])(?<sym>[A-Za-z][\w\u2070-\u209f⁺⁻]*)\s*=\s*(?<num>\d+(?:\.\d+)?)\s*(?:mol)?\s*时",
        RegexOptions.Compiled);

    public static CurveQueryRequest? Extract(string rawText, CurveChart chart)
    {
        if (string.IsNullOrWhiteSpace(rawText) || chart.Series.Count == 0) return null;

        // 1) 拐点 / 最陡变点
        if (KneeRe.IsMatch(rawText))
        {
            var name = PickSeries(rawText, chart);
            if (name == null) return null;
            return new CurveQueryRequest { Type = CurveQueryType.Knee, SeriesName = name };
        }

        // 2) 两曲线交点
        if (IntersectRe.IsMatch(rawText))
        {
            var pair = PickTwoSeries(rawText, chart);
            if (pair == null) return null;
            return new CurveQueryRequest
            {
                Type = CurveQueryType.Intersection,
                SeriesName = pair.Value.a,
                SeriesB = pair.Value.b,
            };
        }

        // 3) 给定 x 查 y：n(X)=<num> 或 X=<num> 时
        var vm = ValueAtXRe.Match(rawText);
        if (!vm.Success) vm = AxisAtXRe.Match(rawText);
        if (vm.Success && double.TryParse(vm.Groups["num"].Value, out var x))
        {
            var name = PickSeries(rawText, chart);
            if (name == null) return null;
            return new CurveQueryRequest { Type = CurveQueryType.ValueAtX, SeriesName = name, X = x };
        }

        // 4) 极值
        var hasMax = MaxRe.IsMatch(rawText);
        var hasMin = MinRe.IsMatch(rawText);
        if (hasMax || hasMin)
        {
            var name = PickSeries(rawText, chart);
            if (name == null) return null;
            bool max = hasMax || !hasMin; // 仅出现"最小"→取最小，否则取最大
            return new CurveQueryRequest { Type = CurveQueryType.Extrema, SeriesName = name, Max = max };
        }

        return null;
    }

    /// <summary>从文本中定位一条真实存在的曲线：优先精确名/归一化名命中；仅当图中只有一条曲线时才默认取它。</summary>
    private static string? PickSeries(string text, CurveChart chart)
    {
        var ntext = Norm(text);
        foreach (var s in chart.Series)
        {
            if (text.Contains(s.Name, StringComparison.Ordinal) || ntext.Contains(Norm(s.Name), StringComparison.Ordinal))
                return s.Name;
        }
        return chart.Series.Count == 1 ? chart.Series[0].Name : null;
    }

    private static (string a, string b)? PickTwoSeries(string text, CurveChart chart)
    {
        var ntext = Norm(text);
        var matched = new List<string>();
        foreach (var s in chart.Series)
        {
            if (text.Contains(s.Name, StringComparison.Ordinal) || ntext.Contains(Norm(s.Name), StringComparison.Ordinal))
                matched.Add(s.Name);
        }
        return matched.Count >= 2 ? (matched[0], matched[1]) : null;
    }

    /// <summary>把上标下标统一为 ASCII，便于文本中的 Fe3+ 与曲线名 Fe³⁺ 对齐。</summary>
    private static string Norm(string s)
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
                '⁺' => '+', '⁻' => '-',
                _ => ch,
            });
        }
        return sb.ToString();
    }
}
