using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// 阶段3D / AC-14：多小题 √/× 逐方程判定聚合。
/// 真值源为符号化学引擎（ChemicalCheckerService）+ 集中语境规则表；断言基于真实引擎输出。
/// </summary>
public class EquationCorrectnessJudgeTests
{
    // ---------- S2：单方程判据（真实引擎）----------

    /// <summary>8 个目标方程的判决必须与期望一致（含语境规则触发）。</summary>
    [Theory]
    [InlineData("2Br^- + Cl2 = Br2 + 2Cl^-", "", "√")]                              // 051-1
    [InlineData("6Fe^2+ + Cr2O7^2- + 14H^+ = 6Fe^3+ + 2Cr^3+ + 7H2O", "", "√")]    // 051-2（补白名单后）
    [InlineData("H2SO3 + Ba^2+ = BaSO3↓ + 2H^+", "H₂SO₃溶液中滴加Ba(NO₃)₂溶液", "×")] // 051-3 Rule A
    [InlineData("2HCO3^- + Cl2 = 2CO2 + Cl^- + ClO^- + H2O", "", "×")]             // 051-4（CO2缺↑）
    [InlineData("[Cu(NH3)2]^2+ + CO + NH3 = [Cu(NH3)3CO]^2+", "亚铜氨溶液除去合成氨原料气中的CO", "×")] // 051-5 Rule B
    [InlineData("SiO2 + 4H^+ + 4F^- = SiF4↑ + 2H2O", "", "×")]                      // 468（HF弱酸不能拆）
    [InlineData("SiO2 + 2OH^- = SiO3^2- + H2O", "", "√")]                           // 471
    [InlineData("SiO2 + Na2CO3 = Na2SiO3 + CO2↑", "", "√")]                         // 472
    public void Judge_TargetEquations_ReturnsExpected(string equation, string context, string expected)
    {
        var verdict = EquationCorrectnessJudge.Judge(equation, context);
        var exp = expected.Length == 0 ? (char?)null : expected[0];
        Assert.Equal(exp, verdict.Symbol);
    }

    /// <summary>Unicode 上下标形式应与 caret 形式等价（归一化后同判）。</summary>
    [Fact]
    public void Judge_UnicodeNotation_EquivalentToCaret()
    {
        var v1 = EquationCorrectnessJudge.Judge("2Br⁻ + Cl₂ = Br₂ + 2Cl⁻", "");
        var v2 = EquationCorrectnessJudge.Judge("2Br^- + Cl2 = Br2 + 2Cl^-", "");
        Assert.Equal(v2.Symbol, v1.Symbol);
        Assert.Equal('√', v1.Symbol);
    }

    /// <summary>051-3 无硝酸根语境时引擎形式全过（Rule A 不触发）；有语境才判 ×。</summary>
    [Fact]
    public void RuleA_OnlyFires_WithNitrateContext()
    {
        var without = EquationCorrectnessJudge.Judge("H2SO3 + Ba^2+ = BaSO3↓ + 2H^+", "");
        Assert.Equal('√', without.Symbol); // 引擎形式全过，无语境规则否定

        var withNitrate = EquationCorrectnessJudge.Judge("H2SO3 + Ba^2+ = BaSO3↓ + 2H^+", "向Ba(NO3)2中通入SO2");
        Assert.Equal('×', withNitrate.Symbol);
    }

    /// <summary>051-5 无"亚铜氨"语境时引擎判 null（超纲/氧化数未知），不蒙数。</summary>
    [Fact]
    public void RuleB_OnlyFires_WithCuprousAmmineContext()
    {
        var without = EquationCorrectnessJudge.Judge("[Cu(NH3)2]^2+ + CO + NH3 = [Cu(NH3)3CO]^2+", "");
        Assert.Null(without.Symbol); // 不蒙数

        var withCtx = EquationCorrectnessJudge.Judge("[Cu(NH3)2]^2+ + CO + NH3 = [Cu(NH3)3CO]^2+", "亚铜氨溶液吸收CO");
        Assert.Equal('×', withCtx.Symbol);
    }

    /// <summary>解析失败 / 仅超纲 → null（不蒙数，不归 ×）。</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("这不是方程式abc")]
    [InlineData("BrO4^- = BrO4^-")] // 超纲物种（高溴酸根不在白名单）但可配平 → 仅超纲 → null（不蒙数）
    public void Judge_Uncertain_ReturnsNull(string equation)
    {
        var verdict = EquationCorrectnessJudge.Judge(equation, "");
        Assert.Null(verdict.Symbol);
    }

    // ---------- 归一化 ----------

    [Theory]
    [InlineData("2Br⁻+Cl₂=Br₂+2Cl⁻", "2Br^-+Cl2=Br2+2Cl^-")]
    [InlineData("Cr₂O₇²⁻", "Cr2O7^2-")]
    [InlineData("Ba²⁺", "Ba^2+")]
    [InlineData("SiO2 + 4H+ + 4F- === SiF4↑ + 2H2O", "SiO2 + 4H+ + 4F- = SiF4↑ + 2H2O")]
    [InlineData("A ⇌ B", "A = B")]
    [InlineData("SiO2 + Na2CO3 --(高温)--> Na2SiO3 + CO2↑", "SiO2 + Na2CO3 = Na2SiO3 + CO2↑")]
    public void NormalizeEquation_ConvertsToCaretAndSingleEquals(string input, string expected)
    {
        Assert.Equal(expected, EquationCorrectnessJudge.NormalizeEquation(input));
    }

    // ---------- S3：聚合求解器 ----------

    private const string Pdf051 =
        "(2024·北京卷)判断下列离子方程式书写的正误,正确的填“√”,错误的填“×”。" +
        "(1)(2024·北京卷)海水提溴过程中,用氯气氧化苦卤得到溴单质:2Br⁻+Cl₂=Br₂+2Cl⁻( )\n" +
        "(2024·北京卷)用绿矾(FeSO₄·7H₂O)将酸性工业废水中的Cr₂O₇²⁻转化为Cr³⁺:6Fe²⁺+Cr₂O₇²⁻+14H⁺=6Fe³⁺+2Cr³⁺+7H₂O( )\n" +
        "(2024·浙江月考)H₂SO₃溶液中滴加Ba(NO₃)₂溶液:H₂SO₃+Ba²⁺=BaSO₃↓+2H⁺( )\n" +
        "(2024·浙江月考)NaHCO₃溶液中通入少量Cl₂:2HCO₃⁻+Cl₂=2CO₂+Cl⁻+ClO⁻+H₂O( )\n" +
        "(2024·湖北卷)亚铜氨溶液除去合成氨原料气中的CO:[Cu(NH₃)₂]²⁺+CO+NH₃⇌[Cu(NH₃)₃CO]²⁺( )";

    [Fact]
    public void Solver_Pdf051_FullAggregation()
    {
        var resp = MultiSubCorrectnessSolver.TrySolve(new EngineRequest { RawText = Pdf051 });
        Assert.NotNull(resp);
        Assert.True(resp!.Ok);
        Assert.Equal("(1)√ (2)√ (3)× (4)× (5)×", resp.FinalResult);
    }

    [Theory]
    [InlineData("(9)用氢氟酸雕刻玻璃:SiO2 + 4H+ + 4F- === SiF4↑ + 2H2O（ ）", "×")]
    [InlineData("(12)盛放强碱溶液不能用磨口玻璃塞的原因:SiO2 + 2OH- === SiO3^2- + H2O（ ）", "√")]
    [InlineData("(13)用纯碱制备普通玻璃的主要反应:SiO2 + Na2CO3 --(高温)--> Na2SiO3 + CO2↑（ ）", "√")]
    public void Solver_SingleSub_ReturnsSingleChar(string stem, string expected)
    {
        var resp = MultiSubCorrectnessSolver.TrySolve(new EngineRequest { RawText = stem });
        Assert.NotNull(resp);
        Assert.Equal(expected, resp!.FinalResult);
    }

    /// <summary>概念题（无方程）→ null，零回归（不被误采纳）。</summary>
    [Fact]
    public void Solver_ConceptStem_NoEquation_ReturnsNull()
    {
        var resp = MultiSubCorrectnessSolver.TrySolve(
            new EngineRequest { RawText = "(8)SiO2可与氢氟酸反应,故SiO2为碱性氧化物（ ）" });
        Assert.Null(resp);
    }

    /// <summary>非正误判断题（无 (N)...( ) 模式）→ null，交给后续分支。</summary>
    [Fact]
    public void Solver_NonJudgmentText_ReturnsNull()
    {
        var resp = MultiSubCorrectnessSolver.TrySolve(
            new EngineRequest { RawText = "在 Cu + HNO3(稀) 反应中，氧化剂是什么？" });
        Assert.Null(resp);
    }

    /// <summary>多小题全部确定 → 聚合成 (1)√ (2)×。</summary>
    [Fact]
    public void Solver_TwoSubs_AllDetermined_Aggregates()
    {
        var stem = "(1)2Br⁻+Cl₂=Br₂+2Cl⁻( )\n(2)SiO2 + 4H+ + 4F- = SiF4↑ + 2H2O（ ）";
        var resp = MultiSubCorrectnessSolver.TrySolve(new EngineRequest { RawText = stem });
        Assert.NotNull(resp);
        Assert.Equal("(1)√ (2)×", resp!.FinalResult);
    }

    /// <summary>任一小题取不到方程/不确定 → 整题 null（不输出部分答案，守 precision）。</summary>
    [Fact]
    public void Solver_OneSubUncertain_ReturnsNull()
    {
        var stem = "(1)2Br⁻+Cl₂=Br₂+2Cl⁻( )\n(2)这不是一个方程式xyz（ ）";
        var resp = MultiSubCorrectnessSolver.TrySolve(new EngineRequest { RawText = stem });
        Assert.Null(resp);
    }

    // ---------- S6：判分 √/× 序列归一化 ----------

    private static QaTestCase CaseKeywords(params string[] keywords) =>
        new() { Id = "u", Answer = "x", Type = "other", AnswerKeywords = keywords.ToList() };

    [Fact]
    public void Scoring_Pdf051_FullString_Matches()
    {
        var c = CaseKeywords("(1)√ (2)√ (3)× (4)× (5)×");
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect("(1)√ (2)√ (3)× (4)× (5)×", c));
    }

    [Fact]
    public void Scoring_Lianxie_Format_Matches()
    {
        var c = CaseKeywords("(1)√ (2)√ (3)× (4)× (5)×");
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect("√√×××", c));
    }

    [Fact]
    public void Scoring_SingleChar_Matches()
    {
        var c = CaseKeywords("×");
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect("×", c));
    }

    [Fact]
    public void Scoring_WrongVerdict_DoesNotMatch()
    {
        var c = CaseKeywords("×");
        Assert.False(GatewayVsLlmEvaluationService.IsAnswerCorrect("√", c));
    }

    [Fact]
    public void Scoring_WrongSequenceOrder_DoesNotMatch()
    {
        var c = CaseKeywords("√√×××");
        Assert.False(GatewayVsLlmEvaluationService.IsAnswerCorrect("√×√××", c));
    }

    /// <summary>非 √/× 关键词回归守卫：原 Contains 行为不变。</summary>
    [Fact]
    public void Scoring_NonCheckmarkKeyword_UnchangedContainsBehavior()
    {
        var c = CaseKeywords("CO2");
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect("生成 CO2 气体", c));
        Assert.False(GatewayVsLlmEvaluationService.IsAnswerCorrect("生成 H2O", c));
    }

    // ---------- S7：连接符 / LaTeX / 注解归一化（T2 可靠性加固）----------

    /// <summary>答案键用 == 而引擎用 = 时，归一化后应判正（pdf-044 类假阴性解锁）。</summary>
    [Fact]
    public void Scoring_Connector_DoubleEquals_NormalizesToEquals()
    {
        var c = CaseKeywords("2H++SO4^2-+Ba2++2OH-==2H2O+BaSO4↓");
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(
            "离子方程式：2H++SO4^2-+Ba2++2OH-=2H2O+BaSO4", c));
    }

    /// <summary>答案键用 → 而引擎用 = 时，归一化后应判正（pdf-532/484 类假阴性解锁）。</summary>
    [Fact]
    public void Scoring_Connector_RightArrow_NormalizesToEquals()
    {
        var c = CaseKeywords("Cl2+CaBr2→CaCl2+Br2");
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(
            "化学方程式：Cl2+CaBr2=CaCl2+Br2", c));
    }

    /// <summary>答案键用 ⇌ 而引擎用 = 时，归一化后应判正（pdf-274 水解类）。</summary>
    [Fact]
    public void Scoring_Connector_EquilibriumArrow_NormalizesToEquals()
    {
        var c = CaseKeywords("Fe3+ + 3H2O ⇌ Fe(OH)3 + 3H+");
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(
            "离子方程式：Fe3+ + 3H2O = Fe(OH)3 + 3H+", c));
    }

    /// <summary>LaTeX \\rightleftharpoons / 上下标归一后可与引擎写法子串匹配（pdf-462 类）。</summary>
    [Fact]
    public void Scoring_LaTeX_RightleftharpoonsAndSubSuperScript_Normalizes()
    {
        var c = CaseKeywords(@"Al^{3+} + 2H_2O \rightleftharpoons AlOOH + 3H^+");
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(
            "离子方程式：Al3+ + 2H2O = AlOOH + 3H+", c));
    }

    /// <summary>引擎输出含 ↑ 注解、答案键省略时仍判正（注解不影响物质恒等）。</summary>
    [Fact]
    public void Scoring_ConditionMarkers_StrippedOnBothSides()
    {
        var c = CaseKeywords("2H++CO3^2-=H2O+CO2");
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(
            "离子方程式：2H+ + CO3^2- = H2O + CO2↑", c));
    }

    /// <summary>回归守卫：√/× 序列匹配不受连接符/空格归一化影响。</summary>
    [Fact]
    public void Scoring_CheckmarkSequence_UnchangedAfterNormalization()
    {
        var c = CaseKeywords("(1)√ (2)√ (3)× (4)× (5)×");
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect("√ √ × × ×", c));
        Assert.False(GatewayVsLlmEvaluationService.IsAnswerCorrect("√ × √ × ×", c));
    }

    /// <summary>回归守卫：选项字母判分不受连接符/空格归一化影响。</summary>
    [Fact]
    public void Scoring_ChoiceLetter_UnchangedAfterNormalization()
    {
        var c = new QaTestCase { Id = "u", Answer = "B", Type = "choice", AnswerKeywords = new System.Collections.Generic.List<string>() };
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect("正确选项为 B", c));
    }

    /// <summary>precision 守卫：归一化后为空的关键词（纯注解标记）不得让任意答案判正。</summary>
    [Fact]
    public void Scoring_EmptyAfterNormalizationKeyword_DoesNotMatchEverything()
    {
        var c = CaseKeywords("↑");
        Assert.False(GatewayVsLlmEvaluationService.IsAnswerCorrect("任意无关答案", c));
    }
}
