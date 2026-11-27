using System.Collections.Generic;
using System.Text;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 选择项陈述判定原语共享的归一化口径：下标/上标 → ASCII 数字，破折号统一，
/// 去空白（不改变大小写，避免元素符号 Co/CO 混淆）。与 FactChoiceMatcher.NormOpt
/// 的上下标归一化保持一致。
/// </summary>
internal static class StatementNorm
{
    private static readonly Dictionary<char, char> Sub = new()
    {
        ['₀'] = '0', ['₁'] = '1', ['₂'] = '2', ['₃'] = '3', ['₄'] = '4',
        ['₅'] = '5', ['₆'] = '6', ['₇'] = '7', ['₈'] = '8', ['₉'] = '9',
    };
    private static readonly Dictionary<char, char> Sup = new()
    {
        ['⁰'] = '0', ['¹'] = '1', ['²'] = '2', ['³'] = '3', ['⁴'] = '4',
        ['⁵'] = '5', ['⁶'] = '6', ['⁷'] = '7', ['⁸'] = '8', ['⁹'] = '9',
    };

    /// <summary>归一化并去全部空白。</summary>
    public static string Norm(string? s) => Norm(s, stripSpace: true);

    /// <summary>归一化；可选保留空白（用于元素边界扫描）。</summary>
    public static string Norm(string? s, bool stripSpace)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (Sub.TryGetValue(c, out var d1)) { sb.Append(d1); continue; }
            if (Sup.TryGetValue(c, out var d2)) { sb.Append(d2); continue; }
            if (c == '⁺') { sb.Append('+'); continue; }
            if (c == '⁻') { sb.Append('-'); continue; }
            if (c == '—' || c == '–' || c == '−') { sb.Append('-'); continue; }
            if (stripSpace && char.IsWhiteSpace(c)) continue;
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>仅把 ASCII 字母/数字视为「同 token 延续」，避免中文字符被 IsLetterOrDigit 当成字母而破坏化学式边界。</summary>
    public static bool IsAsciiAlnum(char c) =>
        (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

    /// <summary>化学名词归一化：共价晶体 ≡ 原子晶体（高中教材同义）。</summary>
    public static string NormCrystalType(string? type)
    {
        var t = Norm(type);
        if (t.Contains("共价晶体")) return "原子晶体";
        return t;
    }
}
