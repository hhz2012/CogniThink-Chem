using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// T3：ΔG 自发性求解器单测。锁定窄触发守卫（缺数据/歧义/多部分/非自发 → null）与三分支兜底可达性。
/// 红线：precision=1，求解器只在可唯一判定时输出；错误结论不被采纳门采纳 → 零 harm。
/// </summary>
public class GibbsSpontaneitySolverTests
{
    // 自包含的 pdf-262 子问1 语境（共享数据 + 子问问法）。ΔG = 74.6 − 1000×80.84/1000 = −6.24 < 0 → 能。
    private const string Pdf262Sub1SelfContained =
        "反应ⅰ. CH4(g)→C(s)+2H2(g)  ΔH1＝＋74.6 kJ/mol  ΔS＝＋80.84 J/(mol·K)，在1000 K时______（填“能”或“不能”）自发进行。";

    private static LlmGatewayService NewGateway() =>
        (LlmGatewayService)RuntimeHelpers.GetUninitializedObject(typeof(LlmGatewayService));

    // ---- TR-3.1：pdf-262 子问1 → 能 ----
    [Fact]
    public void Solver_Pdf262Sub1_Returns能()
    {
        var r = GibbsSpontaneitySolver.TrySolve(Pdf262Sub1SelfContained);
        Assert.NotNull(r);
        Assert.True(r!.Ok == true);
        Assert.StartsWith("能", r.FinalResult); // 结论在开头，避免“不能”含“能”子串的歧义
        Assert.Contains("-6.24", r.FinalResult); // ΔG 数值（ToString 输出半角负号）
    }

    [Theory]
    [InlineData("＋74.6", "＋80.84", "1000", true)]   // ΔG = −6.24 → 能
    [InlineData("＋200", "＋80.84", "1000", false)]   // ΔG = +119.16 → 不能
    [InlineData("＋80.84", "＋80.84", "1000", false)] // ΔG = 0 → 平衡（非“能”）
    public void Solver_VerdictMatchesDeltaG(string dh, string ds, string t, bool expect能)
    {
        var q = $"反应 A→B ΔH＝{dh} kJ/mol ΔS＝{ds} J/(mol·K)，在{t} K时能否自发进行？";
        var r = GibbsSpontaneitySolver.TrySolve(q);
        Assert.NotNull(r);
        // 结论在开头：能 / 不能 / 平衡；用 StartsWith 精确区分（“不能”含“能”子串）
        if (expect能) Assert.StartsWith("能", r!.FinalResult);
        else Assert.False(r!.FinalResult.StartsWith("能"));
    }

    [Fact]
    public void Solver_DeltaGZero_Returns平衡()
    {
        var q = "反应 A→B ΔH＝＋80.84 kJ/mol ΔS＝＋80.84 J/(mol·K)，在1000 K时自发进行的方向？";
        var r = GibbsSpontaneitySolver.TrySolve(q);
        Assert.NotNull(r);
        Assert.StartsWith("平衡", r!.FinalResult);
    }

    // ---- TR-3.2：域外/缺数据/歧义 → null ----
    [Fact]
    public void Solver_MissingDeltaS_ReturnsNull()
    {
        var q = "反应 A→B ΔH＝＋74.6 kJ/mol，在1000 K时能否自发进行？";
        Assert.Null(GibbsSpontaneitySolver.TrySolve(q));
    }

    [Fact]
    public void Solver_MissingTemperature_ReturnsNull()
    {
        var q = "反应 A→B ΔH＝＋74.6 kJ/mol ΔS＝＋80.84 J/(mol·K)，能否自发进行？";
        Assert.Null(GibbsSpontaneitySolver.TrySolve(q));
    }

    [Fact]
    public void Solver_MultipleDeltaH_Ambiguous_ReturnsNull()
    {
        var q = "反应ⅰ ΔH1＝＋74.6 kJ/mol；反应ⅱ ΔH2＝＋100 kJ/mol；ΔS＝＋80.84 J/(mol·K)，在1000 K时能否自发？";
        Assert.Null(GibbsSpontaneitySolver.TrySolve(q));
    }

    [Fact]
    public void Solver_NonSpontaneityQuestion_ReturnsNull()
    {
        Assert.Null(GibbsSpontaneitySolver.TrySolve("2 mol H2O 的质量是多少克？"));
        Assert.Null(GibbsSpontaneitySolver.TrySolve(""));
    }

    [Fact]
    public void Solver_MultiPartWholeQuestion_ReturnsNull_DoesNotPreemptDispatch()
    {
        // 多部分整题（含共享数据）必须返回 null，交由分发逐子问求解，避免抢占分发链路。
        var mp = "(1)反应ⅰ在1000 K时自发进行吗？ΔH＝＋74.6 kJ/mol ΔS＝＋80.84 J/(mol·K) (2)另一问";
        Assert.Null(GibbsSpontaneitySolver.TrySolve(mp));
    }

    [Fact]
    public void Solver_SubWithoutSharedData_ReturnsNull_Honest()
    {
        // 模拟分发传入的子问文本（(N) 标记后片段，不含共享前缀的 ΔH/ΔS）→ 缺数据 → null（诚实，不蒙数）。
        var subOnly = "反应ⅰ在1000 K时______（填“能”或“不能”）自发进行。";
        Assert.Null(GibbsSpontaneitySolver.TrySolve(subOnly));
    }

    // ---- 三分支兜底可达性（主求解器失败后的 null 路径）----
    [Theory]
    [InlineData("calculation")]
    [InlineData("equilibrium")]
    [InlineData("structure_verify")] // → default 分支
    public async Task Routing_各分支_自包含自发题_兜底判正(string taskType)
    {
        var req = new EngineRequest { TaskType = taskType, RawText = Pdf262Sub1SelfContained };
        var resp = await NewGateway().CallEngineAsync(req, new List<ImageAsset>());
        Assert.NotNull(resp);
        Assert.True(resp!.Ok == true, $"{taskType} 分支应兜底 ΔG 判正，got={resp.FinalResult}");
        Assert.StartsWith("能", resp.FinalResult); // 结论在开头，精确区分“能/不能”
    }

    [Fact]
    public async Task Routing_Calculation分支_非自发题_不被ΔG抢占()
    {
        var req = new EngineRequest { TaskType = "calculation", RawText = "2 mol H2O 的质量是多少克？" };
        var resp = await NewGateway().CallEngineAsync(req, new List<ImageAsset>());
        // 不得输出 ΔG 自发性结论
        Assert.DoesNotContain("ΔG", resp?.FinalResult ?? "");
    }
}
