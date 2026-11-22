using CogniThink.TutorBrain.Application.Evaluation;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// P1-2 AC-2 / AC-6 精度红线回归护栏：多部分大题聚合结论的采纳门 <see cref="GatewayVsLlmEvaluationService.AllKeywordsHit"/>
/// 必须对全部答案键走 AND 语义——全部命中才采纳，任一未命中即 null（不蒙数、不部分判正），
/// 与 <see cref="GatewayVsLlmEvaluationService.IsAnswerCorrect"/> 的 OR 语义严格区分，杜绝「答中一个子问即判整题对」的假阳性。
/// 匹配口径与 IsAnswerCorrect 关键词路径完全一致（NormalizeChemicalNotation / 空归一化跳过 / √× 序列），仅语义从 OR 收紧为 AND。
/// </summary>
public class MultiPartAndScoringTests
{
    private static QaTestCase Case(params string[] keywords) =>
        new() { Id = "unit", Type = "fill", AnswerKeywords = new(keywords) };

    // TR-4.1：多部分题全部子问答案键命中 → 采纳（pdf-024 类 ["A","0.2","19"]）
    [Fact]
    public void AllHit_ReturnsTrue()
    {
        var c = Case("A", "0.2", "19");
        Assert.True(GatewayVsLlmEvaluationService.AllKeywordsHit("(1)A (2)0.2 (3)19", c));
    }

    // TR-4.1 / 核心精度守卫：仅命中部分关键词 → 不采纳（AND 区别于 OR 的关键）
    [Fact]
    public void OneMissing_ReturnsFalse()
    {
        var c = Case("A", "0.2", "19");
        Assert.False(GatewayVsLlmEvaluationService.AllKeywordsHit("(1)A (2)0.2", c),
            "缺少子问 (3) 的 19 → 不得判整题对（precision=1 守卫）");
    }

    // AND 与 OR 的语义差异断言：同一份部分命中结论，IsAnswerCorrect(OR) 判正对、AllKeywordsHit(AND) 判负，
    // 证明多部分采纳门不会退化成 OR 假阳性。
    [Fact]
    public void AndSemantics_DiffersFrom_OrSemantics()
    {
        var c = Case("A", "0.2", "19");
        var partial = "(1)A (2)0.2";
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(partial, c),
            "OR 口径：命中任一关键词即判正（非多部分题既有行为，本 spec 不改）");
        Assert.False(GatewayVsLlmEvaluationService.AllKeywordsHit(partial, c),
            "AND 口径：必须全部命中，部分命中不得采纳");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceConclusion_ReturnsFalse(string conclusion)
    {
        Assert.False(GatewayVsLlmEvaluationService.AllKeywordsHit(conclusion, Case("A")));
    }

    [Fact]
    public void NullConclusion_ReturnsFalse()
    {
        Assert.False(GatewayVsLlmEvaluationService.AllKeywordsHit(null, Case("A")));
    }

    [Fact]
    public void EmptyKeywordsList_ReturnsFalse()
    {
        // 无可校验关键词 → 不蒙数、不恒真假阳性
        Assert.False(GatewayVsLlmEvaluationService.AllKeywordsHit("anything", Case()));
    }

    [Fact]
    public void AllKeywordsNormalizeToEmpty_ReturnsFalse()
    {
        // 答案键全为纯注解标记（↑↓△）→ 归一化后全部为空 → 零可校验关键词 → 不采纳
        Assert.False(GatewayVsLlmEvaluationService.AllKeywordsHit("(1)↑ (2)↓", Case("↑", "↓")),
            "纯注解标记全部归一化为空 → anyCheckable=false → 不得恒真采纳");
    }

    [Fact]
    public void EmptyNormalizedKeyword_IsSkipped_NotCausingFalseNegative()
    {
        // 混合关键词："↑" 归一化后为空被跳过，不影响其余真实关键词的 AND 校验
        var c = Case("A", "↑", "0.2");
        Assert.True(GatewayVsLlmEvaluationService.AllKeywordsHit("(1)A (2)0.2", c),
            "空归一化关键词应被跳过，不得因其缺失而误判整题不采纳");
    }

    // 与 IsAnswerCorrect 同口径：化学记号归一化（Unicode 下标 → 普通数字）两侧对称匹配
    [Theory]
    [InlineData("FeCl₃", "FeCl3")]
    [InlineData("FeCl3", "FeCl₃")]
    public void ChemicalNotationNormalization_MatchesBothDirections(string conclusion, string keyword)
    {
        Assert.True(GatewayVsLlmEvaluationService.AllKeywordsHit(conclusion, Case(keyword)),
            $"结论 '{conclusion}' 与键 '{keyword}' 经归一化后应匹配");
    }

    [Fact]
    public void CaseInsensitive_Match()
    {
        Assert.True(GatewayVsLlmEvaluationService.AllKeywordsHit("naoh 溶液", Case("NaOH")));
    }

    // √/× 正误序列（与 IsAnswerCorrect 共用 CheckmarkSequenceMatches）：全对才采纳
    [Fact]
    public void CheckmarkSequence_AllCorrect_ReturnsTrue()
    {
        var c = Case("√×√");
        Assert.True(GatewayVsLlmEvaluationService.AllKeywordsHit("(1)√ (2)× (3)√", c));
    }

    [Fact]
    public void CheckmarkSequence_PartialWrong_ReturnsFalse()
    {
        var c = Case("√×√");
        Assert.False(GatewayVsLlmEvaluationService.AllKeywordsHit("(1)√ (2)√ (3)√", c),
            "中间子问 × 被错写为 √ → 序列不匹配 → 不得采纳");
    }
}
