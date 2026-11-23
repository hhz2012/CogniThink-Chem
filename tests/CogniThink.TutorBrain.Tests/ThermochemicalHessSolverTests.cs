using System.Linq;
using CogniThink.TutorBrain.Application.Evaluation;
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

    [Fact]
    public void Pdf261_燃烧热目标推断_相态保留_解出负285_8()
    {
        // pdf-261 真实题干：目标方程「H2 的燃烧热」未显式写出，题干用 ═(U+2550) 作等号，
        // 且含相态 H2O(l)/H2O(g)。求解器须：① 归一化 ═→=；② 保留相态（否则 H2O(l)→H2O(g)
        // 净计量抵消为 0，R2 退化）；③ 目标段不被 T3/K 垃圾段抢占；④ 推断燃烧目标 H2+1/2O2→H2O(l)，
        // 再由 R1 - R2 + R3 线性组合得 ΔH = -57.2 - 44 - 184.6 = -285.8。
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-261");
        var r = ThermochemicalHessSolver.TrySolve(c.Question);
        Assert.NotNull(r);
        Assert.Equal(-285.8, r!.Value.Value, precision: 1);
    }

    [Fact]
    public void Pdf261_域外_无燃烧热且无显式目标_返回null_不蒙数()
    {
        // 含编号 ΔH 方程但既无显式目标段、也无「燃烧热」提示 → 不臆造目标，返回 null
        var q = "已知：①A(g)+B(g)=C(g) ΔH1=-10 kJ/mol；②C(g)=D(g) ΔH2=-5 kJ/mol。则下列说法正确的是？";
        Assert.Null(ThermochemicalHessSolver.TrySolve(q));
    }
}
