using System.Runtime.CompilerServices;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// Task 10 / AC-8：结构化判分。molecule/curve 特征结论不含"能/正确"等判断词，
/// 走不到 BuildGatewayAnswerAsync 的具体结论分支（gatewayAnswer 为空），
/// 必须用引擎结构化产出与标准答案直接比对；IsAnswerCorrect 关键词路径作为兜底。
/// </summary>
public class StructuredScoringTests
{
    private static LlmGatewayService NewGateway() =>
        (LlmGatewayService)RuntimeHelpers.GetUninitializedObject(typeof(LlmGatewayService));

    private static ImageAsset LoadMoleculeAsset(string id)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "pdf_questions.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var x in doc.RootElement.EnumerateArray())
        {
            if (x.GetProperty("id").GetString() != id) continue;
            if (!x.TryGetProperty("imageAssets", out var assets)) continue;
            foreach (var a in assets.EnumerateArray())
                if (a.GetProperty("type").GetString() == "molecule")
                    return new ImageAsset { Type = "molecule", Payload = a.GetProperty("payload").Clone() };
        }
        throw new InvalidOperationException($"{id} 无 molecule 资产");
    }

    private static QaTestCase Case(string answer, string type = "fill") =>
        new() { Id = "unit", Answer = answer, Type = type };

    /// <summary>
    /// TR-10.1：pdf-199（3-苯基乳酸，fill）经网关产出分子特征 → 结构化判分命中正确；
    /// 而纯关键词路径（无论空回答还是引擎特征结论文本）均不命中，证明结构化是新增价值。
    /// </summary>
    [Fact]
    public async Task Pdf199_结构化判分命中_而关键词路径未命中()
    {
        var asset = LoadMoleculeAsset("pdf-199");
        var req = new EngineRequest { RawText = "3-苯基乳酸的结构", TaskType = "fill" };
        var resp = await NewGateway().CallEngineAsync(req, new List<ImageAsset> { asset });

        Assert.Equal("molecule", resp.AssetKind);
        var c = Case("(1)极性 sp^3 (2)C");

        // 结构化命中（区别于纯关键词路径）
        Assert.True(GatewayVsLlmEvaluationService.IsStructuredCorrect(resp, c),
            $"pdf-199 应通过结构化判分命中。polar={resp.MoleculeIsPolar} hybrid={resp.MoleculeCentralHybridization} " +
            $"groups={string.Join(",", resp.MoleculeFunctionalGroups ?? Array.Empty<string>())} formula={resp.MoleculeFormula} " +
            $"FinalResult={resp.FinalResult}");

        // 关键词路径：传空回答（真实管线 gatewayAnswer 为空）→ 不命中
        Assert.False(GatewayVsLlmEvaluationService.IsAnswerCorrect("", c));
        // 关键词路径：即使用引擎特征结论文本判分 → 仍不命中（特征报告不含判断词/答案关键词子串）
        Assert.False(GatewayVsLlmEvaluationService.IsAnswerCorrect(resp.FinalResult, c),
            "引擎特征结论文本不应通过纯关键词路径命中");

        // 去掉资产类型 → 结构化不命中（回退到关键词，仍 false）
        Assert.False(GatewayVsLlmEvaluationService.IsStructuredCorrect(
            new EngineResponse { AssetKind = null }, c));
    }

    /// <summary>TR-10.2：curve 数值比对带容差（max(0.05, 5%×|答案值|)）。</summary>
    [Theory]
    [InlineData("0.12 mol", true)]   // 精确命中
    [InlineData("0.118", true)]       // 差 0.002，容差内
    [InlineData("0.17", true)]        // 差 0.05，容差边界命中
    [InlineData("0.50", false)]       // 差 0.38，超出容差
    [InlineData("0.20", false)]       // 差 0.08 > 0.05，未命中
    public void Curve数值容差判分(string answer, bool expected)
    {
        var resp = new EngineResponse { AssetKind = "curve", NumericValue = 0.12 };
        Assert.Equal(expected, GatewayVsLlmEvaluationService.IsStructuredCorrect(resp, Case(answer)));
    }

    [Fact]
    public void Curve选择题_不走数值判分()
    {
        var resp = new EngineResponse { AssetKind = "curve", NumericValue = 0.12 };
        Assert.False(GatewayVsLlmEvaluationService.IsStructuredCorrect(resp, Case("0.12", "choice")));
    }

    [Fact]
    public void Curve次数值NumericValue2也参与比对()
    {
        var resp = new EngineResponse { AssetKind = "curve", NumericValue = null, NumericValue2 = 0.5 };
        Assert.True(GatewayVsLlmEvaluationService.IsStructuredCorrect(resp, Case("交点的 y 值为 0.5")));
    }

    [Fact]
    public void Molecule极性_答案极性_引擎极性_命中()
    {
        var resp = new EngineResponse { AssetKind = "molecule", MoleculeIsPolar = true };
        Assert.True(GatewayVsLlmEvaluationService.IsStructuredCorrect(resp, Case("该分子为极性分子")));
    }

    [Fact]
    public void Molecule极性_答案非极性_引擎极性_不命中()
    {
        var resp = new EngineResponse { AssetKind = "molecule", MoleculeIsPolar = true };
        Assert.False(GatewayVsLlmEvaluationService.IsStructuredCorrect(resp, Case("该分子为非极性分子")));
    }

    [Fact]
    public void Molecule杂化_sp3答案sp3_命中()
    {
        var resp = new EngineResponse { AssetKind = "molecule", MoleculeCentralHybridization = "sp3" };
        Assert.True(GatewayVsLlmEvaluationService.IsStructuredCorrect(resp, Case("中心原子杂化方式为 sp^3")));
    }

    [Fact]
    public void Molecule杂化_sp3不误匹配sp3d()
    {
        var resp = new EngineResponse { AssetKind = "molecule", MoleculeCentralHybridization = "sp3" };
        Assert.False(GatewayVsLlmEvaluationService.IsStructuredCorrect(resp, Case("sp3d 杂化")));
    }

    [Fact]
    public void Molecule杂化_答案sp2_引擎sp3_不命中()
    {
        var resp = new EngineResponse { AssetKind = "molecule", MoleculeCentralHybridization = "sp3" };
        Assert.False(GatewayVsLlmEvaluationService.IsStructuredCorrect(resp, Case("中心原子为 sp² 杂化")));
    }

    [Fact]
    public void Molecule官能团_答案含引擎官能团_命中()
    {
        var resp = new EngineResponse
        {
            AssetKind = "molecule",
            MoleculeFunctionalGroups = new[] { "羟基", "羧基" }
        };
        Assert.True(GatewayVsLlmEvaluationService.IsStructuredCorrect(resp, Case("分子中含有羟基")));
    }

    [Fact]
    public void Molecule官能团_答案不含引擎官能团_不命中()
    {
        var resp = new EngineResponse
        {
            AssetKind = "molecule",
            MoleculeFunctionalGroups = new[] { "羟基" }
        };
        Assert.False(GatewayVsLlmEvaluationService.IsStructuredCorrect(resp, Case("分子中含有羧基")));
    }

    [Fact]
    public void Molecule分子式_出现在答案中_命中()
    {
        var resp = new EngineResponse { AssetKind = "molecule", MoleculeFormula = "C5H8O4" };
        Assert.True(GatewayVsLlmEvaluationService.IsStructuredCorrect(resp, Case("C5H8O4, [C5H6O3]n")));
    }

    [Fact]
    public void 无资产类型_不结构化命中()
    {
        var resp = new EngineResponse { MoleculeIsPolar = true }; // AssetKind 为 null
        Assert.False(GatewayVsLlmEvaluationService.IsStructuredCorrect(resp, Case("极性")));
    }

    /// <summary>Task 4 (molname)：引擎确定性命名出现在答案中 → 名称维度结构化命中。</summary>
    [Fact]
    public void Molecule名称_答案含引擎命名_命中()
    {
        var resp = new EngineResponse { AssetKind = "molecule", MoleculeName = "2,4,6-三溴苯酚" };
        Assert.True(GatewayVsLlmEvaluationService.IsStructuredCorrect(resp, Case("该有机物的名称为 2,4,6-三溴苯酚")));
    }

    /// <summary>Task 4 (molname)：引擎命名不在答案中 → 名称维度不命中（不误中）。</summary>
    [Fact]
    public void Molecule名称_答案不含引擎命名_不命中()
    {
        var resp = new EngineResponse { AssetKind = "molecule", MoleculeName = "2,4,6-三溴苯酚" };
        Assert.False(GatewayVsLlmEvaluationService.IsStructuredCorrect(resp, Case("该有机物的名称为 苯酚")));
    }

    /// <summary>
    /// Task 4 (molname)：pdf-522 真实资产经网关 → 产出确定性命名 → 结构化判分命中。
    /// 端到端：若资产被回归为缺陷 SMILES（2,4,5-三溴苯酚），命名与断言会同时失败，守护数据修复。
    /// </summary>
    [Fact]
    public async Task Pdf522_修正资产_经网关_命名结构化命中()
    {
        var asset = LoadMoleculeAsset("pdf-522");
        var req = new EngineRequest { RawText = "该有机物的名称是", TaskType = "fill" };
        var resp = await NewGateway().CallEngineAsync(req, new List<ImageAsset> { asset });

        Assert.True(resp.Ok == true, resp.FinalResult);
        Assert.Equal("molecule", resp.AssetKind);
        Assert.Equal("2,4,6-三溴苯酚", resp.MoleculeName);
        Assert.True(GatewayVsLlmEvaluationService.IsStructuredCorrect(resp, Case("2,4,6-三溴苯酚")),
            "pdf-522 修正资产应通过名称维度结构化命中");
    }
}
