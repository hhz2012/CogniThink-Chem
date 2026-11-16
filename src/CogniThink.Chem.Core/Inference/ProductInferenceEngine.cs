using CogniThink.Chem.Core.Reactions;

namespace CogniThink.Chem.Core.Inference;

/// <summary>
/// 产物推断引擎：给定反应物与条件，在反应知识库中检索产物。
/// 知识驱动确定性推断：只返回知识库命中条目；多条命中（条件变体/并列可能）
/// 全部并列呈现（Underdetermined），绝不猜测产物。
/// </summary>
public static class ProductInferenceEngine
{
    /// <summary>执行产物推断。</summary>
    /// <param name="request">反应物与条件。</param>
    /// <param name="library">反应库（缺省用全局缓存）。</param>
    public static ProductInferenceResult Infer(ProductInferenceRequest request, ReactionLibrary? library = null)
    {
        library ??= ReactionLibrary.Default.Value;
        var result = new ProductInferenceResult();
        var reactantText = string.Join(" + ", request.Reactants);

        // 第 1 步：按反应物组合检索
        var hits = library.FindByReactants(request.Reactants);
        result.Steps.Add(new InferenceStep
        {
            Description = $"在课内反应知识库中检索反应物组合「{reactantText}」，命中 {hits.Count} 条。",
            Evidence = "ReactionLibrary.FindByReactants",
        });
        if (hits.Count == 0)
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = $"课内反应知识库中不存在反应物组合「{reactantText}」的反应；引擎不臆造产物。";
            return result;
        }

        // 第 2 步：条件过滤（查询条件令牌按 ',' 拆分归一）
        var queryConditions = request.Conditions
            .SelectMany(c => c.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToList();
        var satisfied = hits
            .Where(e => ReactionLibrary.ConditionsSatisfied(queryConditions, e.Conditions))
            .ToList();
        if (satisfied.Count == 0)
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = $"知识库中「{reactantText}」有 {hits.Count} 条反应，但在给定条件 [{string.Join("、", queryConditions)}] 下均不成立。";
            result.Steps.Add(new InferenceStep
            {
                Description = $"逐条核对反应条件：{string.Join("；", hits.Select(h => $"{h.Id} 需要 [{string.Join("、", h.Conditions)}]"))}，均不满足。",
                Evidence = "ReactionLibrary.ConditionsSatisfied",
            });
            return result;
        }

        // 第 3 步：条件特异性优先——查询带条件时，优先采纳自身带条件标注的条目
        var final = satisfied;
        if (queryConditions.Count > 0)
        {
            var specific = satisfied.Where(e => e.Conditions.Count > 0).ToList();
            if (specific.Count > 0)
            {
                final = specific;
                result.Steps.Add(new InferenceStep
                {
                    Description = $"条件 [{string.Join("、", queryConditions)}] 下优先采纳条件特异匹配，排除 {satisfied.Count - specific.Count} 条无条件泛化条目。",
                    Evidence = "条件特异性优先规则",
                });
            }
        }

        result.Matches = final;
        foreach (var match in final)
        {
            result.Steps.Add(new InferenceStep
            {
                Description = $"命中反应「{match.Name}」：{match.Equation}",
                Evidence = match.Id,
                KnowledgeId = match.KnowledgeId,
                Source = match.Source,
            });
        }

        if (final.Count == 1)
        {
            result.Status = InferenceStatus.Solved;
            result.Message = $"唯一命中 {final[0].Id}（{final[0].Name}）。";
        }
        else
        {
            result.Status = InferenceStatus.Underdetermined;
            result.Message = $"命中 {final.Count} 条并列可能（{string.Join("、", final.Select(f => f.Id))}），引擎全部列出、不替学生选择。";
        }

        return result;
    }
}
