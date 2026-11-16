using CogniThink.Chem.Core.Inference;
using Xunit;

namespace CogniThink.Chem.Tests;

/// <summary>
/// 电化学引擎（ElectrochemEngine）正式测试：电极反应式逐字比对、
/// 熔融体系边界如实欠定、步骤可溯源（Description 非空、带 KnowledgeId 的步骤必有 Source）。
/// </summary>
public class ElectrochemEngineTests
{
    [Fact]
    public void H01_ZnCopperGalvanic_DerivesExactElectrodeEquations()
    {
        var result = Infer("galvanic", new[] { "Zn", "Cu" }, "CuSO4", "acidic", "Zn + Cu^2+ = Zn^2+ + Cu");
        Assert.Equal(InferenceStatus.Solved, result.Status);
        Assert.Equal("Zn = Zn^2+ + 2e^-", result.Electrodes!.Negative);
        Assert.Equal("Cu^2+ + 2e^- = Cu", result.Electrodes.Positive);
        Assert.Null(result.Electrodes.Anode);
        Assert.Null(result.Electrodes.Cathode);
        // 活动性核对步骤存在（Zn 第 6 位 < Cu 第 11 位）。
        Assert.Contains(result.Steps, s => s.Description.Contains("活动性核对") && s.Evidence.Contains("Zn#6"));
    }

    [Fact]
    public void H02_HydrogenOxygenFuelCellAcidic_SubtractionDerivesOxidationHalf()
    {
        var result = Infer("galvanic", new[] { "Pt", "Pt" }, "H2SO4", "acidic", "2H2 + O2 = 2H2O");
        Assert.Equal(InferenceStatus.Solved, result.Status);
        Assert.Equal("2H2 = 4H^+ + 4e^-", result.Electrodes!.Negative);
        Assert.Equal("O2 + 4H^+ + 4e^- = 2H2O", result.Electrodes.Positive);
        Assert.Contains(result.Steps, s => s.Description.Contains("由电子守恒经减法推得"));
    }

    [Fact]
    public void H03_HydrogenOxygenFuelCellBasic_SubtractionDerivesOxidationHalf()
    {
        var result = Infer("galvanic", new[] { "Pt", "Pt" }, "KOH", "basic", "2H2 + O2 = 2H2O");
        Assert.Equal(InferenceStatus.Solved, result.Status);
        Assert.Equal("2H2 + 4OH^- = 4H2O + 4e^-", result.Electrodes!.Negative);
        Assert.Equal("O2 + 2H2O + 4e^- = 4OH^-", result.Electrodes.Positive);
        Assert.Contains(result.Steps, s => s.Description.Contains("由电子守恒经减法推得"));
    }

    [Fact]
    public void H05_ChlorAlkaliBasic_SubtractionDerivesReductionHalf()
    {
        var result = Infer("electrolytic", new[] { "C", "Fe" }, "NaCl", "basic", "2Cl^- + 2H2O = Cl2↑ + H2↑ + 2OH^-");
        Assert.Equal(InferenceStatus.Solved, result.Status);
        Assert.Equal("2Cl^- = Cl2 + 2e^-", result.Electrodes!.Anode);
        Assert.Equal("2H2O + 2e^- = H2 + 2OH^-", result.Electrodes.Cathode);
        Assert.Null(result.Electrodes.Negative);
        Assert.Contains(result.Steps, s => s.Description.Contains("由电子守恒经减法推得"));
    }

    [Fact]
    public void H06_MoltenSaltMedium_ReturnsUnderdetermined()
    {
        var result = Infer("electrolytic", new[] { "C", "C" }, "Al2O3", "", "");
        Assert.Equal(InferenceStatus.Underdetermined, result.Status);
        Assert.Null(result.Electrodes);
        Assert.Contains("熔融体系不适用水溶液离子-电子法机械拆解", result.Message);
        // 装置判定步骤保留（结论仍可溯源）。
        Assert.Single(result.Steps);
        Assert.Equal("RED-004", result.Steps[0].KnowledgeId);
    }

    [Fact]
    public void UnknownDeviceType_ReturnsNoSolution()
    {
        var result = Infer("solar", new[] { "Zn", "Cu" }, "CuSO4", "acidic", "Zn + Cu^2+ = Zn^2+ + Cu");
        Assert.Equal(InferenceStatus.NoSolution, result.Status);
        Assert.Empty(result.Steps);
    }

    [Fact]
    public void SolvedCases_HaveAtLeastFourTraceableSteps()
    {
        var cases = new[]
        {
            Infer("galvanic", new[] { "Zn", "Cu" }, "CuSO4", "acidic", "Zn + Cu^2+ = Zn^2+ + Cu"),
            Infer("galvanic", new[] { "Pt", "Pt" }, "H2SO4", "acidic", "2H2 + O2 = 2H2O"),
            Infer("galvanic", new[] { "Pt", "Pt" }, "KOH", "basic", "2H2 + O2 = 2H2O"),
            Infer("electrolytic", new[] { "C", "C" }, "CuCl2", "acidic", "Cu^2+ + 2Cl^- = Cu + Cl2↑"),
            Infer("electrolytic", new[] { "C", "Fe" }, "NaCl", "basic", "2Cl^- + 2H2O = Cl2↑ + H2↑ + 2OH^-"),
            Infer("galvanic", new[] { "Fe", "C" }, "NaCl", "basic", "2Fe + O2 + 2H2O = 2Fe(OH)2"),
            Infer("electrolytic", new[] { "C", "C" }, "AgNO3", "acidic", "4Ag^+ + 2H2O = 4Ag + O2↑ + 4H^+"),
        };

        foreach (var result in cases)
        {
            Assert.Equal(InferenceStatus.Solved, result.Status);
            Assert.True(result.Steps.Count >= 4, $"步骤数 {result.Steps.Count} < 4");
            Assert.All(result.Steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Description)));
            Assert.All(
                result.Steps.Where(s => !string.IsNullOrWhiteSpace(s.KnowledgeId)),
                s => Assert.False(string.IsNullOrWhiteSpace(s.Source)));
            // 电子守恒闭合步骤必须存在且溯源到 CONS-ELEC-001。
            Assert.Contains(result.Steps, s => s.KnowledgeId == "CONS-ELEC-001");
        }
    }

    private static ElectrochemResult Infer(string type, string[] electrodes, string electrolyte, string medium, string overall) =>
        ElectrochemEngine.Infer(new ElectrochemRequest
        {
            Type = type,
            Electrodes = electrodes.ToList(),
            Electrolyte = electrolyte,
            Medium = medium,
            Overall = overall,
        });
}
