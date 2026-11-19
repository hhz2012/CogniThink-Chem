using CogniThink.TutorBrain.Application.Chemistry;
using CogniThink.TutorBrain.Application.Gateway;
using CogniThink.TutorBrain.Tests;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// 阶段3B / AC-12：选择题逐选项校验器单测。
/// 真值源 = ChoiceVerifier（F1–F18 可行性规则 + 共存引擎），不调用 LLM。
/// 红线：无唯一解必须 null；守恒不接入。
/// </summary>
public class ChoiceVerifierTests
{
    private static EngineRequest Req(string rawText) => new() { RawText = rawText };

    private static string Render(string stem, params (string L, string text)[] opts)
        => stem + "\n" + string.Join("\n", opts.Select(o => $"{o.L}. {o.text}"));

    // ---------- 归一化（不做化学推断，仅字符串规整） ----------

    [Fact]
    public void Normalize_ConvertsFancySeparatorsAndCharges()
    {
        var eq = ChoiceVerifier.NormalizeOptionEquation(
            "用氢氟酸刻蚀玻璃：SiO3 2- + 4F- + 6H+ ═ SiF4↑ + 3H2O");
        Assert.Equal("SiO3^2- + 4F- + 6H+=SiF4 + 3H2O", eq);
    }

    [Fact]
    public void Normalize_PreservesParserNativeArrowAndStripsStateParens()
    {
        var eq = ChoiceVerifier.NormalizeOptionEquation(
            "高温煅烧硫铁矿: 4FeS2 + 15O2 --(高温)--> 2Fe2O3(s) + 8SO3");
        // --> 由解析器原生支持，不归一化为 =；(s) 被剥离
        Assert.Equal("4FeS2 + 15O2 --(高温)--> 2Fe2O3 + 8SO3", eq);
    }

    [Fact]
    public void Normalize_RecoversLostArrowConditionWordAsSeparator()
    {
        // 渲染丢失箭头：C+H2O(g)高温CO+H2 —— 条件词“高温”夹在两侧化学式之间，应恢复为 =
        var eq = ChoiceVerifier.NormalizeOptionEquation("煤制水煤气:C+H2O(g)高温CO+H2");
        Assert.False(string.IsNullOrEmpty(eq));
        Assert.Contains("=", eq);          // 条件词已恢复为分隔符
        Assert.Contains("H2O", eq);
        Assert.Contains("CO", eq);
        Assert.DoesNotContain("高温", eq);  // 条件词被替换，不再残留

        // 已有真实分隔符（-->）时，不得误替换条件词
        var keep = ChoiceVerifier.NormalizeOptionEquation("高温煅烧硫铁矿: 4FeS2 + 15O2 --(高温)--> 2Fe2O3 + 8SO3");
        Assert.Contains("--(高温)-->", keep);
    }

    // ---------- 唯一判定：ask=correct（3 项不可行 → 唯一剩余） ----------

    [Fact]
    public void Decide_AskCorrect_ThreeInfeasible_ReturnsRemaining()
    {
        var chem = new ChemicalInferenceService(ChemistryFixture.Knowledge);
        var req = Req(Render("下列离子或化学反应对应的方程式书写正确的是( )",
            ("A", "高温煅烧硫铁矿: 4FeS2 + 15O2 --(高温)--> 2Fe2O3 + 8SO3"),          // F7
            ("B", "泡沫灭火器: Al^3+ + 3HCO3- = Al(OH)3 ↓ + 3CO2 ↑"),                 // 正确
            ("C", "FeCl3 溶液蚀刻铜电路板: 2FeCl3 + 3Cu = 2Fe + 3CuCl2"),            // F1
            ("D", "过量氨水吸收 SO2 尾气: SO2 + NH3 · H2O = NH4HSO3")));             // F8
        var o = ChoiceVerifier.TryDecide(req, chem);
        Assert.NotNull(o);
        Assert.Equal("B", o!.AnswerLetter);
        Assert.Contains("正确", o.FinalResult);
        Assert.Contains("B", o.FinalResult);
    }

    // ---------- 唯一判定：ask=wrong（1 项不可行 → 该项） ----------

    [Fact]
    public void Decide_AskWrong_OneInfeasible_ReturnsThat()
    {
        var chem = new ChemicalInferenceService(ChemistryFixture.Knowledge);
        var req = Req(Render("下列离子方程式错误的是( )",
            ("A", "碳酸钙与盐酸: CaCO3 + 2H+ = Ca2+ + CO2 + H2O"),
            ("B", "铁与稀硫酸: Fe + 2H+ = Fe2+ + H2"),
            ("C", "FeCl3 蚀刻铜: 2FeCl3 + 3Cu = 2Fe + 3CuCl2"),   // F1
            ("D", "中和: H+ + OH- = H2O")));
        var o = ChoiceVerifier.TryDecide(req, chem);
        Assert.NotNull(o);
        Assert.Equal("C", o!.AnswerLetter);
    }

    [Fact]
    public void Decide_AskWrong_LostArrowOptionStillRecognized_UniqueWrongIsC()
    {
        // 复现 pdf-110：选项 A 丢失箭头（条件词“高温”作分隔），仍应被识别为方程并判定可行；
        // C 是唯一不可行（F1：Fe3+ 不能被 Cu 还原为 Fe）→ ask=wrong 返回 C，不返回 null。
        var chem = new ChemicalInferenceService(ChemistryFixture.Knowledge);
        var req = Req(Render("下列化学方程式错误的是( )",
            ("A", "煤制水煤气:C+H2O(g)高温CO+H2"),
            ("B", "Na2O2供氧:2Na2O2+2CO2=2Na2CO3+O2"),
            ("C", "覆铜板制作印刷电路板:2FeCl3+3Cu=2Fe+3CuCl2"),   // F1
            ("D", "铅酸蓄电池放电:Pb+PbO2+2H2SO4=2PbSO4+2H2O")));
        var o = ChoiceVerifier.TryDecide(req, chem);
        Assert.NotNull(o);
        Assert.Equal("C", o!.AnswerLetter);
        Assert.Contains("错误", o.FinalResult);
    }

    // ---------- F17：还原半反应生成 O2 不可能 ----------

    [Fact]
    public void Decide_F17_ReductionHalfReactionProducingO2_IsInfeasible()
    {
        var chem = new ChemicalInferenceService(ChemistryFixture.Knowledge);
        var req = Req(Render("下列离子或化学方程式书写正确的是( )",
            ("A", "Ba(OH)2 与少量 NaHCO3: Ba^2+ + OH- + HCO3- = BaCO3 ↓ + H2O"),  // 正确
            ("B", "NaH 生氢: 2NaH + H2O = Na2O + 2H2 ↑"),                           // F14
            ("C", "氢碘酸溶解 Fe(OH)3: Fe(OH)3 + 3H+ = Fe^3+ + 3H2O"),             // F11
            ("D", "正极反应: 2H2O + 4e- = O2 ↑ + 4OH-")));                          // F17
        var o = ChoiceVerifier.TryDecide(req, chem);
        Assert.NotNull(o);
        Assert.Equal("A", o!.AnswerLetter);
        Assert.Contains("F17", string.Join(";", o.Reasons));
    }

    // ---------- F18：HF 刻蚀玻璃反应物应为 SiO2 ----------

    [Fact]
    public void Decide_F18_HFGlassUsesSilicate_IsInfeasible()
    {
        var chem = new ChemicalInferenceService(ChemistryFixture.Knowledge);
        var req = Req(Render("下列过程对应的离子方程式正确的是( )",
            ("A", "用氢氟酸刻蚀玻璃：SiO3 2- + 4F- + 6H+ ═ SiF4↑ + 3H2O"),          // F18
            ("B", "三氯化铁刻铜：2Fe3+ + 3Cu ═ 3Cu2+ + 2Fe"),                        // F1
            ("C", "硫代硫酸钠脱氯：S2O3 2- + 2Cl2 + 3H2O ═ 2SO3 2- + 4Cl- + 6H+"),  // F15
            ("D", "碳酸钠浸泡硫酸钙：CaSO4 + CO3 2- ═ SO4 2- + CaCO3")));           // 正确
        var o = ChoiceVerifier.TryDecide(req, chem);
        Assert.NotNull(o);
        Assert.Equal("D", o!.AnswerLetter);
        Assert.Contains("F18", string.Join(";", o.Reasons));
    }

    // ---------- 离子共存路径 ----------

    [Fact]
    public void Decide_Coexist_ExactlyOneCoexisting_ReturnsIt()
    {
        var chem = new ChemicalInferenceService(ChemistryFixture.Knowledge);
        var req = Req(Render("下列各组离子在水溶液中能大量共存的是( )",
            ("A", "Na+、K+、Cl-、NO3-"),                       // 共存
            ("B", "Ba2+、SO4^2-、Na+、Cl-"),                   // BaSO4↓ 不共存
            ("C", "Ag+、Cl-、K+、NO3-"),                       // AgCl↓ 不共存
            ("D", "H+、OH-、Na+、Cl-")));                      // H2O 不共存
        var o = ChoiceVerifier.TryDecide(req, chem);
        Assert.NotNull(o);
        Assert.Equal("A", o!.AnswerLetter);
    }

    // ---------- 红线：无唯一解 → null（不蒙数） ----------

    [Fact]
    public void Decide_NonUnique_TwoInfeasible_ReturnsNull()
    {
        var chem = new ChemicalInferenceService(ChemistryFixture.Knowledge);
        // ask=correct，但只有 2 项不可行（C、D），A、B 都正确 → 不唯一 → null
        var req = Req(Render("下列方程式书写正确的是( )",
            ("A", "中和: H+ + OH- = H2O"),
            ("B", "铁与稀硫酸: Fe + 2H+ = Fe2+ + H2"),
            ("C", "FeCl3 蚀刻铜: 2FeCl3 + 3Cu = 2Fe + 3CuCl2"),   // F1
            ("D", "高温煅烧硫铁矿: 4FeS2 + 15O2 --(高温)--> 2Fe2O3 + 8SO3"))); // F7
        Assert.Null(ChoiceVerifier.TryDecide(req, chem));
    }

    [Fact]
    public void Decide_UnknownAskDir_ReturnsNull()
    {
        var chem = new ChemicalInferenceService(ChemistryFixture.Knowledge);
        // 题干无「正确/错误/共存」方向词 → 不臆断
        var req = Req(Render("下列有关铁及其化合物的转化，在指定条件下能实现的是（ ）",
            ("A", "Fe --(Cl2,△)--> FeCl3 --(Cu)--> FeCl2"),
            ("B", "Fe --(H2O,高温)--> Fe2O3 --(Al,高温)--> Fe"),
            ("C", "FeCl3(aq) --(蒸干,灼烧)--> Fe2O3 --(H2O)--> Fe(OH)3"),
            ("D", "Fe2O3 --(KClO,KOH)--> K2FeO4 --(H2O)--> H2")));
        Assert.Null(ChoiceVerifier.TryDecide(req, chem));
    }

    // ---------- 转化路径不误命中（分隔符守卫） ----------

    [Fact]
    public void Decide_ConversionPathOption_DoesNotMisFireRules()
    {
        var chem = new ChemicalInferenceService(ChemistryFixture.Knowledge);
        // pdf-016 类转化题：选项是多步转化路径（含多个 -->），不应被 F1 等误命中而假唯一。
        var req = Req(Render("下列转化在指定条件下能实现的是( )",
            ("A", "Fe --(Cl2,△)--> FeCl3 --(Cu)--> FeCl2"),
            ("B", "Fe --(H2O,高温)--> Fe2O3 --(Al,高温)--> Fe"),
            ("C", "FeCl3(aq) --(蒸干,灼烧)--> Fe2O3 --(H2O)--> Fe(OH)3"),
            ("D", "Fe2O3 --(KClO,KOH)--> K2FeO4 --(H2O)--> H2")));
        // 即使答案是 D（转化题问法），引擎也不应唯一断言（askDir 判不出 → null），更不能把 A 误判不可行。
        Assert.Null(ChoiceVerifier.TryDecide(req, chem));
    }

    // ---------- F15 守卫：含 SO4^2- 时不误命中 ----------

    [Fact]
    public void F15_DoesNotFire_WhenSulfateAlsoPresent()
    {
        var chem = new ChemicalInferenceService(ChemistryFixture.Knowledge);
        // C 是正确答案（ask=correct），其方程同时含 S2O3 / Cl2 / SO3 / SO4；
        // 若 F15 缺少 !SO4 守卫会误判 C 不可行 → 4 项都不可行 → 无唯一 → null（测试失败）。
        // 守卫正确时：A=F1、B=F7、D=F8 三项不可行，C 不被误判 → 唯一剩余 C。
        var req = Req(Render("下列方程式书写正确的是( )",
            ("A", "FeCl3 蚀刻铜: 2FeCl3 + 3Cu = 2Fe + 3CuCl2"),                                          // F1
            ("B", "高温煅烧硫铁矿: 4FeS2 + 15O2 --(高温)--> 2Fe2O3 + 8SO3"),                            // F7
            ("C", "硫代硫酸钠与过量氯水: S2O3^2- + 4Cl2 + 5H2O = 2SO3^2- + 2SO4^2- + 8Cl- + 10H+"),    // 含 SO4 → F15 守卫不命中
            ("D", "过量氨水吸收 SO2: SO2 + NH3·H2O = NH4HSO3")));                                      // F8
        var o = ChoiceVerifier.TryDecide(req, chem);
        Assert.NotNull(o);
        Assert.Equal("C", o!.AnswerLetter);
    }
}
