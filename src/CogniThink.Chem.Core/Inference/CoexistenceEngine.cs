using CogniThink.Chem.Core.Chemistry;
using CogniThink.Chem.Core.Reactions;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.Chem.Core.Inference;

/// <summary>
/// 离子共存判定引擎：给定离子组与介质，判定能否大量共存。
/// 方法（知识驱动）：把介质 H^+/OH^- 并入有效离子组，对大小 2~3 的子集
/// 在反应知识库的离子反应中检索；命中即"不能共存"并给出反应依据。
/// 铁律：出现知识库未收录的离子返回 Underdetermined，绝不猜测共存性。
/// </summary>
public static class CoexistenceEngine
{
    /// <summary>执行共存判定。</summary>
    public static CoexistenceResult Judge(CoexistenceRequest request, ReactionLibrary? library = null)
    {
        library ??= ReactionLibrary.Default.Value;
        var result = new CoexistenceResult();

        // 第 1 步：有效离子组 = 题给离子 + 介质离子；介质同时决定条件令牌
        // （共存判定默认常温：加热等附加条件才发生的反应不计入常温冲突）。
        var ions = new List<string>(request.Species);
        var conditionTokens = new List<string>();
        if (request.Medium == "acidic")
        {
            ions.Add("H^+");
            conditionTokens.Add("酸性");
            result.Steps.Add(new InferenceStep
            {
                Description = "酸性介质：把 H^+ 并入待判离子组。",
                Evidence = "介质条件",
                KnowledgeId = "KH-IN-02",
            });
        }
        else if (request.Medium == "basic")
        {
            ions.Add("OH^-");
            conditionTokens.Add("碱性");
            result.Steps.Add(new InferenceStep
            {
                Description = "碱性介质：把 OH^- 并入待判离子组。",
                Evidence = "介质条件",
                KnowledgeId = "KH-IN-02",
            });
        }

        // 第 2 步：白名单校验——未收录离子不判
        foreach (var ion in ions)
        {
            Species species;
            try
            {
                species = Species.Parse(ion);
            }
            catch (FormatException)
            {
                result.Verdict = CoexistenceVerdict.Underdetermined;
                result.Message = $"物种「{ion}」无法解析，引擎不判断。";
                return result;
            }

            var (known, _) = WhitelistGuard.Check(species);
            if (!known)
            {
                result.Verdict = CoexistenceVerdict.Underdetermined;
                result.Message = $"离子「{ion}」不在课内知识白名单中，引擎不判断其共存性。";
                result.Steps.Add(new InferenceStep
                {
                    Description = $"白名单校验：{ion} 未收录。",
                    Evidence = "WhitelistGuard",
                });
                return result;
            }
        }

        // 第 3 步：对大小 2~3 的子集检索知识库离子反应
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var subset in Subsets(ions, 2, 3))
        {
            foreach (var hit in library.FindByReactants(subset))
            {
                if (!seen.Add(hit.Id))
                {
                    continue;
                }

                // 常温判定：需要 △ 等附加条件的条目（如 NH4^+ + OH^- =△= NH3↑）
                // 不构成常温共存冲突，其无条件对应条目（生成 NH3·H2O）才是依据。
                if (!ReactionLibrary.ConditionsSatisfied(conditionTokens, hit.Conditions))
                {
                    continue;
                }

                var conflict = new CoexistenceConflict
                {
                    InvolvedIons = subset.ToList(),
                    Reaction = hit,
                    Reason = ClassifyReason(hit),
                };
                result.Conflicts.Add(conflict);
                result.Steps.Add(new InferenceStep
                {
                    Description = $"离子组 [{string.Join("、", subset)}] 发生反应「{hit.Name}」：{hit.Equation}（{conflict.Reason}）",
                    Evidence = hit.Id,
                    KnowledgeId = hit.KnowledgeId,
                    Source = hit.Source,
                });
            }
        }

        result.Verdict = result.Conflicts.Count > 0
            ? CoexistenceVerdict.NotCoexist
            : CoexistenceVerdict.Coexist;
        result.Message = result.Verdict == CoexistenceVerdict.NotCoexist
            ? $"发现 {result.Conflicts.Count} 组不能共存的离子对（组）。"
            : "课内知识范围内，各离子间不发生反应，能大量共存。";
        if (result.Verdict == CoexistenceVerdict.Coexist)
        {
            result.Steps.Add(new InferenceStep
            {
                Description = $"对全部 {ions.Count} 个离子的 2~3 元子集在知识库中检索，未发现任何反应。",
                Evidence = "ReactionLibrary.FindByReactants",
                KnowledgeId = "KH-IN-02",
            });
        }

        return result;
    }

    /// <summary>按反应条目的标签归类冲突原因。</summary>
    private static string ClassifyReason(ReactionEntry entry)
    {
        if (entry.Tags.Contains("沉淀"))
        {
            return "生成沉淀";
        }

        if (entry.Tags.Contains("产气") || entry.Tags.Contains("气体"))
        {
            return "生成气体";
        }

        if (entry.ReactionType == ReactionType.Redox || entry.Tags.Contains("氧化还原"))
        {
            return "发生氧化还原反应";
        }

        if (entry.Tags.Contains("双水解"))
        {
            return "相互促进水解（双水解）";
        }

        if (entry.Tags.Contains("中和") || entry.Tags.Contains("弱电解质"))
        {
            return "生成弱电解质";
        }

        return entry.Name;
    }

    /// <summary>枚举大小在 [minSize, maxSize] 的子集（保持原序）。</summary>
    private static IEnumerable<IReadOnlyList<string>> Subsets(IReadOnlyList<string> items, int minSize, int maxSize)
    {
        for (var size = minSize; size <= maxSize && size <= items.Count; size++)
        {
            foreach (var combo in Combinations(items, size, 0, new List<string>()))
            {
                yield return combo;
            }
        }
    }

    private static IEnumerable<List<string>> Combinations(
        IReadOnlyList<string> items, int size, int start, List<string> current)
    {
        if (current.Count == size)
        {
            yield return new List<string>(current);
            yield break;
        }

        for (var i = start; i <= items.Count - (size - current.Count); i++)
        {
            current.Add(items[i]);
            foreach (var combo in Combinations(items, size, i + 1, current))
            {
                yield return combo;
            }

            current.RemoveAt(current.Count - 1);
        }
    }
}
