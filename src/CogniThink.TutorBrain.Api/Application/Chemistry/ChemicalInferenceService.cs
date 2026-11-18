namespace CogniThink.TutorBrain.Application.Chemistry;

using CogniThink.TutorBrain.Domain.Chemistry;

public sealed record CoexistenceResult(
    bool CanCoexist,
    IReadOnlyList<CoexistenceConflict> Conflicts,
    IReadOnlyList<string> Evidence);

public sealed record ChemicalQueryResult(
    ChemicalQuery Query,
    string Conclusion,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<CoexistenceConflict> Conflicts,
    IReadOnlyList<Reaction> RelatedReactions);

/// <summary>
/// 化学推理服务：内部查询知识库，给出颜色、溶解性、反应、共存判断与题目结论。
/// 所有结论都带证据链，供导学话术引用，而不是凭模型臆测。
/// </summary>
public sealed class ChemicalInferenceService
{
    private readonly ChemicalKnowledgeBase _kb;

    public ChemicalInferenceService(ChemicalKnowledgeBase kb) => _kb = kb;

    public Substance? GetSubstance(string key) => _kb.FindSubstance(key);

    public ChemicalIon? GetIon(string key) => _kb.FindIon(key);

    /// <summary>物质/离子的颜色：优先溶液颜色，其次固体颜色</summary>
    public string? GetColor(string key)
    {
        var substance = _kb.FindSubstance(key);
        if (substance is not null) return substance.SolutionColor ?? substance.Color;
        return _kb.FindIon(key)?.SolutionColor;
    }

    public SolubilityLevel GetSolubility(string cation, string anion) =>
        _kb.GetSolubility(cation, anion);

    /// <summary>
    /// 离子共存判断：
    /// 1) 命中离子相互作用规则（沉淀 / 气体 / 弱电解质 / 氧化还原 / 络合）
    /// 2) 未覆盖的组合再用溶解度表兜底
    /// </summary>
    public CoexistenceResult CheckCoexistence(
        IEnumerable<string> ions,
        bool acidic = false,
        bool alkaline = false)
    {
        var normalized = ions
            .Select(i => _kb.FindIon(i)?.Formula ?? i)
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Distinct()
            .ToList();

        var conflicts = new List<CoexistenceConflict>();
        var seen = new HashSet<string>();
        var evidence = new List<string>();

        foreach (var rule in _kb.Interactions)
        {
            if (rule.Condition == "酸性" && !acidic) continue;
            if (rule.Condition == "碱性" && !alkaline) continue;

            var matched = rule.Ions.All(x =>
                normalized.Any(i => ChemicalKnowledgeBase.NormalizeKey(i) == ChemicalKnowledgeBase.NormalizeKey(x)));
            if (!matched) continue;

            var key = string.Join("|", rule.Ions.Select(ChemicalKnowledgeBase.NormalizeKey).OrderBy(x => x));
            if (!seen.Add(key)) continue;

            conflicts.Add(new CoexistenceConflict
            {
                Ions = rule.Ions,
                Type = rule.Type,
                Result = rule.Result,
                KnowledgeIds = rule.KnowledgeIds
            });
            evidence.Add(rule.Result);
        }

        // 溶解度兜底：阴离子 × 阳离子
        var anions = normalized.Where(i => i.Contains('-')).ToList();
        if (alkaline) anions.Add("OH-");

        var cations = normalized.Where(i => i.Contains('+')).ToList();

        foreach (var cation in cations.Distinct())
        {
            foreach (var anion in anions.Distinct())
            {
                var level = _kb.GetSolubility(cation, anion);
                if (level != SolubilityLevel.Insoluble) continue;

                var key = string.Join("|", new[] { cation, anion }
                    .Select(ChemicalKnowledgeBase.NormalizeKey).OrderBy(x => x));
                if (!seen.Add(key)) continue;

                conflicts.Add(new CoexistenceConflict
                {
                    Ions = new List<string> { cation, anion },
                    Type = InteractionType.Precipitation,
                    Result = $"{cation} 与 {anion} 生成难溶物",
                    KnowledgeIds = new List<string> { "C-ION-REACTION" }
                });
                evidence.Add($"{cation} 与 {anion} 生成难溶物");
            }
        }

        return new CoexistenceResult(conflicts.Count == 0, conflicts, evidence);
    }

    /// <summary>把编译后的题目查询交给知识库求值</summary>
    public ChemicalQueryResult Evaluate(ChemicalQuery query)
    {
        var evidence = new List<string>();
        var reactions = new List<Reaction>();

        switch (query.Type)
        {
            case ChemicalQueryType.Coexistence:
            {
                var result = CheckCoexistence(query.Ions, query.Acidic, query.Alkaline);
                return new ChemicalQueryResult(
                    query,
                    result.CanCoexist ? "这些离子可以大量共存" : "这些离子不能大量共存",
                    result.Evidence,
                    result.Conflicts,
                    Array.Empty<Reaction>());
            }

            case ChemicalQueryType.Color:
            {
                foreach (var key in query.Substances.Concat(query.Ions))
                {
                    var color = GetColor(key);
                    if (color is not null) evidence.Add($"{key}：{color}");
                }
                return new ChemicalQueryResult(
                    query,
                    evidence.Count > 0 ? "已查到相关颜色" : "未查到颜色信息",
                    evidence,
                    Array.Empty<CoexistenceConflict>(),
                    Array.Empty<Reaction>());
            }

            case ChemicalQueryType.Solubility:
            {
                foreach (var substance in query.Substances)
                {
                    var s = _kb.FindSubstance(substance);
                    if (s is not null) evidence.Add($"{s.Name}（{s.Formula}）：{SolubilityText(s.Solubility)}");
                }
                return new ChemicalQueryResult(
                    query,
                    evidence.Count > 0 ? "已查到溶解性" : "未查到溶解性",
                    evidence,
                    Array.Empty<CoexistenceConflict>(),
                    Array.Empty<Reaction>());
            }

            case ChemicalQueryType.Identification:
            case ChemicalQueryType.Inference:
            case ChemicalQueryType.Equation:
            default:
            {
                foreach (var substance in query.Substances)
                {
                    foreach (var reaction in _kb.GetReactionsFor(substance))
                    {
                        if (reactions.All(r => r.Id != reaction.Id)) reactions.Add(reaction);
                    }
                }

                // 多物质题目优先选择同时命中多个已识别反应物的反应，避免被单一物质的无关反应淹没。
                var rankedReactions = reactions
                    .OrderByDescending(r => r.Reactants.Count(id => query.Substances.Contains(id)))
                    .Take(8)
                    .ToList();

                foreach (var reaction in rankedReactions)
                {
                    evidence.Add($"{reaction.Equation}；现象：{string.Join("、", reaction.Phenomena)}");
                }

                // 没有匹配反应时，再用离子相互作用补充证据。
                if (rankedReactions.Count == 0 && query.Ions.Count > 0)
                {
                    foreach (var ion in query.Ions)
                    {
                        foreach (var interaction in _kb.GetInteractionsFor(ion).Take(3))
                            evidence.Add(interaction.Result);
                    }
                }

                return new ChemicalQueryResult(
                    query,
                    rankedReactions.Count > 0 ? "已匹配到相关反应" : "已解析出相关物质与离子",
                    evidence.Distinct().ToList(),
                    Array.Empty<CoexistenceConflict>(),
                    rankedReactions);
            }
        }
    }

    public static string SolubilityText(SolubilityLevel level) => level switch
    {
        SolubilityLevel.Soluble => "易溶",
        SolubilityLevel.SlightlySoluble => "微溶",
        SolubilityLevel.Insoluble => "难溶",
        SolubilityLevel.Decomposes => "遇水分解",
        SolubilityLevel.Reacts => "与水反应",
        _ => "未知"
    };
}
