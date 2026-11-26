using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// T4：Ksp-pH → 残留金属离子浓度求解器（P0-2）。
/// 红线：域外/缺数据/歧义一律 null（不蒙数）；命中结论以数值开头便于答案键命中。
/// </summary>
public class KspPhResidualSolverTests
{
    private static string LoadPdfQuestion(string id)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "pdf_questions.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var x in doc.RootElement.EnumerateArray())
            if (x.GetProperty("id").GetString() == id)
                return x.GetProperty("question").GetString() ?? "";
        throw new InvalidOperationException($"题库未找到 {id}");
    }

    // ---- TR-4.1：pdf-168 子问(3) 端到端 ----

    [Fact]
    public void Pdf168_调pH6_AlOH3_Ksp_残留Al浓度()
    {
        var stem = LoadPdfQuestion("pdf-168");
        var r = KspPhResidualSolver.TrySolve(stem);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        // 答案键关键词：1.3×10⁻⁹（与官方一致）
        Assert.Contains("1.3×10⁻⁹", r.FinalResult);
        Assert.Contains("Al", r.FinalResult);
        Assert.Contains("Ksp", r.FinalResult ?? "");
    }

    [Fact]
    public void 合成_pH6_AlOH3_残留浓度_13e_9()
    {
        var text = "已知 Al(OH)3 Ksp=1.3×10⁻³³。调 pH=6，过滤后，滤液中 Al3+的浓度为__________mol/L。";
        var r = KspPhResidualSolver.TrySolve(text);
        Assert.NotNull(r);
        Assert.Contains("1.3×10⁻⁹", r!.FinalResult);
    }

    [Fact]
    public void Ksp在方括号中_pH3_FeOH3_残留浓度()
    {
        // Ksp[Fe(OH)3]=2.8×10⁻³⁹, pH=3 → [OH⁻]=10⁻¹¹, c=2.8e-39/(1e-11)^3=2.8e-6
        var text = "已知 Ksp[Fe(OH)3]=2.8×10⁻³⁹。调 pH=3，滤液中 Fe3+的浓度为__________mol/L。";
        var r = KspPhResidualSolver.TrySolve(text);
        Assert.NotNull(r);
        Assert.Contains("2.8×10⁻⁶", r!.FinalResult);
    }

    // ---- TR-4.2：域外/缺数据 → null ----

    [Fact]
    public void 缺Ksp_返回null()
    {
        var text = "调 pH=6，滤液中 Al3+的浓度为__________mol/L。";
        Assert.Null(KspPhResidualSolver.TrySolve(text));
    }

    [Fact]
    public void 缺pH_返回null()
    {
        var text = "已知 Al(OH)3 Ksp=1.3×10⁻³³。滤液中 Al3+的浓度为__________mol/L。";
        Assert.Null(KspPhResidualSolver.TrySolve(text));
    }

    [Fact]
    public void 非氢氧化物体系_AgCl_返回null()
    {
        var text = "已知 Ksp(AgCl)=1.8×10^-10。调 pH=6，滤液中 Ag+的浓度为__________mol/L。";
        Assert.Null(KspPhResidualSolver.TrySolve(text));
    }

    [Fact]
    public void 目标离子无对应氢氧化物Ksp_返回null()
    {
        var text = "已知 Al(OH)3 Ksp=1.3×10⁻³³。调 pH=6，滤液中 Cu2+的浓度为__________mol/L。";
        Assert.Null(KspPhResidualSolver.TrySolve(text));
    }

    [Fact]
    public void 多个不同目标离子_歧义_返回null()
    {
        var text = "已知 Al(OH)3 Ksp=1.3×10⁻³³、Fe(OH)3 Ksp=2.8×10⁻³⁹。调 pH=6，滤液中 Al3+和 Fe3+的浓度分别为__________。";
        Assert.Null(KspPhResidualSolver.TrySolve(text));
    }

    [Fact]
    public void 无浓度问法_谁先沉淀_返回null()
    {
        // 合成 Ksp 题：问「哪种离子先沉淀」，无 pH+浓度残留问法 → 不触发（留给 TryCalculateKspPrecipitation）
        var text = "Ksp(AgCl)=1.8×10^-10，Ksp(AgI)=8.5×10^-17。向含等浓度Cl-和I-的溶液中滴加AgNO3，哪种离子先沉淀？";
        Assert.Null(KspPhResidualSolver.TrySolve(text));
    }

    [Fact]
    public void 空串_返回null()
    {
        Assert.Null(KspPhResidualSolver.TrySolve(""));
        Assert.Null(KspPhResidualSolver.TrySolve(null!));
    }

    [Fact]
    public void pH越界_返回null()
    {
        var text = "已知 Al(OH)3 Ksp=1.3×10⁻³³。调 pH=0，滤液中 Al3+的浓度为__________mol/L。";
        Assert.Null(KspPhResidualSolver.TrySolve(text));
    }

    // ---- 全库零假阳性防护：未解对（baseFalse）题凡触发必判正；触发集锁定 [pdf-168]。
    //      证据基线：_evalscratch/snapshot_pdf278_final.json（已入库跟踪）；dry-fire 见 kspph_dryfire.txt（fired=1）。
    [Fact]
    public void 全题库_未解对题凡触发必判正_且触发集锁定Pdf168()
    {
        var baseline = LoadBaseline();
        Assert.NotEmpty(baseline);
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        var firedIds = new List<string>();
        foreach (var c in cases)
        {
            var r = KspPhResidualSolver.TrySolve(c.Question);
            if (r is null) continue;
            firedIds.Add(c.Id);
            if (baseline.TryGetValue(c.Id, out var baseCorrect) && !baseCorrect)
            {
                Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c),
                    $"{c.Id}（基线未解对）触发但未判正 → 假阳性风险: {r.FinalResult}");
            }
        }
        Assert.Equal(new[] { "pdf-168" }, firedIds.ToArray());
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
