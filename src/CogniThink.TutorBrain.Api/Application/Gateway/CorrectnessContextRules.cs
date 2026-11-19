using System.Text;
using System.Text.RegularExpressions;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 方程式"书写正误"判定的语境规则表（阶段3D / AC-14）。
/// <para>集中收口所有"引擎形式校验全过、但需题干语境才能确定的错误"，
/// 避免散落硬编码分支造成决策语义漂移（经验：阈值/聚合口径必须集中）。</para>
/// <para>每条规则：仅当条件全部命中才返回一条 × 理由；否则返回 null（不影响引擎判定）。
/// 规则未命中时，外层 <see cref="EquationCorrectnessJudge"/> 按引擎结果判 √/null，绝不臆断。</para>
/// <para>新增规则铁律：必须能写出不依赖具体题号的化学条件、配单测、且全量评估 harm=0；
/// 否则不得加入（保持 pdf-051 等题为 null，零回归）。</para>
/// </summary>
internal static class CorrectnessContextRules
{
    /// <summary>命中确定性语境错误时返回理由；否则 null。</summary>
    public static string? Evaluate(string normalizedEquation, string? context)
    {
        var eq = normalizedEquation ?? string.Empty;
        // 语境也归一化 Unicode 上下标，便于 NO₃⁻ / Cr₂O₇²⁻ 等检索
        var ctx = NormalizeForSearch(context);
        var hay = ctx + " " + eq;

        // Rule A：酸性 NO₃⁻ 氧化亚硫酸（S +4 → +6），钡盐应生成 BaSO₄，方程写 BaSO₃ 为错。
        // 触发：反应物含亚硫酸源（H2SO3/SO3^2-/HSO3^-）+ Ba²⁺，产物含 BaSO3，
        //       且语境/方程出现硝酸根（NO3/硝酸/硝酸钡）。
        if (HasSulfiteSource(eq) && Contains(eq, "Ba") && Contains(eq, "BaSO3") &&
            ContainsAny(hay, "NO3", "硝酸", "硝酸盐", "硝酸钡", "Ba(NO3)2"))
        {
            return "语境规则：酸性条件下 NO₃⁻ 将 H₂SO₃（S +4）氧化为 SO₄²⁻，与 Ba²⁺ 生成 BaSO₄，" +
                   "题给产物 BaSO₃ 错误";
        }

        // Rule B：亚铜氨配离子中 Cu 为 +1，配离子电荷应为 +1；
        // 题写 [Cu(NH₃)₂]²⁺ / [Cu(NH₃)₃CO]²⁺（+2）为错。
        if (Contains(ctx, "亚铜氨") &&
            Regex.IsMatch(eq, @"\[Cu\(NH3\)2\]\^2\+|\[Cu\(NH3\)3CO\]\^2\+"))
        {
            return "语境规则：亚铜氨中 Cu 为 +1，配离子 [Cu(NH₃)₂]⁺ / [Cu(NH₃)₃CO]⁺ 电荷应为 +1，题写 +2 错误";
        }

        return null;
    }

    private static bool HasSulfiteSource(string eq) =>
        ContainsAny(eq, "H2SO3", "SO3^2-", "HSO3^-", "SO3");

    private static bool Contains(string s, string needle) =>
        s.Contains(needle, StringComparison.Ordinal);

    private static bool ContainsAny(string s, params string[] needles)
    {
        foreach (var n in needles)
            if (s.Contains(n, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>把 Unicode 上下标转为普通 ASCII（仅用于文本检索，不加 caret）。</summary>
    private static string NormalizeForSearch(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c >= '₀' && c <= '₉') sb.Append((char)('0' + (c - '₀')));
            else if (c >= '⁰' && c <= '⁹') sb.Append((char)('0' + (c - '⁰')));
            else if (c == '⁺') sb.Append('+');
            else if (c == '⁻') sb.Append('-');
            else sb.Append(c);
        }
        return sb.ToString();
    }
}
