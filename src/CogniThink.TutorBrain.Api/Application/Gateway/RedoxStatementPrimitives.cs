using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Parsing;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 氧化还原陈述判定共享原语：文本归一化、方程式抽取、标签解析（题干标签标记 / 陈述标签引用）。
/// 由 <see cref="RedoxElectronStatementJudge"/>、<see cref="RedoxCountStatementJudge"/>、
/// <see cref="RedoxRoleRatioStatementJudge"/> 共用，保证三者口径一致。
/// <para>从 <c>RedoxElectronStatementJudge</c> 纯提取（行为字节级不变，由全库护栏
/// <c>FullBank_RedoxJudge_NeverContradicts_ExistingDecided</c> 保护）；仅新增
/// <see cref="AllLabeledEquations"/> 等只读扩展，供计数/角色原语使用。</para>
/// </summary>
internal static class RedoxStatementPrimitives
{
    // ===================== 文本归一化 =====================

    // 上标数字 → 普通数字；上标 +/- → ^ 电荷标注；下标数字 → 普通数字。
    // 使 OH⁻ / Cr2O7²⁻ / H₂ 等变为 EquationParser 可识别的 OH^- / Cr2O7^2- / H2。
    private static readonly Dictionary<char, char> SuperDigits = new()
    {
        ['\u2070'] = '0', ['\u00b9'] = '1', ['\u00b2'] = '2', ['\u00b3'] = '3',
        ['\u2074'] = '4', ['\u2075'] = '5', ['\u2076'] = '6', ['\u2077'] = '7',
        ['\u2078'] = '8', ['\u2079'] = '9'
    };

    public static string NormalizeText(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '\u207a') { sb.Append("^+"); continue; }
            if (c == '\u207b') { sb.Append("^-"); continue; }
            if (SuperDigits.TryGetValue(c, out var d))
            {
                var digits = new StringBuilder();
                var j = i;
                while (j < s.Length && SuperDigits.TryGetValue(s[j], out var d2)) { digits.Append(d2); j++; }
                if (j < s.Length && (s[j] == '\u207a' || s[j] == '\u207b'))
                {
                    sb.Append('^');
                    sb.Append(digits);
                    sb.Append(s[j] == '\u207a' ? '+' : '-');
                    i = j; // 循环末尾 i++ 跳过 sign
                }
                else
                {
                    sb.Append(digits);
                    i = j - 1; // 循环末尾 i++ 落到 j
                }
                continue;
            }
            if (c >= '\u2080' && c <= '\u2089') { sb.Append((char)('0' + (c - '\u2080'))); continue; }
            sb.Append(c);
        }
        return sb.ToString();
    }

    // ===================== 方程式抽取 =====================

    // 方程式抽取（与 StoichiometryChoiceValidator 同口径，额外允许电荷中的 '-'）。
    public static readonly Regex EquationRe = new(
        @"[A-Za-z0-9()^\s+\-\u00b7]+(?:=|\u2192|->)[A-Za-z0-9()^\s+\-\u00b7\u2191\u2193]+",
        RegexOptions.Compiled);

    public static List<string> FindEquations(string text)
    {
        var list = new List<string>();
        foreach (Match m in EquationRe.Matches(text))
        {
            var cand = m.Value.Trim();
            if (EquationParser.TryParse(cand, out _, out _))
                list.Add(cand);
        }
        return list;
    }

    // ===================== 标签解析 =====================

    // 陈述引用的反应标签（反应ⅱ / 反应① / 反应(1) / 步骤④ / 过程① …）。
    public static readonly Regex LabelRefRe = new(
        @"(?:反应|步骤|过程|第)\s*([\u2160-\u2179\u2460-\u2469]|\(\d+\)|\d+|一二三四五六七八九十)",
        RegexOptions.Compiled);

    public static int? FindLabelRef(string statement)
    {
        var m = LabelRefRe.Match(statement);
        return m.Success ? LabelValue(m.Groups[1].Value) : null;
    }

    public static int? LabelValue(string token)
    {
        if (token.Length == 1)
        {
            var c = token[0];
            if (c >= '\u2160' && c <= '\u2169') return c - '\u2160' + 1; // Ⅰ-Ⅹ
            if (c >= '\u2170' && c <= '\u2179') return c - '\u2170' + 1; // ⅰ-ⅹ
            if (c >= '\u2460' && c <= '\u2469') return c - '\u2460' + 1; // ①-⑩
        }
        if (token.StartsWith('(') && token.EndsWith(')') && token.Length > 2)
        {
            if (int.TryParse(token.Substring(1, token.Length - 2), out var p)) return p;
        }
        if (int.TryParse(token, out var d)) return d;
        var ci = "一二三四五六七八九十".IndexOf(token, StringComparison.Ordinal);
        if (ci >= 0) return ci + 1; // 一..十 → 1..10
        return null;
    }

    // 题干中的标签标记（罗马数字 / 圆圈数字），按出现位置排序。
    public static List<(int value, int end)> FindLabelMarkers(string text)
    {
        var list = new List<(int, int)>();
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            int? v = c >= '\u2160' && c <= '\u2169' ? c - '\u2160' + 1
                   : c >= '\u2170' && c <= '\u2179' ? c - '\u2170' + 1
                   : c >= '\u2460' && c <= '\u2469' ? c - '\u2460' + 1
                   : null;
            if (v.HasValue) list.Add((v.Value, i + 1));
        }
        return list;
    }

    public static string? ResolveLabeled(string stem, int labelValue)
        => ResolveLabeledCore(NormalizeText(stem), labelValue);

    private static string? ResolveLabeledCore(string normalizedStem, int labelValue)
    {
        var markers = FindLabelMarkers(normalizedStem);
        if (markers.Count == 0) return null;
        var mi = markers.FindIndex(m => m.value == labelValue);
        if (mi < 0) return null;
        var segStart = markers[mi].end;
        var segEnd = mi + 1 < markers.Count ? markers[mi + 1].end - 1 : normalizedStem.Length;
        // segEnd 指向下一标记的起始位置（end-1 即下一标记 char 的 index）
        if (segEnd <= segStart) return null;
        var segment = normalizedStem.Substring(segStart, segEnd - segStart);
        var eqs = FindEquations(segment);
        return eqs.Count > 0 ? eqs[0] : null;
    }

    public static string? ResolveUnique(string stem)
    {
        var eqs = FindEquations(NormalizeText(stem));
        return eqs.Count == 1 ? eqs[0] : null; // 多个方程式且无标签 → 歧义，不判
    }

    /// <summary>
    /// 【新增】返回题干中所有可解析为方程式的带标签反应（按标签值升序，同值去重）。
    /// 仅收集 <see cref="ResolveLabeled"/> 成功的标签；用于"反应ⅰ～ⅳ中有 N 个氧化还原反应"等
    /// 计数/角色陈述。任一标签方程不可解析由调用方决定是否整体 Undecided（保守口径）。
    /// </summary>
    public static List<(int label, string equation)> AllLabeledEquations(string stem)
    {
        var s = NormalizeText(stem);
        var markers = FindLabelMarkers(s);
        var result = new List<(int, string)>();
        var seen = new HashSet<int>();
        foreach (var (value, _) in markers)
        {
            if (!seen.Add(value)) continue;
            var eq = ResolveLabeledCore(s, value);
            if (eq is not null) result.Add((value, eq));
        }
        return result;
    }
}
