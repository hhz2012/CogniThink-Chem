using System.Text.Json;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// RedoxChoiceValidator 的单元测试：半反应归一化不变量（电子 E、减号统一、Fe 尾部 e 不被吞）、
/// 已解电极反应题端到端断言，以及非反应式题干/未知选项时的诚实 null。
/// </summary>
public class T1RedoxChoiceValidatorTests
{
    private static EngineRequest Req(string raw) => new() { TaskType = "choice", RawText = raw };

    private static string LoadQuestion(string id)
    {
        var jsonPath = Path.Combine(ChemistryFixture.RepoRoot, "_evalscratch", "task_inputs", "T1.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        foreach (var el in doc.RootElement.EnumerateArray())
            if (el.GetProperty("id").GetString() == id)
                return el.GetProperty("question").GetString()!;
        throw new InvalidOperationException($"未找到 {id}");
    }

    [Theory]
    // 电子统一为 E；Fe/He 等元素尾部的 e- 不被吞（负向回顾）
    [InlineData("Fe−3e⁻=Fe³⁺", "Fe-3E=Fe^3+")]
    [InlineData("Fe - 2e- ═ Fe2+", "Fe-2E=Fe2+")]
    [InlineData("O2+4e⁻+2H2O=4OH⁻", "O2+4E+2H2O=4OH^-")]
    [InlineData("MnO2+H2O+e⁻=MnO(OH)+OH⁻", "MnO2+H2O+E=MnO(OH)+OH^-")]
    // 减号统一（U+2212 → ASCII）、去 ↑↓ 与 (浓) 条件
    [InlineData("3Fe+8HNO₃(浓)=Fe₃O₄+8NO₂↑+4H₂O", "3Fe+8HNO3=Fe3O4+8NO2+4H2O")]
    [InlineData("Pb²⁺+2H₂O−2e⁻=PbO₂+4H⁺", "Pb^2++2H2O-2E=PbO2+4H^+")]
    public void NormRedoxEq_canonicalizes(string raw, string want)
    {
        Assert.Equal(want, RedoxChoiceValidator.NormRedoxEq(raw));
    }

    [Theory]
    [InlineData("pdf-046", "A")]
    [InlineData("pdf-053", "B")]
    [InlineData("pdf-099", "A")]
    public void Solved_electrode_questions_return_expected(string id, string want)
    {
        var o = RedoxChoiceValidator.TryDecide(Req(LoadQuestion(id)));
        Assert.NotNull(o);
        Assert.Equal(want, o!.AnswerLetter);
    }

    [Fact]
    public void Non_reaction_judgement_stem_returns_null()
    {
        // 共存题干，不含「反应式/方程式」判定 → 不应由 Redox 作答
        var q = "在溶液中能大量共存的离子组是( ) A.Na+、K+、Cl-、NO3- B.H+、Fe2+、NO3-、SO4^2- C.NH4+、OH-、Cl-、SO4^2- D.Ba2+、Na+、CO3^2-、Cl-";
        Assert.Null(RedoxChoiceValidator.TryDecide(Req(q)));
    }

    [Fact]
    public void Reaction_stem_with_unknown_options_returns_null()
    {
        // 题干是反应式判定，但选项不含事实库已知反应 → 诚实 null，禁止猜
        var q = "下列反应式正确的是( ) A.X+Y=Z B.A+B=C C.M+N=P D.Q+R=S";
        Assert.Null(RedoxChoiceValidator.TryDecide(Req(q)));
    }
}
