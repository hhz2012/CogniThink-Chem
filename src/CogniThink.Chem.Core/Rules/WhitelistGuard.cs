using CogniThink.Chem.Core.Chemistry;
using CogniThink.Chem.Core.Common;
using CogniThink.Chem.Core.Ionic;

namespace CogniThink.Chem.Core.Rules;

/// <summary>
/// 高中物质白名单守卫：判断一个物种是否在课内白名单内。
/// 白名单 = 溶解性表 ∪ 强酸 ∪ 强碱 ∪ 弱电解质 ∪ 常见氧化物 ∪ 气体 ∪
/// 非金属单质 ∪ 活动性顺序金属单质 ∪ 离子清单（阴/阳离子）。
/// 铁律：白名单外物种标记 <see cref="RiskTags.OutOfScope"/>，
/// 引擎只做形式校验（守恒等），绝不评价其化学正确性、绝不预测其行为。
/// </summary>
public static class WhitelistGuard
{
    private static readonly Lazy<HashSet<string>> KnownIons = new(BuildKnownIons);
    private static readonly Lazy<HashSet<string>> KnownNeutral = new(BuildKnownNeutral);

    /// <summary>
    /// 检查单个物种。
    /// </summary>
    /// <param name="species">物种。离子按 ToString()（caret 形式）匹配，分子按 Body 匹配。</param>
    /// <returns>(是否课内已知, 风险标签)。未知时标签为 <see cref="RiskTags.OutOfScope"/>。</returns>
    public static (bool Known, string RiskTag) Check(Species species)
    {
        var known = species.Charge != 0
            ? KnownIons.Value.Contains(species.ToString())
            : KnownNeutral.Value.Contains(species.Body);
        return (known, known ? RiskTags.None : RiskTags.OutOfScope);
    }

    /// <summary>
    /// 批量检查：返回首个未知物种；全部已知时 Known=true。
    /// </summary>
    public static (bool Known, string RiskTag, Species? Unknown) CheckRange(IEnumerable<Species> species)
    {
        foreach (var item in species)
        {
            var (known, tag) = Check(item);
            if (!known)
            {
                return (false, tag, item);
            }
        }

        return (true, RiskTags.None, null);
    }

    private static HashSet<string> BuildKnownIons()
    {
        var ions = RuleStore.Load().Query<IonInventoryData>("ions");
        var set = new HashSet<string>(StringComparer.Ordinal);
        set.UnionWith(ions.Cations);
        set.UnionWith(ions.Anions);
        return set;
    }

    private static HashSet<string> BuildKnownNeutral()
    {
        var store = RuleStore.Load();
        var electrolytes = store.Query<ElectrolyteData>("electrolytes");
        var ions = store.Query<IonInventoryData>("ions");
        var series = store.Query<ActivitySeriesData>("activity-series");
        var solubility = store.Query<SolubilityData>("solubility");

        var set = new HashSet<string>(StringComparer.Ordinal);
        set.UnionWith(solubility.Table.Keys);
        set.UnionWith(electrolytes.StrongAcids);
        set.UnionWith(electrolytes.StrongBases);
        set.UnionWith(electrolytes.WeakElectrolytes);
        set.UnionWith(electrolytes.CommonOxides);
        set.UnionWith(ions.Gases);
        set.UnionWith(ions.ElementalNonmetals);

        // 课内补充物质（有机物 + 其它无机物），见 substances.json
        var substances = store.Query<SubstanceInventoryData>("substances");
        set.UnionWith(substances.Organics);
        set.UnionWith(substances.OtherInorganics);

        // 活动性顺序中的金属单质（排除参照位 H）
        foreach (var element in series.Series.Where(e => e != "H"))
        {
            set.Add(element);
        }

        return set;
    }
}
