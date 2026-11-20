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
/// 化学计量选择题校验器测试（最保守）。
/// 红线：凡给出非 null 结论必须与答案一致（零假阳性）；方程式/已知量/任一选项不可干净解析必须返回 null（不蒙数）。
/// </summary>
public class StoichiometryChoiceValidatorTests
{
    private static EngineRequest Req(string raw) => new() { TaskType = "choice", RawText = raw };

    private static string Letter(string? answer)
    {
        var m = Regex.Match(answer ?? "", "[A-D]");
        return m.Success ? m.Value : "";
    }

    // ---------- 单元逻辑锁 ----------

    [Fact]
    public void 标况体积_正确方向_唯一成立项()
    {
        var raw = "在反应 2Al + 3H2SO4 = Al2(SO4)3 + 3H2↑ 中，向足量稀硫酸中加入 0.2 mol Al，下列说法正确的是（ ）\n" +
                  "A. 生成 H2 的体积（标准状况）为 6.72 L\n" +
                  "B. 转移电子的物质的量为 0.4 mol\n" +
                  "C. 消耗 H2SO4 的物质的量为 0.2 mol\n" +
                  "D. 生成 Al2(SO4)3 的质量为 68.4 g";
        var o = StoichiometryChoiceValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("A", o!.AnswerLetter);
    }

    [Fact]
    public void 质量与电子转移_精确摩尔质量()
    {
        // Cu 摩尔质量取精确值 63.5：0.2 mol Cu = 12.7 g；电子 0.4 mol
        var raw = "在反应 Fe + CuSO4 = FeSO4 + Cu 中，加入 0.2 mol Fe，下列说法正确的是（ ）\n" +
                  "A. 生成 FeSO4 的质量为 15.2 g\n" +
                  "B. 转移电子的物质的量为 0.2 mol\n" +
                  "C. 消耗 CuSO4 的物质的量为 0.1 mol\n" +
                  "D. 生成 Cu 的质量为 12.7 g";
        var o = StoichiometryChoiceValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("D", o!.AnswerLetter);
    }

    [Fact]
    public void 错误方向_唯一不成立项()
    {
        var raw = "加热时反应 2KMnO4 = K2MnO4 + MnO2 + O2↑ 中，分解 0.2 mol KMnO4，下列说法错误的是（ ）\n" +
                  "A. 生成 O2 的体积（标准状况）为 2.24 L\n" +
                  "B. 生成 K2MnO4 的质量为 19.7 g\n" +
                  "C. 生成 MnO2 的物质的量为 0.2 mol\n" +
                  "D. 生成 O2 的质量为 3.2 g";
        var o = StoichiometryChoiceValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("C", o!.AnswerLetter);
    }

    [Fact]
    public void 题干无方程式_返回null()
    {
        var raw = "取 0.1 mol 某物质进行实验，下列说法正确的是（ ）\n" +
                  "A. 质量为 10 g\nB. 体积为 2.24 L\nC. 含分子 0.1NA\nD. 转移电子 0.2 mol";
        Assert.Null(StoichiometryChoiceValidator.TryDecide(Req(raw)));
    }

    [Fact]
    public void 已知量物种不在方程式中_返回null()
    {
        var raw = "在反应 2H2 + O2 = 2H2O 中，加入 0.5 mol NaCl，下列说法正确的是（ ）\n" +
                  "A. 生成 H2O 的质量为 18 g\nB. 消耗 H2 的物质的量为 1 mol\n" +
                  "C. 消耗 O2 的体积为 11.2 L\nD. 转移电子 2 mol";
        Assert.Null(StoichiometryChoiceValidator.TryDecide(Req(raw)));
    }

    [Fact]
    public void 无唯一候选_返回null()
    {
        // 四个选项都成立 → 正确方向候选=4 不唯一 → null
        var raw = "在反应 2H2 + O2 = 2H2O 中，消耗 0.5 mol O2，下列说法正确的是（ ）\n" +
                  "A. 消耗 H2 的物质的量为 1 mol\nB. 生成 H2O 的质量为 18 g\n" +
                  "C. 生成 H2O 的物质的量为 1 mol\nD. 转移电子的物质的量为 2 mol";
        Assert.Null(StoichiometryChoiceValidator.TryDecide(Req(raw)));
    }

    // ---------- 新增 t3s 题库全部可解 ----------

    [Fact]
    public void 新增t3s题_全部被计量校验器正确解出()
    {
        var cases = GatewayVsLlmEvaluationService.GenTier2Questions()
            .Where(x => x.Id.StartsWith("t3s-"))
            .ToList();
        Assert.True(cases.Count >= 8, $"t3s 题数量 {cases.Count}，期望 ≥8");
        var fails = new List<string>();
        foreach (var c in cases)
        {
            var outcome = StoichiometryChoiceValidator.TryDecide(Req(c.Question));
            var expected = Letter(c.Answer);
            if (outcome is null || outcome.AnswerLetter != expected)
                fails.Add($"{c.Id}(期望 {expected}, 实际 {(outcome?.AnswerLetter ?? "null")})");
        }
        Assert.True(fails.Count == 0, "t3s 题未全对：\n" + string.Join("\n", fails));
    }

    // ---------- 端到端：网关采纳校验器结论（免 LLM） ----------

    [Fact]
    public async Task E2E_网关采纳_计量题结论含正确字母()
    {
        var gw = (LlmGatewayService)RuntimeHelpers.GetUninitializedObject(typeof(LlmGatewayService));
        var cases = GatewayVsLlmEvaluationService.GenTier2Questions()
            .Where(x => x.Id.StartsWith("t3s-"))
            .ToList();
        Assert.True(cases.Count >= 8);
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
        Assert.True(fails.Count == 0, "计量题端到端未被网关正确采纳：\n" + string.Join("\n", fails));
    }

    // ---------- 真题诚实行为 ----------

    [Theory]
    [InlineData("pdf-029")]
    [InlineData("pdf-079")]
    [InlineData("pdf-254")]
    [InlineData("pdf-283")]
    [InlineData("pdf-316")]
    public void 真题计量题_诚实返回null或解必正确(string id)
    {
        var tc = GatewayVsLlmEvaluationService.GenPdfQuestions().FirstOrDefault(x => x.Id == id);
        Assert.NotNull(tc);
        var outcome = StoichiometryChoiceValidator.TryDecide(Req(tc!.Question));
        if (outcome is not null)
            Assert.Equal(Letter(tc.Answer), outcome.AnswerLetter);
    }

    // ---------- 全题库零假阳性护栏 ----------

    [Fact]
    public void Guardrail_全题库_计量校验器无假阳性()
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
                outcome = StoichiometryChoiceValidator.TryDecide(Req(c.Question));
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
        Assert.True(mismatches.Count == 0, "存在假阳性/异常（计量校验器）：\n" + string.Join("\n", mismatches));
        Assert.True(solved.Count >= 8, $"仅解出 {solved.Count} 道（期望 ≥8）：{string.Join(",", solved)}");
    }
}
