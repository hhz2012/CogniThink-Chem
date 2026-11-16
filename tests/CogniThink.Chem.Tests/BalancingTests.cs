using CogniThink.Chem.Core.Balancing;
using CogniThink.Chem.Core.Common;
using CogniThink.Chem.Core.Parsing;
using Xunit;

namespace CogniThink.Chem.Tests;

/// <summary>配平内核与氧化还原分析验收测试（有理数精确求解，无浮点）。</summary>
public class BalancingTests
{
    [Theory]
    [InlineData("Fe + O2 = Fe3O4", "3Fe + 2O2 = Fe3O4")]
    [InlineData("KClO3 = KCl + O2", "2KClO3 = 2KCl + 3O2")]
    [InlineData("H2 + O2 = H2O", "2H2 + O2 = 2H2O")]
    [InlineData("Al + Fe3O4 = Al2O3 + Fe", "8Al + 3Fe3O4 = 4Al2O3 + 9Fe")]
    [InlineData("Na + H2O = NaOH + H2", "2Na + 2H2O = 2NaOH + H2")]
    [InlineData("CuSO4·5H2O = CuSO4 + H2O", "CuSO4·5H2O = CuSO4 + 5H2O")]
    public void Balance_OrdinaryEquations(string input, string expected)
    {
        var result = EquationBalancer.Balance(input);
        Assert.True(result.Success, result.FailReason);
        Assert.Equal(expected, result.BalancedEquation);
    }

    [Fact]
    public void Balance_PreservesConditionAndStateSymbols()
    {
        var result = EquationBalancer.Balance("CaCO3 =高温= CaO + CO2↑");
        Assert.True(result.Success, result.FailReason);
        Assert.Equal("CaCO3 =高温= CaO + CO2↑", result.BalancedEquation);
    }

    [Fact]
    public void Balance_ComplexRedox_IonicForm()
    {
        // 酸性高锰酸钾氧化亚铁离子
        var result = EquationBalancer.Balance("MnO4^- + Fe^2+ + H+ = Mn^2+ + Fe^3+ + H2O");
        Assert.True(result.Success, result.FailReason);
        Assert.Equal("MnO4^- + 5Fe^2+ + 8H^+ = Mn^2+ + 5Fe^3+ + 4H2O", result.BalancedEquation);
    }

    [Fact]
    public void Balance_KMnO4_ConcentratedHCl()
    {
        var result = EquationBalancer.Balance("KMnO4 + HCl = KCl + MnCl2 + Cl2 + H2O");
        Assert.True(result.Success, result.FailReason);
        Assert.Equal("2KMnO4 + 16HCl = 2KCl + 2MnCl2 + 5Cl2 + 8H2O", result.BalancedEquation);
    }

    [Fact]
    public void Balance_Cu_DiluteHNO3()
    {
        var result = EquationBalancer.Balance("Cu + HNO3 = Cu(NO3)2 + NO + H2O");
        Assert.True(result.Success, result.FailReason);
        Assert.Equal("3Cu + 8HNO3 = 3Cu(NO3)2 + 2NO + 4H2O", result.BalancedEquation);
    }

    [Fact]
    public void Balance_ImpossibleEquation_ReportsNoSolution()
    {
        // 生成物多出反应物没有的元素 → 无解，绝不编造
        var result = EquationBalancer.Balance("H2O = H2 + N2");
        Assert.False(result.Success);
        Assert.Equal(RiskTags.NoSolution, result.RiskTag);
    }

    [Fact]
    public void Balance_Underdetermined_ReportsUnderdetermined()
    {
        // 两个互不相关的反应拼在一起 → 零空间维数 2
        var result = EquationBalancer.Balance("H2 + O2 + C + O2 = H2O + CO2");
        Assert.False(result.Success);
        Assert.Equal(RiskTags.Underdetermined, result.RiskTag);
    }

    [Fact]
    public void ConservationViolations_DetectsStudentMistakes()
    {
        var equation = EquationParser.Parse("2Fe + O2 = Fe3O4");
        var violations = EquationBalancer.ConservationViolations(equation);
        Assert.Contains(violations, v => v.StartsWith("元素 Fe 不守恒"));
    }

    [Fact]
    public void ConservationViolations_ChargeImbalance()
    {
        var equation = EquationParser.Parse("Fe^3+ + Cu = Fe^2+ + Cu^2+");
        var violations = EquationBalancer.ConservationViolations(equation);
        Assert.Contains(violations, v => v.StartsWith("电荷不守恒"));
    }

    [Fact]
    public void Redox_Analysis_FeO2_ElectronCount()
    {
        var analysis = RedoxBalancer.SplitHalfReactions("Fe + O2 = Fe3O4");
        Assert.True(analysis.Success, analysis.FailReason);
        Assert.True(analysis.IsRedox);
        Assert.True(analysis.ElectronConservation);
        Assert.Equal(8, analysis.ElectronsTransferred.Num); // 3Fe 失 8e-
    }

    [Fact]
    public void Redox_NonRedox_Detected()
    {
        var analysis = RedoxBalancer.SplitHalfReactions("CaCO3 = CaO + CO2");
        Assert.True(analysis.Success);
        Assert.False(analysis.IsRedox);
    }

    [Fact]
    public void Redox_HalfReactions_AcidicMedium()
    {
        var analysis = RedoxBalancer.SplitHalfReactions(
            "MnO4^- + Fe^2+ + H+ = Mn^2+ + Fe^3+ + H2O", "acidic");
        Assert.True(analysis.Success, analysis.FailReason);
        Assert.NotNull(analysis.OxidationHalf);
        Assert.NotNull(analysis.ReductionHalf);
        Assert.Equal("Fe^2+ = Fe^3+ + e^-", analysis.OxidationHalf!.Expression);
        Assert.Equal("MnO4^- + 8H^+ + 5e^- = Mn^2+ + 4H2O", analysis.ReductionHalf!.Expression);
        Assert.Equal(5, analysis.ElectronsTransferred.Num);
    }

    [Fact]
    public void Redox_HalfReactions_BasicMedium_Disproportionation()
    {
        // Cl2 在碱液中歧化
        var analysis = RedoxBalancer.SplitHalfReactions(
            "Cl2 + OH^- = Cl^- + ClO^- + H2O", "basic");
        Assert.True(analysis.Success, analysis.FailReason);
        Assert.NotNull(analysis.OxidationHalf);
        Assert.NotNull(analysis.ReductionHalf);
        Assert.Equal(1, analysis.ElectronsTransferred.Num);
    }

    [Fact]
    public void Redox_MolecularSpectator_HonestFailure()
    {
        // Cu(NO3)2 含旁观 NO3^-：半反应推导应如实失败并提示改离子形式
        var analysis = RedoxBalancer.SplitHalfReactions("Cu + HNO3 = Cu(NO3)2 + NO + H2O");
        Assert.True(analysis.Success);
        Assert.True(analysis.IsRedox);
        Assert.True(analysis.ElectronConservation);
        Assert.Equal(6, analysis.ElectronsTransferred.Num); // 3Cu 失 6e-
        Assert.True(analysis.OxidationHalf is null || analysis.ReductionHalf is null);
    }

    [Fact]
    public void Redox_FractionalOxidationState_Fe3O4()
    {
        var assignment = OxidationStateAnalyzer.Assign(
            CogniThink.Chem.Core.Chemistry.Species.Parse("Fe3O4"));
        Assert.True(assignment.Success);
        var states = assignment.States["Fe"];
        Assert.Single(states);
        Assert.Equal(new CogniThink.Chem.Core.Math.Rational(8, 3), states[0].Value);
    }

    [Fact]
    public void Redox_SpecialSpecies_NH4NO3()
    {
        var assignment = OxidationStateAnalyzer.Assign(
            CogniThink.Chem.Core.Chemistry.Species.Parse("NH4NO3"));
        Assert.True(assignment.Success);
        Assert.Equal(2, assignment.States["N"].Count); // -3 与 +5
    }
}
