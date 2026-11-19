using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// 网关层配平求解器（阶段3A / AC-11）验收测试。
/// 真值源为 CogniThink.Chem.Core 的 EquationBalancer（已有 21 个核心单测覆盖），
/// 此处重点验证网关层的「eval 记法归一化 + 题干抽取 + 不蒙数」边界。
/// </summary>
public class BalanceGatewaySolverTests
{
    // ---- TR-11.1：能配平的题给出正确最小正整数系数 ----

    [Fact]
    public void TrySolve_FeO2_Gives321()
    {
        var req = new EngineRequest { TaskType = "balance", StudentAnswer = "Fe + O2 = Fe3O4" };
        var outcome = BalanceGatewaySolver.TrySolve(req);
        Assert.NotNull(outcome);
        Assert.Equal("3Fe + 2O2 = Fe3O4", outcome!.BalancedEquation);
    }

    [Fact]
    public void TrySolve_AlHCl_Gives2623()
    {
        var req = new EngineRequest { TaskType = "balance", StudentAnswer = "Al + HCl = AlCl3 + H2" };
        var outcome = BalanceGatewaySolver.TrySolve(req);
        Assert.NotNull(outcome);
        Assert.Equal("2Al + 6HCl = 2AlCl3 + 3H2", outcome!.BalancedEquation);
    }

    [Fact]
    public void Normalize_EvalNotation_StripsBlanksAndCollapsesEquals()
    {
        // 评估集 pdf-089 的真实记法：系数空位 ___ + 多等号 ===
        var raw = "___NO2^- + ___MnO4^- + ___H^+ === ___NO3^- + ___Mn^2+ + ___H2O";
        var normalized = BalanceGatewaySolver.NormalizeForBalancing(raw);
        Assert.Equal("NO2^- + MnO4^- + H^+ = NO3^- + Mn^2+ + H2O", normalized);
    }

    [Fact]
    public void ExtractFirstEquation_Pdf089_PullsPart1Equation()
    {
        var text = "(2025·汇编★★★★)按要求书写下列方程式:(1)NaNO2可作为食品防腐剂。可用酸性KMnO4测定NaNO2残留量,配平反应:___NO2^- + ___MnO4^- + ___H^+ === ___NO3^- + ___Mn^2+ + ___H2O。(2)工业上...";
        var eq = BalanceGatewaySolver.ExtractFirstEquation(text);
        Assert.Equal("___NO2^- + ___MnO4^- + ___H^+ === ___NO3^- + ___Mn^2+ + ___H2O", eq);
    }

    [Fact]
    public void TrySolve_Pdf089RealText_BalancesPart1()
    {
        // 端到端：从真实题干抽取 + 归一化 + 内核配平，结果须与评估预期关键词完全一致
        var text = "(2025·汇编★★★★)按要求书写下列方程式:(1)NaNO2可作为食品防腐剂。可用酸性KMnO4测定NaNO2残留量,配平反应:___NO2^- + ___MnO4^- + ___H^+ === ___NO3^- + ___Mn^2+ + ___H2O。";
        var req = new EngineRequest { TaskType = "balance", RawText = text };
        var outcome = BalanceGatewaySolver.TrySolve(req);
        Assert.NotNull(outcome);
        Assert.Equal("5NO2^- + 2MnO4^- + 6H^+ = 5NO3^- + 2Mn^2+ + 3H2O", outcome!.BalancedEquation);
    }

    // ---- TR-11.2：无唯一解 / 非方程 → null，绝不臆造系数 ----

    [Fact]
    public void TrySolve_NoSolution_ReturnsNull()
    {
        var req = new EngineRequest { TaskType = "balance", StudentAnswer = "H2O = H2 + N2" };
        Assert.Null(BalanceGatewaySolver.TrySolve(req));
    }

    [Fact]
    public void TrySolve_Underdetermined_ReturnsNull()
    {
        var req = new EngineRequest { TaskType = "balance", StudentAnswer = "H2 + O2 + C + O2 = H2O + CO2" };
        Assert.Null(BalanceGatewaySolver.TrySolve(req));
    }

    [Fact]
    public void TrySolve_NoEquation_ReturnsNull()
    {
        var req = new EngineRequest { TaskType = "balance", RawText = "下列说法正确的是" };
        Assert.Null(BalanceGatewaySolver.TrySolve(req));
    }

    [Fact]
    public void ExtractFirstEquation_PHEquals7_NotAnEquation()
    {
        // "pH=7" 含等号但两侧不是元素字母方程 → 不应被抽取
        Assert.Null(BalanceGatewaySolver.ExtractFirstEquation("室温下 pH=7 的溶液呈中性"));
    }

    [Fact]
    public void TrySolve_PlaceHolderSpecies_ReturnsNull()
    {
        // 含未知物种 X → 欠定，不蒙数
        var req = new EngineRequest { TaskType = "balance", StudentAnswer = "CH3OH + NO3- = X + CO2 + H2O" };
        Assert.Null(BalanceGatewaySolver.TrySolve(req));
    }
}
