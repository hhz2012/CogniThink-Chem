using CogniThink.Chem.Core.Structure;
using Xunit;

namespace CogniThink.Chem.Tests;

/// <summary>阶段4：元素周期表/原子结构确定性计算的单测。</summary>
public class PeriodicTableTests
{
    [Theory]
    [InlineData("H", 1)] [InlineData("Fe", 26)] [InlineData("Cu", 29)] [InlineData("Sb", 51)]
    [InlineData("Ga", 31)] [InlineData("As", 33)] [InlineData("Cr", 24)] [InlineData("Ni", 28)]
    [InlineData("氢", 1)] [InlineData("铁", 26)] [InlineData("锑", 51)] [InlineData("铜", 29)]
    public void AtomicNumberOf_符号与中文名(string input, int expected)
        => Assert.Equal(expected, PeriodicTable.AtomicNumberOf(input));

    [Fact]
    public void AtomicNumberOf_未知返回null()
        => Assert.Null(PeriodicTable.AtomicNumberOf("Xyz"));

    [Theory]
    [InlineData(1, "1s¹")]            // H
    [InlineData(2, "1s²")]            // He
    [InlineData(4, "1s²2s²")]         // Be
    [InlineData(7, "1s²2s²2p³")]      // N
    [InlineData(16, "1s²2s²2p⁶3s²3p⁴")] // S
    [InlineData(24, "1s²2s²2p⁶3s²3p⁶3d⁵4s¹")] // Cr（半满例外）
    [InlineData(26, "1s²2s²2p⁶3s²3p⁶3d⁶4s²")] // Fe
    [InlineData(29, "1s²2s²2p⁶3s²3p⁶3d¹⁰4s¹")] // Cu（全满例外）
    public void Configuration_基态电子排布(int z, string expected)
        => Assert.Equal(expected, PeriodicTable.FormatConfiguration(PeriodicTable.Configuration(z)));

    [Theory]
    [InlineData(4, 2, "1s²")]                                  // Be²⁺: 失 2s² -> 1s²
    [InlineData(26, 2, "1s²2s²2p⁶3s²3p⁶3d⁶")]                  // Fe²⁺: 失 4s² -> 3d⁶（不是 Cr 的 3d⁵4s¹）
    [InlineData(26, 3, "1s²2s²2p⁶3s²3p⁶3d⁵")]                  // Fe³⁺: 失 4s² + 1 个 3d -> 3d⁵
    [InlineData(8, -2, "1s²2s²2p⁶")]                           // O²⁻: 得 2e -> 10 电子满壳
    public void Configuration_离子(int z, int charge, string expected)
        => Assert.Equal(expected, PeriodicTable.FormatConfiguration(PeriodicTable.IonConfiguration(z, charge)));

    [Theory]
    [InlineData(26, 2, "3d⁶")]    // Fe²⁺ 价层
    [InlineData(29, 1, "3d¹⁰")]   // Cu⁺ 价层（失 4s¹）
    public void ValenceConfiguration_离子价层(int z, int charge, string expected)
        => Assert.Equal(expected, PeriodicTable.FormatConfiguration(PeriodicTable.ValenceConfiguration(z, charge)));

    [Theory]
    [InlineData(26, 2, 4)]   // Fe²⁺ 3d⁶ -> 4 未成对
    [InlineData(26, 3, 5)]   // Fe³⁺ 3d⁵ -> 5 未成对
    public void UnpairedElectrons_离子(int z, int charge, int expected)
        => Assert.Equal(expected, PeriodicTable.UnpairedElectrons(z, charge));

    [Theory]
    [InlineData(26, "3d⁶4s²")]        // Fe 价层
    [InlineData(28, "3d⁸4s²")]        // Ni 价层
    [InlineData(29, "3d¹⁰4s¹")]       // Cu 价层
    [InlineData(51, "5s²5p³")]        // Sb 价层
    [InlineData(31, "4s²4p¹")]        // Ga 价层
    [InlineData(33, "4s²4p³")]        // As 价层
    [InlineData(22, "3d²4s²")]        // Ti 价层
    public void ValenceConfiguration_价层电子排布(int z, string expected)
        => Assert.Equal(expected, PeriodicTable.FormatConfiguration(PeriodicTable.ValenceConfiguration(z)));

    [Theory]
    [InlineData(26, "四", "VIII")]    // Fe
    [InlineData(29, "四", "IB")]      // Cu
    [InlineData(51, "五", "VA")]      // Sb
    [InlineData(33, "四", "VA")]      // As
    [InlineData(31, "四", "IIIA")]    // Ga
    [InlineData(46, "五", "VIII")]    // Pd（5s⁰）
    [InlineData(47, "五", "IB")]      // Ag
    public void PeriodGroup_周期族(int z, string period, string group)
    {
        var (p, g) = PeriodicTable.PeriodGroup(z);
        Assert.Equal(period, p);
        Assert.Equal(group, g);
    }

    [Theory]
    [InlineData(26, 4)]   // Fe 3d⁶4s² -> 4 未成对
    [InlineData(29, 1)]   // Cu 3d¹⁰4s¹ -> 1
    [InlineData(28, 2)]   // Ni 3d⁸4s² -> 2
    [InlineData(24, 6)]   // Cr 3d⁵4s¹ -> 6
    public void UnpairedElectrons_未成对电子数(int z, int expected)
        => Assert.Equal(expected, PeriodicTable.UnpairedElectrons(z));

    [Theory]
    [InlineData(26, "[Ar]3d⁶4s²")]
    [InlineData(29, "[Ar]3d¹⁰4s¹")]
    [InlineData(51, "[Kr]4d¹⁰5s²5p³")]
    public void NobleGasShorthand_稀有气体简写(int z, string expected)
        => Assert.Equal(expected, PeriodicTable.NobleGasShorthand(z));

    [Fact]
    public void FormatConfiguration_空格变体与ASCII变体可用()
    {
        var occ = PeriodicTable.ValenceConfiguration(28);
        Assert.Equal("3d⁸4s²", PeriodicTable.FormatConfiguration(occ));
        Assert.Equal("3d⁸ 4s²", PeriodicTable.FormatConfiguration(occ, space: true));
        Assert.Equal("3d84s2", PeriodicTable.FormatConfiguration(occ, superscript: false));
    }
}
