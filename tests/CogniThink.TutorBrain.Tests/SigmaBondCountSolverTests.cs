using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// batch3-D：离子盐 σ 键数求解器（pdf-098 子问(1)）。
/// 红线：非 σ 键问法 / 共价分子无法拆为离子盐 / RDKit 失败 → null（不蒙数）。
/// 护栏：全库触发集锁定 [pdf-098]，baseFalse 凡触发必判正（0 假阳性）。
/// </summary>
public class SigmaBondCountSolverTests
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
    public void Pdf098_NH4Cl_σ键4mol并判正()
    {
        var stem = LoadQuestion("pdf-098");
        var r = SigmaBondCountSolver.TrySolve(stem);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("4 mol", r.FinalResult);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult!, Case("pdf-098")),
            $"未判正: {r.FinalResult}");
    }

    [Fact]
    public void 合成_NH4Cl_σ键4()
    {
        var r = SigmaBondCountSolver.TrySolve("1 mol NH4Cl 中含 σ 键的物质的量为______。");
        Assert.NotNull(r);
        Assert.Contains("4 mol", r!.FinalResult);
    }

    [Fact]
    public void 合成_NaCl_离子化合物无共价σ键_为0()
    {
        var r = SigmaBondCountSolver.TrySolve("1 mol NaCl 中含 σ 键的物质的量为______。");
        Assert.NotNull(r);
        Assert.Contains("0 mol", r!.FinalResult);
    }

    [Fact]
    public void 共价分子水_无法拆为离子盐_返回null()
    {
        Assert.Null(SigmaBondCountSolver.TrySolve("1 mol H2O 中含 σ 键的物质的量为______。"));
    }

    [Fact]
    public void 无σ键问法_返回null()
    {
        Assert.Null(SigmaBondCountSolver.TrySolve("1 mol NH4Cl 中含有的电子数为______。"));
    }

    [Fact]
    public void 空串_返回null()
    {
        Assert.Null(SigmaBondCountSolver.TrySolve(""));
        Assert.Null(SigmaBondCountSolver.TrySolve(null!));
    }

    [Fact]
    public void 全题库_未解对题凡触发必判正_且触发集锁定Pdf098()
    {
        var baseline = LoadBaseline();
        Assert.NotEmpty(baseline);
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        var firedIds = new List<string>();
        foreach (var c in cases)
        {
            var r = SigmaBondCountSolver.TrySolve(c.Question);
            if (r is null) continue;
            firedIds.Add(c.Id);
            if (baseline.TryGetValue(c.Id, out var baseCorrect) && !baseCorrect)
            {
                Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c),
                    $"{c.Id}（基线未解对）触发但未判正 → 假阳性风险: {r.FinalResult}");
            }
        }
        Assert.Equal(new[] { "pdf-098" }, firedIds.ToArray());
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
