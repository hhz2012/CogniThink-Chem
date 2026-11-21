using System.Collections.Generic;
using System.Linq;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// ReactionKbWriterSolver（Spec: image-equation-engine-151 / Task 2 + Task 3）验收与护栏测试。
/// 真值源：知识库 reactions/*.json + 确定型反应规律模板；不调 LLM。
/// 红线：无唯一解一律 null 不蒙数；凡非 null 必与真值一致（全题库零假阳性）。
/// </summary>
public class ReactionKbWriterSolverTests
{
    private static EngineResponse? Solve(string raw, string taskType = "unknown") =>
        ReactionKbWriterSolver.TrySolve(new EngineRequest { TaskType = taskType, RawText = raw });

    private static readonly List<QaTestCase> AllCases =
        GatewayVsLlmEvaluationService.BuildTestCases().ToList();

    private static QaTestCase? Case(string id) => AllCases.FirstOrDefault(c => c.Id == id);

    // ============================================================
    // 全题库护栏（TR-2.3）：凡求解器非 null，必被判分判正（零假阳性）
    // ============================================================
    [Fact]
    public void FullBankGuard_EveryNonNullOutput_IsAnswerCorrect()
    {
        int fires = 0;
        foreach (var c in AllCases)
        {
            var raw = c.Question ?? string.Empty;
            var det = LlmGatewayService.ClassifyTaskDeterministic(raw);
            var resp = ReactionKbWriterSolver.TrySolve(new EngineRequest { RawText = raw, TaskType = det });
            if (resp is null || string.IsNullOrWhiteSpace(resp.FinalResult)) continue;
            fires++;
            Assert.True(
                GatewayVsLlmEvaluationService.IsAnswerCorrect(resp.FinalResult, c),
                $"求解器在 {c.Id}（{det}）产出非 null 但判分未通过：{resp.FinalResult}");
        }
        // 至少应覆盖到已验证的方程增益题，防止护栏被意外短路。
        Assert.True(fires >= 4, $"期望至少 4 个非 null 命中（pdf-038/417/174/477），实际 fires={fires}");
    }

    // ============================================================
    // 已验证增益题：非 null 且判正（pdf-038 / pdf-417 / pdf-174）
    // ============================================================
    [Theory]
    [InlineData("pdf-038")]
    [InlineData("pdf-417")]
    [InlineData("pdf-174")]
    public void VerifiedGain_Cases_AreNonNullAndCorrect(string id)
    {
        var c = Case(id);
        Assert.NotNull(c);
        var det = LlmGatewayService.ClassifyTaskDeterministic(c!.Question ?? string.Empty);
        var resp = Solve(c.Question ?? string.Empty, det);
        Assert.NotNull(resp);
        Assert.True(resp!.Ok);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(resp.FinalResult, c),
            $"{id} 输出未被判分通过：{resp.FinalResult}");
    }

    // ============================================================
    // 离子模板正例（Task 3）：锚点齐全 → 产出确定型方程
    // ============================================================
    [Fact]
    public void Ionic_Neutralization_ProducesNetIonicEquation()
    {
        var resp = Solve("向过量的 NaOH 溶液中加入盐酸调至中性，写出反应的离子方程式");
        Assert.NotNull(resp);
        Assert.Contains("H+ + OH- = H2O", resp!.FinalResult);
    }

    [Fact]
    public void Ionic_AcidCarbonate_ProducesEquation()
    {
        var resp = Solve("写出碳酸钠与足量盐酸反应的离子方程式");
        Assert.NotNull(resp);
        Assert.Contains("2H+ + CO3^2- = H2O + CO2", resp!.FinalResult);
    }

    [Fact]
    public void Ionic_AcidBicarbonate_ProducesEquation()
    {
        var resp = Solve("写出碳酸氢钠与盐酸反应的离子方程式");
        Assert.NotNull(resp);
        Assert.Contains("H+ + HCO3- = H2O + CO2", resp!.FinalResult);
    }

    [Fact]
    public void Ionic_HydroxideBicarbonate_ProducesEquation()
    {
        var resp = Solve("写出 NaOH 溶液与 NaHCO3 溶液反应的离子方程式");
        Assert.NotNull(resp);
        Assert.Contains("OH- + HCO3- = CO3^2- + H2O", resp!.FinalResult);
    }

    [Theory]
    [InlineData("等物质的量的 NaHCO3 和 Ba(OH)2 反应的离子方程式是什么？")]   // trap-limit-3
    [InlineData("Ca(HCO3)2 溶液中加入少量 NaOH，反应的离子方程式是什么？")]     // d1-5
    public void Ionic_HydroxideBicarbonate_WithPrecipitatingCation_ReturnsNull(string stem)
    {
        // Ba2+/Ca2+ 会与生成的 CO3^2- 沉淀，净离子方程不同 → 不抢写游离 CO3^2- 简单式。
        Assert.Null(Solve(stem));
    }

    [Fact]
    public void Ionic_BasicCarbonateAcid_ProducesEquation()
    {
        var resp = Solve("写出碱式碳酸铜 Cu2(OH)2CO3 与盐酸反应的离子方程式");
        Assert.NotNull(resp);
        Assert.Contains("Cu2(OH)2CO3 + 4H+ = 2Cu2+ + 3H2O + CO2", resp!.FinalResult);
    }

    [Fact]
    public void Ionic_ManganeseDioxideSulfite_ProducesEquation()
    {
        // 还原酸浸：MnO2 氧化 SO3^2-（pdf-174；大题上下文恢复后 MnO2 / 亚硫酸盐 / 酸性 锚点齐全）。
        var resp = Solve("从褐铁矿型金-银矿(含 MnO2 等)中提取有价金属，还原酸浸加入 H2SO4、Na2SO3，" +
                         "写出 MnO2 反应的离子方程式");
        Assert.NotNull(resp);
        Assert.Contains("MnO2 + SO3^2- + 2H+ = Mn2+ + SO4^2- + H2O", resp!.FinalResult);
    }

    [Theory]
    [InlineData("写出 MnO2 与浓盐酸反应制 Cl2 的离子方程式")]   // 有 MnO2、有酸，但无亚硫酸盐
    [InlineData("写出 MnO2 与 Na2SO3 反应的离子方程式")]         // 有 MnO2、有亚硫酸盐，但无酸性介质
    public void Ionic_ManganeseDioxideSulfite_MissingAnchor_ReturnsNull(string stem)
    {
        Assert.Null(Solve(stem));
    }

    // 判分兼容：变体覆盖去空格与反应物逆序，使 unspaced / 逆序答案键也能子串命中
    [Fact]
    public void Ionic_Output_IncludesSpacelessAndReversedVariants()
    {
        var resp = Solve("写出 NaOH 溶液与 NaHCO3 溶液反应的离子方程式");
        Assert.NotNull(resp);
        Assert.Contains("OH-+HCO3-=CO3^2-+H2O", resp!.FinalResult);
        Assert.Contains("HCO3-+OH-=CO3^2-+H2O", resp.FinalResult);
    }

    // ============================================================
    // 反例 / 守卫：域外 / 歧义 / 冲突一律 null
    // ============================================================
    [Fact]
    public void NoEquationAnchor_ReturnsNull()
    {
        Assert.Null(Solve("计算 1mol 氢气的质量"));
    }

    [Theory]
    [InlineData("choice")]
    [InlineData("balance")]
    [InlineData("atomic_structure")]
    [InlineData("vsepr")]
    [InlineData("judgment")]
    [InlineData("ion_check")]
    public void NonWiredTaskType_ReturnsNull_EvenIfEquationPresent(string taskType)
    {
        Assert.Null(Solve("写出碳酸钠与盐酸反应的离子方程式", taskType));
    }

    [Fact]
    public void BothIonicAndMolecularAsked_ReturnsNull_Ambiguous()
    {
        Assert.Null(Solve("写出 NaOH 与盐酸反应的离子方程式和化学方程式"));
    }

    [Fact]
    public void Neutralization_WithBariumHydroxideAndSulfate_ReturnsNull()
    {
        // Ba(OH)2 + H2SO4 净离子含 BaSO4↓ 沉淀，不是单纯 H++OH- → 不抢写。
        Assert.Null(Solve("向 Ba(OH)2 溶液中滴加稀硫酸至中性，写出反应的离子方程式"));
    }

    [Fact]
    public void AcidInsolubleCarbonate_IonicNotWritten()
    {
        // 难溶碳酸盐（CaCO3）离子方程保留固体，模板不抢写简单 2H++CO3^2-。
        Assert.Null(Solve("写出碳酸钙与盐酸反应的离子方程式"));
    }

    // ============================================================
    // 分子：知识库唯一命中契约（Task 2 / TR-2.2）
    // ============================================================
    [Fact]
    public void Molecular_UniqueHit_NaWater_OutputsEquation()
    {
        var resp = Solve("写出 Na 与 H2O 反应的化学方程式", "reaction_check");
        Assert.NotNull(resp);
        Assert.Contains("2Na + 2H2O = 2NaOH + H2", resp!.FinalResult);
    }

    [Fact]
    public void Molecular_ZeroHit_ReturnsNull()
    {
        // Xe/F2 不在知识库 → 0 命中 → null，不臆造。
        Assert.Null(Solve("写出 Xe 与 F2 反应的化学方程式", "reaction_check"));
    }

    [Fact]
    public void Molecular_MultiHit_ReturnsNull_NoGuess()
    {
        // 同题出现两组独立反应物（C+O2 点燃、Na+H2O）→ 命中 >1 条 → null，不蒙数。
        Assert.Null(Solve("写出 C 与 O2 点燃、Na 与 H2O 反应的化学方程式", "reaction_check"));
    }

    [Fact]
    public void Molecular_ConditionConflict_ReturnsNull()
    {
        // Fe+S 条目需 △；题干无加热/△/Δ → 条件不满足 → null。
        Assert.Null(Solve("写出 Fe 与 S 反应的化学方程式", "reaction_check"));
    }

    [Fact]
    public void Molecular_SubstringSpeciesNotMatchedAsWhole()
    {
        // "Fe" 是 FeSO4 的子串、"S" 也是，但都不是独立物种 → 不应误匹配 Fe+S 条目。
        // （pdf-243 热化学计算曾因此误写方程；边界匹配修复后域外 → null。）
        Assert.Null(Solve(
            "已知 FeSO4·7H2O(s)═FeSO4(s)+7H2O(g) ΔH=a kJ/mol，写出上述变化的化学方程式",
            "reaction_check"));
    }
}
