using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// Tier 2 / Task 18：NA 粒子计数校验器测试（最保守）。
/// 红线：凡给出非 null 结论必须与答案一致（零假阳性）；任一选项不可干净计算必须返回 null。
/// 逻辑精确复刻 _evalscratch/verify_t2n.py（已在全题库扫描验证 0 假阳性）。
/// </summary>
public class NaChoiceValidatorTests
{
    private static EngineRequest Req(string raw) => new() { TaskType = "choice", RawText = raw };

    private static string Letter(string? answer)
    {
        var m = Regex.Match(answer ?? "", "[A-D]");
        return m.Success ? m.Value : "";
    }

    private const string StemC = "设 NA 为阿伏加德罗常数的值。下列说法正确的是（ ）";
    private const string StemW = "设 NA 为阿伏加德罗常数的值。下列说法错误的是（ ）";

    // ---------- TR-18 单元逻辑锁 ----------

    [Fact]
    public void TR18_1_正确方向_唯一成立项()
    {
        var raw = StemC + "\n" +
                  "A. 1 mol H2O 中含有的电子数为 10NA\n" +
                  "B. 1 mol H2O 中含有的中子数为 10NA\n" +
                  "C. 1 mol CO2 中含有的原子数为 2NA\n" +
                  "D. 1 mol NH3 中含有的质子数为 7NA";
        var o = NaChoiceValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("A", o!.AnswerLetter);
    }

    [Fact]
    public void TR18_2_错误方向_唯一不成立项()
    {
        var raw = StemW + "\n" +
                  "A. 1 mol CH4 中含有的电子数为 10NA\n" +
                  "B. 1 mol CO2 中含有的质子数为 22NA\n" +
                  "C. 1 mol H2O 中含有的中子数为 10NA\n" +
                  "D. 1 mol NaCl 中含有的离子数为 2NA";
        var o = NaChoiceValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("C", o!.AnswerLetter);
    }

    [Fact]
    public void TR18_3_质量换算摩尔质量()
    {
        // 44g CO2 = 1 mol；σ键=2 → 2NA 成立；其余算错
        var raw = StemC + "\n" +
                  "A. 18 g H2O 中含有的中子数为 10NA\n" +
                  "B. 44 g CO2 中含有的 σ 键数目为 2NA\n" +
                  "C. 16 g O2 中含有的 π 键数目为 2NA\n" +
                  "D. 28 g N2 中含有的共价键数目为 2NA";
        var o = NaChoiceValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("B", o!.AnswerLetter);
    }

    [Fact]
    public void TR18_4_标况气体22_4L等于1mol()
    {
        var raw = StemC + "\n" +
                  "A. 标准状况下，22.4 L CO2 中含有的原子数为 2NA\n" +
                  "B. 标准状况下，11.2 L O2 中含有的 π 键数目为 NA\n" +
                  "C. 标准状况下，22.4 L SO3 中含有的电子数为 40NA\n" +
                  "D. 标准状况下，22.4 L N2 中含有的共价键数目为 3NA";
        var o = NaChoiceValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("D", o!.AnswerLetter);
    }

    [Fact]
    public void TR18_5_标况非气态陷阱_判为不成立()
    {
        // SO3 标况固态、H2O 标况液态 → 22.4L≠1mol，断言必为假（不得返回 unknown）
        var raw = StemW + "\n" +
                  "A. 标准状况下，22.4 L SO3 中含有的电子数为 40NA\n" +
                  "B. 标准状况下，22.4 L CO2 中含有的电子数为 22NA\n" +
                  "C. 1 mol H2O 中含有的电子数为 10NA\n" +
                  "D. 1 mol NaCl 中含有的离子数为 2NA";
        var o = NaChoiceValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("A", o!.AnswerLetter);
    }

    [Fact]
    public void TR18_6_氧化还原选项_保守返回null()
    {
        // A 为电子转移断言（不可干净计算）→ 整题不臆断
        var raw = StemC + "\n" +
                  "A. 每生成 1 mol Cl2，转移电子数为 2NA\n" +
                  "B. 1 mol H2O 中含有的电子数为 10NA\n" +
                  "C. 1 mol CO2 中含有的质子数为 22NA\n" +
                  "D. 1 mol CH4 中含有的原子数为 5NA";
        Assert.Null(NaChoiceValidator.TryDecide(Req(raw)));
    }

    [Fact]
    public void TR18_7_无唯一解_返回null()
    {
        // 四个选项都成立 → 正确方向候选=4，不唯一 → null（不蒙数）
        var raw = StemC + "\n" +
                  "A. 1 mol H2O 中含有的电子数为 10NA\n" +
                  "B. 1 mol NH3 中含有的电子数为 10NA\n" +
                  "C. 1 mol H2O 中含有的中子数为 8NA\n" +
                  "D. 1 mol CO2 中含有的原子数为 3NA";
        Assert.Null(NaChoiceValidator.TryDecide(Req(raw)));
    }

    [Fact]
    public void TR18_8_事实库分子式不少于40()
    {
        Assert.True(NaChoiceValidator.FormulaCount >= 40,
            $"事实库分子式 {NaChoiceValidator.FormulaCount}，期望 ≥40");
    }

    // ---------- 新增 t2n 题库全部可解（AC-18：≥18 新题全解 + 答对） ----------

    [Fact]
    public void 新增t2n题_全部被NA校验器正确解出()
    {
        var cases = GatewayVsLlmEvaluationService.GenTier2Questions()
            .Where(x => x.Id.StartsWith("t2n-"))
            .ToList();
        Assert.True(cases.Count >= 18, $"t2n 题数量 {cases.Count}，期望 ≥18");
        var fails = new List<string>();
        foreach (var c in cases)
        {
            var outcome = NaChoiceValidator.TryDecide(Req(c.Question));
            var expected = Letter(c.Answer);
            if (outcome is null || outcome.AnswerLetter != expected)
                fails.Add($"{c.Id}(期望 {expected}, 实际 {(outcome?.AnswerLetter ?? "null")})");
        }
        Assert.True(fails.Count == 0, "t2n 题未全对：\n" + string.Join("\n", fails));
    }

    // ---------- 真题诚实行为（不可解析 → null；若可解必对） ----------

    [Theory]
    [InlineData("pdf-026")]
    [InlineData("pdf-093")]
    [InlineData("pdf-421")]
    [InlineData("pdf-422")]
    [InlineData("pdf-423")]
    [InlineData("pdf-424")]
    [InlineData("pdf-425")]
    [InlineData("pdf-427")]
    [InlineData("pdf-419")]
    public void 真题NA题_诚实返回null或解必正确(string id)
    {
        var tc = GatewayVsLlmEvaluationService.GenPdfQuestions().FirstOrDefault(x => x.Id == id);
        Assert.NotNull(tc);
        var outcome = NaChoiceValidator.TryDecide(Req(tc!.Question));
        if (outcome is not null)
            Assert.Equal(Letter(tc.Answer), outcome.AnswerLetter);
    }

    // ---------- 全题库零假阳性护栏 ----------

    [Fact]
    public void Guardrail_全题库_NA校验器无假阳性()
    {
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        Assert.NotEmpty(cases);
        var solved = new List<string>();
        var mismatches = new List<string>();
        foreach (var c in cases)
        {
            var outcome = NaChoiceValidator.TryDecide(Req(c.Question));
            if (outcome is null) continue;
            var expected = Letter(c.Answer);
            if (expected.Length == 0)
            {
                mismatches.Add($"{c.Id} 校验器给出 {outcome.AnswerLetter}，但题库答案无可比对字母（答案='{c.Answer}'）");
                continue;
            }
            if (outcome.AnswerLetter != expected)
                mismatches.Add($"{c.Id} 校验器给出 {outcome.AnswerLetter}，题库答案 {expected}");
            else
                solved.Add($"{c.Id}->{outcome.AnswerLetter}");
        }
        Assert.True(mismatches.Count == 0, "存在假阳性（NA 校验器错选）：\n" + string.Join("\n", mismatches));
        Assert.True(solved.Count >= 1, $"仅解出 {solved.Count} 道：{string.Join(",", solved)}");
    }
}
