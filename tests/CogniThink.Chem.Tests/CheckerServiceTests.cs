using CogniThink.Chem.Core.Common;
using CogniThink.Chem.Core;
using Xunit;

namespace CogniThink.Chem.Tests;

/// <summary>统一入口 ChemicalCheckerService 管线测试。</summary>
public class CheckerServiceTests
{
    [Fact]
    public void MolecularEquation_FullPipeline_Passes()
    {
        var report = ChemicalCheckerService.Check(new CheckRequest
        {
            Equation = "2H2 + O2 =点燃= 2H2O"
        });

        Assert.True(report.IsValid, report.FailureSummary());
        Assert.Contains(report.CheckItems, i => i.CheckName == "白名单校验" && i.Passed);
        Assert.Contains(report.CheckItems, i => i.CheckName == "配平" && i.Passed);
        Assert.Contains(report.CheckItems, i => i.CheckName == "氧化还原分析" && i.Passed);
    }

    [Fact]
    public void IonicEquation_AutoMode_RunsIonicRules()
    {
        var report = ChemicalCheckerService.Check(new CheckRequest
        {
            Equation = "Ba^2+ + SO4^2- = BaSO4↓"
        });

        Assert.True(report.IsValid, report.FailureSummary());
        Assert.Contains(report.CheckItems, i => i.CheckName == "强电解质拆写");
        Assert.Contains(report.CheckItems, i => i.CheckName == "状态符号");
        Assert.Contains(report.CheckItems, i => i.CheckName == "氧化还原分析" && i.Detail.Contains("非氧化还原"));
    }

    [Fact]
    public void MolecularMode_SkipsIonicRules()
    {
        var report = ChemicalCheckerService.Check(new CheckRequest
        {
            Equation = "Fe + Cu^2+ = Fe^2+ + Cu",
            Mode = EquationMode.Molecular
        });

        Assert.True(report.IsValid, report.FailureSummary());
        Assert.DoesNotContain(report.CheckItems, i => i.CheckName == "强电解质拆写");
    }

    [Fact]
    public void UnknownSpecies_FlaggedOutOfScope()
    {
        var report = ChemicalCheckerService.Check(new CheckRequest
        {
            Equation = "Xy + O2 = XyO2"
        });

        Assert.False(report.IsValid);
        Assert.Equal(RiskTags.OutOfScope, report.RiskTag);
        Assert.Contains(report.CheckItems, i => i.CheckName == "白名单校验" && !i.Passed);
        // 形式校验仍然执行：该式守恒且可配平
        Assert.Contains(report.CheckItems, i => i.CheckName == "元素与电荷守恒" && i.Passed);
    }

    [Fact]
    public void UnbalancedStudentEquation_Fails()
    {
        var report = ChemicalCheckerService.Check(new CheckRequest
        {
            Equation = "H2 + O2 = H2O"
        });

        Assert.False(report.IsValid);
        Assert.Contains(report.CheckItems, i => i.CheckName == "元素守恒" && !i.Passed);
        // 引擎能给出正确配平
        Assert.Contains(report.CheckItems, i => i.CheckName == "配平" && i.Detail.Contains("2H2"));
    }

    [Fact]
    public void StudentCoefficientErrors_CaughtByConservation()
    {
        // 学生系数错误（电荷不守恒：左 +3，右 +4）。
        // 氧化还原分析按引擎配平系数（2Fe^3+ + Cu）计算，电子守恒，
        // 系数错误由守恒检查项负责定位——两者职责不重叠。
        var report = ChemicalCheckerService.Check(new CheckRequest
        {
            Equation = "Fe^3+ + Cu = Fe^2+ + Cu^2+"
        });

        Assert.False(report.IsValid);
        Assert.Contains(report.CheckItems, i => i.CheckName == "电荷守恒" && !i.Passed && i.RiskTag == RiskTags.ConservationViolation);
        Assert.Contains(report.CheckItems, i => i.CheckName == "氧化还原分析" && i.Passed && i.Detail.Contains("转移电子数 2"));
    }

    [Fact]
    public void SelfRedoxAmbiguity_NotedForManualAnalysis()
    {
        // NH4NO3 自身氧化还原：氧化剂与还原剂同为一种物质，
        // 引擎不擅自拆解半反应，附注提示（电子守恒结论不受影响）。
        var report = ChemicalCheckerService.Check(new CheckRequest
        {
            Equation = "NH4NO3 = N2O + 2H2O"
        });

        Assert.Contains(report.CheckItems, i => i.CheckName == "氧化还原分析" && i.Passed && i.Detail.Contains("半反应推导失败"));
        // N2O 不在课内白名单，整体仍标记超纲
        Assert.False(report.IsValid);
        Assert.Equal(RiskTags.OutOfScope, report.RiskTag);
    }

    [Fact]
    public void InvalidMedium_FlaggedAsParseError()
    {
        var report = ChemicalCheckerService.Check(new CheckRequest
        {
            Equation = "MnO4^- + 5Fe^2+ + 8H^+ = Mn^2+ + 5Fe^3+ + 4H2O",
            Medium = "中性"
        });

        Assert.False(report.IsValid);
        Assert.Contains(report.CheckItems, i => i.CheckName == "氧化还原分析" && i.RiskTag == RiskTags.ParseError);
    }

    [Fact]
    public void SmilesCheckedByMoleculeModule()
    {
        var report = ChemicalCheckerService.Check(new CheckRequest { Smiles = "CCO" });
        Assert.True(report.IsValid, report.FailureSummary());
        Assert.Contains(report.CheckItems, i => i.CheckName == "官能团识别" && i.Detail.Contains("羟基"));
    }

    [Fact]
    public void SmilesOutOfScope_Flagged()
    {
        // 甲磺酸含磺酸基（超纲）
        var report = ChemicalCheckerService.Check(new CheckRequest { Smiles = "CS(=O)(=O)O" });
        Assert.False(report.IsValid);
        Assert.Equal(RiskTags.OutOfScope, report.RiskTag);
    }

    [Fact]
    public void EmptyInput_ReportsError()
    {
        var report = ChemicalCheckerService.Check(new CheckRequest());
        Assert.False(report.IsValid);
        Assert.NotEmpty(report.ErrorMessage);
    }

    [Fact]
    public void UnparsableEquation_ReportsParseError()
    {
        var report = ChemicalCheckerService.Check(new CheckRequest { Equation = "???" });
        Assert.False(report.IsValid);
        Assert.Equal(RiskTags.ParseError, report.RiskTag);
    }

    [Fact]
    public void HalfReaction_ElectronMinusNotation_PassesConservation()
    {
        var report = ChemicalCheckerService.Check(new CheckRequest
        {
            Equation = "Zn - 2e^- = Zn^2+",
            Mode = EquationMode.HalfReaction
        });

        Assert.True(report.IsValid, report.FailureSummary());
        Assert.Contains(report.CheckItems, i => i.CheckName == "元素与电荷守恒" && i.Passed);
        Assert.Contains(report.CheckItems, i => i.CheckName == "电子守恒" && i.Passed && i.Detail.Contains("失 2"));
        // 半反应模式跳过以完整反应为对象的检查项
        Assert.DoesNotContain(report.CheckItems, i => i.CheckName == "反应库一致性");
        Assert.DoesNotContain(report.CheckItems, i => i.CheckName == "强电解质拆写");
    }

    [Fact]
    public void HalfReaction_MissingElectron_Fails()
    {
        var report = ChemicalCheckerService.Check(new CheckRequest
        {
            Equation = "Cu^2+ = Cu",
            Mode = EquationMode.HalfReaction
        });

        Assert.False(report.IsValid);
        Assert.Contains(report.CheckItems, i => i.CheckName == "电子守恒" && !i.Passed);
    }

    [Fact]
    public void HalfReaction_ChargeNotBalanced_Fails()
    {
        var report = ChemicalCheckerService.Check(new CheckRequest
        {
            Equation = "Cu^2+ + e^- = Cu",
            Mode = EquationMode.HalfReaction
        });

        Assert.False(report.IsValid);
        Assert.Contains(report.CheckItems, i => i.CheckName == "电荷守恒" && !i.Passed);
    }

    [Fact]
    public void HalfReaction_ReductionMiswrittenWithMinusElectron_FailsCharge()
    {
        var report = ChemicalCheckerService.Check(new CheckRequest
        {
            Equation = "Cu^2+ - 2e^- = Cu",
            Mode = EquationMode.HalfReaction
        });

        // 归一化后等价于 Cu^2+ = Cu + 2e^-：左 +2、右 -2，电荷不守恒——
        // 正是"得电子却写成减电子"的高考典型错解，必须判错。
        Assert.False(report.IsValid);
        Assert.Contains(report.CheckItems, i => i.CheckName == "电荷守恒" && !i.Passed);
    }
}
