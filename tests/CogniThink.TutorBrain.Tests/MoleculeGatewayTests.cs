using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// 网关 molecule 分支：资产 → MoleculeAnalyzer 结构特征端到端。
/// TryRouteMolecule 不依赖实例状态，故用 GetUninitializedObject 调公开 CallEngineAsync。
/// </summary>
public class MoleculeGatewayTests
{
    private static LlmGatewayService NewGateway() =>
        (LlmGatewayService)RuntimeHelpers.GetUninitializedObject(typeof(LlmGatewayService));

    private static ImageAsset MakeMoleculeAsset(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return new ImageAsset { Type = "molecule", Payload = doc.RootElement.Clone() };
    }

    private static ImageAsset LoadMoleculeAsset(string id)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "pdf_questions.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var x in doc.RootElement.EnumerateArray())
        {
            if (x.GetProperty("id").GetString() != id) continue;
            foreach (var a in x.GetProperty("imageAssets").EnumerateArray())
                if (a.GetProperty("type").GetString() == "molecule")
                    return new ImageAsset { Type = "molecule", Payload = a.GetProperty("payload").Clone() };
        }
        throw new InvalidOperationException($"{id} 无 molecule 资产");
    }

    /// <summary>TR-9.1：构造 SOCl₂ 资产经网关 → 结论含极性与 sp³ 杂化特征。</summary>
    [Fact]
    public async Task SOCl2资产_经网关_返回极性与sp3特征()
    {
        var asset = MakeMoleculeAsset("{\"name\":\"SOCl2\",\"smiles\":\"O=S(Cl)Cl\",\"valid\":true}");
        var req = new EngineRequest { RawText = "判断 SOCl2 分子的极性和 S 原子的杂化方式", TaskType = "fill" };

        var resp = await NewGateway().CallEngineAsync(req, new List<ImageAsset> { asset });

        Assert.True(resp.Ok == true, resp.FinalResult);
        Assert.Equal("molecule", resp.AssetKind);
        Assert.True(resp.MoleculeIsPolar == true, "SOCl2 应为极性分子");
        Assert.Equal("sp3", resp.MoleculeCentralHybridization);
        Assert.Contains("极性", resp.FinalResult);
        Assert.Contains("sp³", resp.FinalResult);
        Assert.Equal("Cl2OS", resp.MoleculeFormula);
    }

    /// <summary>TR-9.1：pdf-199 真实分子资产（3-苯基乳酸）经网关 → 返回苯环/羟基/羧基特征。</summary>
    [Fact]
    public async Task Pdf199真实资产_经网关_返回官能团特征()
    {
        var asset = LoadMoleculeAsset("pdf-199");
        var req = new EngineRequest { RawText = "3-苯基乳酸的结构", TaskType = "fill" };

        var resp = await NewGateway().CallEngineAsync(req, new List<ImageAsset> { asset });

        Assert.True(resp.Ok == true, resp.FinalResult);
        Assert.Equal("molecule", resp.AssetKind);
        Assert.NotNull(resp.MoleculeFunctionalGroups);
        Assert.Contains("苯环", resp.MoleculeFunctionalGroups);
        Assert.Contains("羟基", resp.MoleculeFunctionalGroups);
        Assert.Contains("羧基", resp.MoleculeFunctionalGroups);
        Assert.NotNull(resp.MoleculeFormula);
        Assert.Contains("分子式", resp.FinalResult);
    }

    /// <summary>
    /// TR-9.2：弱相关分子题（pdf-140）经网关 → 返回分子特征，但不臆造与问题无关的结论。
    /// 特征是 RDKit 真值（分子式/官能团/杂化/极性），FinalResult 为特征报告而非猜测的答案。
    /// </summary>
    [Fact]
    public async Task Pdf140弱相关题_返回特征不臆造无关结论()
    {
        var asset = LoadMoleculeAsset("pdf-140");
        // 题干与分子结构弱相关：网关仍只输出分子特征，不猜题目的具体答案
        var req = new EngineRequest { RawText = "根据所学知识回答下列问题", TaskType = "fill" };

        var resp = await NewGateway().CallEngineAsync(req, new List<ImageAsset> { asset });

        Assert.True(resp.Ok == true, resp.FinalResult);
        Assert.Equal("molecule", resp.AssetKind);
        Assert.NotNull(resp.MoleculeFormula);
        Assert.NotEmpty(resp.MoleculeFormula!);
        // 结论是特征报告（含分子式/官能团关键词），不是臆造的具体答案
        Assert.Contains("分子式", resp.FinalResult);
        Assert.Contains("官能团", resp.FinalResult);
    }

    /// <summary>选择题带 molecule 资产时不短路（保留原有选项校验路径），TryRouteMolecule 返回 null。</summary>
    [Fact]
    public void 选择题_分子分支不短路()
    {
        var asset = MakeMoleculeAsset("{\"smiles\":\"O=S(Cl)Cl\",\"valid\":true}");
        var svc = NewGateway();
        var m = typeof(LlmGatewayService).GetMethod(
            "TryRouteMolecule", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(m);

        var result = m!.Invoke(svc, new object[]
        {
            new EngineRequest { RawText = "下列说法正确的是\nA. xx\nB. yy", TaskType = "choice" },
            new List<ImageAsset> { asset },
        });
        Assert.Null(result);
    }

    /// <summary>无效 molecule 资产 → TryRouteMolecule 返回 null 回退，不抛异常、不臆造。</summary>
    [Fact]
    public void 无效分子资产_回退不臆造()
    {
        var asset = MakeMoleculeAsset("{\"smiles\":\"not_a_valid_smiles_###\",\"valid\":false}");
        var svc = NewGateway();
        var m = typeof(LlmGatewayService).GetMethod(
            "TryRouteMolecule", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(m);

        var result = m!.Invoke(svc, new object[]
        {
            new EngineRequest { RawText = "分析该分子", TaskType = "fill" },
            new List<ImageAsset> { asset },
        });
        Assert.Null(result);
    }
}
