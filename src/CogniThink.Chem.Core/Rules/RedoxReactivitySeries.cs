namespace CogniThink.Chem.Core.Rules;

/// <summary>
/// 氧化还原竞争排序数据（数据源：data/highschool/redox-reactivity-series.json）。
/// 来源：人教版必修第二册常见还原性粒子顺序。
/// 引擎只用它给 D 步「竞争排序」提供确定性真值；表外物种返回空（不猜测）。
/// </summary>
public static class RedoxReactivitySeries
{
    private static readonly Lazy<RedoxReactivitySeriesData> Data =
        new(() => RuleStore.Load().Query<RedoxReactivitySeriesData>("redox-reactivity-series"));

    /// <summary>还原性粒子（由强到弱），用于竞争排序。</summary>
    public static IReadOnlyList<RedoxReactiveEntry> ReducingAgents => Data.Value.Reducing;

    /// <summary>归一化化学式（去 caret）：Fe^2+ / Fe2+ → "fe2+"，便于与 ParsedSpecies.Formula 匹配。</summary>
    public static string NormalizeFormula(string formula) =>
        (formula ?? string.Empty).Replace("^", string.Empty).Trim().ToLowerInvariant();
}

/// <summary>redox-reactivity-series.json 数据模型。</summary>
public sealed class RedoxReactivitySeriesData
{
    public string Version { get; set; } = "";
    public string Source { get; set; } = "";

    /// <summary>还原性由强到弱排列（索引越小还原性越强）。</summary>
    public List<RedoxReactiveEntry> Reducing { get; set; } = new();
}

/// <summary>单个还原性粒子条目。</summary>
public sealed class RedoxReactiveEntry
{
    /// <summary>归一化匹配键（去 caret、小写），如 "fe2+"。</summary>
    public string Match { get; set; } = "";

    /// <summary>写入 correct/options 的键（caret 形式，与站内其余物种键一致），如 "Fe^2+"。</summary>
    public string Key { get; set; } = "";

    /// <summary>展示标签（上下标），如 "Fe²⁺"。</summary>
    public string Label { get; set; } = "";
}
