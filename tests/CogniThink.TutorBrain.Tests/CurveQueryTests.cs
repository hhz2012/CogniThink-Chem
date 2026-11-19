using System.Text.Json;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>CurveQuery：curve payload → 数值查询（插值/极值/交点/拐点）。红线：只做点集数学，不臆造图上没有的数值。</summary>
public class CurveQueryTests
{
    private static ImageAsset MakeCurveAsset(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return new ImageAsset { Type = "curve", Payload = doc.RootElement.Clone() };
    }

    /// <summary>TR-6.1：线性插值精度（误差 &lt; 1e-6）与极值点正确。</summary>
    [Fact]
    public void ValueAtX_插值精度正确_Extremum取到正确顶点()
    {
        var asset = MakeCurveAsset(@"
        {
          ""xLabel"": ""x"", ""yLabel"": ""y"",
          ""xAxisLabeled"": true, ""yAxisLabeled"": true,
          ""series"": [{""name"": ""A"", ""points"": [[0,0],[1,1],[2,4],[3,9]]}]
        }");

        var chart = CurveQuery.Parse(asset);
        Assert.NotNull(chart);
        Assert.Equal("A", chart!.Series[0].Name);

        // x=1.5 落在 (1,1)-(2,4) 段中点，线性插值应为 2.5
        var y = CurveQuery.ValueAtX(chart, "A", 1.5);
        Assert.NotNull(y);
        Assert.True(Math.Abs(y!.Value - 2.5) < 1e-6, $"插值误差过大：{y}");

        // 端点命中
        Assert.True(Math.Abs(CurveQuery.ValueAtX(chart, "A", 0)!.Value - 0.0) < 1e-9);
        Assert.True(Math.Abs(CurveQuery.ValueAtX(chart, "A", 3)!.Value - 9.0) < 1e-9);

        // 超出范围不外推 → null
        Assert.Null(CurveQuery.ValueAtX(chart, "A", -0.1));
        Assert.Null(CurveQuery.ValueAtX(chart, "A", 3.1));

        // 极值：最大 (3,9)，最小 (0,0)
        var max = CurveQuery.Extremum(chart, "A", max: true);
        Assert.NotNull(max);
        Assert.Equal(3.0, max!.Value.x, 6);
        Assert.Equal(9.0, max.Value.y, 6);

        var min = CurveQuery.Extremum(chart, "A", max: false);
        Assert.NotNull(min);
        Assert.Equal(0.0, min!.Value.x, 6);
        Assert.Equal(0.0, min.Value.y, 6);
    }

    /// <summary>TR-6.2：无刻度轴查询返回相对值，结论含"无刻度"精度标注（不臆造真实读数）。</summary>
    [Fact]
    public void Query_无刻度轴_结论标注相对值不蒙真实读数()
    {
        var asset = MakeCurveAsset(@"
        {
          ""xLabel"": ""反应时间"", ""yLabel"": ""产量"",
          ""xAxisLabeled"": false, ""yAxisLabeled"": false,
          ""series"": [{""name"": ""产物"", ""points"": [[0.0,0.2],[0.5,0.7],[1.0,0.9]]}]
        }");

        var res = CurveQuery.Query(asset, new CurveQueryRequest
        {
            Type = CurveQueryType.ValueAtX,
            SeriesName = "产物",
            X = 0.5,
        });

        Assert.True(res.Ok);
        Assert.False(res.XAbsolute, "x 轴无刻度，XAbsolute 应为 false");
        Assert.False(res.YAbsolute, "y 轴无刻度，YAbsolute 应为 false");
        Assert.Contains("无刻度", res.FinalResult);
        // 归一化坐标线性插值 0.5 → 0.7
        Assert.NotNull(res.Value);
        Assert.True(Math.Abs(res.Value!.Value - 0.7) < 1e-6);
    }

    /// <summary>
    /// TR-6.3：pdf-077 真实曲线资产（FeBr₂ 通 Cl₂）。x 轴 n(Cl₂) 有刻度、y 轴无刻度；
    /// Fe³⁺ 曲线在 n(Cl₂)=0.1 处由 1.0 陡降至 0.5（Fe²⁺ 恰好被完全氧化），之后持平 →
    /// 拐点（最陡变点）x 应 ≈ 0.1。验证 Knee 算法与真实 payload 解析。
    /// </summary>
    [Fact]
    public void Knee_pdf077真实Fe3曲线_拐点约0_1()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "pdf_questions.json");
        Assert.True(File.Exists(path), $"测试数据缺失：{path}");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));

        JsonElement item = default;
        foreach (var x in doc.RootElement.EnumerateArray())
            if (x.GetProperty("id").GetString() == "pdf-077") { item = x; break; }
        Assert.True(item.ValueKind == JsonValueKind.Object, "pdf-077 未找到");

        ImageAsset? asset = null;
        foreach (var a in item.GetProperty("imageAssets").EnumerateArray())
            if (a.GetProperty("type").GetString() == "curve")
                asset = new ImageAsset { Type = "curve", Payload = a.GetProperty("payload").Clone() };
        Assert.NotNull(asset);

        var chart = CurveQuery.Parse(asset!);
        Assert.NotNull(chart);
        Assert.True(chart!.XLabeled, "pdf-077 x 轴 n(Cl₂) 应有刻度");
        Assert.False(chart.YLabeled, "pdf-077 y 轴应无刻度（仅 0 标注）");

        var res = CurveQuery.Query(asset!, new CurveQueryRequest
        {
            Type = CurveQueryType.Knee,
            SeriesName = "Fe³⁺",
        });

        Assert.True(res.Ok, res.FinalResult);
        Assert.Equal("Fe³⁺", res.SeriesName);
        Assert.NotNull(res.Value);
        // Fe³⁺ 点 (0,1)→(0.1,0.5) 斜率 -5，(0.1,0.5)→(0.3,0.5) 斜率 0 → 拐点 x=0.1
        Assert.True(Math.Abs(res.Value!.Value - 0.1) < 0.02, $"拐点 x={res.Value}，应≈0.1");
        // y 轴无刻度 → 结论须标注
        Assert.Contains("无刻度", res.FinalResult);
    }
}
