using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// RedoxRoleRatioStatementJudge 护栏（spec FR-3 / Task 4）。
/// 真值源：RedoxBalancer.SplitHalfReactions 的氧化态变化 + 配平系数。
/// 红线：非歧化的跨元素比值（物种/原子比语义不一致）→ Undecided；物种歧义 / 方程不可解析 → Undecided；不蒙数。
/// </summary>
public class RedoxRoleRatioStatementJudgeTests
{
    private static readonly RedoxRoleRatioStatementJudge Judge = new();

    // 歧化：3S + 6NaOH = 2Na2S + Na2SO3 + 3H2O
    // S：0→-2（2 个 S 被还原，共得 4e）、0→+4（1 个 S 被氧化，失 4e）→ 被氧化:被还原 = 1:2
    private const string StemDisproportion = "反应：3S + 6NaOH = 2Na2S + Na2SO3 + 3H2O";

    // ===== 比值（歧化，单一变价元素）=====

    [Fact]
    public void Disproportion_Ratio_1to2_ReturnsTrue()
    {
        Assert.Equal(Verdict.True, Judge.Judge(StemDisproportion, "被氧化与被还原的物质的量之比为1:2"));
    }

    [Fact]
    public void Disproportion_Ratio_2to1_ReturnsFalse()
    {
        Assert.Equal(Verdict.False, Judge.Judge(StemDisproportion, "被氧化与被还原的物质的量之比为2:1"));
    }

    [Fact]
    public void Disproportion_Ratio_ReversedOrder_ReturnsTrue()
    {
        // "被还原与被氧化之比为2:1" 等价于 被氧化:被还原 = 1:2
        Assert.Equal(Verdict.True, Judge.Judge(StemDisproportion, "被还原与被氧化的物质的量之比为2:1"));
    }

    // ===== 角色 =====

    [Fact]
    public void Disproportion_S_BothOxidantAndReductant_ReturnsTrue()
    {
        Assert.Equal(Verdict.True, Judge.Judge(StemDisproportion, "S 既是氧化剂又是还原剂"));
    }

    [Fact]
    public void Disproportion_S_OnlyOxidant_ReturnsFalse()
    {
        Assert.Equal(Verdict.False, Judge.Judge(StemDisproportion, "S 只作氧化剂"));
    }

    [Fact]
    public void SimpleRedox_Cl2_OnlyOxidant_ReturnsTrue()
    {
        // 2Na + Cl2 = 2NaCl：Cl 0→-1 只被还原 → Cl2 只作氧化剂
        var stem = "反应：2Na + Cl2 = 2NaCl";
        Assert.Equal(Verdict.True, Judge.Judge(stem, "Cl2 只作氧化剂"));
    }

    [Fact]
    public void SimpleRedox_Na_IsOxidant_ReturnsFalse()
    {
        var stem = "反应：2Na + Cl2 = 2NaCl";
        Assert.Equal(Verdict.False, Judge.Judge(stem, "Na 是氧化剂"));
    }

    [Fact]
    public void SimpleRedox_NaCl_IsOxidationProduct_ReturnsTrue()
    {
        var stem = "反应：2Na + Cl2 = 2NaCl";
        Assert.Equal(Verdict.True, Judge.Judge(stem, "NaCl 是氧化产物"));
    }

    // ===== 不可判 / 域外 → Undecided（不蒙数）=====

    [Fact]
    public void NonRedox_Ratio_ReturnsUndecided()
    {
        Assert.Equal(Verdict.Undecided, Judge.Judge("反应：NaOH + HCl = NaCl + H2O", "被氧化与被还原的物质的量之比为1:1"));
    }

    [Fact]
    public void NonRedox_Role_ReturnsFalse()
    {
        // 非氧还方程中没有任何物质是氧化剂/还原剂 → 角色陈述为假（化学正确）
        Assert.Equal(Verdict.False, Judge.Judge("反应：NaOH + HCl = NaCl + H2O", "HCl 是氧化剂"));
    }

    [Fact]
    public void CrossElement_Ratio_ReturnsUndecided()
    {
        // 2H2 + O2 = 2H2O：变价元素 H 与 O 两种 → 物种/原子比语义不一致，不判
        Assert.Equal(Verdict.Undecided, Judge.Judge("反应：2H2 + O2 = 2H2O", "被氧化与被还原的物质的量之比为1:1"));
    }

    [Fact]
    public void Disproportion_H2O2_H2O_IsOxidationProduct_ReturnsFalse()
    {
        // 2H2O2 = 2H2O + O2：O -1→-2（H2O，还原）与 -1→0（O2，氧化）
        // → H2O 是还原产物而非氧化产物，陈述为假（化学正确）
        Assert.Equal(Verdict.False,
            Judge.Judge("反应：2H2O2 = 2H2O + O2", "H2O 是氧化产物"));
    }

    [Fact]
    public void ReactantAskedAsProduct_ReturnsUndecided()
    {
        // 2H2O = 2H2 + O2：H2O 只在左侧（反应物），却被问作产物 → 物种侧位歧义，不判
        Assert.Equal(Verdict.Undecided,
            Judge.Judge("反应：2H2O = 2H2 + O2", "H2O 是氧化产物"));
    }

    [Fact]
    public void NoRatioOrRoleKeyword_ReturnsUndecided()
    {
        Assert.Equal(Verdict.Undecided, Judge.Judge(StemDisproportion, "该反应是歧化反应"));
    }

    [Fact]
    public void UnparseableEquation_ReturnsUndecided()
    {
        Assert.Equal(Verdict.Undecided, Judge.Judge("反应：XYZ = ABC", "被氧化与被还原的物质的量之比为1:2"));
    }
}
