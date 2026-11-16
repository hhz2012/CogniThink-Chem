using CogniThink.Chem.Core.Chemistry;
using CogniThink.Chem.Core.Data;

namespace CogniThink.Chem.Core.Reactions;

/// <summary>
/// 反应知识库：载入 data/knowledge/reactions/*.json 并建立两级索引，
/// 是知识驱动推断的地基。推断引擎只在这里检索已知反应，绝不生成库外产物。
///
/// 索引：
/// <list type="bullet">
/// <item>按"排序后反应物集"→ 条目列表：供产物推断精确检索（同一反应物集可能因条件不同对应多条，如 C+O2）。</item>
/// <item>按"任一参与物种"倒排 → 条目列表：供过程推断做转化链图搜索。</item>
/// </list>
/// </summary>
public sealed class ReactionLibrary
{
    private readonly List<ReactionEntry> _entries;
    private readonly Dictionary<string, List<ReactionEntry>> _byReactantSet;
    private readonly Dictionary<string, List<ReactionEntry>> _bySpecies;

    /// <summary>共享默认实例（从 <see cref="DataRoot.Resolve()"/> 定位的数据根载入）。</summary>
    public static readonly Lazy<ReactionLibrary> Default = new(() => Load());

    private ReactionLibrary(IEnumerable<ReactionEntry> entries)
    {
        _entries = entries.ToList();
        _byReactantSet = new Dictionary<string, List<ReactionEntry>>(StringComparer.Ordinal);
        _bySpecies = new Dictionary<string, List<ReactionEntry>>(StringComparer.Ordinal);

        foreach (var entry in _entries)
        {
            var reactantKey = ReactantSetKey(entry.Reactants);
            if (!_byReactantSet.TryGetValue(reactantKey, out var byReactant))
            {
                byReactant = new List<ReactionEntry>();
                _byReactantSet[reactantKey] = byReactant;
            }

            byReactant.Add(entry);

            foreach (var species in entry.Reactants.Concat(entry.Products))
            {
                var key = NormalizeSpecies(species);
                if (!_bySpecies.TryGetValue(key, out var list))
                {
                    list = new List<ReactionEntry>();
                    _bySpecies[key] = list;
                }

                if (!list.Contains(entry))
                {
                    list.Add(entry);
                }
            }
        }
    }

    /// <summary>
    /// 载入反应知识库。
    /// </summary>
    /// <param name="dataRoot">数据根目录；null 时由 <see cref="DataRoot.Resolve"/> 定位。</param>
    /// <exception cref="DirectoryNotFoundException">reactions 目录缺失。</exception>
    public static ReactionLibrary Load(string? dataRoot = null)
    {
        var root = DataRoot.Resolve(dataRoot);
        var directory = Path.Combine(root, "knowledge", "reactions");
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"反应知识库目录不存在：{directory}");
        }

        var entries = new List<ReactionEntry>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            entries.AddRange(ChemDataLoader.ReadItems<ReactionEntry>(file));
        }

        return new ReactionLibrary(entries);
    }

    /// <summary>条目总数。</summary>
    public int Count => _entries.Count;

    /// <summary>全部条目。</summary>
    public IReadOnlyCollection<ReactionEntry> All => _entries;

    /// <summary>
    /// 按反应物集合精确检索（顺序无关）。返回所有反应物集匹配的条目（可能因条件不同而多条）。
    /// 条件筛选由调用方（推断引擎）用 <see cref="ConditionsSatisfied"/> 处理。
    /// </summary>
    public IReadOnlyList<ReactionEntry> FindByReactants(IEnumerable<string> reactants)
    {
        var key = ReactantSetKey(reactants);
        return _byReactantSet.TryGetValue(key, out var list) ? list : Array.Empty<ReactionEntry>();
    }

    /// <summary>返回任一方向涉及该物种（作反应物或产物）的全部条目，供转化链图搜索。</summary>
    public IReadOnlyList<ReactionEntry> Involving(string species)
    {
        var key = NormalizeSpecies(species);
        return _bySpecies.TryGetValue(key, out var list) ? list : Array.Empty<ReactionEntry>();
    }

    /// <summary>
    /// 把条件列表拆成原子条件集合（按 ',' 分割并去空白），如 "△,浓" → {"△","浓"}。
    /// </summary>
    public static IReadOnlySet<string> ConditionTokens(IEnumerable<string>? conditions)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (conditions is null)
        {
            return set;
        }

        foreach (var condition in conditions)
        {
            foreach (var token in condition.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (token.Length > 0)
                {
                    set.Add(token);
                }
            }
        }

        return set;
    }

    /// <summary>
    /// 判断查询条件是否满足条目所需条件：条目条件令牌必须是查询条件令牌的子集。
    /// 条目无条件（常温进行）恒满足。浓/稀等互斥条件因"子集不成立"而自然区分。
    /// </summary>
    public static bool ConditionsSatisfied(IEnumerable<string>? queryConditions, IReadOnlyList<string> entryConditions)
    {
        if (entryConditions.Count == 0)
        {
            return true;
        }

        var queryTokens = ConditionTokens(queryConditions);
        var entryTokens = ConditionTokens(entryConditions);
        return entryTokens.All(queryTokens.Contains);
    }

    /// <summary>反应物集合键（排序后拼接，顺序无关）。</summary>
    private static string ReactantSetKey(IEnumerable<string> reactants) =>
        string.Join("|", reactants.Select(NormalizeSpecies).OrderBy(s => s, StringComparer.Ordinal));

    /// <summary>
    /// 物种归一化：离子统一为 caret 形式（Fe3+ → Fe^3+）；
    /// 结晶水合物（含 '·'）按原文；解析失败退回去空白原文。
    /// </summary>
    private static string NormalizeSpecies(string formula)
    {
        var trimmed = formula.Trim().Replace(" ", "");
        if (trimmed.Contains('·'))
        {
            return trimmed;
        }

        try
        {
            return Species.Parse(trimmed).ToString();
        }
        catch (FormulaException)
        {
            return trimmed;
        }
    }
}
