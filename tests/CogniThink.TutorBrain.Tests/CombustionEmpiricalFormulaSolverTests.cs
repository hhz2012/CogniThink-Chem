using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Gateway;
using CogniThink.TutorBrain.Application.Evaluation;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// CombustionEmpiricalFormulaSolver 单元测试（P2-2：燃烧法测实验式 pdf-328）。
/// 红线：precision=1 / 零假阳性 —— 仅「分子式」+ 燃烧数据齐全（样品质量 + CO₂/H₂O 量）触发，
/// 输出唯一可化整的实验式；缺触发词 / 缺数据 / 氧质量矛盾 / 无法化整 → null（不蒙数）。
/// </summary>
public class CombustionEmpiricalFormulaSolverTests
{
    private static QaTestCase? Case(string id) =>
        GatewayVsLlmEvaluationService.BuildTestCases().FirstOrDefault(c => c.Id == id);

    private static bool Correct(string? conclusion, QaTestCase c) =>
        GatewayVsLlmEvaluationService.IsAnswerCorrect(conclusion, c);

    // ------------------------------------------------------------
    // pdf-328：燃烧数据凭文本算实验式 C9H8O2，命中答案键
    // ------------------------------------------------------------
    [Fact]
    public void Pdf328_燃烧法_返回C9H8O2且判正()
    {
        var c = Case("pdf-328");
        Assert.NotNull(c);
        var r = CombustionEmpiricalFormulaSolver.TrySolve(c!.Question);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("C9H8O2", r.FinalResult);
        Assert.True(Correct(r.FinalResult, c), "pdf-328 结论应被 IsAnswerCorrect（OR）判正");
    }

    // ------------------------------------------------------------
    // 通用：烃（无氧）/ 含氧有机物 实验式计算（非单题硬编码）
    // ------------------------------------------------------------
    [Theory]
    // 烃：1.4 g 燃烧 → 0.1 mol CO2 + 0.1 mol H2O → n(C):n(H)=1:2 → CH2
    [InlineData("1.4 g 某烃完全燃烧生成 0.1 mol CO2 和 0.1 mol H2O，求分子式", "CH2")]
    // 含氧：3.0 g 燃烧 → 0.1 mol CO2 + 0.1 mol H2O → m(O)=1.6 g → n(C):n(H):n(O)=1:2:1 → CH2O
    [InlineData("3.0 g 某有机物完全燃烧生成 0.1 mol CO2 和 0.1 mol H2O，求分子式", "CH2O")]
    // 半整数比需 ×2：0.18/0.16/0.04 → 9:8:2（pdf-328 数据的合成复现）
    [InlineData("2.96 g 化合物完全燃烧生成 0.18 mol CO2 和 0.08 mol H2O，求分子式", "C9H8O2")]
    public void 通用燃烧数据_计算正确实验式(string raw, string expectedFormula)
    {
        var r = CombustionEmpiricalFormulaSolver.TrySolve(raw);
        Assert.NotNull(r);
        Assert.Contains(expectedFormula, r!.FinalResult);
    }

    [Fact]
    public void 域外或数据矛盾_返回Null()
    {
        // 无「分子式」触发词 → null
        Assert.Null(CombustionEmpiricalFormulaSolver.TrySolve("1.4 g 某烃完全燃烧生成 0.1 mol CO2 和 0.1 mol H2O"));
        // 缺 H2O 数据 → null
        Assert.Null(CombustionEmpiricalFormulaSolver.TrySolve("1.4 g 某烃完全燃烧生成 0.1 mol CO2，求分子式"));
        // 缺样品质量 → null
        Assert.Null(CombustionEmpiricalFormulaSolver.TrySolve("某烃完全燃烧生成 0.1 mol CO2 和 0.1 mol H2O，求分子式"));
        // 氧质量为负（数据矛盾：C+H 质量已超样品）→ null，不蒙数
        Assert.Null(CombustionEmpiricalFormulaSolver.TrySolve("1.0 g 某烃完全燃烧生成 1.0 mol CO2 和 1.0 mol H2O，求分子式"));
    }

    // ------------------------------------------------------------
    // 全库零假阳性防护：未解对（baseFalse）题凡触发必判正；触发集锁定 [pdf-328]。
    // 证据基线：_evalscratch/snapshot_pdf278_final.json（已入库跟踪）。
    // ------------------------------------------------------------
    [Fact]
    public void 全题库_未解对题凡触发必判正_且触发集锁定Pdf328()
    {
        var baseline = LoadBaseline();
        Assert.NotEmpty(baseline);
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        var firedIds = new List<string>();
        foreach (var c in cases)
        {
            var r = CombustionEmpiricalFormulaSolver.TrySolve(c.Question);
            if (r is null) continue;
            firedIds.Add(c.Id);
            if (baseline.TryGetValue(c.Id, out var baseCorrect) && !baseCorrect)
            {
                Assert.True(Correct(r.FinalResult, c),
                    $"{c.Id}（基线未解对）触发但未判正 → 假阳性风险: {r.FinalResult}");
            }
        }
        Assert.Equal(new[] { "pdf-328" }, firedIds.ToArray());
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
