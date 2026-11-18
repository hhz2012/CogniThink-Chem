namespace CogniThink.TutorBrain.Application.Ability;

using Microsoft.EntityFrameworkCore;
using CogniThink.TutorBrain.Domain;
using CogniThink.TutorBrain.Infrastructure;

/// <summary>
/// 个人能力图谱服务：
/// 从 KnowledgeMastery / StepTrace / MistakeRecord 实时聚合学生的
/// "知识掌握度 + 能力维度得分 + 错因分布" 三维图谱。
///
/// 铁律：所有数据来自现有表，不新增持久化表，不臆造。
/// </summary>
public sealed class PersonalAbilityGraphService
{
    private const double WeakMasteryThreshold = 0.5;
    private const double WeakAbilityThreshold = 0.4;
    private const double KnowledgeWeight = 0.6;
    private const double AbilityWeight = 0.4;

    private readonly TutorDbContext _db;
    private readonly AbilityEvaluationService _abilityEval;

    public PersonalAbilityGraphService(TutorDbContext db, AbilityEvaluationService abilityEval)
    {
        _db = db;
        _abilityEval = abilityEval;
    }

    /// <summary>
    /// 构建学生的完整个人能力图谱。
    /// </summary>
    public async Task<PersonalAbilityGraph> BuildAsync(string studentId, CancellationToken ct = default)
    {
        // 1. 加载所有知识点 + 学生掌握度
        var nodes = await _db.KnowledgeNodes
            .Where(n => n.Subject == Subject.Chemistry)
            .ToListAsync(ct);

        var masteries = await _db.KnowledgeMasteries
            .Where(m => m.StudentId == studentId)
            .ToDictionaryAsync(m => m.KnowledgeId, ct);

        // 2. 加载学生所有作答轨迹，按知识点聚合错因和练习统计
        var traces = await _db.StepTraces
            .Where(t => t.StudentId == studentId)
            .ToListAsync(ct);

        var problemIds = traces.Select(t => t.ProblemId).Distinct().ToList();
        var steps = problemIds.Count > 0
            ? await _db.ProblemSteps
                .Where(s => problemIds.Contains(s.ProblemId))
                .ToListAsync(ct)
            : new List<ProblemStep>();
        var stepKpMap = steps.ToDictionary(
            s => (s.ProblemId, s.StepIndex),
            s => s.KnowledgeId);

        // 按知识点聚合：练习次数、正确次数、错因分布、最后练习时间
        var kpStats = nodes.ToDictionary(n => n.Id, _ => new KpStats());
        foreach (var trace in traces)
        {
            if (!stepKpMap.TryGetValue((trace.ProblemId, trace.StepIndex), out var kpId)) continue;
            if (!kpStats.ContainsKey(kpId)) continue;

            var stats = kpStats[kpId];
            stats.Attempts += 1;
            if (trace.Verdict == StepVerdict.Correct) stats.Corrects += 1;
            if (trace.ErrorTag != ErrorTag.None)
            {
                if (!stats.ErrorCounts.ContainsKey(trace.ErrorTag))
                    stats.ErrorCounts[trace.ErrorTag] = 0;
                stats.ErrorCounts[trace.ErrorTag] += 1;
            }
            if (trace.CreatedAt > stats.LastPracticedAt)
                stats.LastPracticedAt = trace.CreatedAt;
        }

        // 3. 加载全局能力得分
        var globalAbilityScores = await _abilityEval.EvaluateAsync(studentId, ct);

        // 4. 构建图谱节点
        var graphNodes = nodes.Select(n =>
        {
            var hasMastery = masteries.TryGetValue(n.Id, out var m);
            var mastery = hasMastery ? m!.Value : 0;
            var stats = kpStats[n.Id];
            var abilities = ChemistryAbilityLibrary.GetAbilitiesForKnowledge(n.Id);

            var abilityScores = abilities.Select(a =>
            {
                var global = globalAbilityScores.FirstOrDefault(s => s.Ability == a);
                return new AbilityScoreItem
                {
                    Ability = a,
                    Name = ChemistryAbilityLibrary.Get(a).Name,
                    Score = global?.Score ?? 0.5
                };
            }).ToList();

            var errorPattern = stats.ErrorCounts
                .OrderByDescending(kv => kv.Value)
                .Select(kv => new ErrorTagCount
                {
                    Tag = kv.Key,
                    TagText = kv.Key.ToText(),
                    Count = kv.Value
                })
                .ToList();

            var isWeak = stats.Attempts > 0 && mastery < WeakMasteryThreshold;
            string? weakReason = null;
            if (isWeak)
            {
                var topError = errorPattern.FirstOrDefault();
                weakReason = topError is not null
                    ? $"掌握度 {mastery:F2}，高频错因：{topError.TagText}（{topError.Count}次）"
                    : $"掌握度 {mastery:F2}，低于阈值";
            }

            return new PersonalAbilityNode
            {
                KnowledgeId = n.Id,
                KnowledgeName = n.Name,
                Level = n.Level,
                Description = n.Description,
                Mastery = Math.Round(mastery, 3),
                Attempts = stats.Attempts,
                Corrects = stats.Corrects,
                IsWeak = isWeak,
                AbilityScores = abilityScores,
                ErrorPattern = errorPattern,
                LastPracticedAt = stats.LastPracticedAt == default ? null : stats.LastPracticedAt,
                WeakReason = weakReason
            };
        }).ToList();

        // 5. 构建图谱边（复用知识图谱依赖，标注薄弱传导）
        var nodeIds = nodes.Select(n => n.Id).ToList();
        var edges = await _db.KnowledgeEdges
            .Where(e => nodeIds.Contains(e.FromId) && nodeIds.Contains(e.ToId))
            .ToListAsync(ct);

        var weakSet = graphNodes.Where(n => n.IsWeak).Select(n => n.KnowledgeId).ToHashSet();
        var graphEdges = edges.Select(e => new PersonalAbilityEdge
        {
            FromId = e.FromId,
            ToId = e.ToId,
            FromWeak = weakSet.Contains(e.FromId),
            ToWeak = weakSet.Contains(e.ToId)
        }).ToList();

        // 6. 薄弱知识链：对每个薄弱节点，追溯其薄弱前置
        var weakChains = BuildWeakChains(graphNodes, graphEdges);

        // 7. 全局错因模式
        var globalErrorPattern = kpStats.Values
            .SelectMany(s => s.ErrorCounts)
            .GroupBy(kv => kv.Key)
            .Select(g => new ErrorTagCount
            {
                Tag = g.Key,
                TagText = g.Key.ToText(),
                Count = g.Sum(x => x.Value)
            })
            .OrderByDescending(e => e.Count)
            .ToList();

        // 8. 综合得分
        var attemptedNodes = graphNodes.Where(n => n.Attempts > 0).ToList();
        var knowledgeScore = attemptedNodes.Count > 0
            ? attemptedNodes.Average(n => n.Mastery)
            : 0.5;
        var attemptedAbilities = globalAbilityScores.Where(a => a.Attempts > 0).ToList();
        var abilityScore = attemptedAbilities.Count > 0
            ? attemptedAbilities.Average(a => a.Score)
            : 0.5;
        var overallScore = knowledgeScore * KnowledgeWeight + abilityScore * AbilityWeight;

        // 9. 全局薄弱能力
        var weakAbilities = globalAbilityScores
            .Where(a => a.Attempts > 0 && a.Score < WeakAbilityThreshold)
            .OrderBy(a => a.Score)
            .Select(a => new AbilityScoreItem
            {
                Ability = a.Ability,
                Name = a.Name,
                Score = a.Score
            })
            .ToList();

        return new PersonalAbilityGraph
        {
            StudentId = studentId,
            Nodes = graphNodes,
            Edges = graphEdges,
            KnowledgeScore = Math.Round(knowledgeScore, 3),
            AbilityScore = Math.Round(abilityScore, 3),
            OverallScore = Math.Round(overallScore, 3),
            WeakNodes = graphNodes.Where(n => n.IsWeak).OrderBy(n => n.Mastery).ToList(),
            WeakChains = weakChains,
            GlobalErrorPattern = globalErrorPattern,
            WeakAbilities = weakAbilities
        };
    }

    /// <summary>
    /// 对每个薄弱节点，BFS 追溯其薄弱前置，形成薄弱知识链。
    /// </summary>
    private static List<WeakKnowledgeChain> BuildWeakChains(
        List<PersonalAbilityNode> nodes, List<PersonalAbilityEdge> edges)
    {
        var weakSet = nodes.Where(n => n.IsWeak).Select(n => n.KnowledgeId).ToHashSet();
        var chains = new List<WeakKnowledgeChain>();

        // 对每个薄弱节点，反向追溯（从后继往前置走），找到薄弱前置链
        foreach (var node in nodes.Where(n => n.IsWeak))
        {
            var chain = new List<string> { node.KnowledgeId };
            var current = node.KnowledgeId;

            // 最多追溯 3 层前置
            for (var depth = 0; depth < 3; depth++)
            {
                var prereq = edges
                    .Where(e => e.ToId == current && e.FromWeak)
                    .Select(e => e.FromId)
                    .FirstOrDefault();
                if (prereq is null) break;
                chain.Insert(0, prereq);
                current = prereq;
            }

            if (chain.Count > 1)
            {
                var rootNode = nodes.FirstOrDefault(n => n.KnowledgeId == chain[0]);
                chains.Add(new WeakKnowledgeChain
                {
                    Path = chain,
                    RootCause = rootNode?.WeakReason ?? "前置知识点薄弱"
                });
            }
        }

        return chains;
    }

    private sealed class KpStats
    {
        public int Attempts { get; set; }
        public int Corrects { get; set; }
        public Dictionary<ErrorTag, int> ErrorCounts { get; } = new();
        public DateTime LastPracticedAt { get; set; }
    }
}
