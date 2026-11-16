using CogniThink.Chem.Core.Benchmark;
using CogniThink.Chem.Core.Data;
using CogniThink.Chem.Core.Knowledge;
using CogniThink.Chem.Core.Solving;

namespace CogniThink.Chem.Core.Explanation;

/// <summary>一级引导（苏格拉底式提问的一个层次）。</summary>
public sealed class GuideLevel
{
    public int Level { get; set; }

    /// <summary>层次标题（定位题型/回忆知识点/关键思路/更具体的提示）。</summary>
    public string Title { get; set; } = "";

    /// <summary>本层的引导问题序列。</summary>
    public List<string> Prompts { get; set; } = new();
}

/// <summary>
/// 引导结果：分级启发提问（伴思模式）。结构化层次 + 中文全文双形态。
/// </summary>
public sealed class Guide
{
    public string ProblemId { get; set; } = "";

    public ProblemCategory Category { get; set; }

    public List<GuideLevel> Levels { get; set; } = new();

    public string FullText { get; set; } = "";
}

/// <summary>
/// 引导生成器（"能指导学生思考"）：由 <see cref="Solution"/> 与题面生成分级启发提问。
/// 伴思铁律：只引导、不给答案——引导文本中不得出现答案方程式、成分结论、
/// 物质赋值或计算数值；最多指向知识条目编号/知识点名称，让学生自己推出结论。
/// 四级递进：定位题型 → 回忆知识点 → 关键思路 → 更具体的提示。
/// </summary>
public static class GuideGenerator
{
    /// <summary>生成引导。</summary>
    /// <param name="solution">引擎解答（提供知识点线索与证据编号）。</param>
    /// <param name="problem">题目（提供设问目标与已知条件）。</param>
    /// <param name="knowledge">知识库；缺省从 <see cref="DataRoot.Resolve()"/> 载入。</param>
    public static Guide Generate(Solution solution, ProblemSpec problem, KnowledgeBase? knowledge = null)
    {
        knowledge ??= ChemDataLoader.LoadKnowledge(DataRoot.Resolve());
        var guide = new Guide
        {
            ProblemId = problem.Id,
            Category = problem.Category,
        };

        guide.Levels.Add(BuildLevel1(problem));
        guide.Levels.Add(BuildLevel2(solution, knowledge));
        guide.Levels.Add(BuildLevel3(problem));
        guide.Levels.Add(BuildLevel4(solution, problem));
        guide.FullText = RenderFullText(guide);
        return guide;
    }

    /// <summary>第 1 级：定位题型——先想清楚"这是什么题、要什么答案"。</summary>
    private static GuideLevel BuildLevel1(ProblemSpec problem)
    {
        var targets = problem.Question.Targets.Count > 0
            ? string.Join("、", problem.Question.Targets)
            : "题目的最终设问";
        return new GuideLevel
        {
            Level = 1,
            Title = "定位题型",
            Prompts =
            {
                $"这是一道{Explainer.CategoryName(problem.Category)}题。先问自己：题目到底要求什么？（{targets}）",
                "答案应该以什么形式呈现——化学方程式、存在性结论、物质对应关系，还是精确数值？",
            },
        };
    }

    /// <summary>第 2 级：回忆知识点——激活解题所需的课内知识。</summary>
    private static GuideLevel BuildLevel2(Solution solution, KnowledgeBase knowledge)
    {
        var level = new GuideLevel { Level = 2, Title = "回忆知识点" };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in solution.Steps)
        {
            if (step.KnowledgeId.Length == 0 || !seen.Add(step.KnowledgeId))
            {
                continue;
            }

            if (knowledge.TryGet(step.KnowledgeId, out var item))
            {
                level.Prompts.Add($"回顾知识点「{item.Name}」（{item.TextbookRef}）：你能复述它的要点吗？");
            }
        }

        if (level.Prompts.Count == 0)
        {
            level.Prompts.Add("本题要用的核心公式或反应规律是什么？先把它默写出来，再决定从哪里入手。");
        }

        return level;
    }

    /// <summary>第 3 级：关键思路——按类别给出通用策略提问（不含具体结论）。</summary>
    private static GuideLevel BuildLevel3(ProblemSpec problem)
    {
        var level = new GuideLevel { Level = 3, Title = "关键思路" };
        switch (problem.Category)
        {
            case ProblemCategory.ProductInference:
                var conditions = problem.Given.Conditions.Count > 0
                    ? $"，特别注意条件 [{string.Join("、", problem.Given.Conditions)}]"
                    : "";
                level.Prompts.Add($"反应物组合（{string.Join(" + ", problem.Given.Species)}）在课内学过哪些反应{conditions}？哪一个与题给条件完全吻合？");
                level.Prompts.Add("写出候选方程式后，检查它是否配平、是否满足守恒——有没有漏写的产物？");
                break;

            case ProblemCategory.IonCoexistence:
                var medium = problem.Given.Medium == "acidic"
                    ? "酸性介质会引入 H^+，它与哪些离子不能共存？"
                    : problem.Given.Medium == "basic"
                        ? "碱性介质会引入 OH^-，它与哪些离子不能共存？"
                        : "介质不限时，只需检查题给离子两两之间的反应。";
                level.Prompts.Add("把离子两两配对：有没有哪一对结合会生成沉淀、气体或弱电解质？");
                level.Prompts.Add(medium);
                break;

            case ProblemCategory.SubstanceIdentification:
                level.Prompts.Add("逐步分析每个实验现象：这一步证明了什么、又排除了什么？");
                level.Prompts.Add("把无法解释某一步现象的候选划掉；注意试剂本身会不会引入干扰离子。");
                break;

            case ProblemCategory.IonPowderIdentification:
                level.Prompts.Add("先分清推断对象：是溶液的连续操作，还是固体粉末的分步溶解与现象？每一步操作证明了什么、又排除了什么？");
                level.Prompts.Add("对「无明显现象」的步骤：逐一检查哪些候选若存在就一定会与该步试剂反应——能反应的都要排除；对正向现象再反问：去掉某个成分后，这一步现象还能被其余确定成分完全解释吗？");
                level.Prompts.Add("注意试剂本身引入的离子会不会污染后续检验；无法由任何一步现象证明的候选，应如实归入「不能确定」。");
                break;

            case ProblemCategory.ProcessInference:
                level.Prompts.Add("从最有特征的现象（颜色、气体、沉淀）入手，先锁定链中最确定的那个物质。");
                level.Prompts.Add("再沿转化链逐个验证：每一步的反应物与产物是否都能在候选中找到对应？");
                break;

            case ProblemCategory.StoichiometryCalc:
                var formula = problem.Expected.Calc is { } calc
                    ? ProblemSolver.FormulaOf(calc.Op)
                    : "相应的换算公式";
                level.Prompts.Add($"先确定计算路线（{formula}），再检查每个已知量的单位是否需要换算（如 mL→L）。");
                level.Prompts.Add("代入数值时用分数精确运算，最后检查结果的量纲与数值是否合理。");
                break;

            case ProblemCategory.Electrochemistry:
                level.Prompts.Add("先判断装置类型：这是原电池还是电解池？两种装置的电极名称（正负极/阴阳极）分别对应氧化反应还是还原反应？");
                level.Prompts.Add("写出装置的总反应，标出化合价升高与降低的元素：失电子的一极发生氧化反应，你能据此确定该极的电极反应式吗？");
                level.Prompts.Add("另一极的电极反应式可由总反应减去已知半反应得到——先按电子守恒确定倍数，再移项，最后检查介质（酸性补 H^+、碱性补 OH^-）下各物种是否合理。");
                break;

            case ProblemCategory.ChemicalEquilibrium:
                level.Prompts.Add("先把可逆反应配平，写出三段式框架：起始、转化、平衡三行各物种的量如何用同一转化进度 x 与计量数 ν 表示？");
                level.Prompts.Add("题给的平衡信息（转化率 / 某物种平衡量 / 某物种平衡分数）能列出一个关于 x 的一元一次方程吗？注意转化量 = |ν|·x，别把转化量与平衡量混淆。");
                level.Prompts.Add("求平衡分数时分母是平衡时总物质的量——反应前后气体分子数变化（Δν）会改变总量，别忘了计入；判断移动方向回到勒夏特列原理：改变条件，平衡向减弱这种改变的方向移动。");
                break;

            case ProblemCategory.AqueousIonEquilibrium:
                level.Prompts.Add("先写出溶液中全部电离与水解方程：哪些溶质完全电离？哪些离子发生水解（越弱越水解，Kh = Kw/Ka）？水解或电离竞争后溶液显酸性还是碱性？");
                level.Prompts.Add("列出三大守恒：电荷守恒（阳离子所带正电荷总浓度 = 阴离子所带负电荷总浓度）、物料守恒（某元素各种存在形态的总量比）、质子守恒（得失质子数相等）。浓度排序时完全电离的粒子一般大于弱过程产生的粒子。");
                level.Prompts.Add("遇到两种弱过程粒子比大小（如 NaHCO3 中 c(CO3^2-) 与 c(OH^-)）：先问自己现有守恒关系能否判定；若需解联立平衡才能比较，应如实承认条件不足，不要凭印象下结论。");
                break;

            case ProblemCategory.OrganicInference:
                level.Prompts.Add("先找特征反应：哪些步骤的试剂与条件（溴水、银氨溶液、新制氢氧化铜、浓硫酸/浓硝酸、光照取代、催化加氢等）在课内只对应一类官能团？由此锁定链中最确定的物质。");
                level.Prompts.Add("沿转化链逐个验证：把候选有机物代入每一步，反应物组合与条件能否在课内有机反应中找到完全吻合的条目？产物是否也落在候选集内？");
                level.Prompts.Add("注意同分异构与连锁取代的陷阱：一种条件可能对应多条候选路径，检查是否存在第二种赋值也能解释全部步骤；若有，如实承认条件不足。");
                break;

            case ProblemCategory.IndustrialProcess:
                level.Prompts.Add("先通读整个流程，把步骤分成两类：发生化学转化的步骤（原料→中间产物→产品）与纯操作步骤（过滤、洗涤、灼烧、结晶、循环）。转化链从最有特征的现象或最确定的试剂入手锁定各步物质。");
                level.Prompts.Add("对每个操作步骤问自己：它在除杂、沉淀转化、pH 调控还是分离提纯的哪个环节起作用？除杂原则（不引入新杂质、不减少主体物质）如何约束试剂的加入顺序与用量？");
                level.Prompts.Add("涉及沉淀与 pH 的步骤，回到溶度积判据：沉淀向更难溶的方向转化；控制 pH 使杂质离子沉淀完全而主体离子不沉淀。写目的时先回忆对应规则条目，再用自己的话复述判据。");
                break;

            case ProblemCategory.ExperimentalInquiry:
                level.Prompts.Add("先弄清本题问的是哪一类探究：气体的检验还是验满？基本操作的目的与判据？滴定误差分析？还是由现象反推结论？每一类在教材中都有固定表述模板，先回忆对应条目再作答。");
                level.Prompts.Add("检验与验满别混淆：检验回答「瓶内是什么」，验满证明「已集满到瓶口」——操作位置与判断现象各不相同。写答案时按「操作—现象—结论」三段完整表述。");
                level.Prompts.Add("误差分析先回到操作原理：这一步做与不做改变了哪个量（浓度 c 还是体积 V）？仪器刻度方向会不会让读数偏差放大？由改变量推出结果偏高还是偏低。");
                break;
        }

        return level;
    }

    /// <summary>第 4 级：更具体的提示——指向关键证据（知识条目编号），仍不给出结论。</summary>
    private static GuideLevel BuildLevel4(Solution solution, ProblemSpec problem)
    {
        var level = new GuideLevel { Level = 4, Title = "更具体的提示" };
        if (problem.Category == ProblemCategory.StoichiometryCalc && problem.Expected.Calc is { } calc)
        {
            level.Prompts.Add($"最后的提示：按 {ProblemSolver.FormulaOf(calc.Op)} 代入题给数值，注意精确计算。");
            return level;
        }

        var evidence = solution.Steps
            .Select(s => s.Evidence)
            .FirstOrDefault(e => e.StartsWith("RX-", StringComparison.Ordinal));
        if (evidence is not null)
        {
            level.Prompts.Add($"最后的提示：本题的关键反应记录在知识条目 {evidence} 中——确认它的反应物与条件，结论就在其中。");
        }
        else
        {
            level.Prompts.Add("最后的提示：回到第 2 级的知识点，把与题给条件最吻合的那条规律写出来，再对照题目逐步排查。");
        }

        return level;
    }

    private static string RenderFullText(Guide guide)
    {
        var lines = new List<string>
        {
            $"【{guide.ProblemId}】引导思路（伴思模式：只引导，不给答案）",
        };
        foreach (var level in guide.Levels)
        {
            lines.Add($"◆ 第 {level.Level} 级·{level.Title}");
            foreach (var prompt in level.Prompts)
            {
                lines.Add($"  - {prompt}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}
