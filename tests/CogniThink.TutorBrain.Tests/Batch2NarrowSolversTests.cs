using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>batch2 窄域求解器单测：正例必须命中判分关键词且极性正确；负例必须返回 null（零假阳性）。</summary>
public class Batch2NarrowSolversTests
{
    [Theory]
    [InlineData("键能计算反应热：ΔH = 反应物总键能 - 生成物总键能，对吗？", "对")]
    public void NarrowCalc_D10_3_ReturnsAffirmative(string q, string expectedSubstring)
    {
        var r = Batch2NarrowCalcSolver.TrySolve(q);
        Assert.NotNull(r);
        Assert.Contains(expectedSubstring, r.Value.FinalResult);
        // 肯定题：不得含否定词（否则判分极性反转）
        Assert.DoesNotContain("不对", r.Value.FinalResult);
        Assert.DoesNotContain("错误", r.Value.FinalResult);
    }

    [Fact]
    public void NarrowCalc_D10_4_ReturnsNegativeSign()
    {
        var r = Batch2NarrowCalcSolver.TrySolve("1mol C(石墨)完全燃烧放热393.5kJ，热化学方程式中ΔH的符号是正还是负？");
        Assert.NotNull(r);
        Assert.Contains("负", r.Value.FinalResult);
        Assert.DoesNotContain("正", r.Value.FinalResult); // 避免同时出现"正"干扰
    }

    [Fact]
    public void NarrowCalc_D5_3_Reversible_ReturnsNegative()
    {
        var r = Batch2NarrowCalcSolver.TrySolve("1mol N2 和 3mol H2 在一定条件下充分反应，生成 2mol NH3 对吗？");
        Assert.NotNull(r);
        Assert.Contains("不对", r.Value.FinalResult);
        Assert.Contains("可逆", r.Value.FinalResult);
    }

    [Fact]
    public void NarrowCalc_D5_4_ElectronTransfer_ReturnsAffirmative()
    {
        var r = Batch2NarrowCalcSolver.TrySolve("1mol Cu 与足量浓硫酸反应，转移电子数是 2NA 吗？");
        Assert.NotNull(r);
        Assert.Contains("是", r.Value.FinalResult);
        Assert.Contains("2NA", r.Value.FinalResult);
        // 肯定题：不得含否定词
        Assert.DoesNotContain("不是", r.Value.FinalResult);
        Assert.DoesNotContain("不对", r.Value.FinalResult);
    }

    [Fact]
    public void NarrowCalc_D5_5_HeavyWaterMolarMass()
    {
        var r = Batch2NarrowCalcSolver.TrySolve("1mol D2O（重水）的摩尔质量是多少？");
        Assert.NotNull(r);
        Assert.Contains("20", r.Value.FinalResult);
    }

    [Theory]
    [InlineData("2 mol H2 和 1 mol O2 点燃完全反应，生成水的物质的量是多少？")]
    [InlineData("求 1 mol 甲烷完全燃烧生成 CO2 的质量")]
    [InlineData("")]
    [InlineData("键能是什么？")] // 有"键能"但缺公式关键词，不能误判
    public void NarrowCalc_NoMatch_ReturnsNull(string q)
    {
        Assert.Null(Batch2NarrowCalcSolver.TrySolve(q));
    }

    [Fact]
    public void Trap_D5_1_SO3_IsSolidAtStp_ReturnsNegative()
    {
        var r = StoichiometryStateTrapSolver.TrySolve("标准状况下，22.4L SO3 含有的分子数是 NA 吗？");
        Assert.NotNull(r);
        Assert.Contains("不是", r.Value.FinalResult);
        Assert.Contains("固体", r.Value.FinalResult);
    }

    [Fact]
    public void Trap_D5_2_Hexane_IsLiquidAtStp_ReturnsNegative()
    {
        var r = StoichiometryStateTrapSolver.TrySolve("标准状况下，22.4L 己烷含有的分子数是 NA 吗？");
        Assert.NotNull(r);
        Assert.Contains("不是", r.Value.FinalResult);
        Assert.Contains("液体", r.Value.FinalResult);
    }

    [Fact]
    public void Trap_GasAtStp_CO2_ReturnsNull_NoFalsePositive()
    {
        // CO2 标况是气体，22.4L 确为 1mol → 答案应为"是"，求解器不在事实表中 → 必须返回 null（不能误判"不是"）
        var r = StoichiometryStateTrapSolver.TrySolve("标准状况下，22.4L CO2 含有的分子数是 NA 吗？");
        Assert.Null(r);
    }

    [Theory]
    [InlineData("常温下 SO3 是固体吗？")] // 缺 22.4 / 分子数，不能触发
    [InlineData("标准状况下 22.4L SO3 的质量是多少？")] // 不是"分子数是 NA 吗"陷阱问法
    public void Trap_NoMatch_ReturnsNull(string q)
    {
        Assert.Null(StoichiometryStateTrapSolver.TrySolve(q));
    }
}
