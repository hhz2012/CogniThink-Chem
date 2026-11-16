using CogniThink.Chem.Core.Chemistry;
using CogniThink.Chem.Core.Common;
using CogniThink.Chem.Core.Math;
using CogniThink.Chem.Core.Parsing;

namespace CogniThink.Chem.Core.Balancing;

/// <summary>配平结果。系数按"反应物在前、生成物在后"的物种顺序排列。</summary>
public sealed class BalanceResult
{
    public bool Success { get; init; }

    /// <summary>配平后的方程式字符串（含条件与状态符号）；失败为 null。</summary>
    public string? BalancedEquation { get; init; }

    /// <summary>最小整数系数（以 Rational 表示，保证精确性）；失败为空数组。</summary>
    public Rational[] Coefficients { get; init; } = Array.Empty<Rational>();

    /// <summary>失败原因（成功为 null）。</summary>
    public string? FailReason { get; init; }

    /// <summary>风险标签：无解/欠定/格式错误。</summary>
    public string RiskTag { get; init; } = RiskTags.None;
}

/// <summary>
/// 化学方程式配平器：原子守恒（+ 电荷守恒）齐次线性方程组的有理数零空间求解。
/// 步骤：解析 → 按元素（和电荷）构造守恒矩阵 → 高斯-若尔当消元求零空间 →
/// 维数为 1 时取基向量并整数化为最小正整数系数。
/// 核心铁律：零空间维数为 0 报"无解"，维数大于 1 报"欠定"，绝不编造系数或产物。
/// 高中适用边界：课内所有方程式（含离子方程式）；不涉及结构信息。
/// 易错点：
/// <list type="bullet">
/// <item>用户已写系数会被忽略（Balance 从头计算）；校验学生系数请用 <see cref="ConservationViolations"/>。</item>
/// <item>Fe3O4 等混合价态物质不影响配平（只看元素计数）。</item>
/// </list>
/// </summary>
public static class EquationBalancer
{
    /// <summary>
    /// 配平方程式字符串。
    /// </summary>
    /// <param name="equation">方程式，如 "Fe + O2 = Fe3O4"、"MnO4^- + Fe^2+ + H+ = Mn^2+ + Fe^3+ + H2O"。</param>
    /// <returns>配平结果；无法配平时 Success=false 并给出原因。</returns>
    public static BalanceResult Balance(string equation)
    {
        if (!EquationParser.TryParse(equation, out var parsed, out var error))
        {
            return new BalanceResult { Success = false, FailReason = error, RiskTag = RiskTags.ParseError };
        }

        return Balance(parsed!);
    }

    /// <summary>配平已解析的方程式（忽略原有系数）。</summary>
    public static BalanceResult Balance(ParsedEquation equation)
    {
        var species = equation.AllTerms.Select(t => t.Species).ToList();
        var leftCount = equation.Left.Count;

        // 元素只出现在一侧时提前给出可读错误
        var leftElements = UnionElements(equation.Left.Select(t => t.Species));
        var rightElements = UnionElements(equation.Right.Select(t => t.Species));
        foreach (var e in leftElements.Where(e => !rightElements.Contains(e)))
        {
            return Fail($"元素 {e} 只出现在反应物一侧，无法配平");
        }

        foreach (var e in rightElements.Where(e => !leftElements.Contains(e)))
        {
            return Fail($"元素 {e} 只出现在生成物一侧，无法配平");
        }

        // 构造守恒矩阵：每个元素一行；存在离子时追加电荷行
        var elements = species.SelectMany(s => s.Elements.Keys).Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList();
        var rows = new List<Rational[]>();
        foreach (var element in elements)
        {
            rows.Add(BuildRow(species, leftCount, s => s.Elements.TryGetValue(element, out var c) ? c : 0));
        }

        if (species.Any(s => s.Charge != 0))
        {
            rows.Add(BuildRow(species, leftCount, s => s.Charge));
        }

        var matrix = RationalMatrix.FromRows(rows);
        var basis = matrix.NullSpaceBasis();

        if (basis.Count == 0)
        {
            return Fail("守恒方程组无解：所给物种无法构成配平的方程式");
        }

        if (basis.Count > 1)
        {
            return new BalanceResult
            {
                Success = false,
                FailReason = $"方程欠定（零空间维数 {basis.Count}）：物种组合不完整或存在互不相关的反应",
                RiskTag = RiskTags.Underdetermined
            };
        }

        var vector = basis[0];
        if (vector[0].IsNegative)
        {
            vector = vector.Select(v => -v).ToArray();
        }

        for (var i = 0; i < vector.Length; i++)
        {
            if (vector[i].IsZero)
            {
                return Fail($"物种 {species[i]} 与其他物种没有共同的守恒关系（系数只能为 0）");
            }

            if (vector[i].IsNegative)
            {
                return Fail($"物种 {species[i]} 所在侧与守恒方向矛盾，无法全部取正系数");
            }
        }

        var integers = ToSmallestIntegers(vector);
        var balanced = FormatEquation(equation, integers);
        return new BalanceResult
        {
            Success = true,
            BalancedEquation = balanced,
            Coefficients = integers
        };
    }

    /// <summary>
    /// 按学生书写的系数检查原子/电荷守恒，返回违规描述列表（空 = 守恒）。
    /// 用于"判断学生方程式是否配平"场景，与 Balance 的重新配平互不替代。
    /// </summary>
    public static IReadOnlyList<string> ConservationViolations(ParsedEquation equation)
    {
        var violations = new List<string>();
        var elements = equation.AllSpecies.SelectMany(s => s.Elements.Keys)
            .Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal);

        foreach (var element in elements)
        {
            var left = Sum(equation.Left, t => t.Species.Elements.TryGetValue(element, out var c) ? t.Coefficient * c : 0);
            var right = Sum(equation.Right, t => t.Species.Elements.TryGetValue(element, out var c) ? t.Coefficient * c : 0);
            if (left != right)
            {
                violations.Add($"元素 {element} 不守恒：左 {left}，右 {right}");
            }
        }

        var chargeLeft = Sum(equation.Left, t => t.Coefficient * t.Species.Charge);
        var chargeRight = Sum(equation.Right, t => t.Coefficient * t.Species.Charge);
        if (chargeLeft != chargeRight)
        {
            violations.Add($"电荷不守恒：左 {chargeLeft}，右 {chargeRight}");
        }

        return violations;
    }

    private static BalanceResult Fail(string reason) =>
        new() { Success = false, FailReason = reason, RiskTag = RiskTags.NoSolution };

    private static Rational[] BuildRow(List<Species> species, int leftCount, Func<Species, int> value)
    {
        var row = new Rational[species.Count];
        for (var i = 0; i < species.Count; i++)
        {
            var sign = i < leftCount ? 1 : -1;
            row[i] = new Rational(sign * value(species[i]));
        }

        return row;
    }

    private static HashSet<string> UnionElements(IEnumerable<Species> species) =>
        species.SelectMany(s => s.Elements.Keys).ToHashSet(StringComparer.Ordinal);

    private static int Sum(IEnumerable<ParsedTerm> terms, Func<ParsedTerm, int> selector) =>
        terms.Sum(selector);

    /// <summary>有理数向量 → 最小正整数向量（乘分母最小公倍数，再除最大公约数）。</summary>
    internal static Rational[] ToSmallestIntegers(Rational[] vector)
    {
        var lcm = 1L;
        foreach (var v in vector)
        {
            lcm = Rational.Lcm(lcm, v.Den);
        }

        var scaled = vector.Select(v => new Rational(v.Num * lcm / v.Den)).ToArray();
        var gcd = 0L;
        foreach (var v in scaled)
        {
            gcd = Rational.Gcd(gcd, System.Math.Abs(v.Num));
        }

        return scaled.Select(v => new Rational(v.Num / gcd)).ToArray();
    }

    private static string FormatEquation(ParsedEquation equation, Rational[] coefficients)
    {
        var terms = equation.AllTerms.ToList();
        string FormatTerm(int index)
        {
            var term = terms[index];
            var coef = coefficients[index];
            var prefix = coef == Rational.One ? "" : coef.ToString();
            return $"{prefix}{term.Species}{term.State}";
        }

        var left = string.Join(" + ", Enumerable.Range(0, equation.Left.Count).Select(FormatTerm));
        var right = string.Join(" + ", Enumerable.Range(equation.Left.Count, equation.Right.Count).Select(FormatTerm));
        var separator = equation.Reversible
            ? " ⇌ "
            : equation.Condition is null ? " = " : $" ={equation.Condition}= ";
        return $"{left}{separator}{right}";
    }
}
