using CogniThink.Chem.Core;
using CogniThink.Chem.Core.Balancing;
using CogniThink.Chem.Core.Common;
using Xunit;

namespace CogniThink.Chem.Tests;

/// <summary>
/// 验收测试集（对应指令 6）：基础方程式、氧化还原难题、离子方程式易错题、
/// 有机结构判断、超纲干扰项。全部经由统一入口或对应引擎模块。
/// </summary>
public class AcceptanceTests
{
    // ---------- 1. 基础方程式 ----------

    [Theory]
    [InlineData("Fe + O2 = Fe3O4", "3Fe + 2O2 = Fe3O4")]
    [InlineData("Al + Fe2O3 = Al2O3 + Fe", "2Al + Fe2O3 = Al2O3 + 2Fe")]
    [InlineData("KClO3 = KCl + O2", "2KClO3 = 2KCl + 3O2")]
    public void BasicEquations_BalanceExactly(string input, string expected)
    {
        var result = EquationBalancer.Balance(input);
        Assert.True(result.Success, result.FailReason);
        Assert.Equal(expected, result.BalancedEquation);
    }

    // ---------- 2. 氧化还原难题 ----------

    [Fact]
    public void AcidicPermanganate_RedoxAnalysis()
    {
        // 酸性高锰酸钾滴定亚铁离子（经典难题）
        var report = ChemicalCheckerService.Check(new CheckRequest
        {
            Equation = "MnO4^- + 5Fe^2+ + 8H^+ = Mn^2+ + 5Fe^3+ + 4H2O"
        });

        Assert.True(report.IsValid, report.FailureSummary());
        Assert.Contains(report.CheckItems, i => i.CheckName == "氧化还原分析" && i.Detail.Contains("转移电子数 5"));
    }

    [Fact]
    public void BasicMediumRedox_Passes()
    {
        // 碱性环境：高锰酸根氧化亚硫酸根（介质 = basic）
        var report = ChemicalCheckerService.Check(new CheckRequest
        {
            Equation = "2MnO4^- + 3SO3^2- + H2O = 2MnO2↓ + 3SO4^2- + 2OH^-",
            Medium = "basic"
        });

        Assert.True(report.IsValid, report.FailureSummary());
    }

    [Fact]
    public void Disproportionation_ChlorineInAlkali()
    {
        // 歧化反应：氯气与氢氧化钠
        var report = ChemicalCheckerService.Check(new CheckRequest
        {
            Equation = "Cl2 + 2OH^- = Cl^- + ClO^- + H2O"
        });

        Assert.True(report.IsValid, report.FailureSummary());
    }

    // ---------- 3. 离子方程式易错题 ----------

    [Fact]
    public void WeakAcidMustNotSplit_AceticAcidWithCarbonate()
    {
        // 碳酸钠与醋酸：醋酸是弱酸必须保留化学式（正确写法）
        var report = ChemicalCheckerService.Check(new CheckRequest
        {
            Equation = "CO3^2- + 2CH3COOH = 2CH3COO^- + H2O + CO2↑"
        });

        Assert.True(report.IsValid, report.FailureSummary());
    }

    [Fact]
    public void IronCorrodesFerric_BalancedRedox()
    {
        // Fe + 2Fe^3+ = 3Fe^2+（归中类易错题）
        var report = ChemicalCheckerService.Check(new CheckRequest
        {
            Equation = "Fe + 2Fe^3+ = 3Fe^2+"
        });

        Assert.True(report.IsValid, report.FailureSummary());
    }

    [Fact]
    public void MissingArrowOrPrecipitate_Fails()
    {
        // 易错：漏标 ↑ 或 ↓
        var missingGas = ChemicalCheckerService.Check(new CheckRequest
        {
            Equation = "2H^+ + CO3^2- = H2O + CO2"
        });
        Assert.False(missingGas.IsValid);
        Assert.Contains(missingGas.CheckItems, i => i.CheckName == "状态符号" && !i.Passed);

        var missingPrecipitate = ChemicalCheckerService.Check(new CheckRequest
        {
            Equation = "Ag^+ + Cl^- = AgCl"
        });
        Assert.False(missingPrecipitate.IsValid);
        Assert.Contains(missingPrecipitate.CheckItems, i => i.CheckName == "状态符号" && !i.Passed);
    }

    // ---------- 4. 有机结构判断 ----------

    [Fact]
    public void Glucose_HydroxylAndAldehyde()
    {
        // 葡萄糖（课内糖类代表）：多羟基 + 醛基
        var report = ChemicalCheckerService.Check(new CheckRequest
        {
            Smiles = "OCC(O)C(O)C(O)C(O)C=O"
        });

        Assert.True(report.IsValid, report.FailureSummary());
        Assert.Contains(report.CheckItems, i => i.CheckName == "结构解析" && i.Detail.Contains("C6H12O6"));
        Assert.Contains(report.CheckItems, i => i.CheckName == "官能团识别" && i.Detail.Contains("羟基") && i.Detail.Contains("醛基"));
    }

    // ---------- 5. 超纲干扰项 ----------

    [Fact]
    public void UnknownSpecies_Equation_StillFormallyChecked()
    {
        // 陌生物质：标记超纲，但形式校验（守恒/配平）照常
        var report = ChemicalCheckerService.Check(new CheckRequest
        {
            Equation = "Xy + O2 = XyO2"
        });

        Assert.False(report.IsValid);
        Assert.Equal(RiskTags.OutOfScope, report.RiskTag);
        Assert.Contains(report.CheckItems, i => i.CheckName == "配平" && i.Passed);
    }

    [Fact]
    public void OutOfScopeFunctionalGroup_Sulfonamide()
    {
        // 磺酰胺类（含 S，超纲）
        var report = ChemicalCheckerService.Check(new CheckRequest
        {
            Smiles = "CS(=O)(=O)N"
        });

        Assert.False(report.IsValid);
        Assert.Equal(RiskTags.OutOfScope, report.RiskTag);
    }

    [Fact]
    public void ImpossibleEquation_NoSolution()
    {
        // 元素只出现在一侧：不可能配平
        var result = EquationBalancer.Balance("H2O = H2 + N2");
        Assert.False(result.Success);
        Assert.Equal(RiskTags.NoSolution, result.RiskTag);
    }
}
