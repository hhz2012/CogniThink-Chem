namespace CogniThink.TutorBrain.Tests;

using CogniThink.Chem.Core.Rules;
using CogniThink.TutorBrain.Application.Chemistry;
using Xunit;

/// <summary>
/// 平衡态数值模拟器（第三主体类）的单元测试。
/// 常数通过构造函数注入构造的 AcidConstantsData，避免依赖文件系统与 RuleStore。
/// 铁律验证：缺常数 → Status=Underdetermined、Ph 为 null、有 Warning，绝不蒙数。
/// </summary>
public class EquilibriumStateSimulatorTests
{
    private static EquilibriumStateSimulator NewSim() => new(EquilibriumTestConstants.Make());

    [Fact]
    public void Compute_WeakAcidPh_0d1_CH3COOH_GivesExpectedValue()
    {
        var sim = NewSim();
        var input = new ChemicalSystemInput
        {
            Species = new() { new SpeciesInput { Substance = "CH3COOH", Concentration = 0.1 } }
        };
        var query = new EquilibriumQuery { Quantities = { EquilibriumQuantity.Ph } };

        var result = sim.Compute(input, query);

        Assert.Equal(EquilibriumDetermination.Determined, result.Status);
        Assert.NotNull(result.Ph);
        Assert.True(Math.Abs(result.Ph!.Value - 2.88) < 0.02,
            $"0.1 mol/L CH3COOH 期望 pH≈2.88（Ka=1.75e-5），实际 {result.Ph}");
        Assert.Contains("CH3COOH", result.EvidenceKeys);
        Assert.Contains(result.DerivationSteps, s => s.Contains("Ka"));
    }

    [Fact]
    public void Compute_WeakBasePh_0d1_NH3H2O_GivesExpectedValue()
    {
        var sim = NewSim();
        var input = new ChemicalSystemInput
        {
            Species = new() { new SpeciesInput { Substance = "NH3·H2O", Concentration = 0.1 } }
        };
        var query = new EquilibriumQuery { Quantities = { EquilibriumQuantity.Ph } };

        var result = sim.Compute(input, query);

        Assert.Equal(EquilibriumDetermination.Determined, result.Status);
        Assert.NotNull(result.Ph);
        Assert.True(Math.Abs(result.Ph!.Value - 11.12) < 0.03,
            $"0.1 mol/L NH3·H2O 期望 pH≈11.12（Kb=1.76e-5），实际 {result.Ph}");
        Assert.Contains("NH3·H2O", result.EvidenceKeys);
    }

    [Fact]
    public void Compute_MissingConstant_ReturnsUnderdetermined_NoFabrication()
    {
        var sim = NewSim(); // 注入常数不含 CH3CH2OH（既非 Ka/Kb 收录，也不在任何阶梯）
        var input = new ChemicalSystemInput
        {
            Species = new() { new SpeciesInput { Substance = "CH3CH2OH", Concentration = 0.1 } }
        };
        var query = new EquilibriumQuery { Quantities = { EquilibriumQuantity.Ph } };

        var result = sim.Compute(input, query);

        Assert.Equal(EquilibriumDetermination.Underdetermined, result.Status);
        Assert.Null(result.Ph); // 缺常数绝不蒙数
        Assert.NotEmpty(result.Warnings);
        Assert.DoesNotContain(result.EvidenceKeys, k => k.Contains("CH3CH2OH"));
    }

    [Fact]
    public void Compute_EmptyQuantities_ReturnsUnderdetermined()
    {
        var sim = NewSim();
        var input = new ChemicalSystemInput
        {
            Species = new() { new SpeciesInput { Substance = "CH3COOH", Concentration = 0.1 } }
        };
        var query = new EquilibriumQuery(); // 未指定任何 Quantities

        var result = sim.Compute(input, query);

        Assert.Equal(EquilibriumDetermination.Underdetermined, result.Status);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void Compute_TemperatureNot25_ReturnsUnderdetermined()
    {
        var sim = NewSim();
        var input = new ChemicalSystemInput
        {
            Species = new() { new SpeciesInput { Substance = "CH3COOH", Concentration = 0.1 } }
        };
        var query = new EquilibriumQuery
        {
            Quantities = { EquilibriumQuantity.Ph },
            Given = { ["temperature"] = 60 }
        };

        var result = sim.Compute(input, query);

        Assert.Equal(EquilibriumDetermination.Underdetermined, result.Status);
        Assert.Null(result.Ph);
        Assert.Contains(result.Warnings, w => w.Contains("60"));
    }

    [Fact]
    public void Compute_PrecipitationConversion_AgClToAgI_ReportsKAndProceeds()
    {
        var sim = NewSim();
        var input = new ChemicalSystemInput(); // 转化计算不依赖物种量
        var query = new EquilibriumQuery
        {
            Quantities = { EquilibriumQuantity.PrecipitationConversion },
            Conversion = new ConversionSpec { Original = "AgCl", AddedIon = "I^-", Target = "AgI" }
        };

        var result = sim.Compute(input, query);

        Assert.Equal(EquilibriumDetermination.Determined, result.Status);
        Assert.NotNull(result.ConversionConstantK);
        Assert.True(result.ConversionProceeds,
            "Ksp(AgCl)=1.8e-10 > Ksp(AgI)=8.3e-17，K>1，AgCl 可转化为 AgI");
        Assert.Contains("Ksp:AgCl", result.EvidenceKeys);
        Assert.Contains("Ksp:AgI", result.EvidenceKeys);
    }

    [Fact]
    public void Compute_GivenPh_TakesDirectly()
    {
        var sim = NewSim();
        var input = new ChemicalSystemInput
        {
            Species = new() { new SpeciesInput { Substance = "NaCl", Concentration = 0.1 } }
        };
        var query = new EquilibriumQuery
        {
            Quantities = { EquilibriumQuantity.Ph },
            Given = { ["pH"] = 4.32 }
        };

        var result = sim.Compute(input, query);

        Assert.Equal(EquilibriumDetermination.Determined, result.Status);
        Assert.Equal(4.32, result.Ph);
        Assert.Contains("题给:pH", result.EvidenceKeys);
    }
}

/// <summary>测试用注入常数（镜像 acid-constants.json 的真实数值子集），避免依赖文件系统。</summary>
public static class EquilibriumTestConstants
{
    public static AcidConstantsData Make() => new()
    {
        Version = "test",
        Source = "unit-test injected",
        Kw = new ConstantSpec { Mantissa = "1", Exponent = -14 },
        Ka = new Dictionary<string, ConstantSpec>(StringComparer.Ordinal)
        {
            ["CH3COOH"] = new() { Mantissa = "1.75", Exponent = -5 },
            ["H2CO3:Ka1"] = new() { Mantissa = "4.5", Exponent = -7 }
        },
        Kb = new Dictionary<string, ConstantSpec>(StringComparer.Ordinal)
        {
            ["NH3·H2O"] = new() { Mantissa = "1.76", Exponent = -5 }
        },
        Ksp = new Dictionary<string, ConstantSpec>(StringComparer.Ordinal)
        {
            ["AgCl"] = new() { Mantissa = "1.8", Exponent = -10 },
            ["AgI"] = new() { Mantissa = "8.3", Exponent = -17 },
            ["Mg(OH)2"] = new() { Mantissa = "5.6", Exponent = -12 }
        },
        AcidLadders = new Dictionary<string, List<string>>(StringComparer.Ordinal)
        {
            ["CH3COOH"] = new() { "CH3COOH", "CH3COO^-" }
        },
        BaseLadders = new Dictionary<string, List<string>>(StringComparer.Ordinal)
        {
            ["NH3·H2O"] = new() { "NH3·H2O", "NH4^+" }
        }
    };
}
