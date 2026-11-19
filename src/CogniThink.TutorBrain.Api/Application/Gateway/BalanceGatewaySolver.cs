using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Balancing;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 网关层配平求解器（阶段3A / AC-11）。
/// <para>
/// 真值源：<see cref="EquationBalancer"/>（原子/电荷/电子守恒 → 齐次线性方程组零空间 →
/// 最小正整数系数）。本类只负责从网关请求中<strong>抽取方程式字符串 + 归一化 eval 记法</strong>
/// （___ 系数空位、=== 多等号），再调用内核；不做任何化学知识推断。
/// </para>
/// <para>
/// 红线（不蒙数）：无唯一解（无解/欠定/解析失败/无完整方程）一律返回 null，绝不臆造系数或产物。
/// </para>
/// </summary>
public static class BalanceGatewaySolver
{
    // 主分隔符：===+ / == / → / ⇌ / --条件-->（含 "=通电=" 由 EquationParser 另行处理，此处先捕获多等号）
    private static readonly Regex Separator =
        new(@"(={2,}|→|⇌|[-—–]{1,}[^-—–>]*[-—–]{1,}>)", RegexOptions.Compiled);

    private static readonly Regex UnderscoreBlanks = new(@"_{2,}", RegexOptions.Compiled);
    private static readonly Regex MultiEquals = new(@"={2,}", RegexOptions.Compiled);

    /// <summary>配平成功的产物。</summary>
    public sealed record Outcome(string BalancedEquation, List<string> Steps);

    /// <summary>
    /// 从网关请求尝试配平。成功返回配平结果；失败（无解/欠定/无可解析方程）返回 null。
    /// </summary>
    public static Outcome? TrySolve(EngineRequest req)
    {
        var equation = PickEquation(req);
        if (string.IsNullOrWhiteSpace(equation)) return null;

        var normalized = NormalizeForBalancing(equation);
        if (string.IsNullOrWhiteSpace(normalized)) return null;

        BalanceResult result;
        try
        {
            result = EquationBalancer.Balance(normalized);
        }
        catch
        {
            // 内核应已异常安全；兜底防止任何意外异常把网关链路拖垮
            return null;
        }

        if (!result.Success || string.IsNullOrWhiteSpace(result.BalancedEquation)) return null;

        var steps = new List<string>
        {
            $"抽取并归一化方程式：{normalized}",
            "守恒矩阵求解（原子/电荷守恒 → 高斯消元求零空间 → 最小正整数系数）",
            $"配平结果：{result.BalancedEquation}"
        };
        return new Outcome(result.BalancedEquation, steps);
    }

    /// <summary>
    /// 选取待配平方程式，优先级：学生答案（本身是方程） > LLM 提取的 Left/Right > 从原始题干抽取第一段。
    /// </summary>
    public static string? PickEquation(EngineRequest req)
    {
        // 1) 学生直接给出的方程式
        if (!string.IsNullOrWhiteSpace(req.StudentAnswer) && ContainsSeparator(req.StudentAnswer))
            return req.StudentAnswer;

        // 2) LLM 提取的左右物种（NL→结构化，架构原则允许）
        var left = req.Reaction?.Left?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>();
        var right = req.Reaction?.Right?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>();
        if (left.Count > 0 && right.Count > 0)
            return string.Join(" + ", left) + " = " + string.Join(" + ", right);

        // 3) 从原始题干抽取第一段方程式
        return ExtractFirstEquation(req.RawText ?? string.Empty);
    }

    /// <summary>
    /// 归一化 eval 真实记法：去掉 ___ 系数空位（___NO2^- → NO2^-），把连续等号 === / == 归一成 =。
    /// （==(电解)== 会变成 =(电解)=，再由 EquationParser 提取条件。）
    /// </summary>
    public static string NormalizeForBalancing(string equation)
    {
        if (string.IsNullOrWhiteSpace(equation)) return string.Empty;
        var s = equation.Trim();
        s = UnderscoreBlanks.Replace(s, "");
        s = MultiEquals.Replace(s, "=");
        return s.Trim();
    }

    /// <summary>
    /// 从题干文本抽取第一段方程式：定位第一个主分隔符，向左右扩展到最近句界，
    /// 并要求两侧都含元素字母（避免把 "pH=7" 之类误判为方程）。
    /// </summary>
    public static string? ExtractFirstEquation(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = Separator.Match(text);
        if (!m.Success) return null;

        var sepStart = m.Index;
        var sepEnd = m.Index + m.Length;

        var leftBound = LastIndexOfAny(text, sepStart, '：', ':', '。', '；', ';', '\n', '（', '(');
        var start = leftBound >= 0 ? leftBound + 1 : 0;

        var end = IndexOfAny(text, sepEnd, '。', '；', ';', '\n');
        if (end < 0) end = text.Length;

        var seg = text[start..end].Trim();
        var sides = seg.Split(new[] { '=', '→', '⇌' }, 2);
        if (sides.Length < 2) return null;
        if (!ContainsElementLetter(sides[0]) || !ContainsElementLetter(sides[1])) return null;
        return seg;
    }

    // 学生答案中可能使用单等号 "A + B = C"（多字符分隔符已由 Separator 覆盖）。
    // 仅当等号两侧均含元素字母（ASCII）时才视为方程，排除 pH=7 / x=5 之类误判。
    private static bool ContainsSeparator(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        if (Separator.IsMatch(s)) return true;
        var idx = s.IndexOf('=');
        if (idx < 0) return false;
        return ContainsAsciiLetter(s[..idx]) && ContainsAsciiLetter(s[(idx + 1)..]);
    }

    private static bool ContainsElementLetter(string s) => s.Any(char.IsLetter);
    private static bool ContainsAsciiLetter(string s) =>
        s.Any(c => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'));

    private static int LastIndexOfAny(string s, int before, params char[] any)
    {
        var idx = -1;
        foreach (var c in any)
        {
            var i = s.LastIndexOf(c, before);
            if (i > idx) idx = i;
        }
        return idx;
    }

    private static int IndexOfAny(string s, int after, params char[] any)
    {
        var idx = int.MaxValue;
        foreach (var c in any)
        {
            var i = s.IndexOf(c, after);
            if (i >= 0 && i < idx) idx = i;
        }
        return idx == int.MaxValue ? -1 : idx;
    }
}
