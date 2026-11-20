using System.Text.Json;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// IonicEquationValidator 的单元测试：离子归一化不变量（防止下标/电荷歧义回归）
/// 以及已解题的端到端断言。
/// </summary>
public class T1IonicEquationValidatorTests
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
    [InlineData("NO3-", "NO3^1-")]      // 多原子离子：下标数字不是电荷（查表得电荷 1）
    [InlineData("MnO4-", "MnO4^1-")]
    [InlineData("HCO3-", "HCO3^1-")]
    [InlineData("CO3-", "CO3^2-")]      // 查表得电荷 2
    [InlineData("SO4-", "SO4^2-")]
    [InlineData("Fe3+", "Fe^3+")]       // 金属离子：尾部数字是电荷
    [InlineData("Fe2+", "Fe^2+")]
    [InlineData("Cu2+", "Cu^2+")]
    [InlineData("Ba2+", "Ba^2+")]
    [InlineData("S2O3^2-", "S2O3^2-")]  // 带 ^ 的不改变
    [InlineData("OH-", "OH^1-")]
    [InlineData("CH3COO-", "CH3COO^1-")]
    public void NormIon_normalizes_ascii_ions_without_carets(string raw, string want)
    {
        Assert.Equal(want, IonicEquationValidator.NormIon(raw));
    }

    [Theory]
    [InlineData("Fe³⁺", "Fe3+")]
    [InlineData("CO₂", "CO2")]
    [InlineData("S₂O₃²⁻", "S2O32-")]
    public void KeyNorm_strips_carets_and_unicode(string raw, string want)
    {
        Assert.Equal(want, IonicEquationValidator.KeyNorm(raw));
    }

    [Fact]
    public void pdf062_coexist_returns_B()
    {
        var o = IonicEquationValidator.TryDecide(Req(LoadQuestion("pdf-062")));
        Assert.NotNull(o);
        Assert.Equal("B", o!.AnswerLetter);
    }

    [Fact]
    public void non_choice_question_returns_null()
    {
        var q = "(2024·某某)写出下列反应的离子方程式：(1)钠与水反应____";
        Assert.Null(IonicEquationValidator.TryDecide(Req(q)));
    }
}
