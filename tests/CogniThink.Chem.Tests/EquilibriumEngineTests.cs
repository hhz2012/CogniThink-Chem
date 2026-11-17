using CogniThink.Chem.Core.Benchmark;
using CogniThink.Chem.Core.Inference;
using CogniThink.Chem.Core.Math;
using Xunit;

namespace CogniThink.Chem.Tests;

/// <summary>
/// 化学平衡推断引擎（EquilibriumEngine）正式测试：三段式逐行精确比对（全程有理数、无浮点）、
/// 转化率/摩尔分数/Kp 三种题给信息（转化率/平衡量/平衡分数）反解转化进度 x、
/// 压强/温度/浓度/催化剂扰动的定性移动方向（EQ-001/EQ-002/EQ-003 规则判据）、
/// 题给平衡信息缺失或升温未给反应热符号时如实欠定、
/// 步骤可溯源（Description 非空、带 KnowledgeId 的步骤必有 Source）。
/// 断言值与基准题库 hard-equilibrium.json 的 expected 逐字一致（引擎实证）。
/// </summary>
public class EquilibriumEngineTests
{
    [Fact]
    public void H01_SynthesisAmmonia_ConversionGivesTableFractionAndKp()
    {
        var result = Infer(
            "N2 + 3H2 ⇌ 2NH3", "constantVolume",
            [("N2", "1"), ("H2", "3")],
            known: ("conversion", "N2", "1/3"),
            totalPressure: "10", fractionSpecies: "NH3");
        Assert.Equal(InferenceStatus.Solved, result.Status);
        Assert.Equal("N2 + 3H2 ⇌ 2NH3", result.BalancedEquation);
        AssertRows(result.Answer!,
            ("N2", "1", "-1/3", "2/3"),
            ("H2", "3", "-1", "2"),
            ("NH3", "0", "2/3", "2/3"));
        Assert.Equal("N2", result.Answer!.ConversionSpecies);
        Assert.Equal("1/3", result.Answer.Conversion);
        Assert.Equal("NH3", result.Answer.FractionSpecies);
        Assert.Equal("1/5", result.Answer.Fraction);
        // Kp = (1/5×10)^2 / ((2/15×10)×(4/15×10)^3) = 1/108，精确有理数。
        Assert.Equal("1/108", result.Answer.KValue);
        Assert.Null(result.Answer.Direction);
    }

    [Fact]
    public void H02_So2Oxidation_AmountFractionSolvesProgressExactly()
    {
        var result = Infer(
            "2SO2 + O2 ⇌ 2SO3", "constantPressure",
            [("SO2", "2"), ("O2", "1")],
            known: ("amountFraction", "SO2", "1/3"),
            totalPressure: "1", fractionSpecies: "SO3");
        Assert.Equal(InferenceStatus.Solved, result.Status);
        // 分数方程反解 x = 3/5（恒压 Δν≠0 时总量随 x 变，引擎按精确分数闭合）。
        AssertRows(result.Answer!,
            ("SO2", "2", "-6/5", "4/5"),
            ("O2", "1", "-3/5", "2/5"),
            ("SO3", "0", "6/5", "6/5"));
        Assert.Equal("3/5", result.Answer!.Conversion);
        Assert.Equal("1/2", result.Answer.Fraction);
        Assert.Equal("27/2", result.Answer.KValue);
    }

    [Fact]
    public void H03_N2O4Dissociation_BalanceMolesKnown()
    {
        var result = Infer(
            "N2O4 ⇌ 2NO2", "constantVolume",
            [("N2O4", "1")],
            known: ("moles", "N2O4", "1/2"),
            totalPressure: "2", fractionSpecies: "NO2");
        Assert.Equal(InferenceStatus.Solved, result.Status);
        AssertRows(result.Answer!,
            ("N2O4", "1", "-1/2", "1/2"),
            ("NO2", "0", "1", "1"));
        Assert.Equal("1/2", result.Answer!.Conversion);
        Assert.Equal("2/3", result.Answer.Fraction);
        Assert.Equal("8/3", result.Answer.KValue);
    }

    [Fact]
    public void H04_Compress_ShiftsReverseAndKUnchanged()
    {
        var result = Infer(
            "N2O4 ⇌ 2NO2", "constantVolume",
            [("N2O4", "1")],
            known: ("conversion", "N2O4", "1/2"),
            fractionSpecies: "N2O4",
            disturbance: ("compress", "", ""));
        Assert.Equal(InferenceStatus.Solved, result.Status);
        Assert.Equal("1/3", result.Answer!.Fraction);
        // 加压向气体分子数减小方向移动：逆向；未给总压则 Kp 如实不计算。
        Assert.Equal("reverse", result.Answer.Direction);
        Assert.Null(result.Answer.KValue);
        var ruleStep = result.Steps.First(s => s.KnowledgeId == "KH-RP-03");
        Assert.False(string.IsNullOrWhiteSpace(ruleStep.Source));
        Assert.Contains("平衡常数 K 不变", string.Join("\n", result.Steps.Select(s => s.Description)));
    }

    [Fact]
    public void H05_HeatUpExothermic_ShiftsReverse()
    {
        var result = Infer(
            "2SO2 + O2 ⇌ 2SO3", "constantVolume",
            [("SO2", "2"), ("O2", "1")],
            known: ("conversion", "SO2", "1/2"),
            fractionSpecies: "SO2",
            disturbance: ("heatUp", "", "exothermic"));
        Assert.Equal(InferenceStatus.Solved, result.Status);
        Assert.Equal("2/5", result.Answer!.Fraction);
        // 放热反应升温向吸热（逆）方向移动，K 减小（EQ-002 判据）。
        Assert.Equal("reverse", result.Answer.Direction);
        var kRuleStep = result.Steps.First(s => s.KnowledgeId == "KH-RP-03");
        Assert.False(string.IsNullOrWhiteSpace(kRuleStep.Source));
    }

    [Fact]
    public void H06_Catalyst_NoShiftNoConversionChange()
    {
        var result = Infer(
            "N2 + 3H2 ⇌ 2NH3", "constantVolume",
            [("N2", "1"), ("H2", "3")],
            known: ("conversion", "H2", "2/5"),
            fractionSpecies: "H2",
            disturbance: ("catalyst", "", ""));
        Assert.Equal(InferenceStatus.Solved, result.Status);
        Assert.Equal("9/16", result.Answer!.Fraction);
        // 催化剂只改速率不改平衡：不移动（EQ-003 判据）。
        Assert.Equal("noShift", result.Answer.Direction);
        var catStep = Assert.Single(result.Steps, s => s.KnowledgeId == "KH-RP-02");
        Assert.False(string.IsNullOrWhiteSpace(catStep.Source));
    }

    [Fact]
    public void H07_AddProduct_ShiftsReverseByLeChatelier()
    {
        var result = Infer(
            "N2O4 ⇌ 2NO2", "constantVolume",
            [("N2O4", "1")],
            known: ("conversion", "N2O4", "1/3"),
            fractionSpecies: "N2O4",
            disturbance: ("addSpecies", "NO2", ""));
        Assert.Equal(InferenceStatus.Solved, result.Status);
        AssertRows(result.Answer!,
            ("N2O4", "1", "-1/3", "2/3"),
            ("NO2", "0", "2/3", "2/3"));
        Assert.Equal("1/2", result.Answer!.Fraction);
        Assert.Equal("reverse", result.Answer.Direction);
    }

    [Fact]
    public void H08_NoEquilibriumInfo_IsUnderdeterminedNotGuessed()
    {
        var result = Infer(
            "N2 + 3H2 ⇌ 2NH3", "constantVolume",
            [("N2", "1"), ("H2", "3")],
            known: null);
        // 仅有曲线文字描述、无任何数值：如实欠定，绝不猜转化量。
        Assert.Equal(InferenceStatus.Underdetermined, result.Status);
        Assert.Null(result.Answer);
        Assert.Contains("无平衡信息无法定转化量", result.Message);
    }

    [Fact]
    public void HeatUpWithoutThermalSign_IsUnderdetermined()
    {
        var result = Infer(
            "N2O4 ⇌ 2NO2", "constantVolume",
            [("N2O4", "1")],
            known: ("conversion", "N2O4", "1/2"),
            disturbance: ("heatUp", "", ""));
        // 升温未给反应热符号：方向不可判，如实欠定。
        Assert.Equal(InferenceStatus.Underdetermined, result.Status);
        Assert.Contains("无法判定移动方向", result.Message);
    }

    [Fact]
    public void InvalidContainer_IsNoSolution()
    {
        var result = Infer(
            "N2O4 ⇌ 2NO2", "openBeaker",
            [("N2O4", "1")],
            known: ("conversion", "N2O4", "1/2"));
        Assert.Equal(InferenceStatus.NoSolution, result.Status);
        Assert.Contains("constantVolume", result.Message);
    }

    [Fact]
    public void SolvedCases_HaveAtLeastFourTraceableSteps()
    {
        var cases = new[]
        {
            Infer("N2 + 3H2 ⇌ 2NH3", "constantVolume", [("N2", "1"), ("H2", "3")],
                known: ("conversion", "N2", "1/3"), totalPressure: "10", fractionSpecies: "NH3"),
            Infer("2SO2 + O2 ⇌ 2SO3", "constantPressure", [("SO2", "2"), ("O2", "1")],
                known: ("amountFraction", "SO2", "1/3"), totalPressure: "1", fractionSpecies: "SO3"),
            Infer("N2O4 ⇌ 2NO2", "constantVolume", [("N2O4", "1")],
                known: ("moles", "N2O4", "1/2"), totalPressure: "2", fractionSpecies: "NO2"),
            Infer("N2O4 ⇌ 2NO2", "constantVolume", [("N2O4", "1")],
                known: ("conversion", "N2O4", "1/2"), fractionSpecies: "N2O4",
                disturbance: ("compress", "", "")),
            Infer("2SO2 + O2 ⇌ 2SO3", "constantVolume", [("SO2", "2"), ("O2", "1")],
                known: ("conversion", "SO2", "1/2"), fractionSpecies: "SO2",
                disturbance: ("heatUp", "", "exothermic")),
            Infer("N2 + 3H2 ⇌ 2NH3", "constantVolume", [("N2", "1"), ("H2", "3")],
                known: ("conversion", "H2", "2/5"), fractionSpecies: "H2",
                disturbance: ("catalyst", "", "")),
            Infer("N2O4 ⇌ 2NO2", "constantVolume", [("N2O4", "1")],
                known: ("conversion", "N2O4", "1/3"), fractionSpecies: "N2O4",
                disturbance: ("addSpecies", "NO2", "")),
        };

        foreach (var result in cases)
        {
            Assert.Equal(InferenceStatus.Solved, result.Status);
            Assert.NotNull(result.Answer);
            Assert.True(result.Steps.Count >= 4, $"步骤数 {result.Steps.Count} < 4");
            Assert.All(result.Steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Description)));
            Assert.All(
                result.Steps.Where(s => !string.IsNullOrWhiteSpace(s.KnowledgeId)),
                s => Assert.False(string.IsNullOrWhiteSpace(s.Source)));
        }
    }

    private static void AssertRows(EquilibriumAnswerSpec answer, params (string Species, string Start, string Change, string End)[] rows)
    {
        Assert.Equal(rows.Length, answer.Table.Count);
        for (var i = 0; i < rows.Length; i++)
        {
            Assert.Equal(rows[i].Species, answer.Table[i].Species);
            Assert.Equal(rows[i].Start, answer.Table[i].Start);
            Assert.Equal(rows[i].Change, answer.Table[i].Change);
            Assert.Equal(rows[i].End, answer.Table[i].End);
        }
    }

    private static EquilibriumResult Infer(
        string equation,
        string container,
        (string Species, string Moles)[] initial,
        (string Kind, string Species, string Value)? known,
        string totalPressure = "",
        string fractionSpecies = "",
        (string Kind, string Species, string Thermal)? disturbance = null) =>
        EquilibriumEngine.Infer(new EquilibriumRequest
        {
            Equation = equation,
            Container = container,
            Initial = initial.ToDictionary(t => t.Species, t => Rational.Parse(t.Moles), StringComparer.Ordinal),
            Known = known is null
                ? null
                : new EquilibriumKnownFact
                {
                    Kind = known.Value.Kind,
                    Species = known.Value.Species,
                    Value = Rational.Parse(known.Value.Value),
                },
            TotalPressure = string.IsNullOrEmpty(totalPressure) ? null : Rational.Parse(totalPressure),
            FractionSpecies = fractionSpecies,
            Disturbance = disturbance is null
                ? null
                : new EquilibriumDisturbance
                {
                    Kind = disturbance.Value.Kind,
                    Species = disturbance.Value.Species,
                    Thermal = disturbance.Value.Thermal,
                },
        });
}
