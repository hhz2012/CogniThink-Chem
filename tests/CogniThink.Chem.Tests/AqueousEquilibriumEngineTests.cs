using CogniThink.Chem.Core.Benchmark;
using CogniThink.Chem.Core.Inference;
using Xunit;

namespace CogniThink.Chem.Tests;

/// <summary>
/// 水溶液离子平衡引擎（AqueousEquilibriumEngine）正式测试：粒子浓度排序逐字比对、
/// 三大守恒精确生成、Kh 与 Ka 有理数比较定酸碱性、全粒子排序不可判时如实欠定、
/// 沉淀转化 Ksp 精确求商、步骤可溯源（Description 非空、带 KnowledgeId 的步骤必有 Source）。
/// </summary>
public class AqueousEquilibriumEngineTests
{
    [Fact]
    public void H01_Ch3COONa_DerivesAlkalineOrderAndThreeConservations()
    {
        var result = Infer(Solute("CH3COONa"), ["Na^+", "CH3COO^-", "OH^-", "H^+"]);
        Assert.Equal(InferenceStatus.Solved, result.Status);
        Assert.Equal(
            ["c(Na^+)", "c(CH3COO^-)", "c(OH^-)", "c(H^+)"],
            result.Order);
        Assert.Equal(
            [
                "c(Na^+) + c(H^+) = c(CH3COO^-) + c(OH^-)",
                "c(Na^+) = c(CH3COOH) + c(CH3COO^-)",
                "c(OH^-) = c(H^+) + c(CH3COOH)",
            ],
            result.ConservationEquations);
        Assert.Equal(
            ["CH3COO^- + H2O ⇌ CH3COOH + OH^-", "H2O ⇌ H^+ + OH^-"],
            result.Equations);
        // Kh = Kw/Ka = 1×10^-14/1.75×10^-5 = 1/1750000000，精确有理数比较定碱性。
        Assert.Contains("1/1750000000", result.Steps[3].Description);
        Assert.Contains("显碱性", result.QuantitativeConclusion);
    }

    [Fact]
    public void H02_NH4Cl_DerivesAcidicOrderAndThreeConservations()
    {
        var result = Infer(Solute("NH4Cl"), ["Cl^-", "NH4^+", "H^+", "OH^-"]);
        Assert.Equal(InferenceStatus.Solved, result.Status);
        Assert.Equal(
            ["c(Cl^-)", "c(NH4^+)", "c(H^+)", "c(OH^-)"],
            result.Order);
        Assert.Equal(
            [
                "c(NH4^+) + c(H^+) = c(Cl^-) + c(OH^-)",
                "c(Cl^-) = c(NH3·H2O) + c(NH4^+)",
                "c(H^+) = c(OH^-) + c(NH3·H2O)",
            ],
            result.ConservationEquations);
        Assert.Equal(
            ["NH4^+ + H2O ⇌ NH3·H2O + H^+", "H2O ⇌ H^+ + OH^-"],
            result.Equations);
        Assert.Contains("显酸性", result.QuantitativeConclusion);
    }

    [Fact]
    public void H03_Na2CO3_DerivesStepwiseHydrolysisOrder()
    {
        var result = Infer(Solute("Na2CO3"), ["Na^+", "CO3^2-", "OH^-", "HCO3^-", "H^+"]);
        Assert.Equal(InferenceStatus.Solved, result.Status);
        // Na^+ 式量浓度 0.2 为 CO3^2- 的 2 倍，分步水解两步方程齐备。
        Assert.Equal(
            ["c(Na^+)", "c(CO3^2-)", "c(OH^-)", "c(HCO3^-)", "c(H^+)"],
            result.Order);
        Assert.Equal(
            [
                "c(Na^+) + c(H^+) = 2c(CO3^2-) + c(HCO3^-) + c(OH^-)",
                "c(Na^+) = 2[c(H2CO3) + c(HCO3^-) + c(CO3^2-)]",
                "c(OH^-) = c(H^+) + c(HCO3^-) + 2c(H2CO3)",
            ],
            result.ConservationEquations);
        Assert.Equal(
            [
                "CO3^2- + H2O ⇌ HCO3^- + OH^-",
                "HCO3^- + H2O ⇌ H2CO3 + OH^-",
                "H2O ⇌ H^+ + OH^-",
            ],
            result.Equations);
    }

    [Fact]
    public void H04_NaHCO3_FullOrderIsUnderdeterminedNotGuessed()
    {
        var result = Infer(
            Solute("NaHCO3"), ["Na^+", "HCO3^-", "CO3^2-", "H2CO3", "OH^-", "H^+"]);
        // c(CO3^2-) 为电离产物，与 c(OH^-) 之比需解联立平衡方程（含开方），定性判据不可判，如实欠定。
        Assert.Equal(InferenceStatus.Underdetermined, result.Status);
        Assert.Empty(result.Order);
        Assert.Contains("如实欠定", result.Message);
        // 守恒与弱过程方程仍完整给出（Kh=1/45000000 > Ka2，水解大于电离显碱性）。
        Assert.Equal(
            [
                "c(Na^+) + c(H^+) = 2c(CO3^2-) + c(HCO3^-) + c(OH^-)",
                "c(Na^+) = c(H2CO3) + c(HCO3^-) + c(CO3^2-)",
                "c(H^+) + c(H2CO3) = c(OH^-) + c(CO3^2-)",
            ],
            result.ConservationEquations);
        Assert.Equal(
            [
                "HCO3^- ⇌ H^+ + CO3^2-",
                "HCO3^- + H2O ⇌ H2CO3 + OH^-",
                "H2O ⇌ H^+ + OH^-",
            ],
            result.Equations);
    }

    [Fact]
    public void H05_AcetateBuffer_IonizationWinsOverHydrolysis()
    {
        var result = Infer(
            [Solute("CH3COOH"), Solute("CH3COONa")],
            ["CH3COO^-", "Na^+", "CH3COOH", "H^+", "OH^-"]);
        Assert.Equal(InferenceStatus.Solved, result.Status);
        // Ka(CH3COOH) = 1.75×10^-5 > Kh = 1/1750000000：电离大于水解，显酸性，c(CH3COO^-) > c(Na^+)。
        Assert.Equal(
            ["c(CH3COO^-)", "c(Na^+)", "c(CH3COOH)", "c(H^+)", "c(OH^-)"],
            result.Order);
        Assert.Equal(
            [
                "c(Na^+) + c(H^+) = c(CH3COO^-) + c(OH^-)",
                "[c(CH3COOH) + c(CH3COO^-)] = 2c(Na^+)",
            ],
            result.ConservationEquations);
        Assert.Contains("电离大于水解", result.Steps[5].Description);
        Assert.Contains("显酸性", result.QuantitativeConclusion);
    }

    [Fact]
    public void H06_TitrationToPh7_ChargeConservationFixesEqualPairs()
    {
        var result = Infer(
            [Solute("CH3COOH", "0.1", "0.02"), Solute("NaOH", "0.1", "0.02")],
            ["Na^+", "CH3COO^-", "H^+", "OH^-"],
            ph: "7");
        Assert.Equal(InferenceStatus.Solved, result.Status);
        // 题给中性：电荷守恒直接给出两组等号；组成不定故物料/质子守恒不出。
        Assert.Equal(
            ["c(Na^+) = c(CH3COO^-)", "c(H^+) = c(OH^-)"],
            result.Order);
        Assert.Equal(
            ["c(Na^+) + c(H^+) = c(CH3COO^-) + c(OH^-)"],
            result.ConservationEquations);
        Assert.Contains("显中性", result.QuantitativeConclusion);
    }

    [Fact]
    public void H07_Na2C2O4_DerivesOxalateStepwiseHydrolysis()
    {
        var result = Infer(Solute("Na2C2O4"), ["Na^+", "C2O4^2-", "OH^-", "HC2O4^-", "H^+"]);
        Assert.Equal(InferenceStatus.Solved, result.Status);
        Assert.Equal(
            ["c(Na^+)", "c(C2O4^2-)", "c(OH^-)", "c(HC2O4^-)", "c(H^+)"],
            result.Order);
        Assert.Equal(
            [
                "c(Na^+) + c(H^+) = 2c(C2O4^2-) + c(HC2O4^-) + c(OH^-)",
                "c(Na^+) = 2[c(H2C2O4) + c(HC2O4^-) + c(C2O4^2-)]",
                "c(OH^-) = c(H^+) + c(HC2O4^-) + 2c(H2C2O4)",
            ],
            result.ConservationEquations);
        Assert.Equal(
            [
                "C2O4^2- + H2O ⇌ HC2O4^- + OH^-",
                "HC2O4^- + H2O ⇌ H2C2O4 + OH^-",
                "H2O ⇌ H^+ + OH^-",
            ],
            result.Equations);
        // Kh = Kw/Ka2(H2C2O4) = 1×10^-14/5.4×10^-5 = 1/5400000000。
        Assert.Contains("1/5400000000", result.Steps[4].Description);
    }

    [Fact]
    public void H08_AgClToAgI_ConversionConstantExactRatio()
    {
        var result = AqueousEquilibriumEngine.Infer(new AqueousRequest
        {
            Conversion = new AqueousConversionSpec
            {
                Original = "AgCl",
                AddedIon = "I^-",
                Target = "AgI",
            },
        });
        Assert.Equal(InferenceStatus.Solved, result.Status);
        Assert.Equal(["AgCl + I^- = AgI↓ + Cl^-"], result.Equations);
        // K = Ksp(AgCl)/Ksp(AgI) = 1.8×10^-10/8.3×10^-17 = 180000000/83 > 1（精确有理数，无浮点）。
        Assert.Contains("180000000/83", result.QuantitativeConclusion);
        Assert.Contains("可转化为更难溶", result.QuantitativeConclusion);
        Assert.Equal(
            ["Ksp(AgCl) = c(Ag^+)·c(Cl^-)", "Ksp(AgI) = c(Ag^+)·c(I^-)"],
            result.ConservationEquations);
    }

    [Fact]
    public void SolvedCases_HaveAtLeastFourTraceableSteps()
    {
        var cases = new[]
        {
            Infer(Solute("CH3COONa"), ["Na^+", "CH3COO^-", "OH^-", "H^+"]),
            Infer(Solute("NH4Cl"), ["Cl^-", "NH4^+", "H^+", "OH^-"]),
            Infer(Solute("Na2CO3"), ["Na^+", "CO3^2-", "OH^-", "HCO3^-", "H^+"]),
            Infer(
                [Solute("CH3COOH"), Solute("CH3COONa")],
                ["CH3COO^-", "Na^+", "CH3COOH", "H^+", "OH^-"]),
            Infer(
                [Solute("CH3COOH", "0.1", "0.02"), Solute("NaOH", "0.1", "0.02")],
                ["Na^+", "CH3COO^-", "H^+", "OH^-"],
                ph: "7"),
            Infer(Solute("Na2C2O4"), ["Na^+", "C2O4^2-", "OH^-", "HC2O4^-", "H^+"]),
            AqueousEquilibriumEngine.Infer(new AqueousRequest
            {
                Conversion = new AqueousConversionSpec
                {
                    Original = "AgCl",
                    AddedIon = "I^-",
                    Target = "AgI",
                },
            }),
        };

        foreach (var result in cases)
        {
            Assert.Equal(InferenceStatus.Solved, result.Status);
            Assert.True(result.Steps.Count >= 4, $"步骤数 {result.Steps.Count} < 4");
            Assert.All(result.Steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Description)));
            Assert.All(
                result.Steps.Where(s => !string.IsNullOrWhiteSpace(s.KnowledgeId)),
                s => Assert.False(string.IsNullOrWhiteSpace(s.Source)));
        }
    }

    private static SoluteSpec Solute(string species, string concentration = "0.1", string volumeLiters = "1") =>
        new() { Species = species, Concentration = concentration, VolumeLiters = volumeLiters };

    private static AqueousResult Infer(
        SoluteSpec single, List<string> orderSpecies, string ph = "") => Infer([single], orderSpecies, ph);

    private static AqueousResult Infer(
        List<SoluteSpec> solutes, List<string> orderSpecies, string ph = "") =>
        AqueousEquilibriumEngine.Infer(new AqueousRequest
        {
            Solutes = solutes,
            OrderSpecies = orderSpecies,
            Ph = ph,
        });
}
