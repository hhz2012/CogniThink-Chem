using CogniThink.Chem.Core.Benchmark;
using CogniThink.Chem.Core.Data;
using CogniThink.Chem.Core.Inference;
using CogniThink.Chem.Core.Knowledge;
using CogniThink.Chem.Core.Solving;

namespace CogniThink.Chem.Core.Explanation;

/// <summary>讲解中的一步（结构化形态）。</summary>
public sealed class ExplanationStep
{
    public int Order { get; set; }

    /// <summary>渲染后的中文讲解句。</summary>
    public string Text { get; set; } = "";

    /// <summary>证据（知识条目 id / 方程式 / 检索手段）。</summary>
    public string Evidence { get; set; } = "";

    public string KnowledgeId { get; set; } = "";

    /// <summary>解析出的知识点名称（未登记为空）。</summary>
    public string KnowledgeName { get; set; } = "";

    /// <summary>教材出处。</summary>
    public string Source { get; set; } = "";

    /// <summary>本步的"为什么"：判断依据或因果解释，不含最终答案。</summary>
    public string Why { get; set; } = "";

    /// <summary>本步是否为解题突破口（影响前端是否标 🔑）。</summary>
    public bool IsKeyStep { get; set; }

    /// <summary>本步对应的常见误区（取知识点 pitfall，空则不显示）。</summary>
    public string Pitfall { get; set; } = "";
}

/// <summary>讲解引用的知识点（溯源清单）。</summary>
public sealed class KnowledgeReference
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string TextbookRef { get; set; } = "";

    /// <summary>知识点一句话要点（供卡片展示）。</summary>
    public string Summary { get; set; } = "";

    /// <summary>知识点常见误区（供卡片展示）。</summary>
    public string Pitfall { get; set; } = "";
}

/// <summary>
/// 讲解结果：结构化步骤 + 中文全文双形态。
/// </summary>
public sealed class Explanation
{
    public string ProblemId { get; set; } = "";

    public ProblemCategory Category { get; set; }

    /// <summary>答案小结（按类别渲染）。</summary>
    public string AnswerSummary { get; set; } = "";

    /// <summary>一句话总览（30 秒版，不含方程式与数值结论）。</summary>
    public string OneLineOverview { get; set; } = "";

    public List<ExplanationStep> Steps { get; set; } = new();

    /// <summary>引用知识点清单（去重，按出现顺序）。</summary>
    public List<KnowledgeReference> References { get; set; } = new();

    /// <summary>本题涉及知识点的常见误区（去重，供"易错点"块展示）。</summary>
    public List<string> Pitfalls { get; set; } = new();

    /// <summary>中文全文讲解。</summary>
    public string FullText { get; set; } = "";
}

/// <summary>
/// 讲解器（"能讲"）：把 <see cref="Solution"/> 渲染为逐步中文讲解。
/// 每个带知识点 id 的步骤都经 <see cref="KnowledgeBase"/> 解析出名称与教材出处，
/// 保证讲解与知识库一一对应、全部结论可溯源。
/// </summary>
public sealed class Explainer
{
    private readonly KnowledgeBase _knowledge;

    /// <param name="knowledge">知识库；缺省从 <see cref="DataRoot.Resolve()"/> 载入。</param>
    public Explainer(KnowledgeBase? knowledge = null)
    {
        _knowledge = knowledge ?? ChemDataLoader.LoadKnowledge(DataRoot.Resolve());
    }

    /// <summary>把解答渲染为讲解。</summary>
    public Explanation Explain(Solution solution)
    {
        var explanation = new Explanation
        {
            ProblemId = solution.ProblemId,
            Category = solution.Category,
        };

        foreach (var step in solution.Steps)
        {
            var rendered = new ExplanationStep
            {
                Order = step.Order,
                Text = step.Description,
                Evidence = step.Evidence,
                KnowledgeId = step.KnowledgeId,
                Source = step.Source,
                // 第一步或带反应证据（RX-）的步骤标为突破口
                IsKeyStep = step.Order == 1 || step.Evidence.StartsWith("RX-", StringComparison.Ordinal),
            };

            if (step.KnowledgeId.Length > 0 && _knowledge.TryGet(step.KnowledgeId, out var item))
            {
                rendered.KnowledgeName = item.Name;
                rendered.Text += $"（依据知识点「{item.Name}」，{item.TextbookRef}）";
                // "为什么"层：取知识点一句话要点，不含本题答案
                if (!string.IsNullOrWhiteSpace(item.Summary))
                {
                    rendered.Why = item.Summary;
                }
                // 本步对应常见误区
                if (!string.IsNullOrWhiteSpace(item.Pitfall))
                {
                    rendered.Pitfall = item.Pitfall;
                    if (!explanation.Pitfalls.Contains(item.Pitfall))
                    {
                        explanation.Pitfalls.Add(item.Pitfall);
                    }
                }

                if (explanation.References.All(r => r.Id != item.Id))
                {
                    explanation.References.Add(new KnowledgeReference
                    {
                        Id = item.Id,
                        Name = item.Name,
                        TextbookRef = item.TextbookRef,
                        Summary = item.Summary,
                        Pitfall = item.Pitfall,
                    });
                }
            }
            else if (step.Source.Length > 0)
            {
                rendered.Text += $"（出处：{step.Source}）";
            }

            explanation.Steps.Add(rendered);
        }

        explanation.AnswerSummary = SummarizeAnswer(solution);
        explanation.OneLineOverview = BuildOneLineOverview(solution);
        explanation.FullText = RenderFullText(solution, explanation);
        return explanation;
    }

    /// <summary>
    /// 一句话总览：按类别描述本题要做什么，不含方程式、成分结论、物质赋值与数值。
    /// 用于讲解面板顶部的"30 秒版"总览。
    /// </summary>
    private static string BuildOneLineOverview(Solution solution) => solution.Category switch
    {
        ProblemCategory.ProductInference => "本题由反应物与反应条件推断产物并写出化学方程式。",
        ProblemCategory.IonCoexistence => "本题判断给定离子组能否大量共存，需逐一排查两两反应。",
        ProblemCategory.SubstanceIdentification or ProblemCategory.IonPowderIdentification => "本题依据各步实验现象推断一定存在、一定不存在与不能确定的成分。",
        ProblemCategory.ProcessInference or ProblemCategory.OrganicInference => "本题依据转化链中的现象与条件推断各字母对应的物质。",
        ProblemCategory.StoichiometryCalc => "本题依据已知量与换算关系计算目标物理量的精确数值。",
        ProblemCategory.Electrochemistry => "本题由总反应与装置类型推导正负极（或阴阳极）的电极反应式。",
        ProblemCategory.ChemicalEquilibrium => "本题用三段式与平衡常数关系求平衡组成、转化率或 K 值。",
        ProblemCategory.AqueousIonEquilibrium => "本题分析溶液中电离与水解，写出守恒关系并比较粒子浓度。",
        ProblemCategory.IndustrialProcess => "本题依据流程现象与试剂推断各步物质并说明操作目的。",
        ProblemCategory.ExperimentalInquiry => "本题依据实验操作与现象给出检验方法、判据或结论。",
        _ => "本题依据课内知识完成求解。",
    };

    /// <summary>按类别渲染答案小结。</summary>
    public static string SummarizeAnswer(Solution solution)
    {
        switch (solution.Category)
        {
            case ProblemCategory.ProductInference:
                return solution.Equations.Count == 0
                    ? $"无法给出产物：{solution.Message}"
                    : "化学方程式：" + string.Join("；", solution.Equations);

            case ProblemCategory.IonCoexistence:
                var verdict = solution.Coexistence switch
                {
                    CoexistenceVerdict.Coexist => "能大量共存",
                    CoexistenceVerdict.NotCoexist => "不能大量共存",
                    _ => "无法判断",
                };
                var reasons = solution.Equations.Count > 0
                    ? "，理由方程式：" + string.Join("；", solution.Equations)
                    : "";
                return $"结论：{verdict}{reasons}";

            case ProblemCategory.SubstanceIdentification:
            case ProblemCategory.IonPowderIdentification:
                var groups = new[]
                {
                    ("一定存在", CompositionVerdict.Must),
                    ("一定不存在", CompositionVerdict.MustNot),
                    ("可能存在", CompositionVerdict.Maybe),
                };
                var parts = new List<string>();
                foreach (var (label, verdict2) in groups)
                {
                    var species = solution.Composition
                        .Where(kv => kv.Value == verdict2)
                        .Select(kv => kv.Key)
                        .ToList();
                    if (species.Count > 0)
                    {
                        parts.Add($"{label}：{string.Join("、", species)}");
                    }
                }

                return parts.Count > 0 ? string.Join("；", parts) : $"无法判定：{solution.Message}";

            case ProblemCategory.ProcessInference:
            case ProblemCategory.OrganicInference:
                if (solution.SubstanceMap.Count == 0)
                {
                    return $"无法确定转化链：{solution.Message}";
                }

                var map = string.Join("，", solution.SubstanceMap
                    .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select(kv => $"{kv.Key} 是 {kv.Value}"));
                var chain = solution.Equations.Count > 0
                    ? "；各步方程式：" + string.Join("；", solution.Equations)
                    : "";
                return map + chain;

            case ProblemCategory.StoichiometryCalc:
                return solution.NumericValue is { } value
                    ? $"{value} {solution.NumericUnit}".Trim()
                    : $"无法计算：{solution.Message}";

            case ProblemCategory.Electrochemistry:
                var electrodeParts = new List<string>();
                if (solution.Electrodes is { } el)
                {
                    if (!string.IsNullOrWhiteSpace(el.Negative)) electrodeParts.Add($"负极：{el.Negative}");
                    if (!string.IsNullOrWhiteSpace(el.Positive)) electrodeParts.Add($"正极：{el.Positive}");
                    if (!string.IsNullOrWhiteSpace(el.Anode)) electrodeParts.Add($"阳极：{el.Anode}");
                    if (!string.IsNullOrWhiteSpace(el.Cathode)) electrodeParts.Add($"阴极：{el.Cathode}");
                }

                return electrodeParts.Count > 0
                    ? string.Join("；", electrodeParts)
                    : $"无法给出电极反应式：{solution.Message}";

            case ProblemCategory.ChemicalEquilibrium:
                if (solution.Equilibrium is not { } eq)
                {
                    return $"无法求解平衡：{solution.Message}";
                }

                var eqParts = new List<string>();
                if (eq.Conversion is not null)
                {
                    eqParts.Add($"{eq.ConversionSpecies} 转化率：{eq.Conversion}");
                }

                if (eq.Fraction is not null)
                {
                    eqParts.Add($"{eq.FractionSpecies} 平衡摩尔分数：{eq.Fraction}");
                }

                if (eq.KValue is not null)
                {
                    eqParts.Add($"Kp = {eq.KValue}");
                }

                eqParts.Add(eq.Direction switch
                {
                    "forward" => "扰动后平衡正向移动",
                    "reverse" => "扰动后平衡逆向移动",
                    "noShift" => "扰动后平衡不移动",
                    _ => "无扰动，平衡状态即最终结论",
                });
                return "平衡结论：" + string.Join("；", eqParts);

            case ProblemCategory.AqueousIonEquilibrium:
                if (solution.Aqueous is not { } aq)
                {
                    return $"无法判定粒子浓度关系：{solution.Message}";
                }

                var aqParts = new List<string>();
                if (aq.Order.Count > 0)
                {
                    aqParts.Add("浓度由大到小：" + string.Join(" > ", aq.Order));
                }

                if (aq.ConservationEquations.Count > 0)
                {
                    aqParts.Add("守恒关系：" + string.Join("；", aq.ConservationEquations));
                }

                return aqParts.Count > 0 ? string.Join("。", aqParts) : solution.Message;

            case ProblemCategory.IndustrialProcess:
                if (solution.SubstanceMap.Count == 0)
                {
                    return $"无法推断工业流程：{solution.Message}";
                }

                var flowMap = string.Join("，", solution.SubstanceMap
                    .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select(kv => $"{kv.Key} 是 {kv.Value}"));
                var flowPurposes = solution.Purposes.Count > 0
                    ? "；各步目的：" + string.Join("；", solution.Purposes
                        .OrderBy(kv => int.TryParse(kv.Key, out var n) ? n : int.MaxValue)
                        .Select(kv => $"第{kv.Key}步 {kv.Value}"))
                    : "";
                var flowEquations = solution.Equations.Count > 0
                    ? "；转化链方程式：" + string.Join("；", solution.Equations)
                    : "";
                return flowMap + flowPurposes + flowEquations;

            case ProblemCategory.ExperimentalInquiry:
                if (solution.Experiment is { Conclusions.Count: > 0 })
                {
                    return "实验结论：" + string.Join("；", solution.Experiment.Conclusions);
                }

                return $"无法给出实验结论：{solution.Message}";

            default:
                return solution.Message;
        }
    }

    /// <summary>题目类别的中文名称（委托 <see cref="CategoryCatalog"/> 唯一维护点；未知类别回退枚举名）。</summary>
    public static string CategoryName(ProblemCategory category) =>
        CategoryCatalog.All.TryGetValue(category, out var info) ? info.Name : category.ToString();

    private static string RenderFullText(Solution solution, Explanation explanation)
    {
        var lines = new List<string>
        {
            $"【{solution.ProblemId}｜{CategoryName(solution.Category)}】讲解",
            $"状态：{(solution.IsSolved ? "已解出" : solution.Status.ToString())}",
        };
        if (!string.IsNullOrWhiteSpace(explanation.OneLineOverview))
        {
            lines.Add($"一句话总览：{explanation.OneLineOverview}");
        }

        foreach (var step in explanation.Steps)
        {
            lines.Add($"  {step.Order}. {step.Text}");
            if (!string.IsNullOrWhiteSpace(step.Why))
            {
                lines.Add($"     为什么：{step.Why}");
            }
        }

        lines.Add($"答案：{explanation.AnswerSummary}");
        if (explanation.Pitfalls.Count > 0)
        {
            lines.Add("易错点：" + string.Join("；", explanation.Pitfalls));
        }
        if (explanation.References.Count > 0)
        {
            lines.Add("引用知识点：" + string.Join("；",
                explanation.References.Select(r => $"{r.Name}（{r.TextbookRef}）")));
        }

        return string.Join(Environment.NewLine, lines);
    }
}
