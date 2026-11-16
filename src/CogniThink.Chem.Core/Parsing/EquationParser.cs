using System.Text;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Chemistry;

namespace CogniThink.Chem.Core.Parsing;

/// <summary>方程式中的一项：系数 + 物种 + 状态符号（↑/↓）。</summary>
public sealed class ParsedTerm
{
    public ParsedTerm(int coefficient, bool coefficientExplicit, Species species, string? state)
    {
        Coefficient = coefficient;
        CoefficientExplicit = coefficientExplicit;
        Species = species;
        State = state;
    }

    /// <summary>化学计量数（未显式书写时为 1）。</summary>
    public int Coefficient { get; }

    /// <summary>原式中是否显式写了系数（配平校验时用于区分"学生写了 1"与"未写"）。</summary>
    public bool CoefficientExplicit { get; }

    /// <summary>物种（元素计数 + 电荷）。</summary>
    public Species Species { get; }

    /// <summary>状态符号："↑"、"↓" 或 null。</summary>
    public string? State { get; }

    public override string ToString() =>
        $"{(CoefficientExplicit ? Coefficient.ToString() : "")}{Species}{State}";
}

/// <summary>
/// 解析后的方程式。相比 <see cref="ChemicalEquation"/> 增加：
/// 反应条件提取、可逆号识别、状态符号（↑↓）提取、显式系数标记，
/// 供配平器、离子检查器、状态符号检查使用。
/// </summary>
public sealed class ParsedEquation
{
    public ParsedEquation(
        string raw,
        IReadOnlyList<ParsedTerm> left,
        IReadOnlyList<ParsedTerm> right,
        string? condition,
        bool reversible)
    {
        Raw = raw;
        Left = left;
        Right = right;
        Condition = condition;
        Reversible = reversible;
    }

    /// <summary>原始输入。</summary>
    public string Raw { get; }

    /// <summary>反应物项。</summary>
    public IReadOnlyList<ParsedTerm> Left { get; }

    /// <summary>生成物项。</summary>
    public IReadOnlyList<ParsedTerm> Right { get; }

    /// <summary>反应条件（点燃/加热/催化剂/通电/高温等），无条件为 null。</summary>
    public string? Condition { get; }

    /// <summary>是否可逆反应（⇌）。</summary>
    public bool Reversible { get; }

    /// <summary>是否含离子（任一物种电荷非零）。离子方程式判定用。</summary>
    public bool HasIons => AllTerms.Any(t => t.Species.Charge != 0);

    public IEnumerable<ParsedTerm> AllTerms => Left.Concat(Right);

    public IEnumerable<Species> AllSpecies => AllTerms.Select(t => t.Species);

    public override string ToString() =>
        $"{string.Join(" + ", Left)} {(Reversible ? "⇌" : "=")} {string.Join(" + ", Right)}";
}

/// <summary>
/// 方程式解析器。支持的书写形式（高中常见）：
/// <list type="bullet">
/// <item>分隔符：= / → / -&gt; / ⇌ / ——&gt;（含条件注入 "--点燃--&gt;"、"=点燃="）。</item>
/// <item>项分隔：'+'（允许全角 ＋；电荷中的 + 如 Fe^3+ 不误拆）。</item>
/// <item>系数：项首数字（2H2O）。</item>
/// <item>状态符号：项尾 ↑ / ↓。</item>
/// </list>
/// 易错点：条件文本只提取不校验（条件正误属于教材知识，由伴思层结合规则库讨论，引擎不臆断）。
/// </summary>
public static class EquationParser
{
    // "--点燃-->" / "——加热——>" 等
    private static readonly Regex DashArrow =
        new(@"[-—–]{1,}\s*(?<cond>[^-—–>]*)\s*[-—–]{1,}>", RegexOptions.Compiled);

    // "=点燃="
    private static readonly Regex EqualsCondition =
        new(@"=\s*(?<cond>[^=]+?)\s*=", RegexOptions.Compiled);

    private static readonly Regex LeadingCoefficient =
        new(@"^(?<coef>\d+)(?<rest>.+)$", RegexOptions.Compiled);

    /// <summary>
    /// 解析方程式字符串。
    /// </summary>
    /// <param name="text">方程式，如 "2H2 + O2 =点燃= 2H2O"、"Fe + Cu^2+ = Fe^2+ + Cu"。</param>
    /// <returns>解析结果。</returns>
    /// <exception cref="FormulaException">无法解析（空、缺少分隔符、一侧为空等）。</exception>
    public static ParsedEquation Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new FormulaException("方程式为空");
        }

        var normalized = text.Trim().Replace('＋', '+');
        string? condition = null;
        var reversible = false;

        string leftText, rightText;
        if (normalized.Contains('⇌'))
        {
            var parts = normalized.Split('⇌');
            if (parts.Length != 2)
            {
                throw new FormulaException($"只能包含一个可逆号：{text}");
            }

            reversible = true;
            (leftText, rightText) = (parts[0], parts[1]);
        }
        else
        {
            var dashMatch = DashArrow.Match(normalized);
            if (dashMatch.Success)
            {
                condition = NullIfBlank(dashMatch.Groups["cond"].Value);
                normalized = DashArrow.Replace(normalized, "=", 1);
            }
            else
            {
                var eqMatch = EqualsCondition.Match(normalized);
                if (eqMatch.Success)
                {
                    condition = NullIfBlank(eqMatch.Groups["cond"].Value);
                    normalized = EqualsCondition.Replace(normalized, "=", 1);
                }
            }

            normalized = normalized.Replace("→", "=").Replace("->", "=");
            var sides = normalized.Split('=');
            if (sides.Length != 2)
            {
                throw new FormulaException($"方程式必须包含一个等号或箭头：{text}");
            }

            (leftText, rightText) = (sides[0], sides[1]);
        }

        // 电极反应式的"减电子"写法归一化（见 NormalizeElectronSide）
        (leftText, rightText) = NormalizeElectronSide(leftText, rightText);

        return new ParsedEquation(
            text.Trim(),
            ParseTerms(leftText, text),
            ParseTerms(rightText, text),
            condition,
            reversible);
    }

    private static readonly Regex ElectronMinus = new(@"\s*-\s*(?<coef>\d*)e\^-\s*", RegexOptions.Compiled);

    /// <summary>
    /// 电极反应式的"减电子"写法归一化：把 "Zn - 2e^- = Zn^2+" 中的电子项移到对侧成为加项
    /// （等价于 "Zn = Zn^2+ + 2e^-"），使半反应与普通方程式共用同一条守恒/配平校验管线。
    /// 两侧都可能出现减电子写法（如阴极误写成 "Cu^2+ - 2e^- = Cu" 也需能解析后判错）。
    /// </summary>
    private static (string Left, string Right) NormalizeElectronSide(string left, string right)
    {
        var onLeft = ElectronMinus.Match(left);
        if (onLeft.Success)
        {
            left = ElectronMinus.Replace(left, " ", 1);
            right = right.TrimEnd() + " + " + ElectronTerm(onLeft.Groups["coef"].Value);
        }

        var onRight = ElectronMinus.Match(right);
        if (onRight.Success)
        {
            right = ElectronMinus.Replace(right, " ", 1);
            left = left.TrimEnd() + " + " + ElectronTerm(onRight.Groups["coef"].Value);
        }

        return (left, right);
    }

    private static string ElectronTerm(string coefficient) =>
        $"{(coefficient.Length == 0 ? "1" : coefficient)}e^-";

    /// <summary>安全解析入口。</summary>
    public static bool TryParse(string text, out ParsedEquation? equation, out string? error)
    {
        try
        {
            equation = Parse(text);
            error = null;
            return true;
        }
        catch (FormulaException ex)
        {
            equation = null;
            error = ex.Message;
            return false;
        }
    }

    private static string? NullIfBlank(string s)
    {
        var t = s.Trim();
        return t.Length == 0 ? null : t;
    }

    private static List<ParsedTerm> ParseTerms(string side, string original)
    {
        var tokens = SplitTerms(side, original);
        var terms = new List<ParsedTerm>();
        foreach (var token in tokens)
        {
            var item = token.Trim();
            if (item.Length == 0)
            {
                continue;
            }

            // 状态符号（项尾）
            string? state = null;
            if (item.EndsWith('↑') || item.EndsWith('↓'))
            {
                state = item[^1].ToString();
                item = item[..^1].TrimEnd();
            }

            // 系数
            var coefficient = 1;
            var explicitCoefficient = false;
            var match = LeadingCoefficient.Match(item);
            if (match.Success)
            {
                coefficient = int.Parse(match.Groups["coef"].Value);
                explicitCoefficient = true;
                item = match.Groups["rest"].Value;
            }

            terms.Add(new ParsedTerm(coefficient, explicitCoefficient, FormulaParser.Parse(item), state));
        }

        if (terms.Count == 0)
        {
            throw new FormulaException($"方程式一侧为空：{original}");
        }

        return terms;
    }

    /// <summary>
    /// 按 '+' 拆分项。电荷中的 '+'（如 Fe^3+、SO4^2-）不作为分隔符：
    /// 规则与 <see cref="ChemicalEquation"/> 一致——'+' 前是 '^' 则属于电荷；
    /// 否则其后必须跟随新物种的起始字符（大写字母/括号/数字系数）才算分隔。
    /// </summary>
    private static List<string> SplitTerms(string side, string original)
    {
        var trimmed = side.Trim();
        if (trimmed.Length == 0)
        {
            throw new FormulaException($"方程式一侧为空：{original}");
        }

        if (Regex.IsMatch(trimmed, @"\s\+\s"))
        {
            return Regex.Split(trimmed, @"\s+\+\s+").Where(s => s.Trim().Length > 0).ToList();
        }

        var terms = new List<string>();
        var buffer = new StringBuilder();
        for (var i = 0; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            if (c == '+')
            {
                var next = i + 1 < trimmed.Length ? trimmed[i + 1] : '\0';
                var previous = buffer.Length > 0 ? buffer[^1] : '\0';
                var isSeparator = previous != '^' &&
                                  (char.IsUpper(next) || next is '[' or '(' || char.IsDigit(next));
                if (isSeparator)
                {
                    terms.Add(buffer.ToString());
                    buffer.Clear();
                    continue;
                }
            }

            buffer.Append(c);
        }

        terms.Add(buffer.ToString());
        return terms.Where(s => s.Trim().Length > 0).ToList();
    }
}
