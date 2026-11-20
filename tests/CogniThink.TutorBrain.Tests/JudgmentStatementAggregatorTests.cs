using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// Task 2：<see cref="JudgmentStatementAggregator"/> 正误判断聚合器单元测试。
/// <para>真值源为符号化学引擎（方程小题）+ 既有陈述判定链（概念小题）；断言基于真实引擎输出。</para>
/// <para>核心护栏：任一小题不确定（方程 null / 陈述 Undecided）→ 整题返回 null（不蒙数、零假阳性）。</para>
/// </summary>
public class JudgmentStatementAggregatorTests
{
    private static EngineRequest Req(string raw) => new() { RawText = raw };

    // ---------- 方程小题：真实引擎稳定样例（复用 EquationCorrectnessJudgeTests 已断言的判决） ----------

    /// <summary>全部小题均可确定 → 输出带编号的 √/× 序列（N&gt;1 格式）。</summary>
    [Fact]
    public void AllSubsDetermined_OutputsNumberedSequence()
    {
        var q = "(1)2Br^- + Cl2 = Br2 + 2Cl^-（ ）(2)SiO2 + 4H^+ + 4F^- = SiF4↑ + 2H2O（ ）";
        var r = JudgmentStatementAggregator.TrySolve(Req(q));
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Equal("(1)√ (2)×", r.FinalResult);
    }

    /// <summary>单小题正确方程 → 输出单字符 √。</summary>
    [Fact]
    public void SingleCorrectEquation_OutputsCheckmark()
    {
        var r = JudgmentStatementAggregator.TrySolve(Req("(1)SiO2 + 2OH^- = SiO3^2- + H2O（ ）"));
        Assert.NotNull(r);
        Assert.Equal("√", r!.FinalResult);
    }

    /// <summary>单小题错误方程 → 输出单字符 ×。</summary>
    [Fact]
    public void SingleWrongEquation_OutputsCross()
    {
        var r = JudgmentStatementAggregator.TrySolve(Req("(1)SiO2 + 4H^+ + 4F^- = SiF4↑ + 2H2O（ ）"));
        Assert.NotNull(r);
        Assert.Equal("×", r!.FinalResult);
    }

    // ---------- 聚合铁律：任一不确定 → 整题 null ----------

    /// <summary>方程小题引擎判 null（超纲/氧化数未知等）→ 整题 null，不蒙数。</summary>
    [Fact]
    public void UndeterminedEquationSub_ReturnsNull()
    {
        // 无"亚铜氨"语境时引擎判 null（RuleB_OnlyFires 已断言）
        var r = JudgmentStatementAggregator.TrySolve(
            Req("(1)[Cu(NH3)2]^2+ + CO + NH3 = [Cu(NH3)3CO]^2+（ ）"));
        Assert.Null(r);
    }

    /// <summary>概念小题无任何事实命中 → CompositeStatementJudge 返回 Undecided → 整题 null。</summary>
    [Fact]
    public void UndecidedConceptSub_ReturnsNull()
    {
        var r = JudgmentStatementAggregator.TrySolve(
            Req("(1)某不存在于事实库的概念陈述xyz（ ）"));
        Assert.Null(r);
    }

    /// <summary>混合：前一小题确定（√）、后一小题不确定 → 整题仍 null（不输出部分答案）。</summary>
    [Fact]
    public void MixedDeterminedAndUndecided_ReturnsNull()
    {
        var q = "(1)2Br^- + Cl2 = Br2 + 2Cl^-（ ）(2)某不存在于事实库的概念陈述xyz（ ）";
        var r = JudgmentStatementAggregator.TrySolve(Req(q));
        Assert.Null(r);
    }

    // ---------- 零回归：非判断题不触发 ----------

    /// <summary>非 √/× 判断题文本 → 不命中拆分 → 返回 null（不影响 choice/fill/calc 等其他链路）。</summary>
    [Fact]
    public void NonJudgmentText_ReturnsNull()
    {
        Assert.Null(JudgmentStatementAggregator.TrySolve(Req("下列说法正确的是")));
        Assert.Null(JudgmentStatementAggregator.TrySolve(Req("配平下列方程式：Fe + O2 = Fe3O4")));
        Assert.Null(JudgmentStatementAggregator.TrySolve(Req("")));
    }
}
