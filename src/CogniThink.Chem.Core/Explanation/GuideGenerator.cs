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
/// 六级递进，对应统一解题框架：
/// 审题分类 → 建立模型 → 知识检索 → 规则应用 → 精确求解 → 守恒校验。
/// </summary>
public static class GuideGenerator
{
    /// <summary>生成引导。</summary>
    /// <param name="solution">引擎解答（提供知识点线索与证据编号）。</param>
    /// <param name="problem">题目（提供设问目标与已知条件）。</param>
    /// <param name="knowledge">知识库；缺省从 <see cref="DataRoot.Resolve()"/> 载入。</param>
    /// <param name="reactions">反应知识库；缺省从 <see cref="DataRoot.Resolve()"/> 载入，用于查反应条目。</param>
    public static Guide Generate(Solution solution, ProblemSpec problem, KnowledgeBase? knowledge = null, ReactionLibrary? reactions = null)
    {
        knowledge ??= ChemDataLoader.LoadKnowledge(DataRoot.Resolve());
        reactions ??= ReactionLibrary.Load(DataRoot.Resolve());
        var guide = new Guide
        {
            ProblemId = problem.Id,
            Category = problem.Category,
        };

        guide.Levels.Add(BuildStep1(problem));
        guide.Levels.Add(BuildStep2(problem));
        guide.Levels.Add(BuildStep3(solution, knowledge));
        guide.Levels.Add(BuildStep4(problem));
        guide.Levels.Add(BuildStep5(solution, problem));
        guide.Levels.Add(BuildStep6(solution, problem, knowledge, reactions));
        guide.FullText = RenderFullText(guide);
        return guide;
    }

    /// <summary>第 1 步：审题分类——先想清楚"这是什么题、已知什么、求什么"。</summary>
    private static GuideLevel BuildStep1(ProblemSpec problem)
    {
        var targets = problem.Question.Targets.Count > 0
            ? string.Join("、", problem.Question.Targets)
            : "题目的最终设问";
        return new GuideLevel
        {
            Level = 1,
            Title = "审题分类",
            Prompts =
            {
                $"这是一道{Explainer.CategoryName(problem.Category)}题。先问自己：题目到底要求什么？（{targets}）",
                "答案应该以什么形式呈现——化学方程式、存在性结论、物质对应关系，还是精确数值？",
                "把题给的已知条件逐条列出来，区分「确定条件」和「隐含条件」。",
            },
        };
    }

    /// <summary>第 2 步：建立模型——选定本题的标准解法范式。</summary>
    private static GuideLevel BuildStep2(ProblemSpec problem)
    {
        var level = new GuideLevel { Level = 2, Title = "建立模型" };
        level.Prompts.Add(problem.Category switch
        {
            ProblemCategory.ProductInference => "范式：反应库检索。把反应物+条件与课内反应条目逐一比对。",
            ProblemCategory.IonCoexistence => "范式：介质+两两反应。先定介质（酸/碱/中），再把离子两两配对查反应。",
            ProblemCategory.SubstanceIdentification or ProblemCategory.IonPowderIdentification => "范式：约束传播。逐步分析每步现象，确定「一定存在」和「一定不存在」，剩余归「可能存在」。",
            ProblemCategory.ProcessInference or ProblemCategory.OrganicInference => "范式：转化链回溯。从最特征的现象锁定一个物质，再沿链逐个验证赋值。",
            ProblemCategory.IndustrialProcess => "范式：转化链+每步目的。区分化学转化步骤与纯操作步骤，分析每步在除杂/分离/pH 调控中的作用。",
            ProblemCategory.StoichiometryCalc => "范式：物质的量 n 为中心。所有已知量换算到 n，再用公式求目标量。",
            ProblemCategory.Electrochemistry => "范式：装置判定+离子电子法。先判原电池/电解池，再拆半反应。",
            ProblemCategory.ChemicalEquilibrium => "范式：三段式。起始→转化(ν·x)→平衡，列一元一次方程解 x。",
            ProblemCategory.AqueousIonEquilibrium => "范式：电离/水解+三大守恒。先写全部电离水解方程，再列电荷/物料/质子守恒。",
            ProblemCategory.ExperimentalInquiry => "范式：操作→现象→结论。按教材固定表述模板作答，误差分析回到操作原理。",
            _ => "范式：先确定本题的标准解法类型，再按对应步骤推进。",
        });
        return level;
    }

    /// <summary>第 3 步：知识检索——激活解题所需的课内知识点。</summary>
    private static GuideLevel BuildStep3(Solution solution, KnowledgeBase knowledge)
    {
        var level = new GuideLevel { Level = 3, Title = "知识检索" };
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
                level.Prompts.Add($"知识点「{item.Name}」（{item.Id}，{item.TextbookRef}）。{summary}");
            }
        }

        if (level.Prompts.Count == 0)
        {
            level.Prompts.Add("本题要用的核心公式或反应规律是什么？先把它默写出来，再决定从哪里入手。");
        }

        return level;
    }

    /// <summary>第 4 步：规则应用——按类别用题给已知条件驱动推理（不含具体结论）。</summary>
    private static GuideLevel BuildStep4(ProblemSpec problem)
    {
        var level = new GuideLevel { Level = 4, Title = "规则应用" };
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

    /// <summary>第 5 步：精确求解——设未知量、列方程、代入计算（不给最终数值）。</summary>
    private static GuideLevel BuildStep5(Solution solution, ProblemSpec problem)
    {
        var level = new GuideLevel { Level = 5, Title = "精确求解" };

        if (problem.Category == ProblemCategory.StoichiometryCalc && problem.Expected.Calc is { } calc)
        {
            level.Prompts.Add($"按 {ProblemSolver.FormulaOf(calc.Op)} 代入题给数值，注意单位换算（如 mL→L、g→mol）。");
            level.Prompts.Add("用分数精确运算，避免浮点误差；最后检查结果的量纲是否正确。");
            return level;
        }

        if (problem.Category == ProblemCategory.ChemicalEquilibrium)
        {
            level.Prompts.Add("设转化进度为 x，把三段式中各物种的平衡量用 x 表示。");
            level.Prompts.Add("用题给的平衡信息（转化率/平衡量/分数）列出关于 x 的一元一次方程并求解。");
            level.Prompts.Add("检查 x 是否为正、各物种平衡量是否非负——若矛盾则此题无解。");
            return level;
        }

        // 推断类：设标签→物质的赋值，逐步代入验证
        if (problem.Category is ProblemCategory.ProcessInference or ProblemCategory.OrganicInference or ProblemCategory.IndustrialProcess)
        {
            level.Prompts.Add("给转化链的每个字母标签赋一个候选物质，然后沿链逐步行验证。");
            level.Prompts.Add("若某一步无法解释，回退上一步换一个候选再试；所有步骤都通过的赋值即为答案。");
            return level;
        }

        // 成分推断类
        if (problem.Category is ProblemCategory.SubstanceIdentification or ProblemCategory.IonPowderIdentification)
        {
            level.Prompts.Add("对每个候选物质，用每步实验现象检验：能产生该现象的保留，不能的排除。");
            level.Prompts.Add("注意「无现象」不等于「无反应」——逐一排查哪些候选若存在一定会反应。");
            return level;
        }

        level.Prompts.Add("把第 4 步的推理落实为具体的计算或推导，注意单位与有效数字。");
        return level;
    }

    /// <summary>第 6 步：守恒校验——检查原子/电荷/质量/电子守恒。</summary>
    private static GuideLevel BuildStep6(Solution solution, ProblemSpec problem, KnowledgeBase knowledge, ReactionLibrary reactions)
    {
        var level = new GuideLevel { Level = 6, Title = "守恒校验" };
        level.Prompts.Add("最后做守恒检查：你的结论是否满足原子守恒、电荷守恒、质量守恒？");

        if (problem.Category == ProblemCategory.Electrochemistry)
        {
            level.Prompts.Add("电化学额外检查：两个半反应的电子转移数是否相等？总反应是否与半反应之和一致？");
        }
        else if (problem.Category == ProblemCategory.ChemicalEquilibrium)
        {
            level.Prompts.Add("平衡题检查：K 只与温度有关（EQ-002），催化剂不移动平衡（EQ-003），扰动方向是否符合勒夏特列原理（EQ-001）？");
        }
        else if (problem.Category == ProblemCategory.IonCoexistence)
        {
            level.Prompts.Add("离子共存检查：是否有遗漏的离子对？介质离子（H⁺/OH⁻）是否已纳入考量？");
        }
        else if (problem.Category == ProblemCategory.AqueousIonEquilibrium)
        {
            level.Prompts.Add("水溶液检查：三大守恒式是否正确列出？浓度排序中强电解质粒子是否排在弱过程粒子之前？");
        }
        else if (problem.Category == ProblemCategory.StoichiometryCalc)
        {
            level.Prompts.Add("计算检查：方程式计量数之比是否等于物质的量之比？单位换算是否正确？");
        }

        // 查关键反应条目，给出反应类型与条件（仍不给出产物/方程式）
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
            level.Prompts.Add($"关键反应属于{typeName}{condText}{knName}。回到教材中该类反应的规律，确认你的推导与之一致。");
        }

        if (level.Prompts.Count == 1)
        {
            level.Prompts.Add("回到第 3 步的知识点，把与题给条件最吻合的那条规律写出来，再对照你的结论逐步排查。");
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
            lines.Add($"◆ 第 {level.Level} 步·{level.Title}");
            foreach (var prompt in level.Prompts)
            {
                lines.Add($"  - {prompt}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}
