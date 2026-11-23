using System.Globalization;
using System.Linq;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

public class BondEnergySolverTests
{
    [Fact]
    public void Pdf244_真实题干_解出664_75且通过答案键校验()
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-244");
        var r = BondEnergySolver.TrySolve(c.Question);
        Assert.NotNull(r);
        Assert.True(r!.Ok == true, $"应产出结论，实际 Final={r.FinalResult}");
        Assert.Contains("664.75", r.FinalResult);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c),
            $"结论未通过 pdf-244 答案键校验：{r.FinalResult}");
    }

    [Fact]
    public void 域外_d10_3键能对吗判断_不触发_不干扰既有判正题()
    {
        // d10-3 为 judgment 题（"对吗"），无 kJ/mol 单位、无待求空白 → 必须返回 null，
        // 否则会抢占其当前判正路径。这是零回归红线 guardrail。
        Assert.Null(BondEnergySolver.TrySolve("键能计算反应热：ΔH = 反应物总键能 - 生成物总键能，对吗？"));
    }

    [Fact]
    public void 域外_无键焓语境_返回null()
    {
        Assert.Null(BondEnergySolver.TrySolve("18 g 水中含有多少 mol 电子？"));
        // 含反应与 ΔH 但无「键焓/键能」待求 → 不触发
        Assert.Null(BondEnergySolver.TrySolve("CO(g)+1/2 O2(g)=CO2(g) ΔH=-283 kJ/mol，求该反应的反应热"));
    }

    [Fact]
    public void 域外_目标分子不在键结构表_返回null_不蒙数()
    {
        // 目标分子 / 反应物种缺键结构数据 → 无法计量 → null，不臆造键焓
        var q = "已知 H-H、Cl-Cl 的键焓分别为 436 kJ/mol、243 kJ/mol，XYZ 分子中 X-Y 键的键焓为____ kJ/mol。H2+Cl2=2XYZ ΔH=-184 kJ/mol。";
        Assert.Null(BondEnergySolver.TrySolve(q));
    }

    [Theory]
    [InlineData("键焓", 664.75)]
    [InlineData("键能", 664.75)]
    public void 合成题_键焓反推_数学正确(string cue, double expected)
    {
        // 与 pdf-244 同构：-283 = (799 + 0.5×495) − 2x → x = 664.75
        var q = "已知 O=O、C≡O 的" + cue + "分别为 495 kJ/mol、799 kJ/mol，CO2 分子中碳氧键的" +
                cue + "为____ kJ/mol。CO(g)+1/2 O2(g)=CO2(g) ΔH=-283 kJ/mol。";
        var r = BondEnergySolver.TrySolve(q);
        Assert.NotNull(r);
        Assert.Contains(expected.ToString("F2", CultureInfo.InvariantCulture), r!.FinalResult);
    }
}
