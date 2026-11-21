using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// 优先级 1：<c>judgment-statement-facts</c> 独立通用化学知识陈述事实库的端到端单测。
/// <para>
/// 对题库中 6 道单小题概念判断题（pdf-120/121/122/466/470/473），断言
/// <see cref="JudgmentStatementAggregator"/> 经 FactBaseStatementJudge 命中事实后
/// 输出与题库 answer 一致的 √/×，且不蒙数、不跨题误命中。
/// </para>
/// <para>红线护栏：这些题在加事实前聚合器返回 null（Undecided），加事实后唯一确定。</para>
/// </summary>
public class JudgmentStatementFactsTests
{
    private static EngineRequest Req(string raw) => new() { RawText = raw };

    [Fact]
    public void Pdf120_SiO2光导纤维利用导电性_判错()
    {
        var r = JudgmentStatementAggregator.TrySolve(Req("(4)SiO2可用于生产光导纤维,利用了SiO2的导电性（ ）"));
        Assert.NotNull(r);
        Assert.Equal("×", r!.FinalResult);
    }

    [Fact]
    public void Pdf121_石墨烯碳纳米管碳纤维均属无机非金属材料_判对()
    {
        var r = JudgmentStatementAggregator.TrySolve(Req("(5)石墨烯、碳纳米管、碳纤维均属于无机非金属材料（ ）"));
        Assert.NotNull(r);
        Assert.Equal("√", r!.FinalResult);
    }

    [Fact]
    public void Pdf122_单晶硅芯片主要成分可被氢氟酸腐蚀_判对()
    {
        var r = JudgmentStatementAggregator.TrySolve(Req("(6)单晶硅芯片的主要成分可被氢氟酸腐蚀（ ）"));
        Assert.NotNull(r);
        Assert.Equal("√", r!.FinalResult);
    }

    [Fact]
    public void Pdf466_瓷坩埚不可加热熔化烧碱或纯碱_判对()
    {
        var r = JudgmentStatementAggregator.TrySolve(Req("(7)瓷坩埚不可用于加热熔化烧碱或纯碱（ ）"));
        Assert.NotNull(r);
        Assert.Equal("√", r!.FinalResult);
    }

    [Fact]
    public void Pdf470_月面国旗玄武岩纤维属无机非金属材料_判对()
    {
        var r = JudgmentStatementAggregator.TrySolve(Req("(11)月面国旗由超细玄武岩纤维纺织而成,属于无机非金属材料（ ）"));
        Assert.NotNull(r);
        Assert.Equal("√", r!.FinalResult);
    }

    [Fact]
    public void Pdf473_Na2CO3与Na2SiO3不能证明非金属性Cl大于C大于Si_判错()
    {
        var r = JudgmentStatementAggregator.TrySolve(Req("(14)固体Na2CO3、溶液Na2SiO3证明非金属性:Cl>C>Si（ ）"));
        Assert.NotNull(r);
        Assert.Equal("×", r!.FinalResult);
    }

    /// <summary>
    /// pdf-450：9 段全文本正误判断（题干用引号包裹 √/×，原被路由到 equilibrium）。
    /// 路由信号拓宽后进入 judgment 分支，9 条通用事实全部命中 → 输出 ××√××××××，与 answer 对齐。
    /// 红线护栏：加事实前聚合器返回 null（9 小题全 Undecided），加事实后唯一确定。
    /// </summary>
    [Fact]
    public void Pdf450_九段正误判断_全命中判正()
    {
        var text = "(高考组合题·★★)判断下列说法是否正确,正确的填“√”,错误的填“×”。" +
                   "(1)煤经过气化和液化等物理变化可转化为清洁燃料( )" +
                   "(2)生石灰用作食品抗氧化剂( )" +
                   "(3)高纯硅可用于制作光感电池( )" +
                   "(4)SiO2 具有导电性,可用于制作光导纤维和光电池( )" +
                   "(5)单晶硅和石英互为同素异形体( )" +
                   "(6)合成纤维、人造纤维及碳纤维都属于有机高分子材料( )" +
                   "(7)纤维素在人体内可水解为葡萄糖,故可作人类的营养物质( )" +
                   "(8)可燃冰的燃烧会产生二氧化碳,会增加温室效应气体的排放和形成酸雨( )" +
                   "(9)油脂产生“哈喇”味,因其发生了水解反应( )";
        var r = JudgmentStatementAggregator.TrySolve(Req(text));
        Assert.NotNull(r);
        // 只保留 √/× 符号，断言序列为 ××√××××××（与题库 answer 对齐），分隔符变化不影响判分
        var symbols = new string(r!.FinalResult.Where(ch => ch == '√' || ch == '×').ToArray());
        Assert.Equal("××√××××××", symbols);
    }

    /// <summary>
    /// pdf-119：单小题正误判断，题干为多步工业流程 "SiO2 → Si(粗硅) → SiHCl3 → Si(纯硅)"。
    /// 原被 ExtractEquationAndContext 误解析为含 3 个等号的"方程"，符号引擎无法判定 → 整题 null。
    /// 聚合器改为：方程无法判定时回退到 CompositeStatementJudge；JS-016 事实命中 → 输出 √，与 answer 对齐。
    /// 红线护栏：改聚合器/加事实前返回 null；改后唯一确定且 verdict 化学正确。
    /// </summary>
    [Fact]
    public void Pdf119_工业制高纯硅多步流程_判对()
    {
        var text = "(3)工业制高纯硅:SiO2 --(C,高温)--> Si(粗硅) --(HCl,300℃)--> SiHCl3 --(H2,1100℃)--> Si(纯硅)（ ）";
        var r = JudgmentStatementAggregator.TrySolve(Req(text));
        Assert.NotNull(r);
        Assert.Equal("√", r!.FinalResult);
    }

    /// <summary>
    /// 防跨题误命中：把多子题中某个可判的概念陈述混入含图像依赖小题的大题，
    /// 因存在 Undecided 小题 → 聚合铁律整题仍 null（不输出部分答案 → 不被采纳 → 无 harm）。
    /// </summary>
    [Fact]
    public void MixedWithUndecidedSub_ReturnsNull_NoHarm()
    {
        var q = "(4)SiO2可用于生产光导纤维,利用了SiO2的导电性（ ）(9)可以用装置 制备并收集 SO2（ ）";
        var r = JudgmentStatementAggregator.TrySolve(Req(q));
        Assert.Null(r);
    }
}
