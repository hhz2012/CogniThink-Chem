using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// P1-2 AC-5 回归护栏：多部分 (N) 标记检测与子问切分必须正确，
/// 尤其不得把 (2025·北京卷) / [Cu(NH3)4] / (FeSO4·7H2O) 误判为子问标记，
/// 否则非多部分题会被误触发分发（回归）或子问切分错乱。
/// </summary>
public class MultiPartUtilityTests
{
    [Fact]
    public void IsMultiPart_True_When_TwoOrMore_NumericMarkers()
    {
        Assert.True(MultiPartUtility.IsMultiPart("(1) 第一问 ____ (2) 第二问 ____"));
        Assert.True(MultiPartUtility.IsMultiPart("引言：(1) a；（2）b；(3) c。")); // 含全角（）
    }

    [Fact]
    public void IsMultiPart_False_When_ZeroOrOne_Marker()
    {
        Assert.False(MultiPartUtility.IsMultiPart("单一填空题：求 pH = ____"));
        Assert.False(MultiPartUtility.IsMultiPart("只有一个子问 (1) 的题不算多部分"));
        Assert.False(MultiPartUtility.IsMultiPart(""));
        Assert.False(MultiPartUtility.IsMultiPart(null));
    }

    [Fact]
    public void IsMultiPart_False_For_Year_And_Formula_Brackets()
    {
        // (2025·重庆卷) 数字后是「·」非闭括号 → 不计入；(Se) 括号内非纯数字 → 不计入 → 仅 (1)(2)(3)
        Assert.True(MultiPartUtility.IsMultiPart(
            "(2025·重庆卷)硒(Se)流程如下：(1) 化合价 ____；(2) 水解方程 ____；(3) 状态变化称 ____。"));

        // [Cu(NH3)4] 与 (FeSO4·7H2O) 均非子问标记 → 仅 1 个真实标记 (1) → 非多部分
        Assert.False(MultiPartUtility.IsMultiPart(
            "处理 [Cu(NH3)4]2+ 与 (FeSO4·7H2O)：(1) 写出离子方程式 ____"));
    }

    [Fact]
    public void SplitSubQuestions_Returns_Ordered_And_Strips_Markers()
    {
        var text = "大题引语略：(1) 计算 x 值；(2) 写出方程式；(3) 说明理由。";
        var subs = MultiPartUtility.SplitSubQuestions(text);

        Assert.Equal(3, subs.Count);
        Assert.Equal(1, subs[0].Index);
        Assert.Equal(2, subs[1].Index);
        Assert.Equal(3, subs[2].Index);
        // 子问文本不含前导 "(N)" 标记
        Assert.DoesNotContain("(1)", subs[0].Text);
        Assert.StartsWith("计算 x 值", subs[0].Text);
        Assert.StartsWith("写出方程式", subs[1].Text);
    }

    [Fact]
    public void SplitSubQuestions_Accepts_Fullwidth_Markers_And_Skips_Empty()
    {
        // （1）全角 + (2) 半角混用；(2) 后紧跟 (3) 之间无内容 → 空子问应被跳过
        var text = "（1）第一问答 ____(2) 第二问 ____ (3)";
        var subs = MultiPartUtility.SplitSubQuestions(text);

        Assert.Equal(2, subs.Count);
        Assert.Equal(1, subs[0].Index);
        Assert.Equal(2, subs[1].Index);
        Assert.StartsWith("第一问答", subs[0].Text);
    }

    [Fact]
    public void SplitSubQuestions_Empty_For_NonMultiPart()
    {
        Assert.Empty(MultiPartUtility.SplitSubQuestions("无标记的单题"));
        Assert.Empty(MultiPartUtility.SplitSubQuestions(null));
    }
}
