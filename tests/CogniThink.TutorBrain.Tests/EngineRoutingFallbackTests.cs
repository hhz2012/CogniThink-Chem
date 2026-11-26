// 路由兜底回归测试：锁定「已存在求解器因分支未调用而漏解」的布线修复。
// 与 VseprSolverTests 中的 Vsepr分支_键角原因题_兜底测试 同类：断言生产分支（非 concept/unknown）
// 也能兜底到对应求解器并判正；求解器自身的零假阳性由各 SolverTests 的 Guardrail 锁定。
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

public class EngineRoutingFallbackTests
{
    private static readonly List<QaTestCase> AllCases =
        GatewayVsLlmEvaluationService.BuildTestCases();

    private static QaTestCase? Case(string id) => AllCases.FirstOrDefault(x => x.Id == id);

    private static LlmGatewayService NewGateway() =>
        (LlmGatewayService)RuntimeHelpers.GetUninitializedObject(typeof(LlmGatewayService));

    private static bool Correct(string? a, QaTestCase c) =>
        !string.IsNullOrEmpty(a) && GatewayVsLlmEvaluationService.IsAnswerCorrect(a, c);

    // ============================================================
    // calculation 分支兜底：标况气体摩尔体积陷阱（d5-1 / d5-2）。
    // 生产中这两题被归为 calculation，但 calculation 分支原不调用 StoichiometryStateTrapSolver → 漏解。
    // 求解器内置守卫（全库仅这两题命中且判正，wrong=0），不命中返回 null → 不抢占既有数值计算。
    // ============================================================
    [Theory]
    [InlineData("d5-1")]
    [InlineData("d5-2")]
    public async Task Calculation分支_标况非气态陷阱题_兜底判正(string id)
    {
        var c = Case(id);
        Assert.NotNull(c);
        var req = new EngineRequest { TaskType = "calculation", RawText = c!.Question };
        var resp = await NewGateway().CallEngineAsync(req, c.ImageAssets);
        Assert.NotNull(resp);
        Assert.True(resp!.Ok == true, $"{id} 应经 trap 兜底判正，got={resp.FinalResult}");
        Assert.True(Correct(resp.FinalResult, c), $"{id} 兜底结论应通过判分；got={resp.FinalResult}");
    }

    [Fact]
    public async Task Calculation分支_非陷阱题_不被trap兜底抢占()
    {
        // 普通计算题（非标况22.4非气态陷阱）：trap 不触发，分支走原数值计算路径，不应返回 trap 结论。
        var req = new EngineRequest { TaskType = "calculation", RawText = "2 mol H2O 的质量是多少克？" };
        var resp = await NewGateway().CallEngineAsync(req, new List<ImageAsset>());
        // 无论数值计算是否命中，都不得是 trap 的标况非气态否定句式
        Assert.DoesNotContain("22.4", resp?.FinalResult ?? "");
    }

    // ============================================================
    // default 分支兜底：概念事实求解（d7-4，structure_verify）。
    // 生产中 d7-4 被归为 structure_verify，无专属 case → 落 default 返回 null → 漏解。
    // ConceptFactSolver 内置守卫（全库 35 命中均判正、wrong=0），不命中返回 null → 零回归。
    // ============================================================
    [Fact]
    public async Task Default分支_structureVerify纯事实题_兜底ConceptFact判正()
    {
        var c = Case("d7-4");
        Assert.NotNull(c);
        var req = new EngineRequest { TaskType = "structure_verify", RawText = c!.Question };
        var resp = await NewGateway().CallEngineAsync(req, c.ImageAssets);
        Assert.NotNull(resp);
        Assert.True(resp!.Ok == true, $"d7-4 应经 concept_fact 兜底判正，got={resp.FinalResult}");
        Assert.Contains("手性", resp.FinalResult);
        Assert.True(Correct(resp.FinalResult, c), $"d7-4 兜底结论应通过判分；got={resp.FinalResult}");
    }

    [Fact]
    public async Task Default分支_非事实题_仍返回null()
    {
        // 未建模类型且无概念事实命中 → 维持原行为（Ok=null，交由 LLM），不得误采纳。
        var req = new EngineRequest { TaskType = "structure_verify", RawText = "这是一道没有任何确定性事实命中的开放题。" };
        var resp = await NewGateway().CallEngineAsync(req, new List<ImageAsset>());
        Assert.NotNull(resp);
        Assert.False(resp!.Ok == true, "无命中时 default 不应返回 Ok=true");
    }

    // ============================================================
    // T1：equilibrium 分支兜底——部分题被 LLM 误归 equilibrium，但实为 STP 非气态陷阱
    // （d5-1/d5-2）或纯概念事实（d7-4 手性碳）。主平衡引擎失败后兜底到 trap / ConceptFact，
    // 不抢占已判正的 equilibrium 结论；命中走 IsAnswerCorrect 采纳门，错误不采纳 → 零 harm。
    // ============================================================
    [Theory]
    [InlineData("d5-1")]
    [InlineData("d5-2")]
    public async Task Equilibrium分支_误归的标况陷阱题_兜底判正(string id)
    {
        var c = Case(id);
        Assert.NotNull(c);
        var req = new EngineRequest { TaskType = "equilibrium", RawText = c!.Question };
        var resp = await NewGateway().CallEngineAsync(req, c.ImageAssets);
        Assert.NotNull(resp);
        Assert.True(resp!.Ok == true, $"{id} 在 equilibrium 分支应经 trap 兜底判正，got={resp.FinalResult}");
        Assert.True(Correct(resp.FinalResult, c), $"{id} 兜底结论应通过判分；got={resp.FinalResult}");
    }

    [Fact]
    public async Task Equilibrium分支_误归的概念事实题_兜底判正()
    {
        var c = Case("d7-4");
        Assert.NotNull(c);
        var req = new EngineRequest { TaskType = "equilibrium", RawText = c!.Question };
        var resp = await NewGateway().CallEngineAsync(req, c.ImageAssets);
        Assert.NotNull(resp);
        Assert.True(resp!.Ok == true, $"d7-4 在 equilibrium 分支应经 concept_fact 兜底判正，got={resp.FinalResult}");
        Assert.Contains("手性", resp.FinalResult);
        Assert.True(Correct(resp.FinalResult, c), $"d7-4 兜底结论应通过判分；got={resp.FinalResult}");
    }

    [Fact]
    public async Task Equilibrium分支_主平衡已解题_不被兜底抢占()
    {
        // 真实平衡题若主引擎命中（Ok=true）应在兜底之前返回，不得被 trap/conceptfact 覆盖。
        var req = new EngineRequest
        {
            TaskType = "equilibrium",
            RawText = "25 ℃，在 pH=5 的稀醋酸溶液中，c(CH3COO⁻) 的精确值表示式"
        };
        var resp = await NewGateway().CallEngineAsync(req, new List<ImageAsset>());
        Assert.NotNull(resp);
        Assert.DoesNotContain("22.4", resp!.FinalResult ?? "");
        Assert.DoesNotContain("手性", resp.FinalResult ?? "");
    }
}
