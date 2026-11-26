using System.Runtime.CompilerServices;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// T1（pdf-278 多部分分发 Spec）：多部分大题的单部分占位（电离平衡移动 / 酸性排序 / 曲线读数）
/// 不得作为整题结论返回，以免阻塞多部分子问分发；非多部分题的对应能力必须保持不变（零回归）。
/// </summary>
public class MultiPartPlaceholderGuardTests
{
    private static LlmGatewayService NewGateway() =>
        (LlmGatewayService)RuntimeHelpers.GetUninitializedObject(typeof(LlmGatewayService));

    private static string LoadQuestion(string id)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "pdf_questions.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var x in doc.RootElement.EnumerateArray())
            if (x.GetProperty("id").GetString() == id)
                return x.GetProperty("question").GetString() ?? "";
        throw new InvalidOperationException($"题库未找到 {id}");
    }

    private static ImageAsset LoadPdf077CurveAsset()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "pdf_questions.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var x in doc.RootElement.EnumerateArray())
            if (x.GetProperty("id").GetString() == "pdf-077")
                foreach (var a in x.GetProperty("imageAssets").EnumerateArray())
                    if (a.GetProperty("type").GetString() == "curve")
                        return new ImageAsset { Type = "curve", Payload = a.GetProperty("payload").Clone() };
        throw new InvalidOperationException("pdf-077 无 curve 资产");
    }

    private static async Task<EngineResponse> Call(string text, string? tt = null, IReadOnlyList<ImageAsset>? assets = null)
    {
        tt ??= LlmGatewayService.ClassifyTaskDeterministic(text);
        return await NewGateway().CallEngineAsync(new EngineRequest { RawText = text, TaskType = tt }, assets);
    }

    // ---- TR-1.1：pdf-278 整题占位静默（电离移动 + 酸性排序），非多部分保留 ----

    [Fact]
    public async Task Pdf278整题_电离移动与酸性排序占位均被静默_分发可激活()
    {
        var stem = LoadQuestion("pdf-278");
        Assert.True(MultiPartUtility.IsMultiPart(stem), "pdf-278 题干应为多部分");

        var resp = await Call(stem);

        // 整题不再被任一单部分占位抢占 → Ok!=true → 上层分发器（Ok is not true）得以激活
        Assert.NotEqual(true, resp.Ok);
        var fr = resp.FinalResult ?? "";
        Assert.DoesNotContain("平衡向", fr);   // TryJudgeIonizationShift 占位被静默
        Assert.DoesNotContain("由弱到强", fr); // TryOrderAcidStrength 占位被静默
    }

    [Fact]
    public async Task 非多部分_电离平衡移动占位_保留()
    {
        var text = "25℃时，醋酸溶液中加水稀释，电离平衡如何移动？电离程度如何变化？";
        Assert.False(MultiPartUtility.IsMultiPart(text));

        var resp = await Call(text, "equilibrium");

        Assert.Equal(true, resp.Ok);
        Assert.Contains("电离程度", resp.FinalResult);
    }

    [Fact]
    public async Task 非多部分_酸性排序_保留()
    {
        var text = "CH3COOH、H2CO3、HCN 的酸性由弱到强的顺序为？";
        Assert.False(MultiPartUtility.IsMultiPart(text));

        var resp = await Call(text, "equilibrium");

        Assert.Equal(true, resp.Ok);
        Assert.Contains("由弱到强", resp.FinalResult);
    }

    // ---- TR-1.2：多部分 curve 原始读数占位静默，非多部分保留 ----

    [Fact]
    public async Task 多部分Curve题_曲线读数不作为整题结论()
    {
        var asset = LoadPdf077CurveAsset();
        var text = "(1) 求 Fe³⁺ 曲线的拐点。(2) 求 Br₂ 与 Fe³⁺ 的交点。";
        Assert.True(MultiPartUtility.IsMultiPart(text));

        var resp = await Call(text, assets: new List<ImageAsset> { asset });

        // curve 原始读数只覆盖单个子问，多部分整题不得采纳 → 不阻塞分发
        Assert.NotEqual("curve", resp.AssetKind);
        Assert.NotEqual(true, resp.Ok);
    }

    [Fact]
    public async Task 非多部分Curve裸读数_保留()
    {
        var asset = LoadPdf077CurveAsset();
        var text = "n(Cl₂)=0.12 mol 时 Fe³⁺ 的物质的量";
        Assert.False(MultiPartUtility.IsMultiPart(text));

        var resp = await Call(text, "choice", new List<ImageAsset> { asset });

        Assert.Equal(true, resp.Ok);
        Assert.Equal("curve", resp.AssetKind);
        Assert.NotNull(resp.NumericValue);
    }
}
