using System.Text;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Diagnostics;

namespace CogniThink.Chem.Core.Chemistry;

/// <summary>方程式中的一项：化学计量数 + 物种。</summary>
public sealed class EquationTerm
{
    public EquationTerm(int coefficient, Species species)
    {
        Coefficient = coefficient;
        Species = species;
    }

    public int Coefficient { get; }

    public Species Species { get; }

    public override string ToString() => Coefficient == 1 ? Species.ToString() : $"{Coefficient}{Species}";
}

/// <summary>化学反应方程式。支持 = / -> / → 分隔，项之间用 " + " 或（无空格时）'+' 分隔。</summary>
public sealed class ChemicalEquation
{
    private ChemicalEquation(string raw, IReadOnlyList<EquationTerm> left, IReadOnlyList<EquationTerm> right)
    {
        Raw = raw;
        Left = left;
        Right = right;
    }

    public string Raw { get; }

    public IReadOnlyList<EquationTerm> Left { get; }

    public IReadOnlyList<EquationTerm> Right { get; }

    public static ChemicalEquation Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new FormulaException("方程式为空");
        }

        var normalized = text.Replace("→", "=").Replace("->", "=");
        var sides = normalized.Split('=');
        if (sides.Length != 2)
        {
            throw new FormulaException($"方程式必须包含一个等号：{text}");
        }

        return new ChemicalEquation(
            text.Trim(),
            ParseTerms(sides[0], text),
            ParseTerms(sides[1], text));
    }

    /// <summary>元素与电荷守恒校验。</summary>
    public ValidationReport ValidateBalance()
    {
        var report = new ValidationReport("equation");
        var left = Aggregate(Left);
        var right = Aggregate(Right);

        foreach (var symbol in left.Keys.Union(right.Keys, StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal))
        {
            left.TryGetValue(symbol, out var l);
            right.TryGetValue(symbol, out var r);
            if (l != r)
            {
                report.AddError($"{symbol} 不守恒：左 {l}，右 {r}");
            }
        }

        var chargeLeft = Left.Sum(t => t.Coefficient * t.Species.Charge);
        var chargeRight = Right.Sum(t => t.Coefficient * t.Species.Charge);
        if (chargeLeft != chargeRight)
        {
            report.AddError($"电荷不守恒：左 {chargeLeft}，右 {chargeRight}");
        }

        return report;
    }

    public bool IsBalanced => ValidateBalance().Ok;

    public override string ToString() =>
        $"{string.Join(" + ", Left)} = {string.Join(" + ", Right)}";

    private static Dictionary<string, int> Aggregate(IReadOnlyList<EquationTerm> terms)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var term in terms)
        {
            foreach (var (symbol, count) in term.Species.Elements)
            {
                map.TryGetValue(symbol, out var existing);
                map[symbol] = existing + term.Coefficient * count;
            }
        }

        return map;
    }

    private static List<EquationTerm> ParseTerms(string side, string original)
    {
        var raw = SplitTerms(side, original);
        var terms = new List<EquationTerm>();
        foreach (var token in raw)
        {
            var text = token.Trim();
            if (text.Length == 0)
            {
                continue;
            }

            var coefficient = 1;
            var match = Regex.Match(text, @"^(?<coef>\d+)(?<rest>.+)$");
            if (match.Success)
            {
                coefficient = int.Parse(match.Groups["coef"].Value);
                text = match.Groups["rest"].Value;
            }

            terms.Add(new EquationTerm(coefficient, Species.Parse(text)));
        }

        if (terms.Count == 0)
        {
            throw new FormulaException($"方程式一侧为空：{original}");
        }

        return terms;
    }

    private static List<string> SplitTerms(string side, string original)
    {
        var trimmed = side.Trim();
        if (Regex.IsMatch(trimmed, @"\s\+\s"))
        {
            return Regex.Split(trimmed, @"\s+\+\s+").ToList();
        }

        var terms = new List<string>();
        var buffer = new StringBuilder();
        for (var i = 0; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            if (c == '+')
            {
                var next = i + 1 < trimmed.Length ? trimmed[i + 1] : '\0';
                var previous = buffer.Length > 0 ? buffer[buffer.Length - 1] : '\0';
                var isSeparator = previous != '^' &&
                                  (char.IsUpper(next) || next == '[' || next == '(' || char.IsDigit(next));
                if (isSeparator)
                {
                    terms.Add(buffer.ToString());
                    buffer.Clear();
                    continue;
                }
            }

            buffer.Append(c);
        }

        if (buffer.Length == 0)
        {
            throw new FormulaException($"方程式项解析失败：{original}");
        }

        terms.Add(buffer.ToString());
        return terms;
    }
}
