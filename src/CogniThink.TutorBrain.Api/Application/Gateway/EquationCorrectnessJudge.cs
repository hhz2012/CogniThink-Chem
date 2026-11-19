using System.Text;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core;
using CogniThink.Chem.Core.Common;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 单方程式"书写正误"判定内核（阶段3D / AC-14）。
/// <para>真值源：<see cref="ChemicalCheckerService.Check"/>（符号化学引擎）；LLM 不参与判定。</para>
/// <para>判据（守 precision=100%，不蒙数）：</para>
/// <list type="bullet">
/// <item><b>×</b>：引擎报告含确定性错误标签（守恒/离子书写/状态符号/知识库矛盾/无配平解/氧化还原电子不守恒），
/// 或集中语境规则表（<see cref="CorrectnessContextRules"/>）检出引擎漏判的确定性错误。</item>
/// <item><b>√</b>：全部检查项通过 且 无 OutOfScope（物种在纲内），且无语境规则否定。</item>
/// <item><b>null</b>：其余（解析失败 / 仅超纲 / 氧化数未知 / 语境依赖无法确认）→ 不蒙数。</item>
/// </list>
/// </summary>
public static class EquationCorrectnessJudge
{
    /// <summary>正确判决字符（U+221A）。</summary>
    public const char Right = '√';

    /// <summary>错误判决字符（U+00D7）。</summary>
    public const char Wrong = '×';

    /// <summary>判定结果：Symbol 为 √/×/null；Reasons 为可解释理由。</summary>
    public sealed record Verdict(char? Symbol, List<string> Reasons)
    {
        /// <summary>是否得到确定判决（非 null）。</summary>
        public bool IsDetermined => Symbol.HasValue;
    }

    /// <summary>
    /// 判定单方程式书写正误。
    /// </summary>
    /// <param name="equation">方程式字符串（支持 Unicode 上下标 / === / ⇌ / → / --条件-->）。</param>
    /// <param name="context">该小题冒号前的语境文本（供语境规则表消费，可空）。</param>
    public static Verdict Judge(string? equation, string? context = null)
    {
        var reasons = new List<string>();
        var norm = NormalizeEquation(equation);
        if (string.IsNullOrWhiteSpace(norm))
        {
            reasons.Add("未能抽取到可解析的方程式");
            return new Verdict(null, reasons);
        }

        CheckReport report;
        try
        {
            report = ChemicalCheckerService.Check(new CheckRequest { Equation = norm });
        }
        catch
        {
            reasons.Add("引擎校验抛出异常，保守放弃");
            return new Verdict(null, reasons);
        }

        // 1) 确定性错误 → ×
        var cert = FirstCertainError(report);
        if (cert is not null)
        {
            reasons.Add($"引擎检出确定性错误：{cert.CheckName}（{cert.Detail}）");
            return new Verdict(Wrong, reasons);
        }

        // 2) 语境规则表：引擎未报确定性错误时，用题干语境补抓引擎漏判的确定性错误
        var ctxHit = CorrectnessContextRules.Evaluate(norm, context);
        if (ctxHit is not null)
        {
            reasons.Add(ctxHit);
            return new Verdict(Wrong, reasons);
        }

        // 3) √：全部通过且无超纲（解析失败/仅超纲/氧化数未知等都到不了这里 → null）
        var allPass = report.CheckItems.Count > 0 && report.CheckItems.All(i => i.Passed);
        var hasOutOfScope = report.CheckItems.Any(i => i.RiskTag == RiskTags.OutOfScope);
        if (report.ErrorMessage.Length == 0 && allPass && !hasOutOfScope)
        {
            reasons.Add("引擎全部检查项通过，物种在高中课内范围内");
            return new Verdict(Right, reasons);
        }

        // 4) 其余 → null（不蒙数）
        foreach (var item in report.CheckItems.Where(i => !i.Passed))
            reasons.Add($"{item.CheckName}：{item.Detail}");
        if (report.ErrorMessage.Length > 0)
            reasons.Add(report.ErrorMessage);
        return new Verdict(null, reasons);
    }

    /// <summary>
    /// 取首个"确定性错误"检查项。仅这些标签可确信判 ×；
    /// OutOfScope / 氧化数未知 / 解析失败等都不算确定性错误（归 null）。
    /// </summary>
    private static CheckResult? FirstCertainError(CheckReport report)
    {
        foreach (var item in report.CheckItems)
        {
            if (item.Passed) continue;
            var tag = item.RiskTag;
            if (tag == RiskTags.ConservationViolation ||
                tag == RiskTags.IonicNotationError ||
                tag == RiskTags.StateSymbolError ||
                tag == RiskTags.KnowledgeConflict ||
                tag == RiskTags.NoSolution)
            {
                return item;
            }

            // 氧化还原：仅"电子不守恒"是确定性错误；氧化数未知等 → 不蒙数
            if (tag == RiskTags.RedoxError &&
                item.Detail.Contains("电子", StringComparison.Ordinal) &&
                item.Detail.Contains("不相等", StringComparison.Ordinal))
            {
                return item;
            }
        }

        return null;
    }

    // ============================================================
    // 方程式归一化：Unicode 上下标 → caret/ASCII；多等号/可逆/箭头 → =
    // 归一化后交给 ChemicalCheckerService.Check（EquationParser 接受 caret 电荷 + 单等号）
    // ============================================================

    /// <summary>归一化为引擎可解析的标准记法（caret 电荷 + 单等号）。</summary>
    public static string NormalizeEquation(string? equation)
    {
        if (string.IsNullOrWhiteSpace(equation)) return string.Empty;
        var s = equation.Trim();

        // 1) Unicode 下标 ₀-₉ → 数字；上标连续段 ⁰-⁹⁺⁻ → ^ + ASCII
        s = ConvertUnicodeScripts(s);

        // 2) 多等号 === / == → =
        s = Regex.Replace(s, @"={2,}", "=");
        // 3) 可逆 / 箭头 → =
        s = Regex.Replace(s, @"⇌|→|⟶|⇒|↔", "=");
        // 4) 带条件箭头 --(高温)--> / —条件—→ 归为 =
        s = Regex.Replace(s, @"[-—–]{1,}[^-—–=>]*[-—–]{1,}>", "=");

        return s.Trim();
    }

    private static string ConvertUnicodeScripts(string s)
    {
        var sb = new StringBuilder(s.Length);
        var supers = new StringBuilder();
        foreach (var c in s)
        {
            if (IsSubscriptDigit(c))
            {
                FlushSupers();
                sb.Append(SubscriptDigitToAscii(c));
            }
            else if (IsSuperscriptChar(c))
            {
                supers.Append(SuperscriptToAscii(c));
            }
            else
            {
                FlushSupers();
                sb.Append(c);
            }
        }

        FlushSupers();
        return sb.ToString();

        void FlushSupers()
        {
            if (supers.Length == 0) return;
            sb.Append('^').Append(supers);
            supers.Clear();
        }
    }

    private static bool IsSubscriptDigit(char c) => c >= '₀' && c <= '₉';
    private static char SubscriptDigitToAscii(char c) => (char)('0' + (c - '₀'));

    private static bool IsSuperscriptChar(char c) =>
        (c >= '⁰' && c <= '⁹') || c == '⁺' || c == '⁻' ||
        c == '²' || c == '³' || c == '¹'; // Latin-1 补充区上标数字（² U+00B2 等）

    private static char SuperscriptToAscii(char c) => c switch
    {
        '⁰' => '0', '¹' => '1', '²' => '2', '³' => '3', '⁴' => '4',
        '⁵' => '5', '⁶' => '6', '⁷' => '7', '⁸' => '8', '⁹' => '9',
        '⁺' => '+', '⁻' => '-',
        _ => c
    };
}
