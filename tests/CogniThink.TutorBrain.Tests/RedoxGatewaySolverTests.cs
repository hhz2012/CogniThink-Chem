using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// 网关层氧化还原 / 产物书写求解器（阶段3C / AC-13）验收测试。
/// 真值源：OxidationStateAnalyzer（氧化态分配）+ 高中规则表 + EquationBalancer（守恒配平）。
/// 重点验证：17 题全部解出且结论正确；任何边界外/歧义/不可解析情形一律返回 null（不蒙数）。
/// </summary>
public class RedoxGatewaySolverTests
{
    private static EngineResponse? SolveRedox(string raw) =>
        RedoxGatewaySolver.TrySolve(new EngineRequest { TaskType = "reaction_check", RawText = raw });

    private static EngineResponse? SolveProduct(string raw) =>
        ProductWriterSolver.TrySolve(new EngineRequest { TaskType = "reaction_check", RawText = raw });

    // ============================================================
    // R1：氧化还原角色识别（3 题）
    // ============================================================

    [Fact]
    public void R1_Redox1_Cu_IsReductant()
    {
        var r = SolveRedox("在 Cu + HNO3(稀) → Cu(NO3)2 + NO + H2O 反应中，还原剂是什么？");
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("还原剂", r.FinalResult);
        Assert.Contains("Cu", r.FinalResult);
    }

    [Fact]
    public void R1_Redox2_FeCl3_IsOxidant()
    {
        var r = SolveRedox("在 2FeCl3 + Cu = 2FeCl2 + CuCl2 反应中，氧化剂是什么？");
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("氧化剂", r.FinalResult);
        Assert.Contains("FeCl3", r.FinalResult);
    }

    [Fact]
    public void R1_Redox3_KI_IsOxidized()
    {
        var r = SolveRedox("在 Cl2 + 2KI = 2KCl + I2 反应中，被氧化的物质是什么？");
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("还原剂", r.FinalResult); // 被氧化 → 还原剂
        Assert.Contains("KI", r.FinalResult);
    }

    // ============================================================
    // R2：还原产物 / 电极产物（6 题）
    // ============================================================

    [Fact]
    public void R2_d2_1_MnO4_Acid_GivesMn2()
    {
        var r = SolveRedox("MnO4- 在酸性条件下被还原的产物是什么？");
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("还原产物", r.FinalResult);
        Assert.Contains("Mn2+", r.FinalResult);
    }

    [Fact]
    public void R2_d2_2_MnO4_Neutral_GivesMnO2()
    {
        var r = SolveRedox("MnO4- 在中性条件下被还原的产物是什么？");
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("MnO2", r.FinalResult);
    }

    [Fact]
    public void R2_d2_3_ClO_Acid_GivesCl()
    {
        var r = SolveRedox("ClO- 在酸性条件下的还原产物是什么？");
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("Cl-", r.FinalResult);
    }

    [Fact]
    public void R2_d2_4_DiluteHNO3_GivesNO()
    {
        // 题干无"酸性"字样，靠"稀"判定 → 不能因介质缺失而漏判
        var r = SolveRedox("稀硝酸与铜反应的还原产物是什么？");
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("NO", r.FinalResult);
        Assert.DoesNotContain("NO2", r.FinalResult);
    }

    [Fact]
    public void R2_d8_1_ElectrolysisCuSO4_Anode_GivesO2()
    {
        var r = SolveRedox("电解 CuSO4 溶液，阳极产物是什么？");
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("阳极产物", r.FinalResult);
        Assert.Contains("O2", r.FinalResult);
    }

    [Fact]
    public void R2_d8_2_ElectrolysisNaCl_Anode_GivesCl2()
    {
        var r = SolveRedox("电解 NaCl 溶液，阳极产物是什么？");
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("Cl2", r.FinalResult);
    }

    // ============================================================
    // P3：小可行性规则（2 题）
    // ============================================================

    [Fact]
    public void P3_d8_5_AcidicMedium_NoOH()
    {
        var r = SolveRedox("酸性电解质溶液中，电极反应式可以出现 OH- 吗？");
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("不能", r.FinalResult);
    }

    [Fact]
    public void P3_TrapLimit1_Al_NaOH_GivesH2()
    {
        var r = SolveRedox("Al 和 NaOH 溶液反应能生成 H2 吗？");
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("能", r.FinalResult);
        Assert.Contains("H2", r.FinalResult);
    }

    // ============================================================
    // P1：少量/过量产物书写（6 题，调 EquationBalancer 配平）
    // ============================================================

    [Fact]
    public void P1_d1_1_FeBr2_LittleCl2()
    {
        var r = SolveProduct("FeBr2 溶液中通入少量 Cl2，反应的离子方程式是什么？");
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("2Fe2+", r.FinalResult);
        Assert.Contains("2Fe3+", r.FinalResult);
        Assert.Contains("2Cl-", r.FinalResult);
        Assert.DoesNotContain("Br", r.FinalResult); // 少量 Cl2 不氧化 Br-
    }

    [Fact]
    public void P1_d1_2_FeBr2_ExcessCl2()
    {
        var r = SolveProduct("FeBr2 溶液中通入过量 Cl2，反应的离子方程式是什么？");
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("4Br-", r.FinalResult);
        Assert.Contains("3Cl2", r.FinalResult);
        Assert.Contains("2Br2", r.FinalResult);
        Assert.Contains("6Cl-", r.FinalResult);
    }

    [Fact]
    public void P1_d1_3_NaAlO2_LittleCO2()
    {
        var r = SolveProduct("NaAlO2 溶液中通入少量 CO2，反应的离子方程式是什么？");
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("2AlO2-", r.FinalResult);
        Assert.Contains("CO3", r.FinalResult); // 少量 → CO3^2-（输出去 ^ 后为 CO32-）
        Assert.DoesNotContain("HCO3", r.FinalResult);
    }

    [Fact]
    public void P1_d1_4_NaAlO2_ExcessCO2()
    {
        var r = SolveProduct("NaAlO2 溶液中通入过量 CO2，反应的离子方程式是什么？");
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("HCO3-", r.FinalResult); // 过量 → HCO3-
        Assert.DoesNotContain("CO3", r.FinalResult.Replace("HCO3", ""));
    }

    [Fact]
    public void P1_d1_5_CaHCO32_LittleNaOH()
    {
        var r = SolveProduct("Ca(HCO3)2 溶液中加入少量 NaOH，反应的离子方程式是什么？");
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("Ca2+", r.FinalResult);
        Assert.Contains("CaCO3", r.FinalResult);
        Assert.Contains("H2O", r.FinalResult);
    }

    [Fact]
    public void P1_pdf515_NaCN_LittleCO2()
    {
        // PDF 整句关键词：CN⁻ + CO₂ + H₂O → HCN + HCO₃⁻（归一化后 ASCII）
        var r = SolveProduct("(2)向 NaCN 中通入少量的 CO2，发生反应的离子方程式为____________________。");
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("CN-", r.FinalResult);
        Assert.Contains("HCN", r.FinalResult);
        Assert.Contains("HCO3-", r.FinalResult);
        Assert.Contains("→", r.FinalResult);
    }

    // ============================================================
    // 负例：不蒙数——边界外 / 歧义 / 不可解析 → 一律 null
    // ============================================================

    [Fact]
    public void Null_EmptyText_ReturnsNull()
    {
        Assert.Null(SolveRedox(""));
        Assert.Null(SolveProduct(""));
    }

    [Fact]
    public void R1_NonRedox_NoChanges_ReturnsNull()
    {
        // 非氧化还原反应，无变价 → 不应臆断氧化剂
        Assert.Null(SolveRedox("在 NaOH + HCl = NaCl + H2O 反应中，氧化剂是什么？"));
    }

    [Fact]
    public void R1_Disproportionation_AmbiguousRole_ReturnsNull()
    {
        // 歧化：Cl 既升又降（0→-1 与 0→+1），角色歧义 → null，不蒙数
        Assert.Null(SolveRedox("在 Cl2 + 2NaOH = NaCl + NaClO + H2O 反应中，氧化剂是什么？"));
    }

    [Fact]
    public void R1_MultipleReactantsSameElement_ReturnsNull()
    {
        // 归中：左侧 Fe 与 FeCl3 都含 Fe（变价元素）→ 物种歧义 → null
        Assert.Null(SolveRedox("在 Fe + 2FeCl3 = 3FeCl2 反应中，还原剂是什么？"));
    }

    [Fact]
    public void R1_UnparseableEquation_ReturnsNull()
    {
        Assert.Null(SolveRedox("在 这不是一个化学方程 反应中，氧化剂是什么？"));
    }

    [Fact]
    public void R1_AskNotRecognized_ReturnsNull()
    {
        Assert.Null(SolveRedox("在 Cu + HNO3(稀) → Cu(NO3)2 + NO + H2O 反应中，反应现象是什么？"));
    }

    [Fact]
    public void R2_MediumMissing_MnO4_ReturnsNull()
    {
        // MnO4 还原产物依赖介质，"熔融"不在酸/中/碱表内 → null，不蒙数
        Assert.Null(SolveRedox("KMnO4 在熔融条件下的还原产物是什么？"));
    }

    [Fact]
    public void R2_OffTableOxidant_ReturnsNull()
    {
        Assert.Null(SolveRedox("K3[Fe(CN)6] 的还原产物是什么？"));
    }

    [Fact]
    public void R2_NO3_NoDiluteConcentrated_ReturnsNull()
    {
        // 无"稀/浓" → 产物不定 → null
        Assert.Null(SolveRedox("硝酸的还原产物是什么？"));
    }

    [Fact]
    public void P1_NoEquationKeyword_ReturnsNull()
    {
        // 题干不含"方程式/方程" → 不触发产物书写，避免污染其他 reaction_check 题
        Assert.Null(SolveProduct("FeBr2 溶液中通入少量 Cl2"));
    }

    [Fact]
    public void P1_OffTableReaction_ReturnsNull()
    {
        Assert.Null(SolveProduct("FeCl3 溶液中加入少量 NaOH，反应的离子方程式是什么？"));
    }
}
