using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// B-judge-API Task 2：CorrectnessContextRules 外置 JSON + EquationCorrectnessJudge.JudgeWithIds 携带 ctx id（AC3）。
/// 红线：外置后判决字节级不变（既有 EquationCorrectnessJudgeTests 43 测全过为回归护栏）；
/// ctx 命中携带稳定 id；引擎检查项 / √ / null 路径 CtxRuleId 为 null（R4，不臆造知识锚点）。
/// </summary>
public class CorrectnessContextRulesExternalizationTests
{
    [Fact]
    public void JudgeWithIds_RuleA_CarriesId()
    {
        var (v, id) = EquationCorrectnessJudge.JudgeWithIds(
            "H2SO3 + Ba^2+ = BaSO3↓ + 2H^+", "向Ba(NO3)2中通入SO2");
        Assert.Equal('×', v.Symbol);
        Assert.Equal("ccr-acid-nitrate-sulfite", id);
        // 原 Judge 委托等价
        Assert.Equal(v.Symbol,
            EquationCorrectnessJudge.Judge("H2SO3 + Ba^2+ = BaSO3↓ + 2H^+", "向Ba(NO3)2中通入SO2").Symbol);
    }

    [Fact]
    public void JudgeWithIds_RuleB_CarriesId()
    {
        var (v, id) = EquationCorrectnessJudge.JudgeWithIds(
            "[Cu(NH3)2]^2+ + CO + NH3 = [Cu(NH3)3CO]^2+", "亚铜氨溶液吸收CO");
        Assert.Equal('×', v.Symbol);
        Assert.Equal("ccr-cuprous-ammine-charge", id);
    }

    [Fact]
    public void JudgeWithIds_RuleA_WithoutNitrateContext_NoId_AndRight()
    {
        var (v, id) = EquationCorrectnessJudge.JudgeWithIds(
            "H2SO3 + Ba^2+ = BaSO3↓ + 2H^+", "");
        Assert.Equal('√', v.Symbol);
        Assert.Null(id);
    }

    [Fact]
    public void JudgeWithIds_EngineWrong_NoId_R4()
    {
        // CO2 缺状态符号 ↑ → 引擎检查项判 ×，但无真实 data/rules 锚点（R4：不发节点，不臆造 id）
        var (v, id) = EquationCorrectnessJudge.JudgeWithIds(
            "2HCO3^- + Cl2 = 2CO2 + Cl^- + ClO^- + H2O", "");
        Assert.Equal('×', v.Symbol);
        Assert.Null(id);
    }

    [Fact]
    public void JudgeWithIds_Right_NoId()
    {
        var (v, id) = EquationCorrectnessJudge.JudgeWithIds("2Br^- + Cl2 = Br2 + 2Cl^-", "");
        Assert.Equal('√', v.Symbol);
        Assert.Null(id);
    }

    [Fact]
    public void JudgeWithIds_Null_NoId()
    {
        var (v, id) = EquationCorrectnessJudge.JudgeWithIds("", "");
        Assert.Null(v.Symbol);
        Assert.Null(id);
    }

    /// <summary>
    /// pdf-051 五小题逐子：仅 sub3（Rule A）与 sub5（Rule B）携带 ctx id；
    /// sub1/2（√）与 sub4（引擎 ×）均无 id（R4，engine-only 子不发节点）。
    /// </summary>
    [Fact]
    public void Pdf051_Subs_OnlyTwoCarryCtxId()
    {
        var subs = new (string Eq, string Ctx, char? ExpectedSymbol, string? ExpectedId)[]
        {
            ("2Br^- + Cl2 = Br2 + 2Cl^-", "", '√', null),
            ("6Fe^2+ + Cr2O7^2- + 14H^+ = 6Fe^3+ + 2Cr^3+ + 7H2O", "", '√', null),
            ("H2SO3 + Ba^2+ = BaSO3↓ + 2H^+", "H₂SO₃溶液中滴加Ba(NO₃)₂溶液", '×', "ccr-acid-nitrate-sulfite"),
            ("2HCO3^- + Cl2 = 2CO2 + Cl^- + ClO^- + H2O", "", '×', null),
            ("[Cu(NH3)2]^2+ + CO + NH3 = [Cu(NH3)3CO]^2+", "亚铜氨溶液除去合成氨原料气中的CO", '×', "ccr-cuprous-ammine-charge"),
        };
        foreach (var (eq, ctx, sym, id) in subs)
        {
            var (v, got) = EquationCorrectnessJudge.JudgeWithIds(eq, ctx);
            Assert.Equal(sym, v.Symbol);
            Assert.Equal(id, got);
        }
    }
}
