using System.Text.RegularExpressions;

namespace CogniThink.Chem.Core.Chemistry;

/// <summary>
/// 化学式解析异常。继承 <see cref="FormatException"/>：
/// 语义上就是"输入文本不是合法化学式"，与格式解析失败同类，
/// 使既有 <c>catch (FormatException)</c> 的防御点（解析回退、白名单回退）统一生效。
/// </summary>
public sealed class FormulaException : FormatException
{
    public FormulaException(string message) : base(message)
    {
    }
}

/// <summary>
/// 化学物种：元素计数 + 电荷。解析支持括号与嵌套，如 Fe2(SO4)3、[Ag(NH3)2]+、SO4^2-。
/// 约定：电荷量大于 1 时用 ^ 标注（SO4^2-、Fe^3+）；单元素可直接写 Fe3+。
/// </summary>
public sealed class Species
{
    private static readonly Regex CaretCharge = new(@"\^(?<n>\d*)(?<sign>[+-])$", RegexOptions.Compiled);
    private static readonly Regex BareCharge = new(@"(?<n>\d*)(?<sign>[+-])$", RegexOptions.Compiled);

    private Species(string formula, string body, IReadOnlyDictionary<string, int> elements, int charge)
    {
        Formula = formula;
        Body = body;
        Elements = elements;
        Charge = charge;
    }

    public string Formula { get; }

    /// <summary>去掉电荷标注后的化学式主体（"SO4^2-" → "SO4"），ToString 基于它重建电荷后缀。</summary>
    public string Body { get; }

    public IReadOnlyDictionary<string, int> Elements { get; }

    public int Charge { get; }

    public int AtomCount => Elements.Values.Sum();

    public static Species Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new FormulaException("化学式为空");
        }

        var body = text.Trim().Replace(" ", "");
        var charge = 0;

        var caret = CaretCharge.Match(body);
        if (caret.Success)
        {
            var magnitude = caret.Groups["n"].Value.Length == 0 ? 1 : int.Parse(caret.Groups["n"].Value);
            charge = caret.Groups["sign"].Value == "-" ? -magnitude : magnitude;
            body = body[..caret.Index];
        }
        else
        {
            var bare = BareCharge.Match(body);
            if (bare.Success)
            {
                var magnitude = bare.Groups["n"].Value.Length == 0 ? 1 : int.Parse(bare.Groups["n"].Value);
                var sign = bare.Groups["sign"].Value == "-" ? -1 : 1;
                var head = body[..bare.Index];
                if (magnitude == 1 || IsSingleElement(head))
                {
                    charge = sign * magnitude;
                    body = head;
                }
                else
                {
                    throw new FormulaException($"电荷写法有歧义，请用 ^ 标注：{text}");
                }
            }
        }

        if (body.Length == 0)
        {
            throw new FormulaException($"缺少化学式主体：{text}");
        }

        // 电子伪物种：电极反应式中的 e^-（"Zn - 2e^- = Zn^2+"）。
        // 它没有原子组成，只带 -1 电荷，因此元素计数为空集，守恒校验中仅计入电荷项。
        if (body == "e" && charge == -1)
        {
            return new Species(text.Trim(), "e", new Dictionary<string, int>(StringComparer.Ordinal), -1);
        }

        var elements = ParseBody(body, text);
        return new Species(text.Trim(), body, elements, charge);
    }

    /// <summary>
    /// 由已合并的元素计数直接构造物种（供水合物等需要跨 "·" 合并的解析使用）。
    /// </summary>
    /// <param name="formula">显示用化学式原文。</param>
    /// <param name="elements">元素计数（调用方负责合并完成）。</param>
    /// <param name="charge">净电荷。</param>
    public static Species FromElements(string formula, IReadOnlyDictionary<string, int> elements, int charge) =>
        new(formula, formula, new Dictionary<string, int>(elements, StringComparer.Ordinal), charge);

    private static bool IsSingleElement(string text) =>
        Regex.IsMatch(text, "^[A-Z][a-z]?$");

    private static Dictionary<string, int> ParseBody(string body, string original)
    {
        var stack = new Stack<Dictionary<string, int>>();
        var current = new Dictionary<string, int>(StringComparer.Ordinal);
        var index = 0;

        while (index < body.Length)
        {
            var c = body[index];
            if (char.IsUpper(c))
            {
                var symbol = c.ToString();
                index++;
                while (index < body.Length && char.IsLower(body[index]))
                {
                    symbol += body[index];
                    index++;
                }

                var count = ReadNumber(body, ref index);
                Add(current, symbol, count);
                continue;
            }

            if (c == '(' || c == '[')
            {
                stack.Push(current);
                current = new Dictionary<string, int>(StringComparer.Ordinal);
                index++;
                continue;
            }

            if (c == ')' || c == ']')
            {
                index++;
                var multiplier = ReadNumber(body, ref index);
                if (stack.Count == 0)
                {
                    throw new FormulaException($"括号不匹配：{original}");
                }

                var inner = current;
                current = stack.Pop();
                foreach (var (symbol, count) in inner)
                {
                    Add(current, symbol, count * multiplier);
                }

                continue;
            }

            if (char.IsDigit(c))
            {
                throw new FormulaException($"数字位置非法：{original}");
            }

            throw new FormulaException($"无法解析的字符 '{c}'：{original}");
        }

        if (stack.Count != 0)
        {
            throw new FormulaException($"括号不匹配：{original}");
        }

        return current;
    }

    private static int ReadNumber(string body, ref int index)
    {
        var start = index;
        while (index < body.Length && char.IsDigit(body[index]))
        {
            index++;
        }

        if (start == index)
        {
            return 1;
        }

        return int.Parse(body[start..index]);
    }

    private static void Add(Dictionary<string, int> map, string symbol, int count)
    {
        map.TryGetValue(symbol, out var existing);
        map[symbol] = existing + count;
    }

    public override string ToString() => Charge switch
    {
        0 => Body,
        1 => $"{Body}^+",
        -1 => $"{Body}^-",
        _ => $"{Body}^{System.Math.Abs(Charge)}{(Charge > 0 ? "+" : "-")}"
    };
}
