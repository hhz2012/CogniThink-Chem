using System.Collections.Generic;
using System.Linq;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// CountingChoiceValidator 通用化扩展护栏（spec FR-3 / Task 3）。
/// 红线：既有计数/枚举题行为字节级不变（新路径仅在旧路径返回 null 后触发）；
/// 任一子项不可判 → 整题 null；组合型只稳妥支持正确方向。
/// </summary>
public class CountingChoiceGeneralizationTests
{
    // 基于 general-choice-facts GC-004：①=false, ②=true, ③=false, ④=false，
    // stem 限定 "化学研究中常用的方法" + "分类方法正确的是"。
    private const string Stem004Ctx = "化学研究中常用的方法，下列分类方法正确的是";
    private const string SubItems =
        "①冰水混合物属于混合物②次磷酸（NaH2PO2）是正盐③NO2是酸性氧化物④SO2属于电解质";

    private static string BuildCountQ(string optionsBlock) =>
        Stem004Ctx + "的有几项？" + SubItems + "\n" + optionsBlock;

    // ===== TR-3.1：一般陈述子项 → count-of-correct =====

    [Fact]
    public void GeneralSubItems_CountOfCorrect_ReturnsCorrectOption()
    {
        // 4 子项中仅 ② 为真 → 正确选项为 "1 项"。
        var raw = BuildCountQ("A. 1项\nB. 2项\nC. 3项\nD. 全部");
        var r = CountingChoiceValidator.TryDecide(new EngineRequest { RawText = raw });
        Assert.NotNull(r);
        Assert.Equal("A", r!.AnswerLetter);
    }

    [Fact]
    public void GeneralSubItems_AllCorrect_AllOptionWins()
    {
        // 构造 4 个全真子项 → 正确选项为 "全部"。
        // 复用 GC-004-B 的匹配键构造四条都为真的子项（均含次磷酸/NaH2PO2/正盐，stem 限定满足）。
        var subTrue = "①次磷酸（NaH2PO2）是正盐②NaH2PO2 对应的酸为次磷酸，属正盐③次磷酸钠 NaH2PO2 是正盐④NaH2PO2 为正盐（次磷酸）";
        var raw = Stem004Ctx + "的有几项？" + subTrue + "\nA. 1项\nB. 2项\nC. 3项\nD. 全部";
        var r = CountingChoiceValidator.TryDecide(new EngineRequest { RawText = raw });
        Assert.NotNull(r);
        Assert.Equal("D", r!.AnswerLetter);
    }

    [Fact]
    public void GeneralSubItems_OneUndecidable_ReturnsNull()
    {
        // 子项 ④ 不含任何事实 match → Undecided → 整题 null（红线：不蒙数）。
        var sub = "①冰水混合物属于混合物②次磷酸（NaH2PO2）是正盐③NO2是酸性氧化物④某种不相关物质 Xyz 是正盐";
        var raw = Stem004Ctx + "的有几项？" + sub + "\nA. 1项\nB. 2项\nC. 3项\nD. 全部";
        Assert.Null(CountingChoiceValidator.TryDecide(new EngineRequest { RawText = raw }));
    }

    // ===== 组合型（选项为 ①②③ 编号组合）=====

    [Fact]
    public void Combination_ReturnsOptionMatchingTrueSubItemSet()
    {
        // 真子项集合 = {②} → 正确选项为仅含 ② 的 B。
        var raw = Stem004Ctx + "的组合是" + SubItems + "\nA. ①②③\nB. ②\nC. ②③④\nD. ①③④";
        var r = CountingChoiceValidator.TryDecide(new EngineRequest { RawText = raw });
        Assert.NotNull(r);
        Assert.Equal("B", r!.AnswerLetter);
        Assert.Contains("正确选项为 B", r.FinalResult);
    }

    [Fact]
    public void Combination_WrongDirection_ReturnsNull()
    {
        // 反向组合（"错误的组合"）语义歧义 → 不判，返回 null。
        var stem = "下列分类错误的组合是" + SubItems;
        var raw = stem + "\nA. ①②③\nB. ②\nC. ②③④\nD. ①③④";
        Assert.Null(CountingChoiceValidator.TryDecide(new EngineRequest { RawText = raw }));
    }

    // ===== TR-3.2：既有计数路径行为不变 + 全库精度护栏 =====

    [Fact]
    public void FullBank_AnyCountingOutcome_GradedCorrect_NoHarm()
    {
        // Counting 任一被采纳结论必须判分正确（precision=1.0, harm=0），
        // 覆盖新增的 general-subitems / combination 路径不引入假阳性。
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        var wrong = new List<string>();
        foreach (var c in cases)
        {
            var r = CountingChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });
            if (r is null) continue;
            if (!GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c))
                wrong.Add($"{c.Id}: {r.FinalResult} (answer={c.Answer})");
        }
        Assert.Empty(wrong);
    }
}
