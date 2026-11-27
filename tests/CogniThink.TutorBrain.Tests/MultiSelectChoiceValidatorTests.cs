using System.Collections.Generic;
using System.Linq;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// MultiSelectChoiceValidator 护栏（spec FR-1 / Task 2）。
/// 红线：零假阳性 / 零 harm；末端 fallback 不抢在既有单选校验器之前；不依赖题干「双选/多选」标记。
/// </summary>
public class MultiSelectChoiceValidatorTests
{
    // ===== TR-2.2：安全语义 —— 任一选项 Undecided / 候选不足 均返回 null（不蒙数，不降级单选）=====

    // 合成题基于 general-choice-facts GC-004：A=false, B=true, C=false, D=false，
    // stem 限定 "化学研究中常用的方法" + "分类方法正确的是"（正确方向）。
    private const string Stem004 = "化学研究中常用的方法，下列分类方法正确的是";

    private static string BuildQ(params string[] opts) =>
        Stem004 + "\n" + string.Join("\n", opts.Select((t, i) => $"{(char)('A' + i)}. {t}"));

    [Fact]
    public void AllDecidable_OnlyOneTrue_ReturnsNull_CandidatesBelowTwo()
    {
        // 正确方向：四选项全部可判，但仅 B 为真 → 候选 1 个 < 2 → 不蒙数，返回 null（不降级为单选）。
        var raw = BuildQ("冰水混合物属于混合物", "次磷酸（NaH2PO2）是正盐", "NO2 是酸性氧化物", "SO2 属于电解质");
        Assert.Null(MultiSelectChoiceValidator.TryDecide(new EngineRequest { RawText = raw }));
    }

    [Fact]
    public void OneOptionUndecided_ReturnsNull()
    {
        // 选项 D 不含任何事实 match → Judge 判 Undecided → 整题 null（红线：任一不可判不蒙数）。
        var raw = BuildQ("冰水混合物属于混合物", "次磷酸（NaH2PO2）是正盐", "NO2 是酸性氧化物", "某种不相关物质 Xyz 是正盐");
        Assert.Null(MultiSelectChoiceValidator.TryDecide(new EngineRequest { RawText = raw }));
    }

    // ===== TR-2.3：去标记后仍能多选求解（证明不依赖题干「双选/多选」标记）+ 判分正确 =====

    [Theory]
    [InlineData("pdf-225", "CD")]
    [InlineData("pdf-329", "ACE")]
    [InlineData("pdf-071", "BD")]
    [InlineData("pdf-115", "AC")]
    [InlineData("pdf-475", "ACD")]
    [InlineData("pdf-531", "BC")]
    public void NoMarkerVariant_SolvesMultiSelect_AndGradedCorrect(string id, string expected)
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == id);
        // 剥去题干「双选/多选」标记：证明能力不依赖显式标记（OQ-2）。
        var raw = c.Question.Replace("双选", "").Replace("多选", "");
        var r = MultiSelectChoiceValidator.TryDecide(new EngineRequest { RawText = raw });

        Assert.NotNull(r);
        Assert.Equal(expected, r!.AnswerLetter);
        Assert.Contains(expected, r.FinalResult);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c));
    }

    // ===== 全库精度护栏：MultiSelect 任一被采纳结论必须判分正确（precision=1.0, harm=0）=====

    [Fact]
    public void FullBank_AnyMultiSelectConclusion_GradedCorrect_NoHarm()
    {
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        var wrong = new List<string>();
        foreach (var c in cases)
        {
            var r = MultiSelectChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });
            if (r is null) continue;
            if (!GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c))
                wrong.Add($"{c.Id}: {r.FinalResult} (answer={c.Answer})");
        }
        Assert.Empty(wrong);
    }
}
