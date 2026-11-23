using System.Linq;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

public class PhOrderingSolverTests
{
    [Fact]
    public void Pdf275_真实题干_解出精确排序且通过答案键校验()
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-275");
        var r = PhOrderingSolver.TrySolve(c.Question);
        Assert.NotNull(r);
        Assert.True(r!.Ok == true, $"应产出结论，实际 Final={r.FinalResult}");
        // 答案键为单一精确符号串，输出须原样包含（带圈数字与 > 经归一化不被破坏）
        const string expected = "②>①>④>③>⑤>⑧>⑥>⑦";
        Assert.Contains(expected, r.FinalResult);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c),
            $"结论未通过 pdf-275 答案键校验：{r.FinalResult}");
    }

    [Fact]
    public void 合成题_由小到大_升序排序正确()
    {
        // ①硫酸(pH≈0.7) ②盐酸(pH=1) ③氢氧化钠(pH=13)，由小到大 → ①>②>③
        var q = "常温下，浓度均为 0.1 mol/L 的下列溶液，pH 由小到大的顺序为____。①硫酸 ②盐酸 ③氢氧化钠";
        var r = PhOrderingSolver.TrySolve(q);
        Assert.NotNull(r);
        Assert.Contains("①>②>③", r!.FinalResult);
    }

    [Fact]
    public void 域外_无pH语境_返回null_不触发()
    {
        Assert.Null(PhOrderingSolver.TrySolve(
            "常温下浓度均为 0.1 mol/L 的下列溶液，浓度由大到小的顺序为____。①盐酸 ②氢氧化钠"));
    }

    [Fact]
    public void 域外_物种不在表_返回null_不蒙数()
    {
        // 含 pH + 排序 + 带圈 + 浓度，但「苯酚钠」不在 ph-species 表 → null，不臆造排序
        Assert.Null(PhOrderingSolver.TrySolve(
            "常温下，浓度均为 0.1 mol/L 的下列溶液，pH 由大到小的顺序为____。①苯酚钠 ②醋酸钠"));
    }

    [Fact]
    public void 域外_多种浓度无法分配_返回null_不蒙数()
    {
        // 出现两个不同的 mol/L 浓度且无法逐物种对应 → null
        Assert.Null(PhOrderingSolver.TrySolve(
            "0.1 mol/L 盐酸与 0.2 mol/L 氢氧化钠等体积混合，pH 由大到小的顺序为____。①盐酸 ②氢氧化钠"));
    }
}
