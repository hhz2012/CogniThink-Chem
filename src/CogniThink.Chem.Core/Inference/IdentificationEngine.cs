using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Chemistry;
using CogniThink.Chem.Core.Ionic;
using CogniThink.Chem.Core.Reactions;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.Chem.Core.Inference;

/// <summary>
/// 成分推断引擎：由实验步骤与现象判定候选物种的存在性（Must/MustNot/Maybe）。
/// 两种模式：
/// <list type="bullet">
/// <item>单一实体（SingleEntity）：待测对象是候选中的"某一种"，
/// 逐一检验每个候选能否解释全部证据，唯一一致者 Must、其余 MustNot。</item>
/// <item>混合成分：候选可能共存，逐步累积证据——正证据（反应+现象吻合）→ Must，
/// 负证据（应反应而无现象）→ MustNot，现象直接指示 → Must，
/// 指示离子已有其他 Must 提供者 → 本候选降为 Maybe。</item>
/// </list>
/// 证据手段：知识库反应检索（分子/离子双模式）、现象库关键词匹配、
/// 候选间相互反应（溶解成淀）、沉淀酸溶分类（部分溶/全溶/不溶）、
/// 足量试剂消耗与"除去 X"跟踪。铁律：所有方程式与结论落到知识库条目。
/// </summary>
public static class IdentificationEngine
{
    private static readonly Regex RemovedPattern = new(@"除去\s*([A-Za-z0-9·^+\-()]+)", RegexOptions.Compiled);

    public static IdentificationResult Identify(IdentificationRequest request, ReactionLibrary? library = null)
    {
        library ??= ReactionLibrary.Default.Value;
        var phenomena = RuleStore.Load().Query<PhenomenaData>("phenomena").Items;
        return request.SingleEntity
            ? IdentifySingleEntity(request, library, phenomena)
            : IdentifyMixture(request, library, phenomena);
    }

    // -----------------------------------------------------------------------
    // 单一实体模式
    // -----------------------------------------------------------------------

    private static IdentificationResult IdentifySingleEntity(
        IdentificationRequest request,
        ReactionLibrary library,
        List<PhenomenonEntry> phenomena)
    {
        var result = new IdentificationResult();
        var candidates = request.CandidateSpecies.Distinct(StringComparer.Ordinal).ToList();
        if (request.Steps.Count == 0 || candidates.Count == 0)
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = "缺少实验步骤或候选物种，无法推断。";
            return result;
        }

        var subject = request.Steps[0].From;
        // 归属于待测对象的步骤：From==subject 及对同一试样处理后产物的延续引用
        // （如"①的溶液"、"②的溶液"、"滤液"）；与 subject 不同的单大写字母是另一
        // 实体（如平行试样 B），不参与本对象的判别。
        var subjectSteps = request.Steps.Where(s => !IsForeignEntityLabel(s.From, subject)).ToList();
        result.Steps.Add(new InferenceStep
        {
            Description = $"单一实体模式：待测对象为「{subject}」，候选 {candidates.Count} 种，需唯一解释 {subjectSteps.Count} 步证据。",
            Source = "IdentificationEngine"
        });

        var consistent = new List<(string Candidate, List<ReactionEntry> Evidence)>();
        foreach (var candidate in candidates)
        {
            var evidence = new List<ReactionEntry>();
            var ok = subjectSteps.All(step => CheckSingleStep(candidate, step, library, phenomena, evidence));
            if (ok)
            {
                consistent.Add((candidate, evidence));
            }
        }

        if (consistent.Count == 1)
        {
            var (winner, evidence) = consistent[0];
            result.Status = InferenceStatus.Solved;
            foreach (var c in candidates)
            {
                result.Composition[c] = c == winner ? CompositionVerdict.Must : CompositionVerdict.MustNot;
            }

            result.Equations = evidence.GroupBy(e => e.Id).Select(g => g.First()).ToList();
            foreach (var entry in evidence)
            {
                result.Steps.Add(new InferenceStep
                {
                    Description = $"{winner} 解释证据：{entry.Name}",
                    Evidence = entry.Equation,
                    KnowledgeId = entry.KnowledgeId,
                    Source = entry.Source
                });
            }

            result.Message = $"唯一能解释全部实验证据的是 {winner}，其余候选均与某步证据矛盾。";
        }
        else if (consistent.Count > 1)
        {
            result.Status = InferenceStatus.Underdetermined;
            var names = consistent.Select(x => x.Candidate).ToList();
            foreach (var c in candidates)
            {
                result.Composition[c] = names.Contains(c) ? CompositionVerdict.Maybe : CompositionVerdict.MustNot;
            }

            result.Message = $"候选 {string.Join("、", names)} 都能解释现有证据，需补充鉴别实验。";
        }
        else
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = "没有任何候选能解释全部实验证据，题设或知识库存在缺口。";
        }

        return result;
    }

    /// <summary>单一实体模式下检验一步：候选必须与现象一致（正证据能解释、负证据不反应）。</summary>
    private static bool CheckSingleStep(
        string candidate,
        ExperimentStep step,
        ReactionLibrary library,
        List<PhenomenonEntry> phenomena,
        List<ReactionEntry> evidence)
    {
        var (reagents, tokens) = ReactionMatcher.NormalizeReagents(step.Reagents);
        tokens.AddRange(step.Conditions);
        var reagentSpecies = reagents.Where(IsParseable).ToList();
        var reactants = new List<string> { candidate };
        reactants.AddRange(reagentSpecies);

        var negative = IsNegative(step.Phenomenon);
        if (negative)
        {
            // 负证据：应反应而未观察到现象 → 候选不得有匹配反应
            return ReactionMatcher.Match(reactants, tokens, library, allowSubset: true).Count == 0;
        }

        var indicated = ReactionMatcher.PhenomenaIndicated(step.Phenomenon, phenomena);

        // 分解级联优先：候选先分解、分解产物再与试剂作用并吻合现象
        // （如 NaHCO3 =△= → CO2 → 石灰水变浑浊）；多命中时优先分子方程式，
        // 避免同一反应重复记录分子式与离子式两条证据。
        foreach (var decomposition in library.FindByReactants(new[] { candidate }))
        {
            if (!ReactionLibrary.ConditionsSatisfied(ReactionLibrary.ConditionTokens(tokens), decomposition.Conditions))
            {
                continue;
            }

            foreach (var product in decomposition.Products)
            {
                var followups = new List<string> { product };
                followups.AddRange(reagentSpecies);
                var followMatches = ReactionMatcher.Match(followups, tokens, library, allowSubset: true)
                    .Where(m => PhenomenonAligned(m.Entry, step.Phenomenon, indicated, candidate))
                    .OrderBy(m => m.IsIonic)
                    .ToList();
                if (followMatches.Count > 0)
                {
                    evidence.Add(decomposition);
                    evidence.Add(followMatches[0].Entry);
                    return true;
                }
            }
        }

        var matches = ReactionMatcher.Match(reactants, tokens, library, allowSubset: true);
        if (matches.Count > 0)
        {
            // 特异性排序：分子式优先；现象吻合的条目中，反应物更多者更特异——
            // 子集命中（如 NH4Cl 受热分解）不得压过消耗全部给定反应物的条目
            // （如 NH4Cl + NaOH =△= 制氨）。
            var aligned = matches
                .Where(m => PhenomenonAligned(m.Entry, step.Phenomenon, indicated, candidate))
                .OrderBy(m => m.IsIonic)
                .ThenByDescending(m => m.Entry.Reactants.Count)
                .ToList();
            if (aligned.Count > 0)
            {
                evidence.Add(aligned[0].Entry);
                return true;
            }
        }

        // 现象直接指示候选本身（或其元素关联，如 浅绿色→Fe^2+ → Fe）
        return ReactionMatcher.ExplainsIndication(candidate, indicated);
    }

    // -----------------------------------------------------------------------
    // 混合成分模式
    // -----------------------------------------------------------------------

    /// <summary>沉淀记录：化学式、提供者候选、生成该沉淀的条目。</summary>
    private sealed record PrecipitateRecord(string Formula, HashSet<string> Providers, ReactionEntry Entry);

    private static IdentificationResult IdentifyMixture(
        IdentificationRequest request,
        ReactionLibrary library,
        List<PhenomenonEntry> phenomena)
    {
        var result = new IdentificationResult();
        var candidates = request.CandidateSpecies.Distinct(StringComparer.Ordinal).ToList();
        var verdict = candidates.ToDictionary(c => c, _ => CompositionVerdict.Unknown);
        var consumed = new HashSet<string>(StringComparer.Ordinal);
        var precipitates = new List<PrecipitateRecord>();
        var equations = new List<ReactionEntry>();

        foreach (var step in request.Steps)
        {
            var (reagents, tokens) = ReactionMatcher.NormalizeReagents(step.Reagents);
            tokens.AddRange(step.Conditions);
            var reagentSpecies = reagents.Where(IsParseable).ToList();
            var indicated = ReactionMatcher.PhenomenaIndicated(step.Phenomenon, phenomena);
            var negative = IsNegative(step.Phenomenon);
            var excess = step.Phenomenon.Contains("足量") || step.Phenomenon.Contains("过量");

            // 模式 A：对上一步沉淀的酸溶处理（部分溶/全溶/不溶）
            if (precipitates.Count > 0 && step.From.Contains("沉淀"))
            {
                HandlePrecipitateTreatment(step, tokens, reagentSpecies, precipitates, verdict, equations, library);
                continue;
            }

            // 模式 B：溶解/混合步骤出现沉淀 → 候选两两反应
            if (!negative && step.Phenomenon.Contains("沉淀")
                && (reagentSpecies.Count == 0 || reagentSpecies.All(r => r == "H2O")))
            {
                FindCandidatePrecipitates(step, tokens, candidates, verdict, precipitates, equations, library);
                continue;
            }

            // 模式 C：候选 × 试剂 证据
            // 本步开始前的 Must 快照：提供者降级时区分"此前已确定的提供者"与"本步新判定的提供者"
            var mustBefore = candidates.Where(c => verdict[c] == CompositionVerdict.Must).ToHashSet(StringComparer.Ordinal);
            var stepMust = new List<string>();
            foreach (var candidate in candidates)
            {
                if (verdict[candidate] == CompositionVerdict.MustNot || consumed.Contains(candidate))
                {
                    continue;
                }

                var reactants = new List<string> { candidate };
                reactants.AddRange(reagentSpecies);
                var matches = ReactionMatcher.Match(reactants, tokens, library, allowSubset: true);

                if (negative)
                {
                    if (matches.Count > 0)
                    {
                        verdict[candidate] = CompositionVerdict.MustNot;
                        result.Steps.Add(new InferenceStep
                        {
                            Description = $"第 {step.Step} 步：{candidate} 会与 {string.Join("、", reagentSpecies)} 反应"
                                + $"（{matches[0].Entry.Equation}），但现象为「{step.Phenomenon}」，故不存在。",
                            Evidence = matches[0].Entry.Equation,
                            KnowledgeId = matches[0].Entry.KnowledgeId,
                            Source = matches[0].Entry.Source
                        });
                    }

                    continue;
                }

                var aligned = matches
                    .Where(m => PhenomenonAligned(m.Entry, step.Phenomenon, indicated, candidate))
                    .OrderByDescending(m => m.IsIonic)
                    .ToList();
                if (aligned.Count > 0)
                {
                    verdict[candidate] = CompositionVerdict.Must;
                    stepMust.Add(candidate);
                    equations.Add(aligned[0].Entry);
                    if (excess)
                    {
                        consumed.Add(candidate);
                    }

                    result.Steps.Add(new InferenceStep
                    {
                        Description = $"第 {step.Step} 步：{candidate} 与 {string.Join("、", reagentSpecies)} 反应，"
                            + $"与现象「{step.Phenomenon}」吻合，判定存在。",
                        Evidence = aligned[0].Entry.Equation,
                        KnowledgeId = aligned[0].Entry.KnowledgeId,
                        Source = aligned[0].Entry.Source
                    });
                }
                else if (ReactionMatcher.ExplainsIndication(candidate, indicated))
                {
                    verdict[candidate] = CompositionVerdict.Must;
                    stepMust.Add(candidate);
                    result.Steps.Add(new InferenceStep
                    {
                        Description = $"第 {step.Step} 步：现象「{step.Phenomenon}」直接指示 {candidate}。",
                        Source = "IdentificationEngine"
                    });
                }
            }

            // 提供者降级：本步指示的离子若已有其他 Must 候选提供，则本步新判 Must 的提供者降为 Maybe
            foreach (var ion in indicated.Where(i => i.Contains('^')))
            {
                var providers = candidates.Where(c => ReactionMatcher.SpeciesSetOf(c).Contains(ion)).ToList();
                if (providers.Count <= 1)
                {
                    continue;
                }

                var priorMust = providers.Any(mustBefore.Contains);
                if (!priorMust)
                {
                    continue;
                }

                foreach (var c in stepMust.Where(c => providers.Contains(c) && !mustBefore.Contains(c)))
                {
                    verdict[c] = CompositionVerdict.Maybe;
                    result.Steps.Add(new InferenceStep
                    {
                        Description = $"{ion} 已由其他确定存在的物质提供，{c} 无法确定，降为可能存在。",
                        Source = "IdentificationEngine"
                    });
                }
            }

            // "除去 X" → 消耗跟踪
            foreach (Match m in RemovedPattern.Matches(step.Phenomenon))
            {
                consumed.Add(m.Groups[1].Value);
            }
        }

        foreach (var candidate in candidates.Where(c => verdict[c] == CompositionVerdict.Unknown))
        {
            verdict[candidate] = CompositionVerdict.Maybe;
        }

        result.Composition = verdict;
        result.Equations = equations.GroupBy(e => e.Id).Select(g => g.First()).ToList();
        var must = verdict.Count(kv => kv.Value == CompositionVerdict.Must);
        result.Status = must > 0 ? InferenceStatus.Solved : InferenceStatus.Underdetermined;
        result.Message = must > 0
            ? $"确定存在 {must} 种成分，其余按证据给出排除或存疑判定。"
            : "现有证据不足以确定任何成分。";
        return result;
    }

    /// <summary>模式 A：沉淀 + 酸 → 部分溶解/全部溶解/不溶解，反推沉淀组成与提供者。</summary>
    private static void HandlePrecipitateTreatment(
        ExperimentStep step,
        List<string> tokens,
        List<string> reagentSpecies,
        List<PrecipitateRecord> precipitates,
        Dictionary<string, CompositionVerdict> verdict,
        List<ReactionEntry> equations,
        ReactionLibrary library)
    {
        var dissolved = new List<(PrecipitateRecord Record, ReactionEntry Entry)>();
        var inert = new List<PrecipitateRecord>();
        foreach (var record in precipitates)
        {
            var reactants = new List<string> { record.Formula };
            reactants.AddRange(reagentSpecies);
            var matches = ReactionMatcher.Match(reactants, tokens, library, allowSubset: true)
                .Where(m => m.Entry.Tags.Contains("沉淀溶解") || m.Entry.Tags.Contains("产气"))
                .ToList();
            if (matches.Count > 0)
            {
                dissolved.Add((record, matches[0].Entry));
            }
            else
            {
                inert.Add(record);
            }
        }

        var confirmAll = step.Phenomenon.Contains("部分溶解");
        var confirmDissolved = confirmAll || step.Phenomenon.Contains("溶解");
        var confirmed = confirmAll
            ? precipitates
            : confirmDissolved
                ? dissolved.Select(d => d.Record).ToList()
                : inert;

        foreach (var record in confirmed)
        {
            foreach (var provider in record.Providers)
            {
                if (verdict.ContainsKey(provider) && verdict[provider] != CompositionVerdict.MustNot)
                {
                    verdict[provider] = CompositionVerdict.Must;
                }
            }

            equations.Add(record.Entry);
        }

        foreach (var (_, entry) in dissolved.Where(d => confirmed.Contains(d.Record)))
        {
            equations.Add(entry);
        }
    }

    /// <summary>模式 B：候选两两组合检索沉淀反应，记录沉淀池供后续酸溶步骤使用。</summary>
    private static void FindCandidatePrecipitates(
        ExperimentStep step,
        List<string> tokens,
        List<string> candidates,
        Dictionary<string, CompositionVerdict> verdict,
        List<PrecipitateRecord> precipitates,
        List<ReactionEntry> equations,
        ReactionLibrary library)
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            for (var j = i + 1; j < candidates.Count; j++)
            {
                if (verdict[candidates[i]] == CompositionVerdict.MustNot
                    || verdict[candidates[j]] == CompositionVerdict.MustNot)
                {
                    continue;
                }

                var reactants = new List<string> { candidates[i], candidates[j], "H2O" };
                // 离子方程式优先：成分推断的证据方程式按课内书写惯例取离子式
                // （如 Ba^2+ + CO3^2- = BaCO3↓），同沉淀的分子式条目不再重复记录。
                var matches = ReactionMatcher.Match(reactants, tokens, library, allowSubset: true)
                    .OrderByDescending(m => m.IsIonic)
                    .ToList();
                foreach (var match in matches)
                {
                    var ppt = match.Entry.Products.FirstOrDefault(p => IsInsoluble(p, match.Entry.Equation));
                    if (ppt is null)
                    {
                        continue;
                    }

                    var providers = new HashSet<string>(StringComparer.Ordinal) { candidates[i], candidates[j] };
                    if (precipitates.Any(p => p.Formula == ppt))
                    {
                        continue;
                    }

                    precipitates.Add(new PrecipitateRecord(ppt, providers, match.Entry));
                    equations.Add(match.Entry);
                }
            }
        }
    }

    private static bool IsInsoluble(string formula, string equation)
    {
        if (equation.Contains(formula + "↓"))
        {
            return true;
        }

        try
        {
            return ElectrolyteRules.LookupSolubility(formula) == Solubility.Insoluble;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    // -----------------------------------------------------------------------
    // 现象匹配工具
    // -----------------------------------------------------------------------

    /// <summary>负证据现象：应反应而未观察到变化。</summary>
    private static bool IsNegative(string phenomenon) =>
        phenomenon.Contains("无明显") || phenomenon.Contains("无变化") || phenomenon.Contains("未见");

    /// <summary>与 subject 不同的单大写字母视为另一实体标签（如平行试样 B），其步骤不参与本对象判别。</summary>
    private static bool IsForeignEntityLabel(string text, string subject) =>
        text.Length == 1 && text[0] >= 'A' && text[0] <= 'Z' && text != subject;

    /// <summary>条目现象与步骤现象是否吻合（文本片段/标签/指示产物/直接指示）。</summary>
    private static bool PhenomenonAligned(
        ReactionEntry entry,
        string stepPhenomenon,
        HashSet<string> indicated,
        string candidate)
    {
        foreach (var segment in SplitSegments(entry.Phenomenon))
        {
            if (ReactionMatcher.PhenomenonHit(segment, stepPhenomenon))
            {
                return true;
            }
        }

        if (entry.Tags.Contains("沉淀") && stepPhenomenon.Contains("沉淀"))
        {
            return true;
        }

        if (entry.Tags.Contains("产气") && (stepPhenomenon.Contains("气体") || stepPhenomenon.Contains("气泡")))
        {
            return true;
        }

        if (entry.Tags.Any(tag => tag.Length >= 3 && stepPhenomenon.IndexOf(tag, StringComparison.Ordinal) >= 0))
        {
            return true;
        }

        var normalizedIndicated = indicated.Select(NormalizeBody).ToHashSet(StringComparer.Ordinal);
        if (entry.Products.Any(p => normalizedIndicated.Contains(NormalizeBody(p))))
        {
            return true;
        }

        return indicated.Contains(candidate);
    }

    private static IEnumerable<string> SplitSegments(string phenomenon)
    {
        foreach (var part in ReactionMatcher.StripParens(phenomenon).Split(new[] { '，', '、', '；' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0)
            {
                yield return trimmed;
            }
        }
    }

    private static bool IsParseable(string formula)
    {
        try
        {
            Species.Parse(formula);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string NormalizeBody(string formula)
    {
        try
        {
            return Species.Parse(formula).Body;
        }
        catch (FormatException)
        {
            return formula.Trim();
        }
    }
}
