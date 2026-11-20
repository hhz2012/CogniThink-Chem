using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// 计数 / 组合推理选择题校验器测试（最保守）。
/// 红线：凡给出非 null 结论必须与答案一致（零假阳性）；任一选项不可干净判定必须返回 null（不蒙数）。
/// </summary>
public class CountingChoiceValidatorTests
{
    private static EngineRequest Req(string raw) => new() { TaskType = "choice", RawText = raw };

    private static string Letter(string? answer)
    {
        var m = Regex.Match(answer ?? "", "[A-D]");
        return m.Success ? m.Value : "";
    }

    // ---------- 单元逻辑锁 ----------

    [Fact]
    public void 烷烃构造异构_正确方向_唯一成立项()
    {
        var raw = "下列说法正确的是（ ）\n" +
                  "A. C5H12 的同分异构体有 3 种\n" +
                  "B. C6H14 的同分异构体有 4 种\n" +
                  "C. C4H10 的同分异构体有 3 种\n" +
                  "D. C7H16 的同分异构体有 8 种";
        var o = CountingChoiceValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("A", o!.AnswerLetter);
    }

    [Fact]
    public void 一氯代烷_按烷基异构计数()
    {
        var raw = "下列说法错误的是（ ）\n" +
                  "A. C4H9Cl 的同分异构体有 4 种\n" +
                  "B. C5H11Cl 的同分异构体有 8 种\n" +
                  "C. C3H7Cl 的同分异构体有 3 种\n" +
                  "D. C2H5Cl 的同分异构体有 1 种";
        var o = CountingChoiceValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("C", o!.AnswerLetter);
    }

    [Fact]
    public void 羧酸_按烷基异构计数()
    {
        var raw = "下列说法正确的是（ ）\n" +
                  "A. C5H10O2 属于羧酸的同分异构体有 4 种\n" +
                  "B. C4H8O2 属于羧酸的同分异构体有 3 种\n" +
                  "C. C3H6O2 属于羧酸的同分异构体有 3 种\n" +
                  "D. C6H12O2 属于羧酸的同分异构体有 4 种";
        var o = CountingChoiceValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("A", o!.AnswerLetter);
    }

    [Fact]
    public void 烷烃_含指定甲基数_筛选()
    {
        var raw = "下列说法正确的是（ ）\n" +
                  "A. C7H16 含 3 个甲基的同分异构体有 3 种\n" +
                  "B. C7H16 含 2 个甲基的同分异构体有 2 种\n" +
                  "C. C6H14 含 2 个甲基的同分异构体有 2 种\n" +
                  "D. C5H12 含 2 个甲基的同分异构体有 3 种";
        var o = CountingChoiceValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("A", o!.AnswerLetter);
    }

    [Fact]
    public void 事实表_立方烷与苯_查表判定()
    {
        var raw = "下列说法正确的是（ ）\n" +
                  "A. 立方烷的二氯代物有 3 种\n" +
                  "B. 立方烷的二氯代物有 2 种\n" +
                  "C. 立方烷的六氯代物有 2 种\n" +
                  "D. 苯的二氯代物有 2 种";
        var o = CountingChoiceValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("A", o!.AnswerLetter);
    }

    [Fact]
    public void 有几项正确_子项计数匹配()
    {
        var raw = "下列说法正确的有几项？① C5H12 的同分异构体有 3 种 ② C4H9Cl 的同分异构体有 4 种 ③ C3H7Cl 的同分异构体有 3 种 ④ C2H5Cl 的同分异构体有 1 种\n" +
                  "A. 1 项\nB. 2 项\nC. 3 项\nD. 4 项";
        var o = CountingChoiceValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        // ①真 ②真 ③假（C3H7Cl 应为 2 种） ④真 → 3 项正确
        Assert.Equal("C", o!.AnswerLetter);
    }

    [Fact]
    public void 任一选项不可判_整题返回null()
    {
        // C 项"环上的一氯代物"无分子式、无事实命中 → 不可判 → 整题不臆断
        var raw = "下列关于有机化合物的说法正确的是()\n" +
                  "A. C7H16含3个甲基的同分异构体有2种\n" +
                  "B. C5H10O2属于羧酸的同分异构体有4种\n" +
                  "C. 环上的一氯代物有3种\n" +
                  "D. 立方烷的六氯取代有2种";
        Assert.Null(CountingChoiceValidator.TryDecide(Req(raw)));
    }

    [Fact]
    public void 无唯一候选_返回null()
    {
        // 四个都真 → 正确方向候选=4 不唯一 → null
        var raw = "下列说法正确的是（ ）\n" +
                  "A. C5H12 的同分异构体有 3 种\n" +
                  "B. C4H10 的同分异构体有 2 种\n" +
                  "C. 苯的一氯代物有 1 种\n" +
                  "D. 萘的一氯代物有 2 种";
        Assert.Null(CountingChoiceValidator.TryDecide(Req(raw)));
    }

    [Fact]
    public void 事实表加载_7条()
    {
        Assert.Equal(7, CountingChoiceValidator.FactCount);
    }

    // ---------- 新增 t3count 题库全部可解 ----------

    [Fact]
    public void 新增t3count题_全部被计数校验器正确解出()
    {
        var cases = GatewayVsLlmEvaluationService.GenTier2Questions()
            .Where(x => x.Id.StartsWith("t3count-"))
            .ToList();
        Assert.True(cases.Count >= 10, $"t3count 题数量 {cases.Count}，期望 ≥10");
        var fails = new List<string>();
        foreach (var c in cases)
        {
            var outcome = CountingChoiceValidator.TryDecide(Req(c.Question));
            var expected = Letter(c.Answer);
            if (outcome is null || outcome.AnswerLetter != expected)
                fails.Add($"{c.Id}(期望 {expected}, 实际 {(outcome?.AnswerLetter ?? "null")})");
        }
        Assert.True(fails.Count == 0, "t3count 题未全对：\n" + string.Join("\n", fails));
    }

    // ---------- 端到端：网关采纳校验器结论（免 LLM） ----------

    [Fact]
    public async Task E2E_网关采纳_计数题结论含正确字母()
    {
        var gw = (LlmGatewayService)RuntimeHelpers.GetUninitializedObject(typeof(LlmGatewayService));
        var cases = GatewayVsLlmEvaluationService.GenTier2Questions()
            .Where(x => x.Id.StartsWith("t3count-"))
            .ToList();
        Assert.True(cases.Count >= 10);
        var fails = new List<string>();
        foreach (var c in cases)
        {
            var resp = await gw.CallEngineAsync(
                new EngineRequest { TaskType = "choice", RawText = c.Question },
                new List<ImageAsset>());
            var expected = Letter(c.Answer);
            if (resp.Ok != true || !resp.FinalResult.Contains(expected))
                fails.Add($"{c.Id}(期望 {expected}, Ok={resp.Ok}, FinalResult='{resp.FinalResult}')");
        }
        Assert.True(fails.Count == 0, "计数题端到端未被网关正确采纳：\n" + string.Join("\n", fails));
    }

    // ---------- 真题诚实行为 ----------

    [Fact]
    public void 真题pdf394_不可判项存在_诚实返回null()
    {
        var tc = GatewayVsLlmEvaluationService.GenPdfQuestions().FirstOrDefault(x => x.Id == "pdf-394");
        Assert.NotNull(tc);
        Assert.Null(CountingChoiceValidator.TryDecide(Req(tc!.Question)));
    }

    [Theory]
    [InlineData("pdf-029")]
    [InlineData("pdf-065")]
    [InlineData("pdf-067")]
    [InlineData("pdf-079")]
    [InlineData("pdf-254")]
    [InlineData("pdf-283")]
    [InlineData("pdf-316")]
    public void 真题计数题_诚实返回null或解必正确(string id)
    {
        var tc = GatewayVsLlmEvaluationService.GenPdfQuestions().FirstOrDefault(x => x.Id == id);
        Assert.NotNull(tc);
        var outcome = CountingChoiceValidator.TryDecide(Req(tc!.Question));
        if (outcome is not null)
            Assert.Equal(Letter(tc.Answer), outcome.AnswerLetter);
    }

    // ---------- 全题库零假阳性护栏 ----------

    [Fact]
    public void Guardrail_全题库_计数校验器无假阳性()
    {
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        Assert.NotEmpty(cases);
        var solved = new List<string>();
        var mismatches = new List<string>();
        foreach (var c in cases)
        {
            ChoiceVerifier.Outcome? outcome;
            try
            {
                outcome = CountingChoiceValidator.TryDecide(Req(c.Question));
            }
            catch (Exception ex)
            {
                mismatches.Add($"{c.Id} 校验器抛异常：{ex.GetType().Name}: {ex.Message}");
                continue;
            }
            if (outcome is null) continue;
            var expected = Letter(c.Answer);
            if (expected.Length == 0)
            {
                mismatches.Add($"{c.Id} 给出 {outcome.AnswerLetter}，但题库答案无可比对字母（答案='{c.Answer}'）");
                continue;
            }
            if (outcome.AnswerLetter != expected)
                mismatches.Add($"{c.Id} 给出 {outcome.AnswerLetter}，题库答案 {expected}");
            else
                solved.Add($"{c.Id}->{outcome.AnswerLetter}");
        }
        Assert.True(mismatches.Count == 0, "存在假阳性/异常（计数校验器）：\n" + string.Join("\n", mismatches));
        Assert.True(solved.Count >= 10, $"仅解出 {solved.Count} 道（期望 ≥10）：{string.Join(",", solved)}");
    }
}
