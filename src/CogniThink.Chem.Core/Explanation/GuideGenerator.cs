using CogniThink.Chem.Core.Benchmark;
using CogniThink.Chem.Core.Data;
using CogniThink.Chem.Core.Knowledge;
using CogniThink.Chem.Core.Reactions;
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
    /// <param name="reactions">反应知识库；缺省从 <see cref="DataRoot.Resolve()"/> 载入，用于第 4 级查反应条目。</param>
    public static Guide Generate(Solution solution, ProblemSpec problem, KnowledgeBase? knowledge = null, ReactionLibrary? reactions = null)
    {
        knowledge ??= ChemDataLoader.LoadKnowledge(DataRoot.Resolve());
        reactions ??= ReactionLibrary.Load(DataRoot.Resolve());
        var guide = new Guide
        {
            ProblemId = problem.Id,
            Category = problem.Category,
        };

        guide.Levels.Add(BuildLevel1(problem));
        guide.Levels.Add(BuildLevel2(solution, knowledge));
        guide.Levels.Add(BuildLevel3(problem));
        guide.Levels.Add(BuildLevel4(solution, problem, knowledge, reactions));
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

    /// <summary>第 2 级：回忆知识点——激活解题所需的课内知识，直接呈现要点。</summary>
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
                var summary = !string.IsNullOrWhiteSpace(item.Summary)
                    ? $"要点：{item.Summary}"
                    : "回想这条规律的内容与适用条件。";
                level.Prompts.Add($"知识点「{item.Name}」（{item.TextbookRef}）。{summary}");
            }
        }

        if (level.Prompts.Count == 0)
        {
            level.Prompts.Add("本题要用的核心公式或反应规律是什么？先把它默写出来，再决定从哪里入手。");
        }

        return level;
    }

    /// <summary>第 3 级：关键思路——按类别用题给已知条件驱动提示（不含具体结论）。</summary>
    private static GuideLevel BuildLevel3(ProblemSpec problem)
    {
        var level = new GuideLevel { Level = 3, Title = "关键思路" };
        var g = problem.Given;
        switch (problem.Category)
        {
            case ProblemCategory.ProductInference:
                var conditions = g.Conditions.Count > 0
                    ? $"，特别注意条件 [{string.Join("、", g.Conditions)}]"
                    : "";
                level.Prompts.Add($"反应物组合（{string.Join(" + ", g.Species)}）在课内学过哪些反应{conditions}？哪一个与题给条件完全吻合？");
                level.Prompts.Add("写出候选方程式后，检查它是否配平、是否满足守恒——有没有漏写的产物？");
                break;

            case ProblemCategory.IonCoexistence:
                var ions = string.Join("、", g.Species);
                level.Prompts.Add($"待判离子组为 [{ions}]。把它们两两配对：有没有哪一对结合会生成沉淀、气体或弱电解质？");
                var medium = g.Medium == "acidic"
                    ? "酸性介质会引入 H^+，它与哪些离子不能共存？"
                    : g.Medium == "basic"
                        ? "碱性介质会引入 OH^-，它与哪些离子不能共存？"
                        : "介质不限时，只需检查题给离子两两之间的反应。";
                level.Prompts.Add(medium);
                break;

            case ProblemCategory.SubstanceIdentification:
                var candCount = g.CandidateSpecies.Count;
                level.Prompts.Add($"候选物质共 {candCount} 种，实验过程共 {g.Steps.Count} 步。逐步分析每个现象：这一步证明了什么、又排除了什么？");
                level.Prompts.Add("把无法解释某一步现象的候选划掉；注意试剂本身会不会引入干扰离子。");
                break;

            case ProblemCategory.IonPowderIdentification:
                level.Prompts.Add($"推断对象经历 {g.Steps.Count} 步操作。先分清：是溶液的连续操作，还是固体粉末的分步溶解与现象？每一步证明了什么、又排除了什么？");
                level.Prompts.Add("对「无明显现象」的步骤：逐一检查哪些候选若存在就一定会与该步试剂反应——能反应的都要排除；对正向现象再反问：去掉某个成分后，这一步现象还能被其余确定成分完全解释吗？");
                level.Prompts.Add("注意试剂本身引入的离子会不会污染后续检验；无法由任何一步现象证明的候选，应如实归入「不能确定」。");
                break;

            case ProblemCategory.ProcessInference:
                var procCands = g.CandidateSpecies.Count > 0
                    ? $"候选集共 {g.CandidateSpecies.Count} 种，"
                    : "";
                level.Prompts.Add($"{procCands}转化链共 {g.Steps.Count} 步。从最有特征的现象（颜色、气体、沉淀）入手，先锁定链中最确定的那个物质。");
                level.Prompts.Add("再沿转化链逐个验证：每一步的反应物与产物是否都能在候选中找到对应？");
                break;

            case ProblemCategory.StoichiometryCalc:
                var formula = problem.Expected.Calc is { } calc
                    ? ProblemSolver.FormulaOf(calc.Op)
                    : "相应的换算公式";
                var facts = g.NumericFacts.Count > 0
                    ? $"题给 {g.NumericFacts.Count} 个已知量，"
                    : "";
                level.Prompts.Add($"{facts}先确定计算路线（{formula}），再检查每个已知量的单位是否需要换算（如 mL→L）。");
                level.Prompts.Add("代入数值时用分数精确运算，最后检查结果的量纲与数值是否合理。");
                break;

            case ProblemCategory.Electrochemistry:
                var cell = g.Cell;
                var cellType = cell?.Type == "electrolytic" ? "电解池" : "原电池";
                var electrolyte = !string.IsNullOrWhiteSpace(cell?.Electrolyte) ? $"电解质为 {cell.Electrolyte}，" : "";
                var cellMedium = !string.IsNullOrWhiteSpace(cell?.Medium)
                    ? (cell.Medium == "acidic" ? "酸性介质下配平用 H^+，" : "碱性介质下配平用 OH^-，")
                    : "";
                level.Prompts.Add($"装置类型是{cellType}，{electrolyte}{cellMedium}先回忆两种装置的电极名称（正负极/阴阳极）分别对应氧化反应还是还原反应。");
                level.Prompts.Add("写出装置的总反应，标出化合价升高与降低的元素：失电子的一极发生氧化反应，你能据此确定该极的电极反应式吗？");
                level.Prompts.Add("另一极的电极反应式可由总反应减去已知半反应得到——先按电子守恒确定倍数，再移项，最后检查介质下各物种是否合理。");
                break;

            case ProblemCategory.ChemicalEquilibrium:
                var eq = g.Equilibrium;
                var eqEq = !string.IsNullOrWhiteSpace(eq?.Equation) ? "题给的可逆反应，" : "";
                var knownKind = eq?.Known?.Kind switch
                {
                    "conversion" => "题给某反应物转化率，",
                    "amountFraction" => "题给某组分平衡分数，",
                    "moles" => "题给某组分平衡物质的量，",
                    _ => "",
                };
                var disturbance = eq?.Disturbance is not null
                    ? $"平衡后施加了「{eq.Disturbance.Kind}」扰动，"
                    : "";
                level.Prompts.Add($"{eqEq}{knownKind}{disturbance}先写出三段式框架：起始、转化、平衡三行各物种的量如何用同一转化进度 x 与计量数 ν 表示？");
                level.Prompts.Add("题给的平衡信息能列出一个关于 x 的一元一次方程吗？注意转化量 = |ν|·x，别把转化量与平衡量混淆。");
                level.Prompts.Add("求平衡分数时分母是平衡时总物质的量——反应前后气体分子数变化（Δν）会改变总量；判断移动方向回到勒夏特列原理。");
                break;

            case ProblemCategory.AqueousIonEquilibrium:
                var aq = g.Solution;
                var solutes = aq?.Solutes.Count > 0
                    ? $"溶质为 {string.Join("、", aq.Solutes.Select(s => s.Species))}，"
                    : "";
                var note = !string.IsNullOrWhiteSpace(aq?.Note) ? $"附加条件：{aq.Note}，" : "";
                level.Prompts.Add($"{solutes}{note}先写出溶液中全部电离与水解方程：哪些溶质完全电离？哪些离子发生水解（越弱越水解，Kh = Kw/Ka）？水解或电离竞争后溶液显酸性还是碱性？");
                level.Prompts.Add("列出三大守恒：电荷守恒、物料守恒、质子守恒。浓度排序时完全电离的粒子一般大于弱过程产生的粒子。");
                level.Prompts.Add("遇到两种弱过程粒子比大小：先问自己现有守恒关系能否判定；若需解联立平衡才能比较，应如实承认条件不足，不要凭印象下结论。");
                break;

            case ProblemCategory.OrganicInference:
                var orgCands = g.CandidateSpecies.Count > 0 ? $"候选有机物共 {g.CandidateSpecies.Count} 种，" : "";
                level.Prompts.Add($"{orgCands}转化链共 {g.Steps.Count} 步。先找特征反应：哪些步骤的试剂与条件（溴水、银氨溶液、新制氢氧化铜、浓硫酸/浓硝酸、光照取代、催化加氢等）在课内只对应一类官能团？");
                level.Prompts.Add("沿转化链逐个验证：把候选有机物代入每一步，反应物组合与条件能否在课内有机反应中找到完全吻合的条目？产物是否也落在候选集内？");
                level.Prompts.Add("注意同分异构与连锁取代的陷阱：一种条件可能对应多条候选路径，检查是否存在第二种赋值也能解释全部步骤；若有，如实承认条件不足。");
                break;

            case ProblemCategory.IndustrialProcess:
                level.Prompts.Add($"流程共 {g.Steps.Count} 步。先通读整个流程，把步骤分成两类：发生化学转化的步骤（原料→中间产物→产品）与纯操作步骤（过滤、洗涤、灼烧、结晶、循环）。");
                level.Prompts.Add("对每个操作步骤问自己：它在除杂、沉淀转化、pH 调控还是分离提纯的哪个环节起作用？除杂原则（不引入新杂质、不减少主体物质）如何约束试剂的加入顺序与用量？");
                level.Prompts.Add("涉及沉淀与 pH 的步骤，回到溶度积判据：沉淀向更难溶的方向转化；控制 pH 使杂质离子沉淀完全而主体离子不沉淀。");
                break;

            case ProblemCategory.ExperimentalInquiry:
                var exp = g.Experiment;
                var mode = exp?.Mode switch
                {
                    "gas_test" => "气体验验",
                    "gas_full_test" => "气体验满",
                    "operation_purpose" => "操作目的与误差分析",
                    "phenomenon_conclusion" => "由现象反推结论",
                    _ => "实验探究",
                };
                var targets = exp?.Targets.Count > 0
                    ? $"目标对象 {string.Join("、", exp.Targets)}，"
                    : exp?.Operations.Count > 0
                        ? $"操作 {string.Join("、", exp.Operations)}，"
                        : "";
                level.Prompts.Add($"本题属于{mode}，{targets}先回忆教材中这类问题的固定表述模板，再作答。");
                level.Prompts.Add("检验与验满别混淆：检验回答「瓶内是什么」，验满证明「已集满到瓶口」——操作位置与判断现象各不相同。写答案时按「操作—现象—结论」三段完整表述。");
                level.Prompts.Add("误差分析先回到操作原理：这一步做与不做改变了哪个量（浓度 c 还是体积 V）？仪器刻度方向会不会让读数偏差放大？由改变量推出结果偏高还是偏低。");
                break;
        }

        return level;
    }

    /// <summary>第 4 级：更具体的提示——查关键反应条目，给出反应类型与条件（仍不给出产物/方程式）。</summary>
    private static GuideLevel BuildLevel4(Solution solution, ProblemSpec problem, KnowledgeBase knowledge, ReactionLibrary reactions)
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
        if (evidence is not null && reactions.TryGet(evidence, out var entry))
        {
            var typeName = ReactionTypeName(entry.ReactionType);
            var condText = entry.Conditions.Count > 0
                ? $"，反应条件为 [{string.Join("、", entry.Conditions)}]"
                : "，常温即可进行";
            var knName = "";
            if (!string.IsNullOrWhiteSpace(entry.KnowledgeId) && knowledge.TryGet(entry.KnowledgeId, out var kn))
            {
                knName = $"，对应知识点「{kn.Name}」";
            }
            // 只给反应类型与条件，不透露反应物/产物/方程式/现象，避免直接泄漏答案
            level.Prompts.Add($"最后的提示：本题的关键反应属于{typeName}{condText}{knName}。回到教材中该类反应的规律，结合题给信息推导结论。");
        }
        else if (evidence is not null)
        {
            level.Prompts.Add($"最后的提示：本题的关键反应记录在知识条目 {evidence} 中——确认它的反应物与条件，结论就在其中。");
        }
        else
        {
            level.Prompts.Add("最后的提示：回到第 2 级的知识点，把与题给条件最吻合的那条规律写出来，再对照题目逐步排查。");
        }

        return level;
    }

    /// <summary>反应主类型的中文名称。</summary>
    private static string ReactionTypeName(ReactionType type) => type switch
    {
        ReactionType.Combination => "化合反应",
        ReactionType.Decomposition => "分解反应",
        ReactionType.Displacement => "置换反应",
        ReactionType.DoubleDisplacement => "复分解反应",
        ReactionType.Combustion => "燃烧反应",
        ReactionType.Redox => "氧化还原反应",
        ReactionType.Organic => "有机反应",
        ReactionType.Electrode => "电极反应",
        ReactionType.IonEquilibrium => "离子平衡（电离/水解）",
        ReactionType.Thermochemical => "热化学反应",
        _ => "化学反应",
    };

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
