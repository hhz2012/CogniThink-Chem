using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// batch3-B：碘量法测定 SO2 质量分数求解器（pdf-146 子问(5)）。
/// 红线：非 SO2 碘量法 / 缺任一数值 / 无含量问法 → null（不蒙数）。
/// 护栏：全库触发集锁定 [pdf-146]，baseFalse 凡触发必判正（0 假阳性）。
/// </summary>
public class IodometryMassFractionSolverTests
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
    public void Pdf146_碘量法SO2_含量0_032并判正()
    {
        var stem = LoadQuestion("pdf-146");
        var r = IodometryMassFractionSolver.TrySolve(stem);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("0.032", r.FinalResult);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult!, Case("pdf-146")),
            $"未判正: {r.FinalResult}");
    }

    [Fact]
    public void 合成_数字正确_给出0_032()
    {
        var text = "现将 1000 g 样品配制成 1000 mL 溶液，取出 50 mL，用 5×10^-3 mol·L^-1 的碘标准溶液滴定 SO2 含量，消耗碘标准溶液 5 mL，则样品中 SO2 的含量为____g/kg。";
        var r = IodometryMassFractionSolver.TrySolve(text);
        Assert.NotNull(r);
        Assert.Contains("0.032", r!.FinalResult);
    }

    [Fact]
    public void 无碘_返回null()
    {
        Assert.Null(IodometryMassFractionSolver.TrySolve(
            "用高锰酸钾滴定测定 SO2 含量，现将 1000 g 样品配制成 1000 mL，取出 50 mL，消耗 5 mL。"));
    }

    [Fact]
    public void 无SO2_返回null()
    {
        Assert.Null(IodometryMassFractionSolver.TrySolve(
            "现将 1000 g 样品配制成 1000 mL，取出 50 mL，用碘标准溶液滴定测定维生素 C 含量，消耗 5 mL。"));
    }

    [Fact]
    public void 缺浓度_返回null()
    {
        Assert.Null(IodometryMassFractionSolver.TrySolve(
            "现将 1000 g 样品配制成 1000 mL，取出 50 mL，用碘标准溶液滴定 SO2 含量，消耗 5 mL。"));
    }

    [Fact]
    public void 空串_返回null()
    {
        Assert.Null(IodometryMassFractionSolver.TrySolve(""));
        Assert.Null(IodometryMassFractionSolver.TrySolve(null!));
    }

    [Fact]
    public void 全题库_未解对题凡触发必判正_且触发集锁定Pdf146()
    {
        var baseline = LoadBaseline();
        Assert.NotEmpty(baseline);
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        var firedIds = new List<string>();
        foreach (var c in cases)
        {
            var r = IodometryMassFractionSolver.TrySolve(c.Question);
            if (r is null) continue;
            firedIds.Add(c.Id);
            if (baseline.TryGetValue(c.Id, out var baseCorrect) && !baseCorrect)
            {
                Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c),
                    $"{c.Id}（基线未解对）触发但未判正 → 假阳性风险: {r.FinalResult}");
            }
        }
        Assert.Equal(new[] { "pdf-146" }, firedIds.ToArray());
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
