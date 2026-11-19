using System.Linq;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Common;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>曲线查询类型</summary>
public enum CurveQueryType
{
    /// <summary>给定 x 查 y（线性插值）</summary>
    ValueAtX,
    /// <summary>极值点（最大/最小 y）</summary>
    Extrema,
    /// <summary>两条曲线交点</summary>
    Intersection,
    /// <summary>拐点/陡变点（斜率绝对值最大的线段端点）</summary>
    Knee,
}

/// <summary>曲线查询请求</summary>
public sealed class CurveQueryRequest
{
    public CurveQueryType Type { get; init; }
    /// <summary>目标曲线名（ValueAtX/Extrema/Knee）</summary>
    public string? SeriesName { get; init; }
    /// <summary>第二条曲线名（Intersection）</summary>
    public string? SeriesB { get; init; }
    /// <summary>查询的 x 值（ValueAtX）；坐标空间与 x 轴一致（标注轴=真实值，未标注=归一化 0~1）</summary>
    public double X { get; init; }
    /// <summary>Extrema 取最大(true)/最小(false)</summary>
    public bool Max { get; init; } = true;
}

/// <summary>曲线查询结果</summary>
public sealed class CurveQueryResult
{
    public bool Ok { get; init; }
    /// <summary>可读结论（含无标注轴的精度说明）</summary>
    public string FinalResult { get; init; } = "";
    /// <summary>主数值：ValueAtX→y；Extrema/Knee→x；Intersection→交点 x</summary>
    public double? Value { get; init; }
    /// <summary>次数值：Extrema/Knee→y；Intersection→交点 y</summary>
    public double? Value2 { get; init; }
    public string? SeriesName { get; init; }
    /// <summary>x 轴有刻度（结果 x 为真实值）</summary>
    public bool XAbsolute { get; init; }
    /// <summary>y 轴有刻度（结果 y 为真实值）</summary>
    public bool YAbsolute { get; init; }
}

/// <summary>解析后的曲线图</summary>
public sealed class CurveChart
{
    public string XLabel { get; init; } = "";
    public string YLabel { get; init; } = "";
    public bool XLabeled { get; init; }
    public bool YLabeled { get; init; }
    public List<CurveSeries> Series { get; init; } = new();
}

public sealed class CurveSeries
{
    public string Name { get; init; } = "";
    public List<(double X, double Y)> Points { get; init; } = new();
}

/// <summary>
/// CurveQuery：curve payload → 数值查询（插值/极值/交点/拐点）。
/// 坐标约定（与 vectorize_poc.render_curve 一致）：标注轴方向点坐标已是真实数值（ticks 线性标定）；
/// 未标注轴方向点坐标为归一化 0~1 相对位置。红线：只做点集数学运算，不臆造图上没有的数值；
/// 某轴无刻度时结果标注"该轴无刻度，为相对值"。
/// </summary>
public static class CurveQuery
{
    /// <summary>解析 curve 资产；非 curve / 无 payload / 无 series 返回 null。</summary>
    public static CurveChart? Parse(ImageAsset asset)
    {
        if (asset.Type != "curve" || asset.Payload is not JsonElement p) return null;
        if (!p.TryGetProperty("series", out var se) || se.ValueKind != JsonValueKind.Array) return null;
        var chart = new CurveChart
        {
            XLabel = GetString(p, "xLabel"),
            YLabel = GetString(p, "yLabel"),
            XLabeled = p.TryGetProperty("xAxisLabeled", out var xl) && xl.ValueKind == JsonValueKind.True,
            YLabeled = p.TryGetProperty("yAxisLabeled", out var yl) && yl.ValueKind == JsonValueKind.True,
        };
        foreach (var s in se.EnumerateArray())
        {
            if (s.ValueKind != JsonValueKind.Object) continue;
            if (!s.TryGetProperty("points", out var pts) || pts.ValueKind != JsonValueKind.Array) continue;
            var list = new List<(double, double)>();
            foreach (var pt in pts.EnumerateArray())
            {
                if (pt.ValueKind != JsonValueKind.Array || pt.GetArrayLength() < 2) continue;
                if (!TryGetDouble(pt[0], out var x) || !TryGetDouble(pt[1], out var y)) continue;
                list.Add((x, y));
            }
            list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            chart.Series.Add(new CurveSeries { Name = GetString(s, "name"), Points = list });
        }
        return chart.Series.Count > 0 ? chart : null;
    }

    /// <summary>统一入口：按请求类型查询，返回带精度标注的结论。</summary>
    public static CurveQueryResult Query(ImageAsset asset, CurveQueryRequest req)
    {
        var chart = Parse(asset);
        if (chart == null) return Fail("curve payload 解析失败或无曲线数据");
        var a = FindSeries(chart, req.SeriesName);
        if (req.Type != CurveQueryType.Intersection && a == null)
            return Fail("未找到指定曲线：" + (req.SeriesName ?? "(空)"));

        switch (req.Type)
        {
            case CurveQueryType.ValueAtX:
                {
                    var y = ValueAtX(chart, a!.Name, req.X);
                    if (y == null) return Fail("x 超出曲线范围，无法插值");
                    return new CurveQueryResult
                    {
                        Ok = true, SeriesName = a.Name, Value = y,
                        XAbsolute = chart.XLabeled, YAbsolute = chart.YLabeled,
                        FinalResult = BuildConclusion(chart, $"x={req.X:g} 时，曲线「{a.Name}」的 y = {y:g4}", xUsed: true, yUsed: true),
                    };
                }
            case CurveQueryType.Extrema:
                {
                    var ex = Extremum(chart, a!.Name, req.Max);
                    if (ex == null) return Fail("曲线无可用点");
                    return new CurveQueryResult
                    {
                        Ok = true, SeriesName = a.Name, Value = ex.Value.x, Value2 = ex.Value.y,
                        XAbsolute = chart.XLabeled, YAbsolute = chart.YLabeled,
                        FinalResult = BuildConclusion(chart, $"曲线「{a.Name}」的{(req.Max ? "最大值" : "最小值")}点为 (x={ex.Value.x:g4}, y={ex.Value.y:g4})"),
                    };
                }
            case CurveQueryType.Intersection:
                {
                    var b = FindSeries(chart, req.SeriesB);
                    if (b == null) return Fail("未找到第二条曲线：" + (req.SeriesB ?? "(空)"));
                    var xs = Intersections(chart, a!.Name, b.Name);
                    if (xs.Count == 0) return Fail("两条曲线无交点");
                    var first = xs[0];
                    return new CurveQueryResult
                    {
                        Ok = true, SeriesName = a.Name + " & " + b.Name, Value = first.x, Value2 = first.y,
                        XAbsolute = chart.XLabeled, YAbsolute = chart.YLabeled,
                        FinalResult = BuildConclusion(chart, $"曲线「{a.Name}」与「{b.Name}」交于 (x={first.x:g4}, y={first.y:g4})"),
                    };
                }
            case CurveQueryType.Knee:
                {
                    var k = Knee(chart, a!.Name);
                    if (k == null) return Fail("曲线点不足，无法确定拐点");
                    return new CurveQueryResult
                    {
                        Ok = true, SeriesName = a.Name, Value = k.Value.x, Value2 = k.Value.y,
                        XAbsolute = chart.XLabeled, YAbsolute = chart.YLabeled,
                        FinalResult = BuildConclusion(chart, $"曲线「{a.Name}」的拐点（最陡变点）为 x={k.Value.x:g4}, y={k.Value.y:g4}"),
                    };
                }
        }
        return Fail("未知查询类型");
    }

    /// <summary>线性插值：给定 x 返回 y；x 超出 [min,max] 范围返回 null（不外推）。</summary>
    public static double? ValueAtX(CurveChart chart, string seriesName, double x)
    {
        var s = FindSeries(chart, seriesName);
        if (s == null || s.Points.Count == 0) return null;
        var pts = s.Points;
        if (x < pts[0].X - 1e-12 || x > pts[^1].X + 1e-12) return null;
        if (pts.Count == 1) return pts[0].Y;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            var (x0, y0) = pts[i];
            var (x1, y1) = pts[i + 1];
            if (x >= x0 - 1e-12 && x <= x1 + 1e-12)
            {
                if (Math.Abs(x1 - x0) < 1e-12) return y0;
                var t = (x - x0) / (x1 - x0);
                return y0 + t * (y1 - y0);
            }
        }
        return null;
    }

    /// <summary>极值点：扫描所有点取 y 最大/最小（折线极值必在顶点上）。</summary>
    public static (double x, double y)? Extremum(CurveChart chart, string seriesName, bool max)
    {
        var s = FindSeries(chart, seriesName);
        if (s == null || s.Points.Count == 0) return null;
        var best = s.Points[0];
        foreach (var p in s.Points)
            if (max ? p.Y > best.Y : p.Y < best.Y) best = p;
        return (best.X, best.Y);
    }

    /// <summary>两条曲线的交点（线段相交求解，x 范围重叠才检测）。</summary>
    public static List<(double x, double y)> Intersections(CurveChart chart, string aName, string bName)
    {
        var a = FindSeries(chart, aName); var b = FindSeries(chart, bName);
        var res = new List<(double x, double y)>();
        if (a == null || b == null || a.Points.Count < 2 || b.Points.Count < 2) return res;
        for (int i = 0; i < a.Points.Count - 1; i++)
        {
            var (ax0, ay0) = a.Points[i]; var (ax1, ay1) = a.Points[i + 1];
            for (int j = 0; j < b.Points.Count - 1; j++)
            {
                var (bx0, by0) = b.Points[j]; var (bx1, by1) = b.Points[j + 1];
                var xlo = Math.Max(ax0, bx0); var xhi = Math.Min(ax1, bx1);
                if (xlo > xhi + 1e-12) continue;
                // a(x)=ay0+(ay1-ay0)*(x-ax0)/(ax1-ax0); b(x)=by0+(by1-by0)*(x-bx0)/(bx1-bx0)
                var da = Math.Abs(ax1 - ax0) < 1e-12 ? 0 : (ay1 - ay0) / (ax1 - ax0);
                var db = Math.Abs(bx1 - bx0) < 1e-12 ? 0 : (by1 - by0) / (bx1 - bx0);
                var ca = ay0 - da * ax0;
                var cb = by0 - db * bx0;
                if (Math.Abs(da - db) < 1e-12) continue; // 平行
                var x = (cb - ca) / (da - db);
                if (x < xlo - 1e-9 || x > xhi + 1e-9) continue;
                var y = da * x + ca;
                // 去重（同一交点被相邻段重复检测）
                if (res.All(p => Math.Abs(p.x - x) > 1e-6 || Math.Abs(p.y - y) > 1e-6))
                    res.Add((x, y));
            }
        }
        return res;
    }

    /// <summary>拐点：斜率绝对值最大的线段的终点（曲线最陡变位置）。</summary>
    public static (double x, double y)? Knee(CurveChart chart, string seriesName)
    {
        var s = FindSeries(chart, seriesName);
        if (s == null || s.Points.Count < 2) return null;
        double maxAbsSlope = -1; int segIdx = 0;
        for (int i = 0; i < s.Points.Count - 1; i++)
        {
            var (x0, y0) = s.Points[i]; var (x1, y1) = s.Points[i + 1];
            var dx = x1 - x0;
            var slope = Math.Abs(dx) < 1e-12 ? 0 : Math.Abs((y1 - y0) / dx);
            if (slope > maxAbsSlope) { maxAbsSlope = slope; segIdx = i; }
        }
        if (maxAbsSlope < 1e-12) return null; // 全程水平，无拐点
        return (s.Points[segIdx + 1].X, s.Points[segIdx + 1].Y);
    }

    private static string BuildConclusion(CurveChart chart, string core, bool xUsed = true, bool yUsed = true)
    {
        var notes = new List<string>();
        if (xUsed && !chart.XLabeled) notes.Add("x 轴无刻度，x 为归一化相对位置（0~1）");
        if (yUsed && !chart.YLabeled) notes.Add("y 轴无刻度，y 为归一化相对位置（0~1）");
        return notes.Count > 0 ? core + "（注：" + string.Join("；", notes) + "）" : core;
    }

    private static CurveSeries? FindSeries(CurveChart chart, string? name)
    {
        if (string.IsNullOrEmpty(name)) return chart.Series.FirstOrDefault();
        return chart.Series.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.Ordinal))
               ?? chart.Series.FirstOrDefault(s => s.Name.Contains(name, StringComparison.Ordinal));
    }

    private static CurveQueryResult Fail(string msg) => new() { Ok = false, FinalResult = msg };

    private static string GetString(JsonElement e, string key)
        => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";

    private static bool TryGetDouble(JsonElement e, out double v)
    {
        v = 0;
        if (e.ValueKind == JsonValueKind.Number) return e.TryGetDouble(out v);
        if (e.ValueKind == JsonValueKind.String && double.TryParse(e.GetString(), out v)) return true;
        return false;
    }
}
