using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// Tier 2 / Task 16：物质分类校验器测试。
/// 红线：凡校验器给出非 null 结论，必须与题库答案一致（零假阳性）；无法唯一判定必须返回 null。
/// </summary>
public class ClassificationValidatorTests
{
    private static EngineRequest Req(string raw) => new() { TaskType = "choice", RawText = raw };

    // ---------- 必解真题（AC-16 要求 pdf-001/002/003/031/032 被解出） ----------

    [Theory]
    [InlineData("pdf-001", "B")]
    [InlineData("pdf-002", "C")]
    [InlineData("pdf-003", "D")]
    [InlineData("pdf-031", "A")]
    [InlineData("pdf-032", "B")]
    public void 必解真题_被正确解出(string id, string expected)
    {
        var tc = GatewayVsLlmEvaluationService.GenPdfQuestions().FirstOrDefault(x => x.Id == id);
        Assert.NotNull(tc);
        var outcome = ClassificationValidator.TryDecide(Req(tc!.Question));
        Assert.True(outcome is not null, $"{id} 应被分类校验器解出，但返回 null");
        Assert.Equal(expected, outcome!.AnswerLetter);
    }

    [Fact]
    public void 不可解析或不唯一的题_保守返回null()
    {
        // pdf-004：选项含因果推理，无法干净解析为分类断言 → 必须 null（不臆断）
        var tc004 = GatewayVsLlmEvaluationService.GenPdfQuestions().FirstOrDefault(x => x.Id == "pdf-004");
        Assert.NotNull(tc004);
        Assert.Null(ClassificationValidator.TryDecide(Req(tc004!.Question)));
    }

    // ---------- TR-16 单元锁（构造可控题干） ----------

    [Fact]
    public void TR16_1_P2_类别在前_成员在后_唯一正确()
    {
        var raw = "(★)对下列各组物质的分类正确的是（ ）\n" +
                  "A. 酸性氧化物：二氧化硫、干冰、氧化铝\n" +
                  "B. 同素异形体：¹²C、¹³C、¹⁴C\n" +
                  "C. 混合物：钢铁、氯水、漂白粉\n" +
                  "D. 电解质：氯化钠、水、乙醇";
        var o = ClassificationValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("C", o!.AnswerLetter);
    }

    [Fact]
    public void TR16_2_P4_类别在题干_选项裸物质()
    {
        var raw = "(★)下列物质不属于电解质的是（ ）\nA. CO2\nB. HCl\nC. NaOH\nD. BaSO4";
        var o = ClassificationValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("A", o!.AnswerLetter);

        var raw2 = "(★)下列物质属于非电解质的是（ ）\nA. 硫酸钡\nB. 二氧化硫\nC. 氯化氢\nD. 氯气";
        var o2 = ClassificationValidator.TryDecide(Req(raw2));
        Assert.NotNull(o2);
        Assert.Equal("B", o2!.AnswerLetter);
    }

    [Fact]
    public void TR16_3_关系类_同素异形体与同位素区分()
    {
        // 正确：O2/O3 互为同素异形体；12C/13C 互为同位素（非 同素异形体）
        var raw = "(★)下列各组物质互为同素异形体的是（ ）\n" +
                  "A. 氧气、臭氧\nB. ¹²C、¹³C\nC. 氯化钠、氯化钾\nD. 氧化钠、过氧化钠";
        var o = ClassificationValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("A", o!.AnswerLetter);
    }

    [Fact]
    public void TR16_4_成员不在事实库_保守null不臆断()
    {
        // X 物质不在库 → 任一选项无法判定 → 整题 null
        var raw = "(★)下列物质分类正确的是（ ）\nA. Xyz 是酸\nB. HCl 是酸\nC. NaOH 是碱\nD. NaCl 是盐";
        Assert.Null(ClassificationValidator.TryDecide(Req(raw)));
    }

    [Fact]
    public void TR16_5_不唯一_保守null()
    {
        // A、C 都分类正确 → 不唯一 → null
        var raw = "(★)下列物质分类正确的是（ ）\n" +
                  "A. HCl 是酸\nB. 氨水 是电解质\nC. NaOH 是碱\nD. 乙醇 是电解质";
        Assert.Null(ClassificationValidator.TryDecide(Req(raw)));
    }

    // ---------- 新增 t2c 题库全部可解（AC-16：≥25 新分类题全解 + 答对） ----------

    [Fact]
    public void 新增t2c题_全部被分类校验器正确解出()
    {
        var cases = GatewayVsLlmEvaluationService.GenTier2Questions()
            .Where(x => x.Id.StartsWith("t2c-"))
            .ToList();
        Assert.True(cases.Count >= 25, $"t2c 题数量 {cases.Count}，期望 ≥25");
        foreach (var c in cases)
        {
            var outcome = ClassificationValidator.TryDecide(Req(c.Question));
            Assert.True(outcome is not null, $"{c.Id} 应被分类校验器解出，但返回 null");
            var expected = Regex.Match(c.Answer ?? "", "[A-D]");
            Assert.True(expected.Success, $"{c.Id} 题库答案无可比对字母（答案='{c.Answer}'）");
            Assert.Equal(expected.Value, outcome!.AnswerLetter);
        }
    }

    // ---------- 全题库零假阳性护栏（AC-19 精确率 100%） ----------

    [Fact]
    public void Guardrail_全题库_分类校验器无假阳性()
    {
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        Assert.NotEmpty(cases);
        var solved = new List<string>();
        foreach (var c in cases)
        {
            var outcome = ClassificationValidator.TryDecide(Req(c.Question));
            if (outcome is null) continue;
            // 凡给出结论，必须是选择题且答案字母与题库一致
            var expected = Regex.Match(c.Answer ?? "", "[A-D]");
            Assert.True(expected.Success,
                $"{c.Id} 校验器给出 {outcome.AnswerLetter}，但题库答案无可比对字母（答案='{c.Answer}'）");
            Assert.Equal(expected.Value, outcome.AnswerLetter);
            solved.Add($"{c.Id}->{outcome.AnswerLetter}");
        }
        // 至少应解出必解的 5 道，证明覆盖生效
        Assert.True(solved.Count >= 5, $"仅解出 {solved.Count} 道，期望 ≥5：{string.Join(",", solved)}");
    }
}
