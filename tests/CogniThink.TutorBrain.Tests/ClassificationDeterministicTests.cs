using System.Runtime.CompilerServices;
using CogniThink.TutorBrain.Application.Common;
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

    // ---- 新增：judgment（正误判断填 √/×）----
    [Fact]
    public void Judgment_正误判断_归类为judgment()
        => Assert.Equal("judgment", Classify("正误判断，正确的填√，错误的填×"));

    [Fact]
    public void Judgment_书写的正误_归类为judgment_不被unknown截走()
        // pdf-051 式题干："判断…书写的正误，正确的填√，错误的填×"（不含"正误判断"整词）
        => Assert.Equal("judgment", Classify("判断下列离子方程式书写的正误，正确的填√，错误的填×"));

    [Fact]
    public void Judgment_填对勾叉信号_归类为judgment()
        => Assert.Equal("judgment", Classify("下列说法是否正确，正确的填√，错误的填×"));

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

    // ============================================================
    // P1-4：自由填空概念路由放宽（插在 equation_writer 与 calculation 之间）
    // 全库 757 预校验（_evalscratch/verify/p14_routing_drift_precheck.py）断言 0 道已判正题重分类。
    // ============================================================

    // ---- 正例：3 道误路由到 calculation 的概念题被截走到 concept ----
    [Fact]
    public void P14_除去的离子_归类为concept_不被calculation截走()
        => Assert.Equal("concept", Classify("(4)步骤④中用盐酸调节 pH 至 3～4，除去的离子有____。"));

    [Fact]
    public void P14_调pH目的_归类为concept_不被calculation截走()
        => Assert.Equal("concept", Classify("(3)①Ⅲ加硫酸调节溶液pH的目的是____。"));

    [Fact]
    public void P14_配制浓度可能_归类为concept_不被calculation截走()
        => Assert.Equal("concept", Classify("若操作a只进行了1次，则配制浓度可能____(填偏大偏小或无影响)"));

    // ---- 靠事实库解（路由不动，保持 unknown，瀑布含 ConceptFactSolver）----
    [Fact]
    public void P14_沉淀1金属元素_保持unknown_靠事实库命中()
        => Assert.Equal("unknown", Classify("(2)“沉淀1”含金属元素____(填元素符号)。"));

    [Fact]
    public void P14_方案2优点_保持unknown_靠事实库命中()
        => Assert.Equal("unknown", Classify("(7)相比于方案3，方案2的优点是____。"));

    // ---- 0 漂移：合取守卫不误伤；代表性题 taskType 不变 ----
    [Fact]
    public void P14_NoSteal_ph无目的_不被concept截走()
        // "ph" 单独出现（无 "目的"）不得触发 concept，证明 ph∧目的 合取守卫
        => Assert.Equal("calculation", Classify("0.1mol/L 盐酸的 pH 等于多少"));

    [Fact]
    public void P14_NoSteal_配制浓度缺可能_不被concept截走()
        // "配制"∧"浓度" 但缺 "可能" 不得触发 concept，证明三元合取守卫
        => Assert.Equal("calculation", Classify("配制一定物质的量浓度的溶液需要哪些仪器"));

    [Fact]
    public void P14_NoSteal_排除域参考题_不被重分类()
        // 与排除项 pdf-483 同域（加入氯化钠+作用）的已判正表述，不得被新 cue 重分类
        => Assert.Equal("unknown", Classify("电解熔融氯化铍制备金属铍时，加入氯化钠的主要作用是____。"));

    // ============================================================
    // P1-5：方程书写问法补齐（cue A：「…方程式为 / 用…方程式表示」→ equation_writer）
    // 插在 equation_writer 块之后、P1-4 concept cues 之前。全库 757 预校验
    // （_evalscratch/verify/p15_drift_precheck.py）：affected=16，correct-touched 恰为
    // 预期 5 道（pdf-174/417/456/515/539），无意外漂移；5 道保持判正由 eval AC-3 实证。
    // ============================================================

    // ---- 正例 (a)：3 道 unknown 被 cue A 截走到 equation_writer ----
    [Fact]
    public void P15_化学方程式为_归类为equation_writer_pdf164()
        => Assert.Equal("equation_writer", Classify("(5)“沉淀”步骤中生成 BaTiO(C2O4)2 的化学方程式为________________________________。"));

    [Fact]
    public void P15_离子方程式为_归类为equation_writer_pdf462()
        => Assert.Equal("equation_writer", Classify("(4)“滤饼”的成分为 AlOOH,则“沉铝”的离子方程式为________________________。"));

    [Fact]
    public void P15_用离子方程式表示_归类为equation_writer_pdf179()
        => Assert.Equal("equation_writer", Classify("（3）用离子方程式表示置换过程中加入 Fe 的目的：____________________。"));

    // ---- (b)：5 道已判正题改判为 equation_writer（分类断言；保持判正由 eval AC-3 实证）----
    [Fact]
    public void P15_已判正5道_改判为equation_writer_保持判正待eval实证()
    {
        // pdf-174：MnO2 反应的离子方程式为
        Assert.Equal("equation_writer", Classify("(2)“还原酸浸”时，MnO2 反应的离子方程式为____________________。"));
        // pdf-417：用离子方程式表示加入盐酸后发生的反应
        Assert.Equal("equation_writer", Classify("(6)请用离子方程式表示加入盐酸后发生的反应：__________________________。"));
        // pdf-456：装置A中产生SO2的化学方程式为
        Assert.Equal("equation_writer", Classify("(1)装置A中产生SO2的化学方程式为"));
        // pdf-515：向 NaCN 中通入少量 CO2，发生反应的离子方程式为
        Assert.Equal("equation_writer", Classify("(2)向 NaCN 中通入少量的 CO2，发生反应的离子方程式为____________________。"));
        // pdf-539：GaN 反应生成[Ga(OH)4]⁻的离子方程式为
        Assert.Equal("equation_writer", Classify("(2)Ⅰ中GaN反应生成[Ga(OH)4]⁻的离子方程式为________________________。"));
    }

    // ---- (c) 不窃取 guard：cue A 不抢占高优先级分支 ----
    [Fact]
    public void P15_NoSteal_choice选项含化学方程式为_仍为choice()
        // 选项中出现 cue A 子串，但 choice 正则先匹配 → 保持 choice（证明 cue A 不越权）
        => Assert.Equal("choice", Classify("下列说法正确的是\nA. 该反应的化学方程式为 2H2+O2=2H2O  B. xxx  C. yyy  D. zzz"));

    [Fact]
    public void P15_NoSteal_旧equation_writer问法仍命中_无回归()
        // 既有「书写/写出」+「方程式」路径不受影响（cue A 为增补，不替换）
        => Assert.Equal("equation_writer", Classify("书写下列反应的离子方程式：Fe + CuSO4 ="));

    // ---- (d) 回归守卫：equation_writer 分支必须可达 ProductWriterSolver ----
    // P1-5 修复（根因见 .trae/specs/routing-unknown-backfill）：cue A 把 pdf-515 从 LLM 兜底的
    // reaction_check 重分类到 equation_writer 后，equation_writer 分支原本只调 Known/ReactionKb，
    // 不含 ProductWriterSolver → pdf-515（NaCN + 少量 CO2）丢失求解器 → LOST。
    // 修复后该分支按 reaction_check 同款顺序（Product → Known → ReactionKb）补齐。
    // 端到端断言：经 cue A 归为 equation_writer 的 pdf-515 式题干，由网关产出 ProductWriterSolver 方程。
    private static LlmGatewayService NewGateway() =>
        (LlmGatewayService)RuntimeHelpers.GetUninitializedObject(typeof(LlmGatewayService));

    [Fact]
    public async Task P15_equationWriter分支_可达ProductWriterSolver_pdf515()
    {
        var stem = "(2)向 NaCN 中通入少量的 CO2，发生反应的离子方程式为____________________。";

        // 1) cue A 把该题干归到 equation_writer（而非保留 LLM 的 reaction_check / 落 unknown）
        Assert.Equal("equation_writer", Classify(stem));

        // 2) equation_writer 分支应命中 ProductWriterSolver 并产出正确方程（不得 Ok=null）
        var req = new EngineRequest { TaskType = "equation_writer", RawText = stem };
        var resp = await NewGateway().CallEngineAsync(req, new List<ImageAsset>());

        Assert.NotNull(resp);
        Assert.True(resp.Ok == true, $"expected Ok=true, got FinalResult={resp.FinalResult}");
        Assert.StartsWith("离子方程式：", resp.FinalResult);
        // ProductWriterSolver 指纹：去 ^、用 → 箭头、含 HCN/HCO3-（区别于 ReactionKbWriterSolver 的 = 与 ^）
        Assert.Contains("CN", resp.FinalResult);
        Assert.Contains("CO2", resp.FinalResult);
        Assert.Contains("H2O", resp.FinalResult);
        Assert.Contains("HCN", resp.FinalResult);
        Assert.Contains("HCO3", resp.FinalResult);
        Assert.DoesNotContain("^", resp.FinalResult);
    }

    [Fact]
    public async Task P15_equationWriter分支_无锚点不蒙数_返回Null()
    {
        // cue A 归 equation_writer，但无任何书写求解器锚点（无特定反应物/量关键词/KB 命中）→ 必须 Ok=null
        var stem = "(3)写出该反应的离子方程式为____________________。";
        Assert.Equal("equation_writer", Classify(stem));

        var req = new EngineRequest { TaskType = "equation_writer", RawText = stem };
        var resp = await NewGateway().CallEngineAsync(req, new List<ImageAsset>());

        Assert.NotNull(resp);
        Assert.True(resp.Ok == null, $"无锚点应返回 Ok=null，不得臆造方程: FinalResult={resp.FinalResult}");
    }
}