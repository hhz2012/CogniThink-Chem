using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// batch2-A：相对密度 → 平均摩尔质量求解器（pdf-024）。
/// 红线：缺「相对密度」/缺「摩尔质量」问法/数值不可解析/歧义 → null（不蒙数）。
/// </summary>
public class VaporDensityMolarMassSolverTests
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

    // ---- 正向 ----

    [Fact]
    public void Pdf024_对氢气相对密度9_5_摩尔质量19()
    {
        var stem = LoadQuestion("pdf-024");
        var r = VaporDensityMolarMassSolver.TrySolve(stem);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("19", r.FinalResult);
        Assert.Contains("平均摩尔质量", r.FinalResult ?? "");
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult!, BuildCase("pdf-024", stem)),
            $"未判正: {r.FinalResult}");
    }

    [Fact]
    public void 合成_对空气相对密度2_摩尔质量58()
    {
        var text = "某气体对空气的相对密度为 2，求该气体的平均摩尔质量为____g/mol。";
        var r = VaporDensityMolarMassSolver.TrySolve(text);
        Assert.NotNull(r);
        Assert.Contains("58", r!.FinalResult);
    }

    [Fact]
    public void 合成_对氢气相对密度14_摩尔质量28()
    {
        var text = "某气体对H2的相对密度为14，其摩尔质量为____g/mol。";
        var r = VaporDensityMolarMassSolver.TrySolve(text);
        Assert.NotNull(r);
        Assert.Contains("28", r!.FinalResult);
    }

    // ---- 域外/缺数据/歧义 → null ----

    [Fact]
    public void 无相对密度_返回null()
    {
        Assert.Null(VaporDensityMolarMassSolver.TrySolve("求某气体的平均摩尔质量为____g/mol。"));
    }

    [Fact]
    public void 无摩尔质量问法_返回null()
    {
        Assert.Null(VaporDensityMolarMassSolver.TrySolve("某气体对氢气的相对密度为9.5，求其密度。"));
    }

    [Fact]
    public void 歧义_两个不同相对密度_返回null()
    {
        var text = "某气体对氢气的相对密度为9.5，对空气的相对密度为3，求平均摩尔质量。";
        Assert.Null(VaporDensityMolarMassSolver.TrySolve(text));
    }

    [Fact]
    public void 空串_返回null()
    {
        Assert.Null(VaporDensityMolarMassSolver.TrySolve(""));
        Assert.Null(VaporDensityMolarMassSolver.TrySolve(null!));
    }

    // ---- 全库零假阳性防护：baseFalse 凡触发必判正；触发集锁定 [pdf-024]。
    //      证据基线：_evalscratch/snapshot_gap_batch1.json（pdf-024 为 pureLlmOnly，baseFalse）。
    [Fact]
    public void 全题库_未解对题凡触发必判正_且触发集锁定Pdf024()
    {
        var baseline = LoadBaseline();
        Assert.NotEmpty(baseline);
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        var firedIds = new List<string>();
        foreach (var c in cases)
        {
            var r = VaporDensityMolarMassSolver.TrySolve(c.Question);
            if (r is null) continue;
            firedIds.Add(c.Id);
            if (baseline.TryGetValue(c.Id, out var baseCorrect) && !baseCorrect)
            {
                Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c),
                    $"{c.Id}（基线未解对）触发但未判正 → 假阳性风险: {r.FinalResult}");
            }
        }
        Assert.Equal(new[] { "pdf-024" }, firedIds.ToArray());
    }

    private static QaTestCase BuildCase(string id, string question)
    {
        return GatewayVsLlmEvaluationService.BuildTestCases().First(c => c.Id == id);
    }

    private static Dictionary<string, bool> LoadBaseline()
    {
        var path = Path.Combine(FindRepoRoot(), "_evalscratch", "snapshot_gap_batch1.json");
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
