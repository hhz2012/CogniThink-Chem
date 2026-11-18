namespace CogniThink.TutorBrain.Application.Ability;

using Microsoft.EntityFrameworkCore;
using CogniThink.TutorBrain.Domain;
using CogniThink.TutorBrain.Infrastructure;

/// <summary>单项能力的评价结果</summary>
public sealed class AbilityScore
{
    public ChemistryAbility Ability { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>能力得分 0~1</summary>
    public double Score { get; set; }

    /// <summary>等级：优秀/良好/一般/薄弱</summary>
    public string Level => Score switch
    {
        >= 0.8 => "优秀",
        >= 0.6 => "良好",
        >= 0.4 => "一般",
        _ => "薄弱"
    };

    public bool IsWeak => Score < 0.4;

    public int Attempts { get; set; }
    public int Corrects { get; set; }

    /// <summary>平均提示级别（越高越依赖提示，能力越弱）</summary>
    public double AvgHintLevel { get; set; }

    /// <summary>该能力上的高频错因</summary>
    public List<string> TopErrorTags { get; set; } = new();
}

/// <summary>
/// 能力评价服务：从学生的作答轨迹（StepTraces）聚合各能力得分。
/// 不新增数据表，复用 StepTraces + ProblemSteps 计算。
/// </summary>
public sealed class AbilityEvaluationService
{
    private const double DefaultScore = 0.5;

    private readonly TutorDbContext _db;

    public AbilityEvaluationService(TutorDbContext db) => _db = db;

    /// <summary>
    /// 获取学生各能力的综合得分。
    /// 计算方法：从 StepTraces 逐条映射能力影响，累加后裁剪到 [0,1]。
    /// </summary>
    public async Task<List<AbilityScore>> EvaluateAsync(string studentId, CancellationToken ct = default)
    {
        var traces = await _db.StepTraces
            .Where(t => t.StudentId == studentId)
            .ToListAsync(ct);

        if (traces.Count == 0)
        {
            // 无作答记录，返回默认得分
            return ChemistryAbilityLibrary.AllAbilities.Select(a =>
            {
                var meta = ChemistryAbilityLibrary.Get(a);
                return new AbilityScore
                {
                    Ability = a,
                    Name = meta.Name,
                    Description = meta.Description,
                    Score = DefaultScore,
                    Attempts = 0,
                    Corrects = 0
                };
            }).ToList();
        }

        // 加载所有相关题目的步骤，获取 knowledgeId
        var problemIds = traces.Select(t => t.ProblemId).Distinct().ToList();
        var steps = await _db.ProblemSteps
            .Where(s => problemIds.Contains(s.ProblemId))
            .ToListAsync(ct);
        var stepMap = steps.ToDictionary(
            s => (s.ProblemId, s.StepIndex),
            s => s.KnowledgeId);

        // 初始化各能力得分
        var scores = ChemistryAbilityLibrary.AllAbilities.ToDictionary(
            a => a, _ => DefaultScore);
        var attempts = ChemistryAbilityLibrary.AllAbilities.ToDictionary(a => a, _ => 0);
        var corrects = ChemistryAbilityLibrary.AllAbilities.ToDictionary(a => a, _ => 0);
        var hintSums = ChemistryAbilityLibrary.AllAbilities.ToDictionary(a => a, _ => 0.0);
        var errorCounts = ChemistryAbilityLibrary.AllAbilities
            .ToDictionary(a => a, _ => new Dictionary<ErrorTag, int>());

        foreach (var trace in traces)
        {
            if (!stepMap.TryGetValue((trace.ProblemId, trace.StepIndex), out var knowledgeId))
                continue;

            var impacts = AbilityPerformanceMapper.Map(
                knowledgeId, trace.Verdict, trace.ErrorTag, trace.HintLevel);

            foreach (var imp in impacts)
            {
                scores[imp.Ability] = AbilityPerformanceMapper.Clamp(scores[imp.Ability] + imp.ScoreDelta);
                attempts[imp.Ability] += 1;
                hintSums[imp.Ability] += trace.HintLevel;

                if (trace.Verdict == StepVerdict.Correct)
                    corrects[imp.Ability] += 1;

                if (trace.ErrorTag != ErrorTag.None)
                {
                    var weakAb = ChemistryAbilityLibrary.GetAbilityForErrorTag(trace.ErrorTag);
                    if (weakAb.HasValue && weakAb.Value == imp.Ability)
                    {
                        if (!errorCounts[imp.Ability].ContainsKey(trace.ErrorTag))
                            errorCounts[imp.Ability][trace.ErrorTag] = 0;
                        errorCounts[imp.Ability][trace.ErrorTag] += 1;
                    }
                }
            }
        }

        return ChemistryAbilityLibrary.AllAbilities.Select(a =>
        {
            var meta = ChemistryAbilityLibrary.Get(a);
            var att = attempts[a];
            var topErrors = errorCounts[a]
                .OrderByDescending(kv => kv.Value)
                .Take(3)
                .Select(kv => kv.Key.ToText())
                .ToList();

            return new AbilityScore
            {
                Ability = a,
                Name = meta.Name,
                Description = meta.Description,
                Score = Math.Round(scores[a], 3),
                Attempts = att,
                Corrects = corrects[a],
                AvgHintLevel = att > 0 ? Math.Round(hintSums[a] / att, 2) : 0,
                TopErrorTags = topErrors
            };
        }).ToList();
    }

    /// <summary>获取学生最薄弱的 N 项能力</summary>
    public async Task<List<AbilityScore>> GetWeakAbilitiesAsync(
        string studentId, int topN = 3, CancellationToken ct = default)
    {
        var scores = await EvaluateAsync(studentId, ct);
        return scores
            .Where(s => s.Attempts > 0)
            .OrderBy(s => s.Score)
            .Take(topN)
            .ToList();
    }

    /// <summary>获取某知识点考察的能力及学生在这些能力上的得分</summary>
    public async Task<List<AbilityScore>> EvaluateForKnowledgeAsync(
        string studentId, string knowledgeId, CancellationToken ct = default)
    {
        var allScores = await EvaluateAsync(studentId, ct);
        var abilities = ChemistryAbilityLibrary.GetAbilitiesForKnowledge(knowledgeId);
        return allScores.Where(s => abilities.Contains(s.Ability)).ToList();
    }
}
