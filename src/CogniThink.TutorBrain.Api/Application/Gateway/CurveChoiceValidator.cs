using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CogniThink.TutorBrain.Application.Common;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// Task 41：曲线图像选择题逐选项校验——用 curve 资产的点集真值判定选项。
/// <para>真值源：<see cref="CurveQuery"/>（ValueAtX 线性插值 / Extremum / Intersections）。</para>
/// <para>红线（不蒙数、零假阳性）：仅当四个选项<b>全部</b>能干净归入显式数值断言且候选唯一 → 返回答案字母；
/// 仅支持两轴均标注（真实数值）的曲线；需多步化学推理、未标注轴、定性判断 → 诚实 null。</para>
/// </summary>
public static class CurveChoiceValidator
{
    private const string ConstraintNote =
        "【高中限定】曲线判定基于 curve 资产的点集真值（指定 x 处 y / 极值 / 两曲线比较 / 交点）；" +
        "要求两轴均标注真实刻度；需化学推理、定性比较、未标注轴的断言不臆断。";

    private const string Num = @"[-+]?\d+(?:\.\d+)?";

    public static ChoiceVerifier.Outcome? TryDecide(EngineRequest req, IReadOnlyList<ImageAsset>? imageAssets)
    {
        // 守卫1：存在可解析的 curve 资产，且两轴均标注（未标注轴为归一化 0~1，不能比对真实数值）
        var asset = imageAssets?.FirstOrDefault(a => a.Type == "curve");
        if (asset is null) return null;
        var chart = CurveQuery.Parse(asset);
        if (chart is null || chart.Series.Count == 0) return null;
        if (!chart.XLabeled || !chart.YLabeled) return null;

        // 守卫2：题干指向该图；4 选项 A-D；askDir 明确
        var raw = req.RawText ?? "";
        var (stem, opts) = ChoiceVerifier.ParseOptions(raw);
        if (opts.Count != 4 || !"ABCD".All(c => opts.ContainsKey(c.ToString()))) return null;
        if (!Regex.IsMatch(stem, @"如图|下图|图像|所示")) return null;

        var askDir = AskDir(stem);
        if (askDir == "?") return null;

        // 逐选项判定：四选项必须全部可判
        var verdicts = new List<(string letter, bool ok)>();
        var reasons = new List<string>();
        foreach (var kv in opts.OrderBy(x => x.Key))
        {
            var v = JudgeOption(kv.Value, chart);
            if (v is null) return null; // 任一不可判 → 整题 null（不蒙数）
            verdicts.Add((kv.Key, v.Value));
            reasons.Add($"{kv.Key}：{kv.Value}（真值 {(v.Value ? "真" : "假")}）");
        }
        return ChooseUnique(verdicts, askDir, reasons);
    }

    /// <summary>判定一个选项的真伪；不可判（无法归入支持断言类型 / 引用不存在的曲线或 x 越界 / 歧义）返回 null。</summary>
    private static bool? JudgeOption(string text, CurveChart chart)
    {
        var hits = new List<bool>();

        // 1. 两曲线同极值比较（先判，避免被单曲线极值部分命中）
        var ce = MatchCompareExtremum(text, chart);
        if (ce != null) hits.Add(ce.Value);

        // 2. 指定 x 处两曲线 y 比较
        var cx = MatchCompareAtX(text, chart);
        if (cx != null) hits.Add(cx.Value);

        // 3. 交点横坐标
        var inter = MatchIntersection(text, chart);
        if (inter != null) hits.Add(inter.Value);

        // 4. 单曲线极值 y
        var ex = MatchExtremum(text, chart);
        if (ex != null) hits.Add(ex.Value);

        // 5. 指定 x 处 y 值
        var vx = MatchValueAtX(text, chart);
        if (vx != null) hits.Add(vx.Value);

        // 恰好一个断言命中才判定；零命中或多命中（歧义）→ null
        return hits.Count == 1 ? hits[0] : null;
    }

    // ===================== 断言匹配器 =====================

    private static bool? MatchValueAtX(string text, CurveChart chart)
    {
        var m = Regex.Match(text, $@"当\s*x\s*=\s*(?<x>{Num})\s*时[，,]\s*曲线(?<n>.+?)\s*的\s*y\s*为\s*(?<v>{Num})");
        if (!m.Success) return null;
        if (!ParseNum(m.Groups["x"].Value, out var x) || !ParseNum(m.Groups["v"].Value, out var v)) return null;
        var actual = CurveQuery.ValueAtX(chart, m.Groups["n"].Value.Trim(), x);
        if (actual == null) return null; // 曲线不存在或 x 越界 → 不可判
        return ApproxEq(actual.Value, v);
    }

    private static bool? MatchExtremum(string text, CurveChart chart)
    {
        var m = Regex.Match(text, $@"曲线(?<n>.+?)\s*的\s*y\s*(?<w>最大值|最小值)\s*为\s*(?<v>{Num})");
        if (!m.Success) return null;
        if (!ParseNum(m.Groups["v"].Value, out var v)) return null;
        var max = m.Groups["w"].Value == "最大值";
        var ex = CurveQuery.Extremum(chart, m.Groups["n"].Value.Trim(), max);
        if (ex == null) return null;
        return ApproxEq(ex.Value.y, v);
    }

    private static bool? MatchCompareAtX(string text, CurveChart chart)
    {
        var m = Regex.Match(text, $@"当\s*x\s*=\s*(?<x>{Num})\s*时[，,]\s*曲线(?<n1>.+?)\s*的\s*y\s*(?<dir>大于|小于)\s*曲线(?<n2>.+?)\s*的\s*y");
        if (!m.Success) return null;
        if (!ParseNum(m.Groups["x"].Value, out var x)) return null;
        var a = CurveQuery.ValueAtX(chart, m.Groups["n1"].Value.Trim(), x);
        var b = CurveQuery.ValueAtX(chart, m.Groups["n2"].Value.Trim(), x);
        if (a == null || b == null) return null;
        return m.Groups["dir"].Value == "大于" ? a > b : a < b;
    }

    private static bool? MatchCompareExtremum(string text, CurveChart chart)
    {
        var m = Regex.Match(text, $@"曲线(?<n1>.+?)\s*的\s*y\s*(?<w>最大值|最小值)\s*(?<dir>大于|小于|等于)\s*曲线(?<n2>.+?)\s*的\s*y\s*(?<w2>最大值|最小值)");
        if (!m.Success) return null;
        if (m.Groups["w"].Value != m.Groups["w2"].Value) return null; // 必须同是最大值或同是最小值
        var max = m.Groups["w"].Value == "最大值";
        var a = CurveQuery.Extremum(chart, m.Groups["n1"].Value.Trim(), max);
        var b = CurveQuery.Extremum(chart, m.Groups["n2"].Value.Trim(), max);
        if (a == null || b == null) return null;
        return m.Groups["dir"].Value switch
        {
            "大于" => a.Value.y > b.Value.y,
            "小于" => a.Value.y < b.Value.y,
            "等于" => ApproxEq(a.Value.y, b.Value.y),
            _ => (bool?)null,
        };
    }

    private static bool? MatchIntersection(string text, CurveChart chart)
    {
        var m = Regex.Match(text, $@"曲线(?<n1>.+?)\s*与\s*曲线(?<n2>.+?)\s*的\s*交点横坐标为\s*(?<v>{Num})");
        if (!m.Success) return null;
        if (!ParseNum(m.Groups["v"].Value, out var v)) return null;
        var xs = CurveQuery.Intersections(chart, m.Groups["n1"].Value.Trim(), m.Groups["n2"].Value.Trim());
        return xs.Any(p => ApproxEq(p.x, v));
    }

    // ===================== 工具 =====================

    private static bool ParseNum(string s, out double v)
        => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

    private static bool ApproxEq(double a, double b)
        => Math.Abs(a - b) <= 1e-4 * Math.Max(1.0, Math.Abs(b));

    private static string AskDir(string stem)
    {
        if (Regex.IsMatch(stem, @"错误|不正确|不相符|有误")) return "wrong";
        if (Regex.IsMatch(stem, @"正确|符合|准确")) return "correct";
        return "?";
    }

    private static ChoiceVerifier.Outcome? ChooseUnique(List<(string letter, bool ok)> verdicts, string askDir, List<string> reasons)
    {
        var wantTrue = askDir == "correct";
        var candidates = verdicts.Where(v => v.ok == wantTrue).Select(v => v.letter).ToList();
        if (candidates.Count != 1) return null;
        var letter = candidates[0];
        var dirWord = wantTrue ? "正确选项" : "错误选项";
        return new ChoiceVerifier.Outcome(letter, $"{dirWord}为 {letter}", reasons, ConstraintNote);
    }
}
