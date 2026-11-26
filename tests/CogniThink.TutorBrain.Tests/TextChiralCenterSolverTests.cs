using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// batch2-B：文本结构简式 → 手性碳原子数求解器（pdf-169）。
/// 红线：无「手性碳」/无计数问法/无可解析结构简式/互斥 → null（不蒙数）。
/// </summary>
public class TextChiralCenterSolverTests
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
    public void Pdf169_酒石酸_手性碳数目2()
    {
        var stem = LoadQuestion("pdf-169");
        var r = TextChiralCenterSolver.TrySolve(stem);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("2", r.FinalResult);
        Assert.Contains("手性碳", r.FinalResult ?? "");
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult!, BuildCase("pdf-169")),
            $"未判正: {r.FinalResult}");
    }

    [Fact]
    public void 合成_酒石酸缩合式_手性碳2()
    {
        var text = "某有机物结构简式为 HOOC(CHOH)2COOH，其分子中手性碳原子数目为____。";
        var r = TextChiralCenterSolver.TrySolve(text);
        Assert.NotNull(r);
        Assert.Contains("2", r!.FinalResult);
    }

    [Fact]
    public void 合成_草酸_手性碳0()
    {
        var text = "某酸结构简式为 HOOCCOOH，分子中手性碳原子数目为____。";
        var r = TextChiralCenterSolver.TrySolve(text);
        Assert.NotNull(r);
        Assert.Contains("0", r!.FinalResult);
    }

    // ---- 域外/缺数据/歧义 → null ----

    [Fact]
    public void 无手性碳问法_返回null()
    {
        Assert.Null(TextChiralCenterSolver.TrySolve("某有机物结构简式为 HOOC(CHOH)2COOH，求分子式。"));
    }

    [Fact]
    public void 无计数问法_返回null()
    {
        Assert.Null(TextChiralCenterSolver.TrySolve("某有机物结构简式为 HOOC(CHOH)2COOH，判断是否有手性碳。"));
    }

    [Fact]
    public void 无可解析结构简式_返回null()
    {
        Assert.Null(TextChiralCenterSolver.TrySolve("某有机物（结构见图），手性碳原子数目为____。"));
    }

    [Fact]
    public void 空串_返回null()
    {
        Assert.Null(TextChiralCenterSolver.TrySolve(""));
        Assert.Null(TextChiralCenterSolver.TrySolve(null!));
    }

    // ---- 全库零假阳性防护：baseFalse 凡触发必判正；触发集锁定 [pdf-169]。
    //      证据基线：_evalscratch/snapshot_gap_batch1.json（pdf-169 为 pureLlmOnly，baseFalse）。
    [Fact]
    public void 全题库_未解对题凡触发必判正_且触发集锁定Pdf169()
    {
        var baseline = LoadBaseline();
        Assert.NotEmpty(baseline);
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        var firedIds = new List<string>();
        foreach (var c in cases)
        {
            var r = TextChiralCenterSolver.TrySolve(c.Question);
            if (r is null) continue;
            firedIds.Add(c.Id);
            if (baseline.TryGetValue(c.Id, out var baseCorrect) && !baseCorrect)
            {
                Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c),
                    $"{c.Id}（基线未解对）触发但未判正 → 假阳性风险: {r.FinalResult}");
            }
        }
        Assert.Equal(new[] { "pdf-169" }, firedIds.ToArray());
    }

    private static QaTestCase BuildCase(string id)
        => GatewayVsLlmEvaluationService.BuildTestCases().First(c => c.Id == id);

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
