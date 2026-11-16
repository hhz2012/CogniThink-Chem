using CogniThink.Chem.Core.Common;
using CogniThink.Chem.Core.Ionic;
using Xunit;

namespace CogniThink.Chem.Tests;

/// <summary>
/// 离子方程式校验测试：覆盖正确书写、守恒错误、弱电解质误拆、
/// 状态符号易错题、微溶物语境与可逆平衡语境。
/// </summary>
public class IonicTests
{
    // ---------- 正确书写：应全部通过 ----------

    [Theory]
    [InlineData("Ba^2+ + SO4^2- = BaSO4↓")]
    [InlineData("CaCO3 + 2H^+ = Ca^2+ + H2O + CO2↑")]
    [InlineData("CH3COOH + OH^- = CH3COO^- + H2O")]
    [InlineData("Fe + Cu^2+ = Fe^2+ + Cu")]
    [InlineData("2Na + 2H2O = 2Na^+ + 2OH^- + H2↑")]
    [InlineData("Ag^+ + Cl^- = AgCl↓")]
    [InlineData("Al^3+ + 3NH3·H2O = Al(OH)3↓ + 3NH4^+")]
    [InlineData("Cl2 + 2OH^- = Cl^- + ClO^- + H2O")]
    [InlineData("MnO4^- + 5Fe^2+ + 8H^+ = Mn^2+ + 5Fe^3+ + 4H2O")]
    public void CorrectIonicEquations_Pass(string equation)
    {
        var report = IonicEquationChecker.Check(equation);
        Assert.True(report.IsValid, report.FailureSummary());
    }

    // ---------- 守恒错误 ----------

    [Fact]
    public void ChargeNotConserved_Fails()
    {
        // 铁离子腐蚀铜板：学生漏写电荷守恒（左 +3，右 +4）
        var report = IonicEquationChecker.Check("Fe^3+ + Cu = Fe^2+ + Cu^2+");
        Assert.False(report.IsValid);
        Assert.Contains(report.CheckItems, i => !i.Passed && i.CheckName == "电荷守恒" && i.RiskTag == RiskTags.ConservationViolation);
    }

    [Fact]
    public void ElementNotConserved_Fails()
    {
        // 钠与水反应未配平（H：左 2，右 3）
        var report = IonicEquationChecker.Check("Na + H2O = Na^+ + OH^- + H2↑");
        Assert.False(report.IsValid);
        Assert.Contains(report.CheckItems, i => !i.Passed && i.CheckName == "元素守恒");
    }

    // ---------- 离子共存 / 弱电解质拆写 ----------

    [Fact]
    public void AcidBasePairWithoutWater_Fails()
    {
        // H^+ 与 OH^- 同在反应物一侧却未生成 H2O
        var report = IonicEquationChecker.Check("H^+ + OH^- = H2");
        Assert.False(report.IsValid);
        Assert.Contains(report.CheckItems, i => !i.Passed && i.CheckName == "离子共存" && i.RiskTag == RiskTags.IonicNotationError);
    }

    [Fact]
    public void WeakAcidRadicalWithProton_NoConjugateProduct_Fails()
    {
        // CO3^2- 与 H^+ 反应但生成物中无 H2CO3/HCO3^-/CO2
        var report = IonicEquationChecker.Check("CO3^2- + 2H^+ = O2 + H2O");
        Assert.False(report.IsValid);
        Assert.Contains(report.CheckItems, i => !i.Passed && i.CheckName == "离子共存");
    }

    [Fact]
    public void ProductsContainingProtonAndWeakAcidRadical_Fails()
    {
        // 水的电离写成不可逆形式：H^+ 与 OH^- 不应同时作为生成物
        var report = IonicEquationChecker.Check("H2O = H^+ + OH^-");
        Assert.False(report.IsValid);
        Assert.Contains(report.CheckItems, i => !i.Passed && i.CheckName == "离子共存");
    }

    [Fact]
    public void ReversibleIonizationOfWater_Passes()
    {
        // 可逆号语境允许共轭对共存
        var report = IonicEquationChecker.Check("H2O ⇌ H^+ + OH^-");
        Assert.True(report.IsValid, report.FailureSummary());
    }

    [Fact]
    public void ReversibleWeakAcidIonization_Passes()
    {
        var report = IonicEquationChecker.Check("CH3COOH ⇌ CH3COO^- + H^+");
        Assert.True(report.IsValid, report.FailureSummary());
    }

    [Fact]
    public void StrongAcidNotSplit_Fails()
    {
        // 盐酸与氢氧化钠：HCl 是强酸应拆
        var report = IonicEquationChecker.Check("HCl + OH^- = Cl^- + H2O");
        Assert.False(report.IsValid);
        Assert.Contains(report.CheckItems, i => !i.Passed && i.CheckName == "强电解质拆写" && i.Detail.Contains("HCl"));
    }

    [Fact]
    public void SolubleSaltNotSplit_Fails()
    {
        // 生成物 NaCl 可溶，不应以分子形式保留
        var report = IonicEquationChecker.Check("Ba^2+ + SO4^2- + 2Na^+ + 2Cl^- = BaSO4↓ + 2NaCl");
        Assert.False(report.IsValid);
        Assert.Contains(report.CheckItems, i => !i.Passed && i.CheckName == "强电解质拆写" && i.Detail.Contains("NaCl"));
    }

    // ---------- 状态符号 ----------

    [Fact]
    public void MissingPrecipitateSymbol_Fails()
    {
        var report = IonicEquationChecker.Check("Ba^2+ + SO4^2- = BaSO4");
        Assert.False(report.IsValid);
        Assert.Contains(report.CheckItems, i => !i.Passed && i.CheckName == "状态符号" && i.RiskTag == RiskTags.StateSymbolError);
    }

    [Fact]
    public void MissingGasSymbol_Fails()
    {
        var report = IonicEquationChecker.Check("CaCO3 + 2H^+ = Ca^2+ + H2O + CO2");
        Assert.False(report.IsValid);
        Assert.Contains(report.CheckItems, i => !i.Passed && i.CheckName == "状态符号" && i.Detail.Contains("CO2"));
    }

    [Fact]
    public void SolubleProductMarkedDown_Fails()
    {
        // NaCl 可溶却标 ↓
        var report = IonicEquationChecker.Check("Na^+ + Cl^- = NaCl↓");
        Assert.False(report.IsValid);
        Assert.Contains(report.CheckItems, i => !i.Passed && i.CheckName == "状态符号" && i.Detail.Contains("NaCl"));
    }

    [Fact]
    public void NonGasMarkedUp_Fails()
    {
        var report = IonicEquationChecker.Check("Ag^+ + Cl^- = AgCl↑");
        Assert.False(report.IsValid);
        Assert.Contains(report.CheckItems, i => !i.Passed && i.CheckName == "状态符号" && i.Detail.Contains("不是气体"));
    }

    [Fact]
    public void StateSymbolOnReactant_Fails()
    {
        var report = IonicEquationChecker.Check("Fe↓ + Cu^2+ = Fe^2+ + Cu");
        Assert.False(report.IsValid);
        Assert.Contains(report.CheckItems, i => !i.Passed && i.CheckName == "状态符号" && i.Detail.Contains("反应物"));
    }

    // ---------- 微溶物与语境：只提示不判错 ----------

    [Fact]
    public void SlightlySolublePrecipitate_PassesWithNote()
    {
        // Ca(OH)2 微溶，标 ↓ 不判错
        var report = IonicEquationChecker.Check("Ca^2+ + 2OH^- = Ca(OH)2↓");
        Assert.True(report.IsValid, report.FailureSummary());
        Assert.Contains(report.CheckItems, i => i.CheckName == "状态符号" && i.Detail.Contains("微溶"));
    }

    [Fact]
    public void SlightlySolubleBaseNotSplit_PassesWithNote()
    {
        // Ca(OH)2 微溶，以分子形式出现不判错（石灰乳语境）
        var report = IonicEquationChecker.Check("Ca(OH)2 + 2H^+ = Ca^2+ + 2H2O");
        Assert.True(report.IsValid, report.FailureSummary());
        Assert.Contains(report.CheckItems, i => i.CheckName == "强电解质拆写" && i.Detail.Contains("微溶"));
    }

    [Fact]
    public void ReversibleHydrolysis_MissingDown_PassesWithNote()
    {
        // 水解平衡语境：Fe(OH)3 不标 ↓ 属教材写法，只提示
        var report = IonicEquationChecker.Check("Fe^3+ + 3H2O ⇌ Fe(OH)3 + 3H^+");
        Assert.True(report.IsValid, report.FailureSummary());
        Assert.Contains(report.CheckItems, i => i.CheckName == "状态符号" && i.Detail.Contains("仅提示"));
    }

    // ---------- 非离子方程式：按化学方程式处理 ----------

    [Fact]
    public void MolecularEquation_SkipsIonicChecks()
    {
        var report = IonicEquationChecker.Check("2H2 + O2 =点燃= 2H2O");
        Assert.True(report.IsValid, report.FailureSummary());
        Assert.Contains(report.CheckItems, i => i.Detail.Contains("按化学方程式处理"));
    }

    // ---------- 解析失败 ----------

    [Fact]
    public void UnparsableInput_ReportsParseError()
    {
        var report = IonicEquationChecker.Check("???");
        Assert.False(report.IsValid);
        Assert.NotEmpty(report.ErrorMessage);
        Assert.Equal(RiskTags.ParseError, report.RiskTag);
    }
}
