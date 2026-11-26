using CogniThink.Chem.Core.Common;
using CogniThink.Chem.Core.Molecules;
using Xunit;

namespace CogniThink.Chem.Tests;

/// <summary>
/// 分子模块（RDKit 封装）测试：SMILES 解析、分子式、高中官能团白名单、超纲标记。
/// </summary>
public class MoleculeTests
{
    [Theory]
    [InlineData("CCO", "C2H6O", "羟基")]                 // 乙醇
    [InlineData("CC(=O)O", "C2H4O2", "羧基")]             // 乙酸
    [InlineData("CC=O", "C2H4O", "醛基")]                 // 乙醛
    [InlineData("CC(=O)C", "C3H6O", "酮羰基")]            // 丙酮
    [InlineData("CC(=O)OCC", "C4H8O2", "酯基")]           // 乙酸乙酯
    [InlineData("CCOCC", "C4H10O", "醚键")]               // 乙醚
    [InlineData("C=C", "C2H4", "碳碳双键")]               // 乙烯
    [InlineData("C#C", "C2H2", "碳碳三键")]               // 乙炔
    [InlineData("CCCl", "C2H5Cl", "卤原子")]              // 氯乙烷
    [InlineData("c1ccccc1", "C6H6", "苯环")]              // 苯
    [InlineData("NCC(=O)O", "C2H5NO2", "氨基")]           // 甘氨酸（同时含羧基）
    public void InScopeFunctionalGroups_Recognized(string smiles, string formula, string expectedGroup)
    {
        var analysis = MoleculeValidator.Analyze(smiles);
        Assert.True(analysis.Success, analysis.FailReason);
        Assert.Equal(formula, analysis.MolecularFormula);
        Assert.Contains(expectedGroup, analysis.FunctionalGroups);
        Assert.Empty(analysis.OutOfScopeGroups);
        Assert.Equal(RiskTags.None, analysis.RiskTag);
    }

    [Fact]
    public void CarboxylHydroxyl_NotCountedAsHydroxyl()
    {
        // 乙酸只报羧基，不额外报羟基
        var analysis = MoleculeValidator.Analyze("CC(=O)O");
        Assert.Contains("羧基", analysis.FunctionalGroups);
        Assert.DoesNotContain("羟基", analysis.FunctionalGroups);
    }

    [Fact]
    public void EsterOxygen_NotCountedAsEther()
    {
        // 乙酸乙酯只报酯基，不误报醚键
        var analysis = MoleculeValidator.Analyze("CC(=O)OCC");
        Assert.Contains("酯基", analysis.FunctionalGroups);
        Assert.DoesNotContain("醚键", analysis.FunctionalGroups);
    }

    [Fact]
    public void Nitrobenzene_NitroAndBenzene()
    {
        var analysis = MoleculeValidator.Analyze("O=[N+]([O-])c1ccccc1");
        Assert.True(analysis.Success);
        Assert.Contains("硝基", analysis.FunctionalGroups);
        Assert.Contains("苯环", analysis.FunctionalGroups);
    }

    [Fact]
    public void PeptideBond_Recognized()
    {
        // N-甲基乙酰胺（肽键模型）
        var analysis = MoleculeValidator.Analyze("CC(=O)NC");
        Assert.True(analysis.Success);
        Assert.Contains("肽键", analysis.FunctionalGroups);
    }

    [Fact]
    public void Alkane_NoFunctionalGroup_StillValid()
    {
        var report = MoleculeValidator.Check("CC");
        Assert.True(report.IsValid, report.FailureSummary());
        Assert.Contains(report.CheckItems, i => i.Detail.Contains("无官能团"));
    }

    [Theory]
    [InlineData("CS(=O)(=O)O", "磺酸基")]   // 甲磺酸
    [InlineData("CCS", "巯基")]             // 乙硫醇
    [InlineData("C[Si](C)(C)C", "非高中有机常见元素 Si")] // 四甲基硅烷
    public void OutOfScopeStructures_Flagged(string smiles, string expectedFeature)
    {
        var analysis = MoleculeValidator.Analyze(smiles);
        Assert.True(analysis.Success);
        Assert.Contains(expectedFeature, analysis.OutOfScopeGroups);
        Assert.Equal(RiskTags.OutOfScope, analysis.RiskTag);
    }

    [Fact]
    public void InvalidSmiles_Fails()
    {
        var report = MoleculeValidator.Check("CC(");
        Assert.False(report.IsValid);
        Assert.Equal(RiskTags.ParseError, report.RiskTag);
        Assert.NotEmpty(report.ErrorMessage);
    }

    /// <summary>
    /// 漂移守护：MoleculeAnalyzer 与 MoleculeValidator 共用 <see cref="FunctionalGroupVocabulary.InScope"/>，
    /// 对一组覆盖各类官能团的分子，两者识别出的官能团名称集合应完全一致（顺序无关）。
    /// 若未来有人改了其中一套词表，本测试会红，倒逼口径统一。
    /// </summary>
    [Theory]
    [InlineData("CC(N)=O")]
    [InlineData("COc1ccccc1")]
    [InlineData("CN(C)C")]
    [InlineData("CSC")]
    [InlineData("NCC(=O)O")]
    [InlineData("CC(=O)OCC")]
    [InlineData("O=[N+]([O-])c1ccccc1")]
    [InlineData("Nc1cccc(C(=O)O)c1")]
    [InlineData("c1ccccc1")]
    public void Analyzer与Validator_官能团集合一致(string smiles)
    {
        var a = MoleculeAnalyzer.Analyze(smiles);
        var v = MoleculeValidator.Analyze(smiles);
        Assert.True(a.Success, a.FailReason);
        Assert.True(v.Success, v.FailReason);
        Assert.Equal(new HashSet<string>(a.FunctionalGroups), new HashSet<string>(v.FunctionalGroups));
    }
}
