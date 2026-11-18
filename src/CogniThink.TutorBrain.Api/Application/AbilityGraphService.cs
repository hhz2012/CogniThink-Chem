namespace CogniThink.TutorBrain.Application;

using Microsoft.EntityFrameworkCore;
using CogniThink.TutorBrain.Domain;
using CogniThink.TutorBrain.Infrastructure;

/// <summary>能力图谱中的节点：知识点 + 学生掌握度</summary>
public sealed record AbilityNode(
    string KnowledgeId,
    string Name,
    Subject Subject,
    CognitiveLevel Level,
    double Mastery,
    int Attempts,
    bool IsWeak,
    string? Description);

/// <summary>能力图谱中的边：前置 → 后继依赖</summary>
public sealed record AbilityEdge(string FromId, string ToId);

/// <summary>
/// 个人能力图谱：把学生对各知识点的掌握度叠加到知识依赖图上，
/// 形成"哪些掌握了、哪些薄弱、依赖关系如何"的可视化结构。
///
/// 铁律：掌握度只来自知识库判定（BKT 更新），不臆造；
/// 薄弱阈值统一为 0.5，低于即视为需巩固。
/// </summary>
public sealed class AbilityGraphService
{
    private const double WeakThreshold = 0.5;

    private readonly TutorDbContext _db;

    public AbilityGraphService(TutorDbContext db) => _db = db;

    /// <summary>获取学生的完整能力图谱（节点+边）</summary>
    public async Task<(List<AbilityNode> Nodes, List<AbilityEdge> Edges)> GetGraphAsync(
        string studentId,
        Subject? subject = null,
        CancellationToken ct = default)
    {
        var nodes = await _db.KnowledgeNodes
            .Where(n => subject == null || n.Subject == subject)
            .ToListAsync(ct);

        var masteries = await _db.KnowledgeMasteries
            .Where(m => m.StudentId == studentId)
            .ToDictionaryAsync(m => m.KnowledgeId, ct);

        var nodeIds = nodes.Select(n => n.Id).ToList();
        var edges = await _db.KnowledgeEdges
            .Where(e => nodeIds.Contains(e.FromId) && nodeIds.Contains(e.ToId))
            .Select(e => new AbilityEdge(e.FromId, e.ToId))
            .ToListAsync(ct);

        var abilityNodes = nodes.Select(n =>
        {
            var hasMastery = masteries.TryGetValue(n.Id, out var m);
            var mastery = hasMastery ? m!.Value : 0;
            var attempts = hasMastery ? m!.Attempts : 0;
            return new AbilityNode(
                n.Id,
                n.Name,
                n.Subject,
                n.Level,
                mastery,
                attempts,
                mastery < WeakThreshold,
                n.Description);
        }).ToList();

        return (abilityNodes, edges);
    }

    /// <summary>获取单个知识点的掌握度（含前置薄弱项）</summary>
    public async Task<AbilityNode?> GetNodeAsync(
        string studentId,
        string knowledgeId,
        CancellationToken ct = default)
    {
        var node = await _db.KnowledgeNodes.FirstOrDefaultAsync(n => n.Id == knowledgeId, ct);
        if (node is null) return null;

        var mastery = await _db.KnowledgeMasteries
            .FirstOrDefaultAsync(m => m.StudentId == studentId && m.KnowledgeId == knowledgeId, ct);

        return new AbilityNode(
            node.Id,
            node.Name,
            node.Subject,
            node.Level,
            mastery?.Value ?? 0,
            mastery?.Attempts ?? 0,
            (mastery?.Value ?? 0) < WeakThreshold,
            node.Description);
    }

    /// <summary>获取某知识点的薄弱前置（掌握度低于阈值）</summary>
    public Task<List<WeakKnowledgeItem>> GetWeakPrerequisitesAsync(
        string studentId,
        string knowledgeId,
        CancellationToken ct = default)
    {
        // 复用知识图谱服务的薄弱前置查询
        var graph = new KnowledgeGraphService(_db);
        return graph.GetWeakPrerequisitesAsync(studentId, knowledgeId, WeakThreshold, ct);
    }

    /// <summary>获取学生全部薄弱知识点</summary>
    public Task<List<WeakKnowledgeItem>> GetWeakKnowledgeAsync(
        string studentId,
        CancellationToken ct = default)
    {
        var graph = new KnowledgeGraphService(_db);
        return graph.GetWeakKnowledgeAsync(studentId, WeakThreshold, ct);
    }
}
