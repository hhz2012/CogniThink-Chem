using System.Linq;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

public class CatalystSolverTests
{
    [Fact]
    public void Pdf175_真实题干_解出铜氨络离子且通过答案键校验()
    {
        // 真实题干两步循环：①消耗 [Cu(NH3)4]^2+、②再生，配平循环后净计量为 0 → 唯一催化剂。
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-175");
        var r = CatalystSolver.TrySolve(c.Question);
        Assert.NotNull(r);
        Assert.True(r!.Ok == true, $"应产出结论，实际 Final={r.FinalResult}");
        Assert.Contains("催化剂", r.FinalResult);
        // 答案键含 '[Cu(NH3)4]^2+'，输出物种须原样命中
        Assert.Contains("[Cu(NH3)4]^2+", r.FinalResult);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c),
            $"结论未通过 pdf-175 答案键校验：{r.FinalResult}");
    }

    [Fact]
    public void 合成题_两步循环_正确识别先消耗后再生的物种()
    {
        // ① 消耗 X、② 再生 X；Z 为中间体（先生成后消耗），应被催化剂判据淘汰。
        var q = "反应① X + Y === Z + M，反应② Z + Y === X + N。上述过程的催化剂为____。";
        var r = CatalystSolver.TrySolve(q);
        Assert.NotNull(r);
        Assert.Contains("催化剂", r!.FinalResult);
        Assert.Contains("X", r.FinalResult);
        // 中间体 Z 不应被判为催化剂（先右后左：minLeft=2 > maxRight=1）
        Assert.DoesNotContain("Z", r.FinalResult);
    }

    [Fact]
    public void 域外_无催化剂设问_返回null_不触发()
    {
        // 含「催化剂」但无「催化剂为/是/作催化剂」设问 → 不触发，避免抢占既有路径
        Assert.Null(CatalystSolver.TrySolve("该反应中二氧化锰是催化剂吗？请判断正误。"));
    }

    [Fact]
    public void 域外_非两步循环_返回null_不蒙数()
    {
        // 仅带圈 ①，缺 ② → steps.Count != 2 → 不臆造催化剂
        Assert.Null(CatalystSolver.TrySolve(
            "反应① A + B === C + D。上述过程中的催化剂为____。"));
    }

    [Fact]
    public void 域外_两步无共同物种_循环不可配平_返回null_不蒙数()
    {
        // 两步无任何跨步骤物种 → 无法推导循环比 → null，不蒙数
        var q = "反应① A + B === C，反应② D + E === F。上述过程的催化剂为____。";
        Assert.Null(CatalystSolver.TrySolve(q));
    }
}
