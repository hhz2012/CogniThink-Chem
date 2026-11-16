using CogniThink.Chem.Core.Diagnostics;

namespace CogniThink.Chem.Core.Knowledge;

/// <summary>
/// 知识本体：覆盖清单的内存视图与检索入口（M1）。
/// 只负责知识点本身，不负责规则与题目内容。
/// </summary>
public sealed class KnowledgeBase
{
    private readonly Dictionary<string, KnowledgeItem> _items;

    public KnowledgeBase(IEnumerable<KnowledgeItem> items)
    {
        _items = new Dictionary<string, KnowledgeItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (!_items.TryAdd(item.Id, item))
            {
                throw new ArgumentException($"知识点 id 重复：{item.Id}");
            }
        }
    }

    public int Count => _items.Count;

    public IReadOnlyCollection<KnowledgeItem> All => _items.Values;

    public IReadOnlyList<KnowledgeItem> MainItems =>
        _items.Values.Where(i => i.Tier == KnowledgeTier.Main).ToList();

    public IReadOnlyList<KnowledgeItem> ExtendedItems =>
        _items.Values.Where(i => i.Tier == KnowledgeTier.Extended).ToList();

    public bool TryGet(string id, out KnowledgeItem item) => _items.TryGetValue(id, out item!);

    public KnowledgeItem GetRequired(string id)
    {
        if (!_items.TryGetValue(id, out var item))
        {
            throw new KeyNotFoundException($"未登记的知识点：{id}");
        }

        return item;
    }

    /// <summary>按关键词（匹配名称或 id）与模块过滤。</summary>
    public IReadOnlyList<KnowledgeItem> Search(string? keyword = null, KnowledgeModule? module = null)
    {
        IEnumerable<KnowledgeItem> query = _items.Values;

        if (module is not null)
        {
            query = query.Where(i => i.Module == module.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(i =>
                i.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                i.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderBy(i => i.Module).ThenBy(i => i.Id, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>直接前置知识点（未登记的 id 会被忽略，由 Validate 负责报错）。</summary>
    public IReadOnlyList<KnowledgeItem> DirectPrerequisites(string id)
    {
        var item = GetRequired(id);
        return item.Prerequisites
            .Where(p => _items.ContainsKey(p))
            .Select(p => _items[p])
            .ToList();
    }

    /// <summary>传递闭包的前置知识点（广度优先，去重，不含自身）。</summary>
    public IReadOnlyList<KnowledgeItem> PrerequisiteChain(string id)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { id };
        var queue = new Queue<string>();
        foreach (var p in GetRequired(id).Prerequisites)
        {
            queue.Enqueue(p);
        }

        var result = new List<KnowledgeItem>();
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current) || !_items.TryGetValue(current, out var item))
            {
                continue;
            }

            result.Add(item);
            foreach (var p in item.Prerequisites)
            {
                queue.Enqueue(p);
            }
        }

        return result;
    }

    /// <summary>各模块的主干/延伸条目数，用于覆盖矩阵。</summary>
    public IReadOnlyDictionary<KnowledgeModule, (int Main, int Extended)> ModuleCoverage()
    {
        return _items.Values
            .GroupBy(i => i.Module)
            .ToDictionary(
                g => g.Key,
                g => (g.Count(i => i.Tier == KnowledgeTier.Main), g.Count(i => i.Tier == KnowledgeTier.Extended)));
    }

    public ValidationReport Validate()
    {
        var report = new ValidationReport("knowledge");
        foreach (var item in _items.Values)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                report.AddError($"{item.Id} 名称为空");
            }

            if (string.IsNullOrWhiteSpace(item.TextbookRef))
            {
                report.AddWarning($"{item.Id} 缺少教材出处");
            }

            foreach (var p in item.Prerequisites.Where(p => !_items.ContainsKey(p)))
            {
                report.AddError($"{item.Id} 的前置知识点未登记：{p}");
            }
        }

        return report;
    }
}
