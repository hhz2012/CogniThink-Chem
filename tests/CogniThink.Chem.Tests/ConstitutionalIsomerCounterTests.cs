using CogniThink.Chem.Core.Molecules;
using Xunit;

namespace CogniThink.Chem.Tests;

/// <summary>
/// ConstitutionalIsomerCounter 已知值锁：对照高中教材异构体数目，防止算法回归。
/// 全部断言为教材确定值（不考虑立体异构）。
/// </summary>
public class ConstitutionalIsomerCounterTests
{
    [Fact]
    public void Alkane_Isomer_Counts_Match_Textbook()
    {
        // C4~C7 烷烃构造异构体数（教材值）
        Assert.Equal(2, ConstitutionalIsomerCounter.CountAlkaneIsomers(4));
        Assert.Equal(3, ConstitutionalIsomerCounter.CountAlkaneIsomers(5));
        Assert.Equal(5, ConstitutionalIsomerCounter.CountAlkaneIsomers(6));
        Assert.Equal(9, ConstitutionalIsomerCounter.CountAlkaneIsomers(7));
    }

    [Fact]
    public void Alkane_C8_Isomer_Count_Match_Textbook()
    {
        // C8H18 构造异构体 = 18（冷启动约 27s，仅本测试触发一次缓存）
        Assert.Equal(18, ConstitutionalIsomerCounter.CountAlkaneIsomers(8));
    }

    [Fact]
    public void Alkyl_Isomer_Counts_Match_Textbook()
    {
        // 丁基(C4H9)=4、戊基(C5H11)=8（教材值）
        Assert.Equal(4, ConstitutionalIsomerCounter.CountAlkylIsomers(4));
        Assert.Equal(8, ConstitutionalIsomerCounter.CountAlkylIsomers(5));
    }

    [Fact]
    public void C7_With3Methyl_Isomers_Equal_3()
    {
        // C7H16 含 3 个甲基的构造异构体 = 3（对应 pdf-394 选项 A 声称"2 种"为假）
        Assert.Equal(3, ConstitutionalIsomerCounter.CountAlkaneIsomersWithMethylCount(7, 3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(-1)]
    public void OutOfRange_Returns_Empty(int n)
    {
        Assert.Empty(ConstitutionalIsomerCounter.EnumerateAlkanes(n));
        Assert.Equal(0, ConstitutionalIsomerCounter.CountAlkaneIsomers(n));
        Assert.Equal(0, ConstitutionalIsomerCounter.CountAlkylIsomers(n));
        Assert.Equal(0, ConstitutionalIsomerCounter.CountAlkaneIsomersWithMethylCount(n, 3));
    }

    [Fact]
    public void EnumerateAlkanes_Returns_Distinct_Canonical_Smiles()
    {
        var set = ConstitutionalIsomerCounter.EnumerateAlkanes(6);
        Assert.Equal(5, set.Count);
        Assert.Contains("CCCCCC", set);               // 正己烷
        Assert.Contains("CCCC(C)C", set);            // 2-甲基戊烷
        Assert.Contains("CCC(C)CC", set);             // 3-甲基戊烷
        Assert.Contains("CCC(C)(C)C", set);           // 2,2-二甲基丁烷
        Assert.Contains("CC(C)C(C)C", set);           // 2,3-二甲基丁烷
    }
}
