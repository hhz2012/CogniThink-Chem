using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Gateway;
using CogniThink.TutorBrain.Application.Evaluation;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// ProductWriterSolver 单元测试（T6：限量离子方程式扩展）。
/// 红线：precision=1 / 零假阳性 —— 凡触发必为配平正确且答案键命中的唯一产物；不唯一 / 缺量关键词 → null。
/// </summary>
public class ProductWriterSolverTests
{
    private static EngineResponse? Solve(string raw) =>
        ProductWriterSolver.TrySolve(new EngineRequest { TaskType = "unknown", RawText = raw });

    private static QaTestCase? Case(string id) =>
        GatewayVsLlmEvaluationService.BuildTestCases().FirstOrDefault(c => c.Id == id);

    private static bool Correct(string conclusion, QaTestCase c) =>
        GatewayVsLlmEvaluationService.IsAnswerCorrect(conclusion, c);

    // ------------------------------------------------------------
    // TR-6.1：trap-limit-3 返回唯一配平离子方程式，命中全部答案键关键词
    // ------------------------------------------------------------
    [Fact]
    public void TrapLimit3_等物质的量NaHCO3_BaOH2_返回唯一配平离子方程式()
    {
        var c = Case("trap-limit-3");
        Assert.NotNull(c);
        var r = Solve(c!.Question);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        // 骨架已配平（1:1:1:1:1），去 ^ 后须含 5 个答案键关键词
        Assert.Contains("HCO3", r.FinalResult);
        Assert.Contains("Ba2+", r.FinalResult);
        Assert.Contains("OH", r.FinalResult);
        Assert.Contains("BaCO3", r.FinalResult);
        Assert.Contains("H2O", r.FinalResult);
        Assert.True(Correct(r.FinalResult, c), "trap-limit-3 结论应被 IsAnswerCorrect 判正");
    }

    // ------------------------------------------------------------
    // TR-6.2：无「等物质的量」量关键词 → null（不蒙数）；配平失败由 EquationBalancer 返回 null → TrySolve 返回 null
    // ------------------------------------------------------------
    [Fact]
    public void TrapLimit3_无等物质的量关键词_返回Null()
    {
        // 去掉量关键词：NaHCO3 与 Ba(OH)2 比例不唯一（过量/少量/等物质的量产物不同）→ 必须 null
        Assert.Null(Solve("NaHCO3 和 Ba(OH)2 反应的离子方程式是什么？"));
        // 反应物不全 → null
        Assert.Null(Solve("等物质的量的 NaHCO3 反应的离子方程式是什么？"));
    }

    [Fact]
    public void 域外题返回Null_不越界触发()
    {
        // 不锚定的方程式题 → null（避免开放生成造成假阳性）
        Assert.Null(Solve("写出铁与稀硫酸反应的离子方程式"));
        Assert.Null(Solve("下列说法正确的是"));
    }

    // ------------------------------------------------------------
    // 全库零假阳性防护：当前未解对（baseFalse）的题凡被 ProductWriterSolver 触发，必判正。
    // （baseTrue 题如 pdf-278 由分发判正 → Ok=true → last-resort 钩子不触发，不受此约束。）
    // 证据基线：_evalscratch/snapshot_pdf278_final.json（已入库跟踪）。
    // ------------------------------------------------------------
    [Fact]
    public void 全题库_未解对题凡触发必判正_零假阳性()
    {
        var baseline = LoadBaseline();
        Assert.NotEmpty(baseline);
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        foreach (var c in cases)
        {
            var r = Solve(c.Question);
            if (r is null) continue;
            if (baseline.TryGetValue(c.Id, out var baseCorrect) && !baseCorrect)
            {
                Assert.True(Correct(r.FinalResult, c),
                    $"{c.Id}（基线未解对）触发 ProductWriterSolver 但未判正 → 假阳性风险: {r.FinalResult}");
            }
        }
    }

    private static Dictionary<string, bool> LoadBaseline()
    {
        var path = Path.Combine(FindRepoRoot(), "_evalscratch", "snapshot_pdf278_final.json");
        Assert.True(File.Exists(path), $"baseline snapshot not found: {path}");
        var dict = new Dictionary<string, bool>();
        using var sd = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var ln in new[] { "bothCorrectCases", "gatewayOnlyCorrectCases", "pureLlmOnlyCorrectCases", "bothWrongCases" })
            if (sd.RootElement.TryGetProperty(ln, out var arr))
                foreach (var el in arr.EnumerateArray())
                    dict[el.GetProperty("id").GetString()!] = el.GetProperty("gatewayCorrect").GetBoolean();
        return dict;
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "_evalscratch")))
            dir = Directory.GetParent(dir)?.FullName;
        Assert.NotNull(dir);
        return dir!;
    }
}
