using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Chemistry;
using CogniThink.Chem.Core.Ionic;
using CogniThink.Chem.Core.Reactions;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.Chem.Core.Inference;

/// <summary>一次反应检索命中：条目 + 命中模式（分子/离子）。</summary>
public sealed class ReactionMatch
{
    public ReactionEntry Entry { get; init; } = null!;

    /// <summary>true = 通过电离池子集匹配命中（离子方程式条目）。</summary>
    public bool IsIonic { get; init; }
}

/// <summary>
/// 反应检索器：产物/过程/成分推断共用的"反应物组合 → 知识条目"匹配设施。
/// 两种模式：
/// <list type="bullet">
/// <item>分子模式：反应物集合与条目反应物集精确相等（或 allowSubset 时条目反应物 ⊆ 查询集）。</item>
/// <item>离子模式：把强酸/强碱/可溶盐电离成离子池（水溶液语境恒含 H2O），
/// 条目反应物（须含至少一个离子）全部落在池内即命中。</item>
/// </list>
/// 铁律：只返回知识库条目，绝不生成库外反应。
/// </summary>
public static class ReactionMatcher
{
    /// <summary>
    /// 试剂注解归一化："CO2(过量)" → 试剂 CO2 + 条件令牌 "CO2过量"；
    /// "H2SO4(稀)" → 试剂 H2SO4 + 令牌 "稀"；"KMnO4(H^+)" → 试剂 KMnO4 与 H^+ + 令牌 "酸性"；
    /// "(足量)/(适量)" 仅剥离。无注解试剂原样保留。
    /// </summary>
    public static (List<string> Reagents, List<string> Tokens) NormalizeReagents(IEnumerable<string> rawReagents)
    {
        var reagents = new List<string>();
        var tokens = new List<string>();
        foreach (var raw in rawReagents)
        {
            var text = raw.Trim();
            var open = text.LastIndexOf('(');
            if (open < 0 || !text.EndsWith(')'))
            {
                if (text.Length > 0)
                {
                    reagents.Add(text);
                }

                continue;
            }

            var basePart = text[..open].Trim();
            var note = text[(open + 1)..^1].Trim();
            if (basePart.Length > 0)
            {
                reagents.Add(basePart);
            }

            if (note.Contains("过量"))
            {
                tokens.Add(basePart + "过量");
            }
            else if (note is "稀" or "浓")
            {
                tokens.Add(note);
            }
            else if (note.Contains("H^+"))
            {
                reagents.Add("H^+");
                tokens.Add("酸性");
            }
            else if (note.Contains("OH^-"))
            {
                reagents.Add("OH^-");
                tokens.Add("碱性");
            }
            // "足量"、"适量"、"稀溶液" 等注解：仅剥离，不产生条件令牌
        }

        return (reagents, tokens);
    }

    /// <summary>
    /// 构造离子池：强酸/强碱/可溶盐拆为离子，其余物种（分子、难溶物、弱电解质、
    /// 不可解析的试剂名）原样保留；水溶液语境恒加入 H2O。
    /// </summary>
    public static HashSet<string> BuildIonPool(IEnumerable<string> species)
    {
        var pool = new HashSet<string>(StringComparer.Ordinal) { "H2O" };
        foreach (var s in species.Distinct(StringComparer.Ordinal))
        {
            if (s.Contains('^'))
            {
                pool.Add(s);
                continue;
            }

            var ionizable = ElectrolyteRules.StrongAcids.Contains(s)
                || ElectrolyteRules.StrongBases.Contains(s)
                || IonizationMap.IsSolubleSalt(s);
            var ions = ionizable ? IonizationMap.GetIons(s) : null;
            if (ions is null)
            {
                pool.Add(s);
            }
            else
            {
                foreach (var (ion, _) in ions)
                {
                    pool.Add(ion);
                }
            }
        }

        return pool;
    }

    /// <summary>
    /// 在知识库中检索与反应物组合匹配、条件满足、（可选）产物含指定物种的条目。
    /// </summary>
    /// <param name="reactants">反应物（分子式/离子式混合）。</param>
    /// <param name="conditionTokens">查询条件令牌（已按 ',' 拆分归一）。</param>
    /// <param name="library">反应知识库。</param>
    /// <param name="requiredProduct">要求条目产物包含该物种（按化学式主体比较）；null 不限。</param>
    /// <param name="allowSubset">分子模式允许条目反应物 ⊆ 查询集（用于含辅助试剂的成分推断）。</param>
    public static List<ReactionMatch> Match(
        IReadOnlyCollection<string> reactants,
        IEnumerable<string> conditionTokens,
        ReactionLibrary library,
        string? requiredProduct = null,
        bool allowSubset = false)
    {
        var tokens = ReactionLibrary.ConditionTokens(conditionTokens);
        var reactantSet = reactants.ToHashSet(StringComparer.Ordinal);
        var matches = new List<ReactionMatch>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // 分子模式：反应物集精确相等（或子集）；含离子的条目留给离子模式
        IEnumerable<ReactionEntry> molecular = allowSubset
            ? library.All.Where(e => e.Reactants.Count > 0 && e.Reactants.All(reactantSet.Contains))
            : library.FindByReactants(reactants);
        foreach (var entry in molecular)
        {
            // 电离/水解/沉淀溶解平衡（⇌）条目不是"发生到底"的反应，不作为推断证据
            if (entry.ReactionType == ReactionType.IonEquilibrium)
            {
                continue;
            }

            if (entry.Reactants.Any(r => r.Contains('^')))
            {
                continue;
            }

            if (!ReactionLibrary.ConditionsSatisfied(tokens, entry.Conditions))
            {
                continue;
            }

            if (requiredProduct is not null && !ContainsProduct(entry, requiredProduct))
            {
                continue;
            }

            if (seen.Add(entry.Id))
            {
                matches.Add(new ReactionMatch { Entry = entry, IsIonic = false });
            }
        }

        // 离子模式：条目反应物（含离子）全部落在电离池内
        var pool = BuildIonPool(reactants);
        if (pool.Any(p => p.Contains('^')))
        {
            foreach (var entry in library.All)
            {
                // 水解/电离平衡条目反应物（如 NH4^+ + H2O）在水溶液池恒含 H2O 时会普遍误命中，
                // 整体跳过；彻底双水解（如 Al^3+ + HCO3^-）是 DoubleDisplacement 类型，不受影响。
                if (entry.ReactionType == ReactionType.IonEquilibrium)
                {
                    continue;
                }

                if (!entry.Reactants.Any(r => r.Contains('^')))
                {
                    continue;
                }

                if (!entry.Reactants.All(pool.Contains))
                {
                    continue;
                }

                if (!ReactionLibrary.ConditionsSatisfied(tokens, entry.Conditions))
                {
                    continue;
                }

                if (requiredProduct is not null && !ContainsProduct(entry, requiredProduct))
                {
                    continue;
                }

                if (seen.Add(entry.Id))
                {
                    matches.Add(new ReactionMatch { Entry = entry, IsIonic = true });
                }
            }
        }

        // 条件特异性优先：查询带条件时，优先采纳自身带条件标注的条目
        if (tokens.Count > 0)
        {
            var specific = matches.Where(m => m.Entry.Conditions.Count > 0).ToList();
            if (specific.Count > 0)
            {
                matches = specific;
            }
            else
            {
                // 变体对排除：同一反应物组合存在条件被查询令牌满足的条件条目时
                // （查询明确指定了该条件变体，如 "CO2过量" 指向 NA-015），
                // 无条件条目是互补变体（如 NA-014 对应 CO2 不足），不得保留——
                // 否则带条件查询会取到互补变体的产物，产生错误的第二解。
                matches = matches
                    .Where(m => !HasSatisfiedConditionalVariant(m, reactantSet, tokens, library, allowSubset))
                    .ToList();
            }
        }

        return matches;
    }

    /// <summary>
    /// 变体对检查：无条件命中条目是否存在同反应物组合的另一条件条目，
    /// 且其条件被查询令牌满足（是则无条件条目为互补变体，应排除）。
    /// 只在"查询带条件而命中条目均无条件"时调用。
    /// </summary>
    private static bool HasSatisfiedConditionalVariant(
        ReactionMatch match,
        HashSet<string> reactantSet,
        IReadOnlySet<string> tokens,
        ReactionLibrary library,
        bool allowSubset)
    {
        if (match.Entry.Conditions.Count > 0)
        {
            return false;
        }

        IEnumerable<ReactionEntry> siblings = allowSubset
            ? library.All.Where(e => e.Reactants.Count > 0 && e.Reactants.All(reactantSet.Contains))
            : library.FindByReactants(reactantSet);
        foreach (var sibling in siblings)
        {
            if (sibling.Id == match.Entry.Id
                || sibling.Conditions.Count == 0
                || sibling.ReactionType == ReactionType.IonEquilibrium
                || sibling.Reactants.Any(r => r.Contains('^')) != match.IsIonic)
            {
                continue;
            }

            if (ReactionLibrary.ConditionsSatisfied(tokens, sibling.Conditions))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>条目产物是否包含指定物种（按化学式主体比较，忽略电荷与状态符号）。</summary>
    public static bool ContainsProduct(ReactionEntry entry, string product)
    {
        var target = NormalizeBody(product);
        return entry.Products.Any(p => NormalizeBody(p) == target);
    }

    // -----------------------------------------------------------------------
    // 现象匹配工具（成分推断与过程推断共用）
    // -----------------------------------------------------------------------

    /// <summary>现象库检索：返回现象文本指示的物种集合。</summary>
    internal static HashSet<string> PhenomenaIndicated(string text, List<PhenomenonEntry> phenomena)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in phenomena)
        {
            if (PhenomenonHit(item.Keyword, text))
            {
                foreach (var species in item.Species)
                {
                    set.Add(species);
                }
            }
        }

        return set;
    }

    /// <summary>
    /// 现象关键词命中：去括号后双向子串。
    /// 刻意不做字符窗口/切片宽松匹配：现象关键词是判别性证据，
    /// 切片会把弱相关词（如"产生气体""溶解""绿色"）误配到无关现象条目，
    /// 污染指示物种集合；结论必须落在明确的现象条目上。
    /// </summary>
    internal static bool PhenomenonHit(string keyword, string text)
    {
        var k = StripParens(keyword).Trim();
        var t = StripParens(text).Trim();
        if (k.Length == 0 || t.Length == 0)
        {
            return false;
        }

        return t.IndexOf(k, StringComparison.Ordinal) >= 0 || k.IndexOf(t, StringComparison.Ordinal) >= 0;
    }

    /// <summary>候选的溶液物种集合：自身 + （强酸/强碱/可溶盐时）电离出的离子。</summary>
    internal static HashSet<string> SpeciesSetOf(string formula)
    {
        var set = new HashSet<string>(StringComparer.Ordinal) { formula };
        var ionizable = ElectrolyteRules.StrongAcids.Contains(formula)
            || ElectrolyteRules.StrongBases.Contains(formula)
            || IonizationMap.IsSolubleSalt(formula);
        if (ionizable)
        {
            var ions = IonizationMap.GetIons(formula);
            if (ions is not null)
            {
                foreach (var (ion, _) in ions)
                {
                    set.Add(ion);
                }
            }
        }

        return set;
    }

    /// <summary>
    /// 单质候选与指示物种的元素关联（浅绿色→Fe^2+ → Fe）。
    /// 指示侧只接受单元素物种（单元素离子或单质）：CO3^2- 等多元素离子
    /// 不得以"共享元素 C"把单质 C 关联进来（否则 C 能解释碳酸盐的产气现象）。
    /// </summary>
    internal static bool ElementAssociated(string candidate, HashSet<string> indicated)
    {
        try
        {
            var species = Species.Parse(candidate);
            if (species.Charge != 0 || species.Elements.Count != 1)
            {
                return false;
            }

            var element = species.Elements.Keys.First();
            return indicated.Any(i => SingleElementOf(i) == element);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>物种能否解释现象指示：自身/电离离子被指示，或单质元素关联。</summary>
    internal static bool ExplainsIndication(string formula, HashSet<string> indicated) =>
        indicated.Count > 0
        && (SpeciesSetOf(formula).Overlaps(indicated) || ElementAssociated(formula, indicated));

    /// <summary>物种若为单元素（单元素离子或单质）返回其唯一元素，否则返回 null。</summary>
    private static string? SingleElementOf(string formula)
    {
        try
        {
            var species = Species.Parse(formula);
            return species.Elements.Count == 1 ? species.Elements.Keys.First() : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    internal static string StripParens(string text) =>
        Regex.Replace(text, "（[^）]*）|\\([^)]*\\)", "");

    /// <summary>化学式主体归一化；解析失败退回原文。</summary>
    private static string NormalizeBody(string formula)
    {
        try
        {
            return Species.Parse(formula).Body;
        }
        catch (FormatException)
        {
            return formula.Trim();
        }
    }
}
