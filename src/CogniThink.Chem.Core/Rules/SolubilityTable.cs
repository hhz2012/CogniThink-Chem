using CogniThink.Chem.Core.Ionic;

namespace CogniThink.Chem.Core.Rules;

/// <summary>
/// 溶解性表查询服务（数据源：data/highschool/solubility.json）。
/// 表外物质返回 null，引擎不判断——白名单铁律。
/// </summary>
public static class SolubilityTable
{
    private static readonly Lazy<IReadOnlyDictionary<string, Solubility>> Table =
        new(() => RuleStore.Load().Query<SolubilityData>("solubility").Table);

    /// <summary>
    /// 查询溶解性。
    /// </summary>
    /// <param name="formula">去电荷化学式主体（如 "BaSO4"、"Ca(OH)2"）。</param>
    /// <returns>溶解性；表外物质返回 null。</returns>
    public static Solubility? Lookup(string formula) =>
        Table.Value.TryGetValue(formula, out var solubility) ? solubility : null;

    /// <summary>是否收录于溶解性表。</summary>
    public static bool IsKnown(string formula) => Table.Value.ContainsKey(formula);
}

/// <summary>
/// 金属活动性顺序服务（数据源：data/highschool/activity-series.json）。
/// 来源：人教版必修第一册金属活动性顺序。
/// 高中适用边界：仅用于金属单质置换、与酸/水反应难易的定性判断；
/// 引擎不用它预测具体产物（产物由 LLM 层推断后再经引擎校验）。
/// </summary>
public static class ActivitySeries
{
    private static readonly Lazy<ActivitySeriesData> Data =
        new(() => RuleStore.Load().Query<ActivitySeriesData>("activity-series"));

    /// <summary>活动性顺序（含参照位 H），索引越小越活泼。</summary>
    public static IReadOnlyList<string> Series => Data.Value.Series;

    /// <summary>元素在顺序中的位置；不存在返回 -1。</summary>
    public static int IndexOf(string element) => Data.Value.Series.IndexOf(element);

    /// <summary>是否为课内金属单质（H 是参照位，不是金属）。</summary>
    public static bool IsKnownMetal(string element) => element != "H" && IndexOf(element) >= 0;

    /// <summary>
    /// 判断 a 是否比 b 更活泼（索引更靠前）。
    /// 两者都必须在顺序表内，否则返回 false（引擎不猜测表外元素）。
    /// </summary>
    public static bool MoreActive(string a, string b)
    {
        var ia = IndexOf(a);
        var ib = IndexOf(b);
        return ia >= 0 && ib >= 0 && ia < ib;
    }

    /// <summary>是否排在氢之前（可从稀酸中置换出氢）。表外元素返回 false。</summary>
    public static bool BeforeHydrogen(string element)
    {
        var index = IndexOf(element);
        return index >= 0 && index < IndexOf("H");
    }
}
