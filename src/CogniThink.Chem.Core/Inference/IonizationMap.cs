using CogniThink.Chem.Core.Chemistry;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.Chem.Core.Inference;

/// <summary>
/// 电离映射：把课内离子化合物化学式拆解为构成离子（含个数）。
/// 数据源：ions.json 的阴阳离子清单。判定依据仅有两条硬规则：
/// 电荷平衡（n×阳离子电荷 + m×阴离子电荷 = 0）与元素组成守恒。
/// 铁律：拆解方案不唯一或表外物质一律返回 null，引擎绝不猜测电离方式。
/// </summary>
public static class IonizationMap
{
    private static readonly Lazy<IonInventoryData> IonData =
        new(() => RuleStore.Load().Query<IonInventoryData>("ions"));

    /// <summary>
    /// 将中性化学式拆为离子；若本身就是离子则原样返回（个数 1）。
    /// </summary>
    /// <param name="formula">L2 写法化学式，如 "Na2CO3"、"BaCl2"、"CO3^2-"。</param>
    /// <returns>唯一的 (离子, 个数) 列表；无法唯一拆解返回 null。</returns>
    public static IReadOnlyList<(string Ion, int Count)>? GetIons(string formula)
    {
        if (formula.Contains('^'))
        {
            return new List<(string, int)> { (formula, 1) };
        }

        Species species;
        try
        {
            species = Species.Parse(formula);
        }
        catch (FormatException)
        {
            return null;
        }

        if (species.Charge != 0)
        {
            return null;
        }

        var solutions = new List<List<(string, int)>>();
        foreach (var cation in IonData.Value.Cations)
        {
            foreach (var anion in IonData.Value.Anions)
            {
                var c = SafeParse(cation);
                var a = SafeParse(anion);
                if (c is null || a is null || c.Charge <= 0 || a.Charge >= 0)
                {
                    continue;
                }

                // 电荷平衡的最小整数解：n 个阳离子 + m 个阴离子
                var g = (int)CogniThink.Chem.Core.Math.Rational.Gcd(c.Charge, -a.Charge);
                var n = -a.Charge / g;
                var m = c.Charge / g;

                var combined = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var (element, count) in c.Elements)
                {
                    combined[element] = combined.GetValueOrDefault(element) + count * n;
                }
                foreach (var (element, count) in a.Elements)
                {
                    combined[element] = combined.GetValueOrDefault(element) + count * m;
                }

                if (ElementsEqual(combined, species.Elements))
                {
                    solutions.Add(new List<(string, int)> { (cation, n), (anion, m) });
                }
            }
        }

        return solutions.Count == 1 ? solutions[0] : null;
    }

    /// <summary>
    /// 可溶盐判定：化学式在溶解性表中标记为 soluble。
    /// </summary>
    public static bool IsSolubleSalt(string formula) =>
        !formula.Contains('^') &&
        CogniThink.Chem.Core.Ionic.ElectrolyteRules.LookupSolubility(formula) ==
        CogniThink.Chem.Core.Ionic.Solubility.Soluble;

    private static Species? SafeParse(string text)
    {
        try
        {
            return Species.Parse(text);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static bool ElementsEqual(Dictionary<string, int> left, IReadOnlyDictionary<string, int> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var (element, count) in left)
        {
            if (!right.TryGetValue(element, out var other) || other != count)
            {
                return false;
            }
        }

        return true;
    }
}
