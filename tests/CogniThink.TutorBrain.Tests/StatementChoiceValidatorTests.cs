using System.Collections.Generic;
using System.Linq;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// StatementChoiceValidator 护栏（spec FR-4 / Task 6）。
/// 设计：StatementTruth 是「钉住的回归集」（经核对的题 → 期望字母），不是穷举白名单。
/// 单选择陈述路径因事实库 / 氧还原语可解出钉住集之外的题——只要全部判对即合法，
/// 精度红线由 <see cref="FullBank_AllAdoptions_GradedCorrect"/> 兜底；完整 LOST / GAINED 由 T10 生产 eval diff 核对。
/// 红线：任一选项不可判即整题 null；候选不唯一即 null（交 MultiSelect）；不蒙数。
/// </summary>
public class StatementChoiceValidatorTests
{
    /// <summary>
    /// 钉住的回归集：questionId → 期望答案字母。新增确认收益须经核对后登记。
    /// </summary>
    public static readonly Dictionary<string, string> StatementTruth = new()
    {
        // pdf-075：A/D 事实锚定 True，B 氧还电子计数 False（实际 6e），C 氧还反应计数 True → 唯一假项 B
        ["pdf-075"] = "B",
    };

    public static IEnumerable<object[]> TruthCases =>
        StatementTruth.Select(kv => new object[] { kv.Key, kv.Value });

    [Theory]
    [MemberData(nameof(TruthCases))]
    public void KnownQuestion_SolvedToExpectedLetter(string id, string expected)
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == id);
        var outcome = StatementChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });
        Assert.NotNull(outcome);
        Assert.Equal(expected, outcome!.AnswerLetter);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(outcome.AnswerLetter, c));
    }

    /// <summary>覆盖：钉住集中每道题都被解出且答到期望字母。</summary>
    [Fact]
    public void FullBank_PinnedTruth_AllSolvedAndCorrect()
    {
        var solved = new Dictionary<string, string>();
        foreach (var c in GatewayVsLlmEvaluationService.BuildTestCases())
        {
            var o = StatementChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });
            if (o is not null) solved[c.Id] = o.AnswerLetter;
        }
        var missing = StatementTruth.Keys.Where(k => !solved.ContainsKey(k)).ToList();
        Assert.True(missing.Count == 0, "钉住集未解出: " + string.Join(", ", missing));
        var mismatch = StatementTruth.Where(kv => solved.TryGetValue(kv.Key, out var a) && a != kv.Value)
            .Select(kv => $"{kv.Key} 期望 {kv.Value} 实际 {solved[kv.Key]}").ToList();
        Assert.True(mismatch.Count == 0, "钉住集字母不符: " + string.Join(", ", mismatch));
    }

    /// <summary>精度红线：全库单选择陈述路径的每一个采纳答案都判对（harm=0）。</summary>
    [Fact]
    public void FullBank_AllAdoptions_GradedCorrect()
    {
        var wrong = new List<string>();
        foreach (var c in GatewayVsLlmEvaluationService.BuildTestCases())
        {
            var o = StatementChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });
            if (o is not null && !GatewayVsLlmEvaluationService.IsAnswerCorrect(o.AnswerLetter, c))
                wrong.Add($"{c.Id}->{o.AnswerLetter}");
        }
        Assert.True(wrong.Count == 0, "单选择陈述路径给出了错误答案: " + string.Join(", ", wrong));
    }

    // ===== 合成用例：阈值行为（FR-4 口径）=====

    private static EngineRequest Req(string raw) => new() { RawText = raw };

    private const string H2oStem = "反应：2H2 + O2 = 2H2O，下列说法正确的是\n" +
        "A. 每生成1 mol H2O转移2 mol电子\n" +
        "B. 每生成1 mol H2O转移3 mol电子\n" +
        "C. 每生成1 mol H2O转移4 mol电子\n" +
        "D. 每生成1 mol H2O转移5 mol电子";

    [Fact]
    public void UniqueTrueCandidate_ReturnsLetter()
    {
        // 正确方向，唯一真项 A → 返回 A
        Assert.Equal("A", StatementChoiceValidator.TryDecide(Req(H2oStem))?.AnswerLetter);
    }

    [Fact]
    public void TwoTrueCandidates_ReturnsNull_ToMultiSelect()
    {
        var raw = "反应：2H2 + O2 = 2H2O，下列说法正确的是\n" +
            "A. 每生成1 mol H2O转移2 mol电子\n" +       // True
            "B. 每消耗1 mol O2转移4 mol电子\n" +        // True
            "C. 每生成1 mol H2O转移3 mol电子\n" +       // False
            "D. 每消耗1 mol O2转移2 mol电子";            // False
        Assert.Null(StatementChoiceValidator.TryDecide(Req(raw)));
    }

    [Fact]
    public void ContainsUndecidedOption_ReturnsNull()
    {
        var raw = "反应：2H2 + O2 = 2H2O，下列说法正确的是\n" +
            "A. 每生成1 mol H2O转移2 mol电子\n" +
            "B. 每生成1 mol H2O转移3 mol电子\n" +
            "C. 每生成1 mol H2O转移4 mol电子\n" +
            "D. 水是氧化物";                            // 非氧还电子/计数陈述 → Undecided
        Assert.Null(StatementChoiceValidator.TryDecide(Req(raw)));
    }

    [Fact]
    public void WrongDirectionUniqueFalseCandidate_ReturnsLetter()
    {
        var raw = "反应：2H2 + O2 = 2H2O，下列说法错误的是\n" +
            "A. 每生成1 mol H2O转移2 mol电子\n" +       // True
            "B. 每生成1 mol H2O转移3 mol电子\n" +       // False
            "C. 每生成1 mol H2O转移4 mol电子\n" +       // False
            "D. 每生成1 mol H2O转移5 mol电子";            // False
        // 错误方向，假项有 3 个 → 不唯一 → null（不蒙数）
        Assert.Null(StatementChoiceValidator.TryDecide(Req(raw)));
    }
}
