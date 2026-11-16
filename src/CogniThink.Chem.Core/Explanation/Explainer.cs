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
}

/// <summary>讲解引用的知识点（溯源清单）。</summary>
public sealed class KnowledgeReference
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string TextbookRef { get; set; } = "";
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

    public List<ExplanationStep> Steps { get; set; } = new();

    /// <summary>引用知识点清单（去重，按出现顺序）。</summary>
    public List<KnowledgeReference> References { get; set; } = new();

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
            };

            if (step.KnowledgeId.Length > 0 && _knowledge.TryGet(step.KnowledgeId, out var item))
            {
                rendered.KnowledgeName = item.Name;
                rendered.Text += $"（依据知识点「{item.Name}」，{item.TextbookRef}）";
                if (explanation.References.All(r => r.Id != item.Id))
                {
                    explanation.References.Add(new KnowledgeReference
                    {
                        Id = item.Id,
                        Name = item.Name,
                        TextbookRef = item.TextbookRef,
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
        explanation.FullText = RenderFullText(solution, explanation);
        return explanation;
    }

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

            default:
                return solution.Message;
        }
    }

    /// <summary>题目类别的中文名称。</summary>
    public static string CategoryName(ProblemCategory category)
    {
        switch (category)
        {
            case ProblemCategory.ProductInference: return "产物推断";
            case ProblemCategory.SubstanceIdentification: return "成分推断";
            case ProblemCategory.ProcessInference: return "过程推断";
            case ProblemCategory.IonCoexistence: return "离子共存";
            case ProblemCategory.StoichiometryCalc: return "定量计算";
            default: return category.ToString();
        }
    }

    private static string RenderFullText(Solution solution, Explanation explanation)
    {
        var lines = new List<string>
        {
            $"【{solution.ProblemId}｜{CategoryName(solution.Category)}】讲解",
            $"状态：{(solution.IsSolved ? "已解出" : solution.Status.ToString())}",
        };
        foreach (var step in explanation.Steps)
        {
            lines.Add($"  {step.Order}. {step.Text}");
        }

        lines.Add($"答案：{explanation.AnswerSummary}");
        if (explanation.References.Count > 0)
        {
            lines.Add("引用知识点：" + string.Join("；",
                explanation.References.Select(r => $"{r.Name}（{r.TextbookRef}）")));
        }

        return string.Join(Environment.NewLine, lines);
    }
}
