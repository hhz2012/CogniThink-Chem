using System.Linq;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

public class Carbonyl18ElectronSolverTests
{
    [Fact]
    public void Pdf228_真实题干_解出5和10且通过答案键校验()
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-228");
        var r = Carbonyl18ElectronSolver.TrySolve(c.Question);
        Assert.NotNull(r);
        Assert.True(r!.Ok == true, $"应产出结论，实际 Final={r.FinalResult}");
        Assert.Contains("5 10", r.FinalResult);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c),
            $"结论未通过 pdf-228 答案键校验：{r.FinalResult}");
    }

    [Theory]
    [InlineData("Ni", 4)]   // Ni 价电子 10 → n=(18-10)/2=4
    [InlineData("Cr", 6)]   // Cr 价电子 6  → n=(18-6)/2=6
    [InlineData("Fe", 5)]   // Fe 价电子 8  → n=(18-8)/2=5
    public void 常见羰基金属_n符合18电子规则(string metal, int expectedN)
    {
        var q = $"金属羰基配合物 {metal}(CO)n 的中心原子价层电子数与配体提供电子数之和为 18，则 n = ______。";
        var r = Carbonyl18ElectronSolver.TrySolve(q);
        Assert.NotNull(r);
        Assert.Contains($"n=(18-", r!.FinalResult);
        Assert.Contains($"/2={expectedN}", r.FinalResult);
    }

    [Fact]
    public void 域外_无羰基通式_返回null()
    {
        Assert.Null(Carbonyl18ElectronSolver.TrySolve("18 g 水中含有多少 mol 电子？"));
        Assert.Null(Carbonyl18ElectronSolver.TrySolve("Fe + Cl2 = FeCl3，求转移电子数"));
    }

    [Fact]
    public void 域外_含18但非18电子规则语境_返回null()
    {
        // 含「18」与「电子」但无 M(CO)n 通式 → 不触发
        Assert.Null(Carbonyl18ElectronSolver.TrySolve("18 电子的微粒有哪些？请写出三种"));
    }
}
