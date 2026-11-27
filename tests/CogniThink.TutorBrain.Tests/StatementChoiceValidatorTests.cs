using System.Collections.Generic;
using System.Linq;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// StatementChoiceValidator 护栏（spec FR-4 / Task 6）。
/// 真值表：经单选择陈述路径端到端可解的题，逐题核对字母；全库扫描保证零假阳性、已采纳答案全部判对。
/// 红线：任一选项不可判即整题 null；候选不唯一即 null（交 MultiSelect）；不蒙数。
/// </summary>
public class StatementChoiceValidatorTests
{
    /// <summary>
    /// 真值表：questionId → 期望答案字母。新增可解题须经核对后登记（T8 收益登记处）。
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

    /// <summary>全库零假阳性：除真值表外，单选择陈述路径不得给出任何答案。</summary>
    [Fact]
    public void FullBank_AnyAdoption_MustBeInTruthTable()
    {
        var solved = new List<string>();
        foreach (var c in GatewayVsLlmEvaluationService.BuildTestCases())
        {
            var o = StatementChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });
            if (o is not null) solved.Add(c.Id);
        }
        var stray = solved.Where(id => !StatementTruth.ContainsKey(id)).ToList();
        Assert.True(stray.Count == 0,
            "单选择陈述路径在真值表之外给出了答案（须核对后登记或修复）: " + string.Join(", ", stray));
    }

    /// <summary>覆盖：真值表中每道题都被解出。</summary>
    [Fact]
    public void FullBank_Coverage_EqualsTruthTable()
    {
        var solved = new HashSet<string>();
        foreach (var c in GatewayVsLlmEvaluationService.BuildTestCases())
        {
            var o = StatementChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });
            if (o is not null) solved.Add(c.Id);
        }
        Assert.Equal(StatementTruth.Keys.ToHashSet(), solved);
    }

    /// <summary>精度红线：全库单选择陈述路径的每一个采纳答案都判对。</summary>
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
