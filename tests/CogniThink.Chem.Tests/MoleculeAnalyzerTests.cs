using CogniThink.Chem.Core.Molecules;
using Xunit;

namespace CogniThink.Chem.Tests;

/// <summary>MoleculeAnalyzer：RDKit 结构特征（不饱和度/官能团/杂化/极性）。红线：解析失败如实返回空，不臆造。</summary>
public class MoleculeAnalyzerTests
{
    /// <summary>TR-8.1：SOCl₂ 中心原子 S 杂化 sp³、极性；不饱和度在苯/乙烯上正确。</summary>
    [Fact]
    public void SOCl2_中心S杂化sp3且为极性分子()
    {
        var r = MoleculeAnalyzer.Analyze("O=S(Cl)Cl");

        Assert.True(r.Success, r.FailReason);
        Assert.Equal("Cl2OS", r.MolecularFormula);
        Assert.Equal("S", r.CentralAtomSymbol);
        Assert.Equal("sp3", r.CentralAtomHybridization);
        Assert.Equal(4, r.CentralAtomStericNumber);  // 3 σ + 1 孤对
        Assert.Equal(1, r.CentralAtomLonePairs);
        Assert.True(r.IsPolar, "SOCl₂ 三角锥形，应为极性分子");
        Assert.Contains("极性", r.FinalResult);
        Assert.Contains("sp³", r.FinalResult);
    }

    [Theory]
    [InlineData("c1ccccc1", 4)]   // 苯 Ω=4
    [InlineData("C=C", 1)]         // 乙烯 Ω=1
    [InlineData("CCO", 0)]         // 乙醇 Ω=0
    [InlineData("CC(=O)O", 1)]     // 乙酸 Ω=1（羰基）
    [InlineData("C#C", 2)]         // 乙炔 Ω=2
    public void 不饱和度_与分子式一致(string smiles, int expectedDou)
    {
        var r = MoleculeAnalyzer.Analyze(smiles);
        Assert.True(r.Success, r.FailReason);
        Assert.Equal(expectedDou, r.DegreeOfUnsaturation);
    }

    /// <summary>极性近似（高中口径）回归：CO2/CCl4 非极性，H2O/NH3/SOCl2/CH3Cl 极性。</summary>
    [Theory]
    [InlineData("O=C=O", false)]   // CO2 直线对称
    [InlineData("ClC(Cl)(Cl)Cl", false)] // CCl4 正四面体对称
    [InlineData("O", true)]        // H2O 极性
    [InlineData("N", true)]        // NH3 极性
    [InlineData("O=S(Cl)Cl", true)]
    [InlineData("ClC", true)]      // CH3Cl 极性
    [InlineData("O=O", false)]     // O2 非极性
    public void 极性近似_符合高中常见结论(string smiles, bool expectedPolar)
    {
        var r = MoleculeAnalyzer.Analyze(smiles);
        Assert.True(r.Success, r.FailReason);
        Assert.Equal(expectedPolar, r.IsPolar);
    }

    /// <summary>杂化覆盖：CO2 中心 C 为 sp，乙烯 C 为 sp2。</summary>
    [Theory]
    [InlineData("O=C=O", "C", "sp")]
    [InlineData("C=C", "C", "sp2")]
    [InlineData("O", "O", "sp3")]
    public void 中心原子杂化_正确(string smiles, string symbol, string hybrid)
    {
        var r = MoleculeAnalyzer.Analyze(smiles);
        Assert.True(r.Success, r.FailReason);
        Assert.Equal(symbol, r.CentralAtomSymbol);
        Assert.Equal(hybrid, r.CentralAtomHybridization);
    }

    /// <summary>TR-8.2：无效 SMILES / 空输入 → Success=false，不抛异常、不臆造特征。</summary>
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("invalid_not_a_smiles_###")]
    public void 无效输入_如实失败不臆造(string? smiles)
    {
        var r = MoleculeAnalyzer.Analyze(smiles);
        Assert.False(r.Success);
        Assert.False(string.IsNullOrWhiteSpace(r.FinalResult));
        Assert.Empty(r.FunctionalGroups);
        Assert.Null(r.CentralAtomHybridization);
    }

    /// <summary>TR-8.3：3-苯基乳酸（pdf-199 资产）识别出 ≥3 种官能团（苯环/羟基/羧基）。</summary>
    [Fact]
    public void 多官能团分子_识别至少3种()
    {
        var r = MoleculeAnalyzer.Analyze("OC(Cc1ccccc1)CC(=O)O"); // 3-苯基乳酸
        Assert.True(r.Success, r.FailReason);
        Assert.Contains("苯环", r.FunctionalGroups);
        Assert.Contains("羟基", r.FunctionalGroups);
        Assert.Contains("羧基", r.FunctionalGroups);
        Assert.True(r.FunctionalGroups.Count >= 3,
            $"应识别≥3种官能团，实际：{string.Join(",", r.FunctionalGroups)}");
    }

    /// <summary>molblock 输入也能解析（pdf 资产 payload 含 molblock 字段）。</summary>
    [Fact]
    public void Molblock输入_可解析出特征()
    {
        // 苯的最小 molblock（V2000）
        const string molblock = @"
  Mrv1922 08202412002D

  6  6  0  0  0  0  0  0  0  0999 V2000
    0.0000    1.0000    0.0000 C   0  0  0  0  0  0  0  0  0  0  0  0
    0.8660    0.5000    0.0000 C   0  0  0  0  0  0  0  0  0  0  0  0
    0.8660   -0.5000    0.0000 C   0  0  0  0  0  0  0  0  0  0  0  0
    0.0000   -1.0000    0.0000 C   0  0  0  0  0  0  0  0  0  0  0  0
   -0.8660   -0.5000    0.0000 C   0  0  0  0  0  0  0  0  0  0  0  0
   -0.8660    0.5000    0.0000 C   0  0  0  0  0  0  0  0  0  0  0  0
  1  2  2  0  0  0  0
  2  3  1  0  0  0  0
  3  4  2  0  0  0  0
  4  5  1  0  0  0  0
  5  6  2  0  0  0  0
  6  1  1  0  0  0  0
M  END
";
        var r = MoleculeAnalyzer.Analyze(null, molblock);
        Assert.True(r.Success, r.FailReason);
        Assert.Equal("C6H6", r.MolecularFormula);
        Assert.Contains("苯环", r.FunctionalGroups);
        Assert.Equal(4, r.DegreeOfUnsaturation);
    }
}
