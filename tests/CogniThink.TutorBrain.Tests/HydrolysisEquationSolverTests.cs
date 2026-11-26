using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// batch3-A：除杂语境水解方程求解器（pdf-274）。
/// 红线：缺「平衡原理」或缺除杂语境或无可水解阳离子 → null（不蒙数）。
/// 护栏：全库触发集锁定 [pdf-274]，baseFalse 凡触发必判正（0 假阳性）。
/// </summary>
public class HydrolysisEquationSolverTests
{
    private static string LoadQuestion(string id)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "pdf_questions.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var x in doc.RootElement.EnumerateArray())
            if (x.GetProperty("id").GetString() == id)
                return x.GetProperty("question").GetString() ?? "";
        throw new InvalidOperationException($"题库未找到 {id}");
    }

    private static QaTestCase Case(string id) =>
        GatewayVsLlmEvaluationService.BuildTestCases().First(c => c.Id == id);

    [Fact]
    public void Pdf274_除FeCl3_输出Fe3与Cu2水解方程并判正()
    {
        var stem = LoadQuestion("pdf-274");
        var r = HydrolysisEquationSolver.TrySolve(stem);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("Fe3+ + 3H2O", r.FinalResult);
        Assert.Contains("Cu2+ + 2H2O", r.FinalResult);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult!, Case("pdf-274")),
            $"未判正: {r.FinalResult}");
    }

    [Fact]
    public void 合成_平衡原理除杂_含可水解阳离子_输出方程()
    {
        var text = "除去 MgCl2 溶液中的 AlCl3，应该加入____，用平衡原理解释原因。";
        var r = HydrolysisEquationSolver.TrySolve(text);
        Assert.NotNull(r);
        Assert.Contains("Al3+ + 3H2O", r!.FinalResult);
        Assert.Contains("Mg2+ + 2H2O", r.FinalResult);
    }

    [Fact]
    public void 无平衡原理_返回null()
    {
        Assert.Null(HydrolysisEquationSolver.TrySolve("除去 CuCl2 中的 FeCl3，应该加入什么？"));
    }

    [Fact]
    public void 无除杂语境_返回null()
    {
        Assert.Null(HydrolysisEquationSolver.TrySolve(
            "用平衡原理解释为什么 FeCl3 溶液显酸性。"));
    }

    [Fact]
    public void 无可水解阳离子_返回null()
    {
        Assert.Null(HydrolysisEquationSolver.TrySolve(
            "除去 NaCl 溶液中的 Na2CO3，用平衡原理解释原因。"));
    }

    [Fact]
    public void 空串_返回null()
    {
        Assert.Null(HydrolysisEquationSolver.TrySolve(""));
        Assert.Null(HydrolysisEquationSolver.TrySolve(null!));
    }

    [Fact]
    public void 全题库_未解对题凡触发必判正_且触发集锁定Pdf274()
    {
        var baseline = LoadBaseline();
        Assert.NotEmpty(baseline);
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        var firedIds = new List<string>();
        foreach (var c in cases)
        {
            var r = HydrolysisEquationSolver.TrySolve(c.Question);
            if (r is null) continue;
            firedIds.Add(c.Id);
            if (baseline.TryGetValue(c.Id, out var baseCorrect) && !baseCorrect)
            {
                Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c),
                    $"{c.Id}（基线未解对）触发但未判正 → 假阳性风险: {r.FinalResult}");
            }
        }
        Assert.Equal(new[] { "pdf-274" }, firedIds.ToArray());
    }

    private static Dictionary<string, bool> LoadBaseline()
    {
        var path = Path.Combine(FindRepoRoot(), "_evalscratch", "snapshot_gap_batch3_base.json");
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
