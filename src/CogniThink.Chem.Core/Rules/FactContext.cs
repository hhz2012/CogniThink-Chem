namespace CogniThink.Chem.Core.Rules;

/// <summary>事实上下文：键值事实的集合，规则前提在它上面求值。</summary>
public sealed class FactContext
{
    private readonly Dictionary<string, List<string>> _facts = new(StringComparer.OrdinalIgnoreCase);

    public FactContext Add(string key, params string[] values)
    {
        if (!_facts.TryGetValue(key, out var list))
        {
            list = new List<string>();
            _facts[key] = list;
        }

        foreach (var v in values)
        {
            if (!list.Contains(v, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(v);
            }
        }

        return this;
    }

    public IReadOnlyList<string> Values(string key) =>
        _facts.TryGetValue(key, out var list) ? list : Array.Empty<string>();

    public bool HasKey(string key) => _facts.ContainsKey(key);

    public bool HasValue(string key, string value) =>
        _facts.TryGetValue(key, out var list) && list.Contains(value, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Keys => _facts.Keys;

    public static FactContext From(IReadOnlyDictionary<string, string[]>? map)
    {
        var ctx = new FactContext();
        if (map is null)
        {
            return ctx;
        }

        foreach (var (key, values) in map)
        {
            ctx.Add(key, values.ToArray());
        }

        return ctx;
    }

    public FactContext Clone()
    {
        var clone = new FactContext();
        foreach (var (key, values) in _facts)
        {
            clone.Add(key, values.ToArray());
        }

        return clone;
    }

    public override string ToString() =>
        string.Join("; ", _facts.Select(kv => $"{kv.Key}={string.Join("|", kv.Value)}"));
}
