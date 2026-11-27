using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// RedoxCountStatementJudge 护栏（spec FR-2 / Task 3）。
/// 真值源：范围内每个带标签方程式经 RedoxBalancer 判定 IsRedox，统计真值与声称 N 比对。
/// 红线：标签范围不明 / 范围内任一方程不可解析 / 声称计数不可提取 → Undecided；不蒙数。
/// </summary>
public class RedoxCountStatementJudgeTests
{
    private static readonly RedoxCountStatementJudge Judge = new();

    // pdf-075 四个反应（ⅲ 非氧还 → 3 个氧还）
    private const string StemPdf075 =
        "反应如下：\n" +
        "ⅰ. 2XeF2 + 2H2O = 2Xe + O2 + 4HF\n" +
        "ⅱ. 2XeF2 + 4OH^- = 2Xe + O2 + 4F^- + 2H2O\n" +
        "ⅲ. XeF6 + 3H2O = XeO3 + 6HF\n" +
        "ⅳ. 2XeF6 + 4Na^+ + 16OH^- = Na4XeO6 + Xe + O2 + 12F^- + 8H2O\n" +
        "下列说法错误的是";

    [Fact]
    public void Pdf075_LabeledRange_ThreeRedox_ReturnsTrue()
    {
        Assert.Equal(Verdict.True, Judge.Judge(StemPdf075, "反应ⅰ～ⅳ中有3个氧化还原反应"));
    }

    [Fact]
    public void Pdf075_LabeledRange_ClaimTwo_ReturnsFalse()
    {
        Assert.Equal(Verdict.False, Judge.Judge(StemPdf075, "反应ⅰ～ⅳ中有2个氧化还原反应"));
    }

    [Fact]
    public void Pdf075_LabeledRange_ClaimFour_ReturnsFalse()
    {
        Assert.Equal(Verdict.False, Judge.Judge(StemPdf075, "反应ⅰ～ⅳ中有4个氧化还原反应"));
    }

    [Fact]
    public void RealPdf075_OptionC_ReturnsTrue()
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().FirstOrDefault(x => x.Id == "pdf-075");
        if (c is null) return;
        var (_, opts) = ChoiceVerifier.ParseOptions(c.Question);
        Assert.Equal(Verdict.True, Judge.Judge(c.Question, opts["C"]));
    }

    [Fact]
    public void ChineseDigit_Claim_ReturnsTrue()
    {
        Assert.Equal(Verdict.True, Judge.Judge(StemPdf075, "反应ⅰ～ⅳ中有三个氧化还原反应"));
    }

    [Fact]
    public void RefersToAllReactions_ReturnsTrue()
    {
        Assert.Equal(Verdict.True, Judge.Judge(StemPdf075, "上述反应中氧化还原反应有3个"));
    }

    // ===== 不可判输入 → Undecided（不蒙数）=====

    [Fact]
    public void NoRangeAndNoAllRef_ReturnsUndecided()
    {
        // 没有标签区间，也没有"上述/各反应"指代 → 计数范围不明
        Assert.Equal(Verdict.Undecided, Judge.Judge(StemPdf075, "有3个氧化还原反应"));
    }

    [Fact]
    public void ClaimedCountMissing_ReturnsUndecided()
    {
        Assert.Equal(Verdict.Undecided, Judge.Judge(StemPdf075, "反应ⅰ～ⅳ中有若干个氧化还原反应"));
    }

    [Fact]
    public void KeywordMissing_ReturnsUndecided()
    {
        Assert.Equal(Verdict.Undecided, Judge.Judge(StemPdf075, "反应ⅰ～ⅳ中有3个反应"));
    }

    [Fact]
    public void UnparseableEquationInRange_ReturnsUndecided()
    {
        // 范围内 ⅲ 的方程故意写坏（缺物种）→ 不可解析 → 整体不判
        var bad = "反应如下：\nⅰ. 2XeF2 + 2H2O = 2Xe + O2 + 4HF\nⅲ. XeF6 + = + \n下列说法错误的是";
        Assert.Equal(Verdict.Undecided, Judge.Judge(bad, "反应ⅰ～ⅲ中有2个氧化还原反应"));
    }

    [Fact]
    public void LabelNotInStem_ReturnsUndecided()
    {
        Assert.Equal(Verdict.Undecided, Judge.Judge("某反应：2H2+O2=2H2O", "反应⑤～⑧中有3个氧化还原反应"));
    }
}
