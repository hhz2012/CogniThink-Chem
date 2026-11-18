namespace CogniThink.TutorBrain.Application.Chemistry;

using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using CogniThink.TutorBrain.Domain.Chemistry;

/// <summary>一条反应的计量系数（物质 Id → 系数）</summary>
public sealed class ReactionStoichiometry
{
    public Dictionary<string, int> Reactants { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> Products { get; } = new(StringComparer.Ordinal);

    /// <summary>无法从方程式解析时，退化为所有物质系数 1</summary>
    public bool IsFallback { get; init; }

    public string? Warning { get; init; }

    public int CoefficientOfReactant(string substanceId) =>
        Reactants.TryGetValue(substanceId, out var c) ? c : 1;
}

/// <summary>
/// 方程式计量解析器：从 reactions.json 的 equation 文本读取系数，供状态推演做限量试剂计算。
/// 只依赖方程式文本与知识库物质索引，不做元素配平校验（数据入库时已校验）。
/// 解析失败时退化为系数 1，并在 <see cref="Warnings"/> 中留痕，绝不影响推演主流程。
/// </summary>
public sealed class ReactionEquationParser
{
    private static readonly Regex CjkParenthetical =
        new(@"[（(][^)）]*[\u4e00-\u9fff][^)）]*[)）]", RegexOptions.Compiled);

    private static readonly Regex LeadingCoefficient =
        new(@"^(?<coef>\d+)(?<rest>.+)$", RegexOptions.Compiled);

    /// <summary>聚合物方程式中的字母系数，如 nH2O、nC6H12O6</summary>
    private static readonly Regex LeadingLetterCoefficient =
        new(@"^(?<coef>[a-z])(?=[A-Z(\[].+$)", RegexOptions.Compiled);

    private readonly ChemicalKnowledgeBase _kb;
    private readonly ConcurrentDictionary<string, ReactionStoichiometry> _cache = new(StringComparer.Ordinal);

    public ReactionEquationParser(ChemicalKnowledgeBase kb) => _kb = kb;

    public IReadOnlyList<string> Warnings => _cache.Values
        .Where(s => s.Warning is not null)
        .Select(s => s.Warning!)
        .ToList();

    public ReactionStoichiometry Get(Reaction reaction) =>
        _cache.GetOrAdd(reaction.Id, _ => Parse(reaction));

    private ReactionStoichiometry Parse(Reaction reaction)
    {
        try
        {
            var (left, right) = SplitSides(reaction.Equation);
            var reactants = ParseSide(left, reaction.Reactants);
            var products = ParseSide(right, reaction.Products);

            if (reactants is null || products is null)
                return Fallback(reaction, "方程式项无法映射到知识库物质");

            if (!SameSet(reactants.Keys, reaction.Reactants) ||
                !SameSet(products.Keys, reaction.Products))
                return Fallback(reaction, "方程式物质集合与反应记录不一致");

            var result = new ReactionStoichiometry();
            foreach (var pair in reactants) result.Reactants[pair.Key] = pair.Value;
            foreach (var pair in products) result.Products[pair.Key] = pair.Value;
            return result;
        }
        catch (Exception ex)
        {
            return Fallback(reaction, ex.Message);
        }
    }

    private static ReactionStoichiometry Fallback(Reaction reaction, string reason)
    {
        var result = new ReactionStoichiometry
        {
            IsFallback = true,
            Warning = $"反应 {reaction.Id} 计量解析失败（{reason}），已按系数 1 处理：{reaction.Equation}"
        };
        foreach (var id in reaction.Reactants) result.Reactants[id] = 1;
        foreach (var id in reaction.Products) result.Products[id] = 1;
        return result;
    }

    /// <summary>按 ⇌ / = / → 拆分方程式两侧</summary>
    private static (string Left, string Right) SplitSides(string equation)
    {
        var text = equation.Replace('＋', '+').Trim();
        foreach (var separator in new[] { "⇌", "=", "→", "->" })
        {
            var index = text.IndexOf(separator, StringComparison.Ordinal);
            if (index < 0) continue;
            return (text[..index], text[(index + separator.Length)..]);
        }

        throw new FormatException("方程式缺少分隔符");
    }

    private Dictionary<string, int>? ParseSide(string side, IReadOnlyList<string> expectedIds)
    {
        var cleaned = CjkParenthetical.Replace(side, string.Empty)
            .Replace("↑", string.Empty)
            .Replace("↓", string.Empty)
            .Trim();

        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rawTerm in SplitTerms(cleaned))
        {
            var term = rawTerm.Trim();
            if (term.Length == 0) continue;

            var coefficient = 1;
            var match = LeadingCoefficient.Match(term);
            if (match.Success)
            {
                coefficient = int.Parse(match.Groups["coef"].Value);
                term = match.Groups["rest"].Value;
            }
            else
            {
                var letterMatch = LeadingLetterCoefficient.Match(term);
                if (letterMatch.Success) term = term[letterMatch.Groups["coef"].Length..];
            }

            var substance = ResolveTerm(term, expectedIds, used);
            if (substance is null) return null;

            used.Add(substance.Id);
            result[substance.Id] = result.TryGetValue(substance.Id, out var existing)
                ? existing + coefficient
                : coefficient;
        }

        return result.Count == 0 ? null : result;
    }

    /// <summary>
    /// 将方程式项解析为物质。葡萄糖与果糖化学式相同，需要借用反应记录中的物质顺序消歧。
    /// </summary>
    private Substance? ResolveTerm(
        string term,
        IReadOnlyList<string> expectedIds,
        IReadOnlySet<string> used)
    {
        var direct = _kb.FindSubstance(term);
        if (direct is not null && expectedIds.Contains(direct.Id) && !used.Contains(direct.Id))
            return direct;

        // 化学式相同但记录中使用不同 Id（如葡萄糖/果糖）时，按未使用的同化学式物质顺延。
        if (direct is not null)
        {
            var alternative = expectedIds
                .Where(id => !used.Contains(id))
                .Select(_kb.FindSubstanceById)
                .FirstOrDefault(s => s is not null &&
                    string.Equals(s.Formula, direct.Formula, StringComparison.OrdinalIgnoreCase));
            if (alternative is not null) return alternative;
        }

        var byExpected = expectedIds
            .Where(id => !used.Contains(id))
            .Select(_kb.FindSubstanceById)
            .FirstOrDefault(s => s is not null &&
                (s.Name == term || (s.Aliases?.Contains(term) ?? false)));
        return byExpected;
    }

    /// <summary>按顶层 "+" 拆分（括号内的 + 不拆）</summary>
    private static IEnumerable<string> SplitTerms(string side)
    {
        var depth = 0;
        var buffer = new StringBuilder();
        foreach (var c in side)
        {
            switch (c)
            {
                case '(' or '[' or '（':
                    depth++;
                    buffer.Append(c);
                    break;
                case ')' or ']' or '）':
                    depth = Math.Max(0, depth - 1);
                    buffer.Append(c);
                    break;
                case '+' when depth == 0:
                    yield return buffer.ToString();
                    buffer.Clear();
                    break;
                default:
                    buffer.Append(c);
                    break;
            }
        }

        yield return buffer.ToString();
    }

    private static bool SameSet(IEnumerable<string> actual, IEnumerable<string> expected) =>
        actual.OrderBy(x => x, StringComparer.Ordinal)
            .SequenceEqual(expected.Distinct().OrderBy(x => x, StringComparer.Ordinal),
                StringComparer.Ordinal);
}
