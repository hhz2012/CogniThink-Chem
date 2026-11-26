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
/// AcidFirstIonizationSolver 单元测试（T7：pdf-272 子问(1) 弱酸第一步电离文本路径）。
/// 红线：precision=1 / 零假阳性 —— 仅「第一步电离/一级电离」+ 可解析酸分子式触发，输出 HₙA ⇌ Hₙ₋₁A⁻ + H⁺；
/// 缺触发词 / 无酸分子式 / 解析歧义 → null（不蒙数）。触发面极窄（全库仅 pdf-272 命中），last-resort 钩子构造性零 LOST。
/// </summary>
public class AcidFirstIonizationSolverTests
{
    private static QaTestCase? Case(string id) =>
        GatewayVsLlmEvaluationService.BuildTestCases().FirstOrDefault(c => c.Id == id);

    private static bool Correct(string? conclusion, QaTestCase c) =>
        GatewayVsLlmEvaluationService.IsAnswerCorrect(conclusion, c);

    // ------------------------------------------------------------
    // TR-7.1：pdf-272 子问(1) 第一步电离 → 唯一配平方程式，命中答案键关键词
    // ------------------------------------------------------------
    [Fact]
    public void Pdf272_草酸第一步电离_返回唯一配平方程式且判正()
    {
        var c = Case("pdf-272");
        Assert.NotNull(c);
        var r = AcidFirstIonizationSolver.TrySolve(c!.Question);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        // 第一步电离：H2C2O4 失去一个 H+ → HC2O4- + H+
        Assert.Equal("H2C2O4 ⇌ HC2O4- + H+", r.FinalResult);
        Assert.Contains("HC2O4- + H+", r.FinalResult);
        Assert.True(Correct(r.FinalResult, c), "pdf-272 第一步电离结论应被 IsAnswerCorrect（OR）判正");
    }

    // ------------------------------------------------------------
    // TR-7.2：通用酸第一步电离生成正确共轭碱（非单题硬编码）
    // ------------------------------------------------------------
    [Theory]
    [InlineData("写出 H2CO3 的第一步电离方程式", "H2CO3 ⇌ HCO3- + H+")]
    [InlineData("H3PO4 一级电离方程式", "H3PO4 ⇌ H2PO4- + H+")]
    [InlineData("HF 的第一步电离", "HF ⇌ F- + H+")]
    [InlineData("已知 H2SO3 是弱酸，写出其第一步电离方程式", "H2SO3 ⇌ HSO3- + H+")]
    public void 通用弱酸第一步电离_生成正确共轭碱(string raw, string expected)
    {
        var r = AcidFirstIonizationSolver.TrySolve(raw);
        Assert.NotNull(r);
        Assert.Equal(expected, r!.FinalResult);
    }

    [Fact]
    public void 域外或缺触发词_返回Null()
    {
        // 无「第一步电离/一级电离」触发词 → null
        Assert.Null(AcidFirstIonizationSolver.TrySolve("写出 H2C2O4 的电离方程式"));
        Assert.Null(AcidFirstIonizationSolver.TrySolve("下列说法正确的是"));
        // 触发词但无可解析酸分子式 → null（不蒙数）
        Assert.Null(AcidFirstIonizationSolver.TrySolve("写出草酸的第一步电离方程式"));
    }

    // ------------------------------------------------------------
    // 全库零假阳性防护：当前未解对（baseFalse）的题凡触发必判正；
    // 触发集被锁定在 [pdf-272]（基线 baseFalse，GAIN），不得出现其它误触发。
    // 证据基线：_evalscratch/snapshot_pdf278_final.json（已入库跟踪）。
    // ------------------------------------------------------------
    [Fact]
    public void 全题库_未解对题凡触发必判正_且触发集锁定Pdf272()
    {
        var baseline = LoadBaseline();
        Assert.NotEmpty(baseline);
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        var firedIds = new List<string>();
        foreach (var c in cases)
        {
            var r = AcidFirstIonizationSolver.TrySolve(c.Question);
            if (r is null) continue;
            firedIds.Add(c.Id);
            if (baseline.TryGetValue(c.Id, out var baseCorrect) && !baseCorrect)
            {
                Assert.True(Correct(r.FinalResult, c),
                    $"{c.Id}（基线未解对）触发但未判正 → 假阳性风险: {r.FinalResult}");
            }
        }
        // 触发面极窄：全库仅 pdf-272 命中（baseFalse → 预期 GAIN），无其它误触发
        Assert.Equal(new[] { "pdf-272" }, firedIds.ToArray());
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
