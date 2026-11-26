using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// batch4：酯化反应产物求解器（pdf-356 / pdf-526 / pdf-527）。
/// 红线：非酯化 / 缺对应反应物名 / 非该组合 → null（不蒙数）。
/// 护栏：全库触发集锁定 {pdf-356, pdf-526, pdf-527}，baseFalse 凡触发必判正（0 假阳性）。
/// 基线：_evalscratch/snapshot_gap_batch3.json（batch3 交付后的当前生产态）。
/// </summary>
public class EsterificationProductSolverTests
{
    private static QaTestCase Case(string id) =>
        GatewayVsLlmEvaluationService.BuildTestCases().First(c => c.Id == id);

    [Fact]
    public void Pdf356_乙二酸乙二醇_环酯与聚酯均判正()
    {
        var c = Case("pdf-356");
        var r = EsterificationProductSolver.TrySolve(c.Question);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("C4H4O4", r.FinalResult);
        Assert.Contains("(C4H2O3)n + (2n-1)H2O", r.FinalResult);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult!, c),
            $"未判正: {r.FinalResult}");
    }

    [Fact]
    public void Pdf526_乳酸自身酯化_六元环酯判正()
    {
        var c = Case("pdf-526");
        var r = EsterificationProductSolver.TrySolve(c.Question);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("2CH3CH(OH)COOH", r.FinalResult);
        Assert.Contains("CH3CH(O)COOCH(CH3)CO", r.FinalResult);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult!, c),
            $"未判正: {r.FinalResult}");
    }

    [Fact]
    public void Pdf527_乳酸自身酯化_聚酯判正()
    {
        var c = Case("pdf-527");
        var r = EsterificationProductSolver.TrySolve(c.Question);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("[−OCH(CH3)CO−]_n", r.FinalResult);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult!, c),
            $"未判正: {r.FinalResult}");
    }

    [Theory]
    [InlineData("乙酸与乙二醇 1:1 酯化生成 CH3COOCH2CH2OH + H2O")]          // 缺乙二酸 → null（避免覆盖 pdf-354）
    [InlineData("乙二酸与乙醇 1:2 酯化")]                                      // 缺乙二醇 → null（pdf-355 由 batch3 solver 处理）
    [InlineData("乳酸发生氧化反应生成丙酮酸")]                                  // 非酯化问法 → null
    [InlineData("羟基酸自身酯化生成链状酯")]                                    // 无具体酸名且无六元环酯/聚酯 → null
    [InlineData("NaSbO3 是合成聚酯的催化剂")]                                  // 无酯化/反应物 → null
    public void 域外_返回null(string text)
    {
        Assert.Null(EsterificationProductSolver.TrySolve(text));
    }

    [Fact]
    public void 空串_返回null()
    {
        Assert.Null(EsterificationProductSolver.TrySolve(""));
        Assert.Null(EsterificationProductSolver.TrySolve(null!));
    }

    [Fact]
    public void 全题库_触发集锁定356_526_527_且baseFalse凡触发必判正()
    {
        var baseline = LoadBaseline();
        Assert.NotEmpty(baseline);
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        var fired = new HashSet<string>();
        foreach (var c in cases)
        {
            var r = EsterificationProductSolver.TrySolve(c.Question);
            if (r is null) continue;
            Assert.True(fired.Add(c.Id), $"{c.Id} 重复触发");
            // baseFalse（含 3 个目标）：触发输出必须被判正，否则即为假阳性/错采纳风险
            if (baseline.TryGetValue(c.Id, out var baseCorrect) && !baseCorrect)
            {
                Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c),
                    $"{c.Id}（基线未解对）触发但未判正 → 假阳性风险: {r.FinalResult}");
            }
        }
        Assert.Equal(new HashSet<string> { "pdf-356", "pdf-526", "pdf-527" }, fired);
    }

    private static Dictionary<string, bool> LoadBaseline()
    {
        var path = Path.Combine(FindRepoRoot(), "_evalscratch", "snapshot_gap_batch3.json");
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
