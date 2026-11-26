using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// batch3-C：乙二酸与乙醇 1:2 酯化方程求解器（pdf-355）。
/// 红线：非酯化 / 非乙二酸+乙醇+1:2 → null（不蒙数）。
/// 护栏：全库触发集锁定 [pdf-355]，baseFalse 凡触发必判正（0 假阳性）。
/// </summary>
public class EsterificationEquationSolverTests
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
    public void Pdf355_乙二酸乙醇1比2_输出二酯方程并判正()
    {
        var stem = LoadQuestion("pdf-355");
        var r = EsterificationEquationSolver.TrySolve(stem);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("CH3CH2OOC-COOCH2CH3", r.FinalResult);
        Assert.Contains("2H2O", r.FinalResult);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult!, Case("pdf-355")),
            $"未判正: {r.FinalResult}");
    }

    [Fact]
    public void 合成_乙二酸乙醇1比2_命中产物()
    {
        var text = "书写酯化反应方程式：乙二酸与乙醇的物质的量之比为 1：2。";
        var r = EsterificationEquationSolver.TrySolve(text);
        Assert.NotNull(r);
        Assert.Contains("CH3CH2OOC-COOCH2CH3", r!.FinalResult);
    }

    [Fact]
    public void 无酯化_返回null()
    {
        Assert.Null(EsterificationEquationSolver.TrySolve(
            "乙二酸与乙醇的物质的量之比为1:2，求生成水的质量。"));
    }

    [Fact]
    public void 非1比2_返回null()
    {
        Assert.Null(EsterificationEquationSolver.TrySolve(
            "书写酯化反应方程式：乙二酸与乙醇的物质的量之比为1:1。"));
    }

    [Fact]
    public void 无乙二酸_返回null()
    {
        Assert.Null(EsterificationEquationSolver.TrySolve(
            "书写酯化反应方程式：乙酸与乙醇的物质的量之比为1:2。"));
    }

    [Fact]
    public void 空串_返回null()
    {
        Assert.Null(EsterificationEquationSolver.TrySolve(""));
        Assert.Null(EsterificationEquationSolver.TrySolve(null!));
    }

    [Fact]
    public void 全题库_未解对题凡触发必判正_且触发集锁定Pdf355()
    {
        var baseline = LoadBaseline();
        Assert.NotEmpty(baseline);
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        var firedIds = new List<string>();
        foreach (var c in cases)
        {
            var r = EsterificationEquationSolver.TrySolve(c.Question);
            if (r is null) continue;
            firedIds.Add(c.Id);
            if (baseline.TryGetValue(c.Id, out var baseCorrect) && !baseCorrect)
            {
                Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c),
                    $"{c.Id}（基线未解对）触发但未判正 → 假阳性风险: {r.FinalResult}");
            }
        }
        Assert.Equal(new[] { "pdf-355" }, firedIds.ToArray());
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
