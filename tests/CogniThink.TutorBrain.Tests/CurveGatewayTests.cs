using System.Runtime.CompilerServices;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// 网关 curve 分支：资产路由 + 查询意图提取端到端。
/// TryRouteGraph/TryRouteCurve 不依赖实例状态，故用 GetUninitializedObject 构造无参实例调用公开 CallEngineAsync。
/// </summary>
public class CurveGatewayTests
{
    private static ImageAsset LoadPdf077CurveAsset(out string question, out string renderedOptions)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "pdf_questions.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement item = default;
        foreach (var x in doc.RootElement.EnumerateArray())
            if (x.GetProperty("id").GetString() == "pdf-077") { item = x; break; }
        Assert.True(item.ValueKind == JsonValueKind.Object, "pdf-077 未找到");

        question = item.GetProperty("question").GetString() ?? "";
        var opts = item.GetProperty("options");
        renderedOptions = string.Join("\n", opts.EnumerateObject()
            .OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => p.Name + ". " + p.Value.GetString()));

        foreach (var a in item.GetProperty("imageAssets").EnumerateArray())
            if (a.GetProperty("type").GetString() == "curve")
                return new ImageAsset { Type = "curve", Payload = a.GetProperty("payload").Clone() };
        throw new InvalidOperationException("pdf-077 无 curve 资产");
    }

    private static LlmGatewayService NewGateway() =>
        (LlmGatewayService)RuntimeHelpers.GetUninitializedObject(typeof(LlmGatewayService));

    /// <summary>TR-7.1：pdf-077 真实曲线资产经网关查询 Fe³⁺ 拐点，数值 0.1 进入结论。</summary>
    [Fact]
    public async Task Pdf077_网关查询Fe3拐点_数值进入结论()
    {
        var asset = LoadPdf077CurveAsset(out _, out _);
        var req = new EngineRequest { RawText = "求 Fe³⁺ 曲线的拐点", TaskType = "choice" };

        var resp = await NewGateway().CallEngineAsync(req, new List<ImageAsset> { asset });

        Assert.True(resp.Ok == true, resp.FinalResult);
        Assert.Equal("curve", resp.AssetKind);
        Assert.NotNull(resp.NumericValue);
        Assert.True(Math.Abs(resp.NumericValue!.Value - 0.1) < 0.02, $"拐点数值={resp.NumericValue}，应≈0.1");
        Assert.Contains("0.1", resp.FinalResult);
        // y 轴无刻度 → 结论须含精度标注，不蒙真实读数
        Assert.Contains("无刻度", resp.FinalResult);
    }

    /// <summary>路由守卫：完整 pdf-077 题干含 Concept 事实（GC-729），应优先返回选择字母 C，
    /// 而不是被 TryRouteCurve 的 ValueAtX 读数（y=0.5）抢占——曲线读数是中间量并非题目最终选项。</summary>
    [Fact]
    public async Task Pdf077_完整题干选项_Concept优先于曲线读数()
    {
        var asset = LoadPdf077CurveAsset(out var question, out var renderedOptions);
        var rawText = question + "\n" + renderedOptions;

        var resp = await NewGateway().CallEngineAsync(
            new EngineRequest { RawText = rawText, TaskType = "choice" },
            new List<ImageAsset> { asset });

        // Concept 守卫命中：返回选择字母（pdf-077 错误项=C），而非曲线读数
        Assert.True(resp.Ok == true, resp.FinalResult);
        Assert.Contains("C", resp.FinalResult);
        Assert.DoesNotContain("0.12", resp.FinalResult); // 未被曲线读数抢占
        Assert.NotEqual("curve", resp.AssetKind);        // 曲线路由未命中
        Assert.Null(resp.NumericValue);
    }

    /// <summary>能力保留：裸曲线读数查询（无完整题干/选项 → Concept 事实不命中）仍走 TryRouteCurve，
    /// 返回 Fe³⁺ @ x=0.12 的读数 y=0.5。证明守卫未破坏 curve 引擎对真实读数请求的能力。</summary>
    [Fact]
    public async Task Pdf077_裸读数查询_仍走曲线路由()
    {
        var asset = LoadPdf077CurveAsset(out _, out _);
        var req = new EngineRequest { RawText = "n(Cl₂)=0.12 mol 时 Fe³⁺ 的物质的量", TaskType = "choice" };

        var resp = await NewGateway().CallEngineAsync(req, new List<ImageAsset> { asset });

        Assert.True(resp.Ok == true, resp.FinalResult);
        Assert.Equal("curve", resp.AssetKind);
        Assert.NotNull(resp.NumericValue);
        // Fe³⁺ @ x=0.12：点 (0.1,0.5)-(0.3,0.5) 水平段 → y=0.5
        Assert.True(Math.Abs(resp.NumericValue!.Value - 0.5) < 1e-6, $"读数={resp.NumericValue}，应=0.5");
        Assert.Contains("0.12", resp.FinalResult);
    }

    /// <summary>TR-7.2：查询意图无法识别时返回 null（回退通用分支），不臆造数值。</summary>
    [Fact]
    public void Extract_无法识别意图_返回null不猜()
    {
        var chart = CurveQuery.Parse(MakeAsset(@"
        {
          ""xAxisLabeled"": true, ""yAxisLabeled"": true,
          ""series"": [{""name"": ""A"", ""points"": [[0,0],[1,1]]},{""name"": ""B"", ""points"": [[0,1],[1,0]]}]
        }"));
        Assert.NotNull(chart);

        // 纯选择题干，无拐点/交点/n(X)=/极值关键词 → 不猜
        Assert.Null(CurveIntentExtractor.Extract(
            "下列说法正确的是 A.xxx B.yyy C.zzz D.www", chart!));

        // 有「拐点」但图中两条曲线都未被提及 → 不猜用哪条
        Assert.Null(CurveIntentExtractor.Extract("求该图的拐点", chart!));

        // 有「交点」但只提到一条曲线 → 不猜
        Assert.Null(CurveIntentExtractor.Extract("A 曲线的交点在哪", chart!));

        // 空文本 / 无曲线 → null
        Assert.Null(CurveIntentExtractor.Extract("", chart!));
    }

    /// <summary>意图提取：给 x 查 y、极值、交点均能正确绑定到图中真实曲线。</summary>
    [Fact]
    public void Extract_可识别意图_绑定真实曲线()
    {
        var chart = CurveQuery.Parse(MakeAsset(@"
        {
          ""xAxisLabeled"": true, ""yAxisLabeled"": true,
          ""series"": [{""name"": ""Fe³⁺"", ""points"": [[0,1],[0.1,0.5]]},{""name"": ""Br₂"", ""points"": [[0.1,1],[0.3,0.5]]}]
        }"));
        Assert.NotNull(chart);

        var knee = CurveIntentExtractor.Extract("Fe³⁺ 曲线的拐点", chart!);
        Assert.NotNull(knee);
        Assert.Equal(CurveQueryType.Knee, knee!.Type);
        Assert.Equal("Fe³⁺", knee.SeriesName);

        var vx = CurveIntentExtractor.Extract("n(Cl₂)=0.12 mol 时 Fe³⁺ 的物质的量", chart!);
        Assert.NotNull(vx);
        Assert.Equal(CurveQueryType.ValueAtX, vx!.Type);
        Assert.Equal(0.12, vx.X, 6);
        Assert.Equal("Fe³⁺", vx.SeriesName);

        var inter = CurveIntentExtractor.Extract("Fe³⁺ 与 Br₂ 的交点", chart!);
        Assert.NotNull(inter);
        Assert.Equal(CurveQueryType.Intersection, inter!.Type);
        Assert.Equal("Fe³⁺", inter.SeriesName);
        Assert.Equal("Br₂", inter.SeriesB);
    }

    private static ImageAsset MakeAsset(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return new ImageAsset { Type = "curve", Payload = doc.RootElement.Clone() };
    }
}
