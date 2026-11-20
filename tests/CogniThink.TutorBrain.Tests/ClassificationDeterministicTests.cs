using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// 阶段 6 / Task 23（AC-23）：ClassifyTaskDeterministic 分类链单元测试。
/// 验证新增分支（atomic_structure / equilibrium 扩展 / reaction_check 扩展）正确归类，
/// 且不抢占 choice 与已有求解分支（零回归）。分类优先级：choice > 共存 > 配平 > 平衡 > 原子结构 > 计算 >
/// 反应校验 > 结构 > vsepr > 滴定 > 概念 > unknown。
/// </summary>
public class ClassificationDeterministicTests
{
    private static string Classify(string text) => LlmGatewayService.ClassifyTaskDeterministic(text);

    // ---- 新增：atomic_structure ----
    [Fact]
    public void Atomic_价层电子排布式_归类为atomic_structure()
        => Assert.Equal("atomic_structure", Classify("基态N原子的价层电子排布式是_______________________"));

    [Fact]
    public void Atomic_轨道表示式_归类为atomic_structure()
        => Assert.Equal("atomic_structure", Classify("基态Fe原子的3d轨道表示式"));

    [Fact]
    public void Atomic_未成对电子_归类为atomic_structure()
        => Assert.Equal("atomic_structure", Classify("基态Mn原子未成对电子数为____"));

    [Fact]
    public void Atomic_含mol与pH的电子排布题_优先归atomic_不被calculation截走()
        => Assert.Equal("atomic_structure", Classify("加入2 mol/L的NaOH调节pH为8，基态Te原子的简化电子排布式为____"));

    // ---- 新增：equilibrium 扩展 ----
    [Fact]
    public void Equilibrium_守恒式正确_归类为equilibrium()
        => Assert.Equal("equilibrium", Classify("Na2CO3溶液中，电荷守恒式正确的是"));

    [Fact]
    public void Equilibrium_酸性由弱到强_归类为equilibrium()
        => Assert.Equal("equilibrium", Classify("CH3COOH、H2CO3、HCN酸性由弱到强的顺序是"));

    [Fact]
    public void Equilibrium_酸性由强到弱_归类为equilibrium()
        => Assert.Equal("equilibrium", Classify("HClO4、H2SO4、H3PO4酸性由强到弱排序"));

    // ---- 新增：reaction_check 扩展 ----
    [Fact]
    public void Reaction_正误判断_归类为reaction_check()
        => Assert.Equal("reaction_check", Classify("正误判断，正确的填√，错误的填×"));

    [Fact]
    public void Reaction_是否可行_归类为reaction_check()
        => Assert.Equal("reaction_check", Classify("焙烧后的产物直接用酸浸取，是否可行"));

    // ---- 不抢占：choice 最高优先级 ----
    [Fact]
    public void Choice_含电子排布仍是choice_不被原子分支抢占()
    {
        var q = "基态N原子的价层电子排布式是\nA. 2s22p3  B. 2s22p5  C. 3s23p3  D. 3s23p5";
        Assert.Equal("choice", Classify(q));
    }

    [Fact]
    public void Choice_含正误判断仍是choice_不被反应分支抢占()
    {
        var q = "下列说法正误判断正确的是\nA. xxx  B. yyy  C. zzz  D. www";
        Assert.Equal("choice", Classify(q));
    }

    // ---- 不抢占：已有分支 ----
    [Fact]
    public void NoSteal_平衡题仍为equilibrium()
        => Assert.Equal("equilibrium", Classify("升高温度，化学平衡向吸热方向移动，转化率如何变化"));

    [Fact]
    public void NoSteal_计算题仍为calculation()
        => Assert.Equal("calculation", Classify("配制100mL 0.5mol/L的NaCl溶液，需要NaCl多少克"));

    [Fact]
    public void NoSteal_vsepr题仍为vsepr_不被原子分支误抢()
        => Assert.Equal("vsepr", Classify("用价层电子对互斥理论判断NH3的空间构型和中心原子杂化类型"));

    [Fact]
    public void NoSteal_离子共存仍为ion_check()
        => Assert.Equal("ion_check", Classify("下列离子在酸性溶液中能否大量共存"));

    [Fact]
    public void NoSteal_配平仍为balance()
        => Assert.Equal("balance", Classify("配平下列化学方程式，系数为"));

    // ---- 兜底 ----
    [Fact]
    public void Fallback_纯概念仍为concept_无关键词为unknown()
    {
        Assert.Equal("concept", Classify("什么是强电解质"));
        Assert.Equal("unknown", Classify("比较两种物质的性质并说明理由"));
    }
}