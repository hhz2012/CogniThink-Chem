using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>盖斯定律热化学 ΔH 求解器单测（batch2 calc 子集）。</summary>
public class ThermochemicalHessSolverTests
{
    [Theory]
    [InlineData(
        "H2(g) + 1/2O2(g) = H2O(l) ΔH=-285.8kJ/mol。则 2H2O(l) = 2H2(g) + O2(g) 的ΔH是多少？",
        571.6)]
    [InlineData(
        "已知：①C(s)+O2(g)=CO2(g) ΔH=-393.5kJ/mol；②CO(g)+1/2O2(g)=CO2(g) ΔH=-283.0kJ/mol。则 C(s)+1/2O2(g)=CO(g) 的ΔH是多少？",
        -110.5)]
    [InlineData(
        "已知：反应 i:2C₂H₄(g)⇌C₄H₈(g)  ΔH₁＝－104.7 kJ/mol；反应 ii:2C₂H₄(g)＋H₂(g)⇌C₄H₁₀(g)  ΔH₂＝－230.7 kJ/mol；计算反应 C₄H₈(g)＋H₂(g)⇌C₄H₁₀(g) 的 ΔH＝______kJ/mol",
        -126.0)]
    [InlineData(
        "已知：① 2H2 + O2 = 2H2O ΔH=-571.6 kJ/mol；② H2 + 1/2O2 = H2O ΔH=-285.8 kJ/mol。则 2H2 + O2 = 2H2O 的ΔH是多少？",
        -571.6)]
    public void HessSolver_ReturnsExpectedDeltaH(string question, double expected)
    {
        var result = ThermochemicalHessSolver.TrySolve(question);
        Assert.NotNull(result);
        Assert.Equal(expected, result.Value.Value, precision: 1);
    }

    [Fact]
    public void HessSolver_DuplicatedStem_StillSolves()
    {
        // pdf-260 真实题干：已知反应 Ⅰ/Ⅱ/Ⅲ 重复出现两次（BuildTestCases 拼接所致），
        // 求解器须容忍冗余方程，仍给出唯一 ΔH。
        var q = "反应Ⅰ:CH4(g)+2H2S(g)⇌CS2(g)+4H2(g)  ΔH1=+260 kJ/mol；反应Ⅱ:CH4(g)⇌C(s)+2H2(g)  ΔH2=+90 kJ/mol；反应Ⅲ:2H2S(g)⇌S2(g)+2H2(g)  ΔH3=+181 kJ/mol。\n反应Ⅰ:CH4(g)+2H2S(g)⇌CS2(g)+4H2(g)  ΔH1=+260 kJ/mol；反应Ⅱ:CH4(g)⇌C(s)+2H2(g)  ΔH2=+90 kJ/mol；反应Ⅲ:2H2S(g)⇌S2(g)+2H2(g)  ΔH3=+181 kJ/mol。反应Ⅳ:CS2(g)⇌S2(g)+C(s)  ΔH4=______ kJ/mol。";
        var result = ThermochemicalHessSolver.TrySolve(q);
        Assert.NotNull(result);
        Assert.Equal(11.0, result.Value.Value, precision: 1);
    }

    [Fact]
    public void HessSolver_InsufficientData_ReturnsNull()
    {
        // 只有一个已知方程，目标涉及未知物质 → 不可解
        var q = "已知 ① 2H2 + O2 = 2H2O ΔH=-571.6 kJ/mol。则 2Na + Cl2 = 2NaCl 的ΔH是多少？";
        Assert.Null(ThermochemicalHessSolver.TrySolve(q));
    }

    [Fact]
    public void HessSolver_NoEquation_ReturnsNull()
    {
        Assert.Null(ThermochemicalHessSolver.TrySolve("下列说法正确的是 A. xxx B. yyy"));
    }
}
