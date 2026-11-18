namespace CogniThink.TutorBrain.Application;

using Microsoft.EntityFrameworkCore;
using CogniThink.TutorBrain.Domain;
using CogniThink.TutorBrain.Infrastructure;

public sealed record WeakKnowledgeItem(
    string KnowledgeId,
    string Name,
    Subject Subject,
    double Mastery,
    bool IsPrerequisite);

/// <summary>知识图谱服务：依赖链查询、薄弱前置定位</summary>
public sealed class KnowledgeGraphService
{
    private readonly TutorDbContext _db;

    public KnowledgeGraphService(TutorDbContext db) => _db = db;

    public Task<List<KnowledgeNode>> GetNodesAsync(Subject? subject, CancellationToken ct = default) =>
        _db.KnowledgeNodes
            .Where(n => subject == null || n.Subject == subject)
            .OrderBy(n => n.Subject).ThenBy(n => n.Level)
            .ToListAsync(ct);

    public Task<KnowledgeNode?> GetNodeAsync(string id, CancellationToken ct = default) =>
        _db.KnowledgeNodes.FirstOrDefaultAsync(n => n.Id == id, ct);

    public Task<Dictionary<string, string>> GetNameMapAsync(IEnumerable<string> ids, CancellationToken ct = default) =>
        _db.KnowledgeNodes
            .Where(n => ids.Contains(n.Id))
            .ToDictionaryAsync(n => n.Id, n => n.Name, ct);

    /// <summary>
    /// 查询全部前置知识点（递归，BFS）。
    /// 导学原则：前置未掌握时不能直接跳到后继知识点。
    /// </summary>
    public async Task<List<string>> GetPrerequisiteChainAsync(string knowledgeId, CancellationToken ct = default)
    {
        var result = new List<string>();
        var visited = new HashSet<string> { knowledgeId };
        var queue = new Queue<string>();
        queue.Enqueue(knowledgeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var parents = await _db.KnowledgeEdges
                .Where(e => e.ToId == current)
                .Select(e => e.FromId)
                .ToListAsync(ct);
            foreach (var p in parents)
            {
                if (visited.Add(p))
                {
                    result.Add(p);
                    queue.Enqueue(p);
                }
            }
        }

        return result;
    }

    /// <summary>查询某知识点的薄弱前置（掌握度低于阈值）</summary>
    public async Task<List<WeakKnowledgeItem>> GetWeakPrerequisitesAsync(
        string studentId,
        string knowledgeId,
        double threshold = 0.5,
        CancellationToken ct = default)
    {
        var chain = await GetPrerequisiteChainAsync(knowledgeId, ct);
        if (chain.Count == 0) return new List<WeakKnowledgeItem>();

        var nodes = await _db.KnowledgeNodes.Where(n => chain.Contains(n.Id)).ToListAsync(ct);
        var masteries = await _db.KnowledgeMasteries
            .Where(m => m.StudentId == studentId && chain.Contains(m.KnowledgeId))
            .ToDictionaryAsync(m => m.KnowledgeId, m => m.Value, ct);

        return nodes
            .Select(n => new WeakKnowledgeItem(
                n.Id,
                n.Name,
                n.Subject,
                masteries.TryGetValue(n.Id, out var v) ? v : 0,
                true))
            .Where(x => x.Mastery < threshold)
            .OrderBy(x => x.Mastery)
            .ToList();
    }

    /// <summary>学生全部薄弱知识点（仅统计做过的）</summary>
    public async Task<List<WeakKnowledgeItem>> GetWeakKnowledgeAsync(
        string studentId,
        double threshold = 0.5,
        CancellationToken ct = default)
    {
        var rows = await (
            from m in _db.KnowledgeMasteries
            join n in _db.KnowledgeNodes on m.KnowledgeId equals n.Id
            where m.StudentId == studentId && m.Attempts > 0 && m.Value < threshold
            orderby m.Value ascending
            select new WeakKnowledgeItem(n.Id, n.Name, n.Subject, m.Value, false)
        ).Take(20).ToListAsync(ct);

        return rows;
    }
}
