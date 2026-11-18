namespace CogniThink.TutorBrain.Application.Ability;

using CogniThink.TutorBrain.Domain;

/// <summary>知识掌握度摘要</summary>
public sealed class KnowledgeMasterySummary
{
    public string KnowledgeId { get; set; } = string.Empty;
    public string KnowledgeName { get; set; } = string.Empty;
    public double Mastery { get; set; }
    public int Attempts { get; set; }
    public bool IsWeak { get; set; }
}

/// <summary>
/// 知识 + 能力综合评价结果。
/// </summary>
public sealed class ComprehensiveEvaluation
{
    public string StudentId { get; set; } = string.Empty;

    /// <summary>综合得分（知识 60% + 能力 40%）</summary>
    public double OverallScore { get; set; }

    /// <summary>综合等级</summary>
    public string OverallLevel => OverallScore switch
    {
        >= 0.8 => "优秀",
        >= 0.6 => "良好",
        >= 0.4 => "中等",
        _ => "需加强"
    };

    /// <summary>知识维度得分（各知识点掌握度加权平均）</summary>
    public double KnowledgeScore { get; set; }

    /// <summary>能力维度得分（各能力得分平均）</summary>
    public double AbilityScore { get; set; }

    /// <summary>各知识点掌握度</summary>
    public List<KnowledgeMasterySummary> KnowledgeMasteries { get; set; } = new();

    /// <summary>各能力得分</summary>
    public List<AbilityScore> AbilityScores { get; set; } = new();

    /// <summary>薄弱知识点</summary>
    public List<KnowledgeMasterySummary> WeakKnowledge { get; set; } = new();

    /// <summary>薄弱能力</summary>
    public List<AbilityScore> WeakAbilities { get; set; } = new();

    /// <summary>高频错因（全局）</summary>
    public List<string> TopErrorTags { get; set; } = new();

    /// <summary>教学建议（基于薄弱项生成）</summary>
    public List<string> TeachingSuggestions { get; set; } = new();
}

/// <summary>
/// 知识 + 能力综合评价服务。
/// 结合知识点掌握度（BKT）和能力得分，给出综合评价和教学建议。
/// </summary>
public sealed class ComprehensiveEvaluationService
{
    private const double KnowledgeWeight = 0.6;
    private const double AbilityWeight = 0.4;
    private const double WeakThreshold = 0.5;

    private readonly AbilityEvaluationService _abilityEval;
    private readonly AbilityGraphService _abilityGraph;

    public ComprehensiveEvaluationService(
        AbilityEvaluationService abilityEval,
        AbilityGraphService abilityGraph)
    {
        _abilityEval = abilityEval;
        _abilityGraph = abilityGraph;
    }

    /// <summary>
    /// 生成学生的知识 + 能力综合评价。
    /// </summary>
    public async Task<ComprehensiveEvaluation> EvaluateAsync(
        string studentId, CancellationToken ct = default)
    {
        // 1. 能力得分
        var abilityScores = await _abilityEval.EvaluateAsync(studentId, ct);

        // 2. 知识掌握度
        var (nodes, _) = await _abilityGraph.GetGraphAsync(studentId, Subject.Chemistry, ct);
        var knowledgeMasteries = nodes.Select(n => new KnowledgeMasterySummary
        {
            KnowledgeId = n.KnowledgeId,
            KnowledgeName = n.Name,
            Mastery = n.Mastery,
            Attempts = n.Attempts,
            IsWeak = n.IsWeak
        }).ToList();

        // 3. 计算维度得分
        var attemptedKnowledge = knowledgeMasteries.Where(k => k.Attempts > 0).ToList();
        var knowledgeScore = attemptedKnowledge.Count > 0
            ? attemptedKnowledge.Average(k => k.Mastery)
            : 0.5;

        var attemptedAbilities = abilityScores.Where(a => a.Attempts > 0).ToList();
        var abilityScore = attemptedAbilities.Count > 0
            ? attemptedAbilities.Average(a => a.Score)
            : 0.5;

        // 4. 综合得分
        var overallScore = knowledgeScore * KnowledgeWeight + abilityScore * AbilityWeight;

        // 5. 薄弱项
        var weakKnowledge = knowledgeMasteries
            .Where(k => k.Attempts > 0 && k.Mastery < WeakThreshold)
            .OrderBy(k => k.Mastery)
            .ToList();

        var weakAbilities = abilityScores
            .Where(a => a.Attempts > 0 && a.Score < 0.4)
            .OrderBy(a => a.Score)
            .ToList();

        // 6. 高频错因
        var topErrors = abilityScores
            .SelectMany(a => a.TopErrorTags)
            .GroupBy(t => t)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => g.Key)
            .ToList();

        // 7. 教学建议
        var suggestions = GenerateSuggestions(weakKnowledge, weakAbilities, topErrors);

        return new ComprehensiveEvaluation
        {
            StudentId = studentId,
            OverallScore = Math.Round(overallScore, 3),
            KnowledgeScore = Math.Round(knowledgeScore, 3),
            AbilityScore = Math.Round(abilityScore, 3),
            KnowledgeMasteries = knowledgeMasteries,
            AbilityScores = abilityScores,
            WeakKnowledge = weakKnowledge,
            WeakAbilities = weakAbilities,
            TopErrorTags = topErrors,
            TeachingSuggestions = suggestions
        };
    }

    private static List<string> GenerateSuggestions(
        List<KnowledgeMasterySummary> weakKnowledge,
        List<AbilityScore> weakAbilities,
        List<string> topErrors)
    {
        var suggestions = new List<string>();

        if (weakKnowledge.Count > 0)
        {
            var names = string.Join("、", weakKnowledge.Take(3).Select(k => k.KnowledgeName));
            suggestions.Add($"知识点「{names}」掌握度偏低，建议回顾基础概念并做针对性练习。");
        }

        foreach (var ab in weakAbilities.Take(2))
        {
            var meta = ChemistryAbilityLibrary.Get(ab.Ability);
            suggestions.Add($"{ab.Name}较弱：{meta.StrategyHint}");
        }

        if (topErrors.Count > 0)
        {
            suggestions.Add($"高频错因为「{string.Join("、", topErrors)}」，建议在后续练习中重点关注。");
        }

        if (suggestions.Count == 0)
        {
            suggestions.Add("目前表现均衡，建议继续保持并挑战更高难度题目。");
        }

        return suggestions;
    }
}
