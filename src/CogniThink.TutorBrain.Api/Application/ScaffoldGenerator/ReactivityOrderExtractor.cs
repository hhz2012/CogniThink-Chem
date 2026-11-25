namespace CogniThink.TutorBrain.Application.ScaffoldGenerator;

using CogniThink.Chem.Core.Parsing;
using CogniThink.Chem.Core.Rules;

// ============================================================
// Task 1 (P0-2): D 步竞争排序提取 —— 把体系中出现的还原性粒子按教材顺序排序。
// 铁律：只取顺序表内、且体系中确实存在的粒子；<2 个 → 返回 null（D 降级），绝不猜测表外顺序。
// 数据源：data/highschool/redox-reactivity-series.json（人教版，可溯源）。
// ============================================================

/// <summary>单个待排序还原性粒子（key 写入 correct/options，label 用于展示）。</summary>
internal sealed class ReactiveSpecies
{
    public string Key { get; }
    public string Label { get; }
    public ReactiveSpecies(string key, string label) { Key = key; Label = label; }
}

/// <summary>D 步竞争排序提取结果（按还原性由强到弱，有 provenance）。</summary>
internal sealed class ReactivityOrderExtraction
{
    public IReadOnlyList<ReactiveSpecies> OrderedSpecies { get; }
    public string Provenance { get; }

    public ReactivityOrderExtraction(IReadOnlyList<ReactiveSpecies> orderedSpecies, string provenance)
    {
        OrderedSpecies = orderedSpecies;
        Provenance = provenance;
    }
}

internal static class ReactivityOrderExtractor
{
    public static ReactivityOrderExtraction? Extract(ParsedEquation? parsed)
    {
        if (parsed is null) return null;

        // 体系全部物种（归一化：去 caret、小写），大小写不敏感匹配。
        var formulas = parsed.AllSpecies
            .Select(s => RedoxReactivitySeries.NormalizeFormula(s.Formula))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 顺序表按还原性由强到弱遍历，命中体系存在的粒子即收录（保序）。
        var ordered = new List<ReactiveSpecies>();
        foreach (var entry in RedoxReactivitySeries.ReducingAgents)
        {
            if (formulas.Contains(entry.Match))
                ordered.Add(new ReactiveSpecies(entry.Key, entry.Label));
        }

        if (ordered.Count < 2) return null;

        var labels = string.Join(" > ", ordered.Select(s => s.Label));
        return new ReactivityOrderExtraction(ordered, $"RedoxReactivitySeries(教材): {labels}");
    }
}
