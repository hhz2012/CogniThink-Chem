using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// Ksp-pH → 残留金属离子浓度求解器（P0-2 能力补全）。
/// <para>域：题干给出某金属氢氧化物 M(OH)ₙ 的 Ksp，且显式「调 pH=x」并问某金属离子的浓度
/// （如 pdf-168 子问(3)：调 pH=6，Ksp[Al(OH)₃]=1.3×10⁻³³ → c(Al³⁺)=Ksp/[OH⁻]³=1.3×10⁻⁹）。</para>
/// <para>红线：Ksp 与价态 n 只从题干文本解析（不硬编码化学真值）；缺 Ksp / 缺 pH / 目标离子氢氧化物
/// 不在库 / 目标歧义 / 非氢氧化物体系 → 返回 null（不蒙数）。仅当能唯一确定时输出，结论以数值开头
/// 便于答案键命中；采纳门必经答案键校验 → precision=1、零 harm。</para>
/// </summary>
public static class KspPhResidualSolver
{
    // 数值 pH：pH=6 / pH = 6.5 / 调pH=6
    private static readonly Regex PhRegex = new(
        @"pH\s*[=＝:：]\s*(\d+(?:\.\d+)?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 目标离子问法：c(Al3+) / 滤液中 Al3+ 的浓度 / Al3+浓度为
    // 字符类含 ASCII + / - 与上标电荷，确保 "Al3+" / "Fe²⁺" 等能完整捕获（核心归一化时再剥离电荷）。
    private static readonly Regex[] TargetPatterns =
    {
        new(@"c\s*\(\s*([A-Za-z][A-Za-z0-9()⁺⁻²³⁴⁺\^+\-]+)\s*\)",
            RegexOptions.Compiled),
        new(@"(?:滤液|溶液|上清液|水相)?\s*中\s*([A-Za-z][A-Za-z0-9()⁺⁻²³⁴⁺\^+\-]*)\s*(?:的)?\s*浓度",
            RegexOptions.Compiled),
    };

    /// <summary>命中则返回含数值浓度的结论；域外/缺数据/歧义返回 null。</summary>
    public static EngineResponse? TrySolve(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!raw.Contains("Ksp", StringComparison.OrdinalIgnoreCase)) return null;
        if (!raw.Contains("pH", StringComparison.OrdinalIgnoreCase)) return null;
        if (!raw.Contains("浓度", StringComparison.Ordinal)) return null;

        // 1) pH 数值（取首个；非 (0,14) → 域外）
        var phMatch = PhRegex.Match(raw);
        if (!phMatch.Success) return null;
        if (!double.TryParse(phMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var pH)) return null;
        if (pH <= 0 || pH >= 14) return null;

        // 2) 解析所有 M(OH)ₙ 的 Ksp（物种在 Ksp 前或 Ksp 后的括号/方括号里）
        var hydroxides = ParseHydroxideKsp(raw);
        if (hydroxides.Count == 0) return null;

        // 3) 目标离子（问浓度的那个）
        var targets = ParseTargetIons(raw);
        if (targets.Count == 0) return null;

        // 目标离子必须能唯一对应到一个氢氧化物；多个不同目标 → 歧义不蒙数
        string? matchedMetal = null;
        HydroxideKsp? matched = null;
        foreach (var t in targets)
        {
            var core = NormalizeMetalCore(t);
            if (string.IsNullOrEmpty(core)) return null;
            var hit = hydroxides.FirstOrDefault(h => h.MetalCore == core);
            if (hit is null) return null; // 问的离子没有对应氢氧化物 Ksp → 不蒙数
            if (matchedMetal is null) { matchedMetal = core; matched = hit; }
            else if (matchedMetal != core) return null; // 多个不同目标离子 → 歧义
        }
        if (matched is null) return null;

        // 4) 计算 c(Mⁿ⁺) = Ksp / [OH⁻]ⁿ，[OH⁻] = 10^(pH-14)
        var oh = Math.Pow(10, pH - 14);
        if (oh <= 0) return null;
        var conc = matched.Ksp / Math.Pow(oh, matched.N);
        if (double.IsNaN(conc) || double.IsInfinity(conc) || conc <= 0) return null;

        var concText = FormatScientific(conc, matched.MantissaDecimals);
        var kspText = FormatScientific(matched.Ksp, matched.MantissaDecimals);
        var finalResult = $"滤液中 c({matched.Metal}³⁺) = {concText} mol/L" +
                          $"（Ksp[{matched.Metal}(OH){matched.N}]={kspText}，pH={pH:0.##} 时 [OH⁻]=10{ExponentSuperscript(pH - 14)}，c=Ksp/[OH⁻]^{matched.N}）。";

        return new EngineResponse
        {
            Ok = true,
            FinalResult = finalResult,
            DerivationSteps = new List<string>
            {
                $"pH = {pH:0.##}，[OH⁻] = 10^(pH−14) = 10{ExponentSuperscript(pH - 14)} mol/L",
                $"Ksp[{matched.Metal}(OH){matched.N}] = {kspText}，溶度积 c({matched.Metal}{matched.N}+)·[OH⁻]^{matched.N} = Ksp",
                $"c({matched.Metal}{matched.N}+) = Ksp / [OH⁻]^{matched.N} = {kspText} / (10{ExponentSuperscript(pH - 14)})^{matched.N} = {concText}"
            },
            ReasoningNodes = new List<ReasoningNode>
            {
                new()
                {
                    Step = 1,
                    Description = "由 pH 与氢氧化物 Ksp 推导残留金属离子浓度",
                    Premises = new List<string> { $"题干给出 Ksp[{matched.Metal}(OH){matched.N}]={kspText}，调 pH={pH:0.##}" },
                    KnowledgeId = $"ksp-ph:{matched.Metal}(OH){matched.N}",
                    Conclusion = finalResult,
                    EvidenceLevel = EvidenceLevel.EngineDerived
                }
            },
            ConstraintNote = "【高中限定】M(OH)ₙ 残留离子浓度 c=Ksp/[OH⁻]ⁿ；Ksp 与价态取自题干，缺数据/歧义不蒙数",
            Confidence = 1.0
        };
    }

    private sealed record HydroxideKsp(string MetalCore, string Metal, int N, double Ksp, int MantissaDecimals);

    // 解析形如 "Al(OH)3 Ksp=1.3×10⁻³³" / "Ksp[Pb(OH)2]=10^-14.8" / "Ksp(Fe(OH)3)=2.8×10^-39"
    private static List<HydroxideKsp> ParseHydroxideKsp(string raw)
    {
        var list = new List<HydroxideKsp>();
        // 物种在 Ksp 前：<species> Ksp = <value>
        foreach (Match m in Regex.Matches(raw,
                     @"([A-Z][A-Za-z0-9()]{1,15})\s*(?:的)?\s*Ksp\s*[=＝]\s*([^\s；;，,。）)]+)"))
        {
            AddIfHydroxide(list, m.Groups[1].Value, m.Groups[2].Value);
        }
        // 物种在 Ksp 后的括号/方括号：Ksp[<species>] = <value> / Ksp(<species>) = <value>
        foreach (Match m in Regex.Matches(raw,
                     @"Ksp\s*[[(]\s*([A-Z][A-Za-z0-9()]{1,15})\s*[\])]\s*[=＝]\s*([^\s；;，,。）)]+)"))
        {
            AddIfHydroxide(list, m.Groups[1].Value, m.Groups[2].Value);
        }
        return list;
    }

    private static void AddIfHydroxide(List<HydroxideKsp> list, string speciesRaw, string valueRaw)
    {
        var species = speciesRaw.Trim();
        var m = Regex.Match(species, @"^([A-Z][a-z]?)\s*\(\s*OH\s*\)\s*(\d+)$", RegexOptions.IgnoreCase);
        if (!m.Success) return; // 非 M(OH)ₙ 体系（如 AgCl / FeS）→ 忽略
        var metal = m.Groups[1].Value;
        if (!int.TryParse(m.Groups[2].Value, out var n) || n <= 0) return;

        var ksp = ParseKspValue(valueRaw, out var mantissaDecimals);
        if (ksp is null || ksp <= 0) return;
        var core = NormalizeMetalCore(metal);
        if (list.Any(h => h.MetalCore == core)) return; // 同一金属只取首次
        list.Add(new HydroxideKsp(core, metal, n, ksp.Value, mantissaDecimals));
    }

    // 解析 "1.3×10⁻³³" / "10^-14.8" / "6.4×10^-36" / "1.8e-10"
    private static double? ParseKspValue(string raw, out int mantissaDecimals)
    {
        mantissaDecimals = 0;
        var norm = NormalizeSuperscripts(raw);
        // a×10^b 或 a*10^b 或 a x 10^b 或 a×10b（无^）
        var m = Regex.Match(norm,
            @"^(\d+(?:\.\d+)?)\s*[×xX*]\s*10\s*(?:\^)?\s*([+-]?\d+(?:\.\d+)?)$");
        if (m.Success)
        {
            var man = m.Groups[1].Value;
            mantissaDecimals = DecimalPlaces(man);
            if (double.TryParse(man, NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
                && double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
                return a * Math.Pow(10, b);
            return null;
        }
        // 10^b（mantissa = 1）
        m = Regex.Match(norm, @"^10\s*(?:\^)?\s*([+-]?\d+(?:\.\d+)?)$");
        if (m.Success)
        {
            mantissaDecimals = 0;
            if (double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
                return Math.Pow(10, b);
            return null;
        }
        // 科学计数 aEb
        m = Regex.Match(norm, @"^(\d+(?:\.\d+)?)[eE]([+-]?\d+)$");
        if (m.Success)
        {
            var man = m.Groups[1].Value;
            mantissaDecimals = DecimalPlaces(man);
            if (double.TryParse(man + "e" + m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return v;
        }
        return null;
    }

    private static int DecimalPlaces(string s)
    {
        var i = s.IndexOf('.');
        return i < 0 ? 0 : s.Length - i - 1;
    }

    // 提取问浓度的目标离子（去重、保序）
    private static List<string> ParseTargetIons(string raw)
    {
        var set = new List<string>();
        foreach (var pat in TargetPatterns)
        {
            foreach (Match m in pat.Matches(raw))
            {
                var t = m.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(t)) continue;
                // 排除 OH/H 等离子（不是金属残留离子问法）
                var core = NormalizeMetalCore(t);
                if (core is "oh" or "h" or "o" or "n" or "c" or "s" or "cl" or "na" or "k") continue;
                if (!set.Contains(t)) set.Add(t);
            }
        }
        return set;
    }

    private static string NormalizeMetalCore(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        // 去电荷/上标/下标/括号，取元素核心
        var t = Regex.Replace(s, @"[\[\]()]", "");
        t = NormalizeSuperscripts(t);
        t = Regex.Replace(t, @"[\^+\-⁺⁻0-9]", "");
        return t.Trim().ToLowerInvariant();
    }

    private static string NormalizeSuperscripts(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            sb.Append(c switch
            {
                '⁰' => '0', '¹' => '1', '²' => '2', '³' => '3', '⁴' => '4',
                '⁵' => '5', '⁶' => '6', '⁷' => '7', '⁸' => '8', '⁹' => '9',
                '⁺' => '+', '⁻' => '-',
                '₀' => '0', '₁' => '1', '₂' => '2', '₃' => '3', '₄' => '4',
                '₅' => '5', '₆' => '6', '₇' => '7', '₈' => '8', '₉' => '9',
                '－' => '-', '−' => '-', '﹣' => '-',
                _ => c
            });
        }
        return sb.ToString();
    }

    // 科学计数法输出：mantissa×10^exp，用全角 × 和上标数字/负号，匹配答案键写法（如 1.3×10⁻⁹）
    private static string FormatScientific(double value, int mantissaDecimals)
    {
        if (value <= 0) return value.ToString(CultureInfo.InvariantCulture);
        var exp = (int)Math.Floor(Math.Log10(value));
        var mantissa = value / Math.Pow(10, exp);
        // 按 Ksp 尾数小数位格式化（至少保留；若被圆整为 0 则退化为 4 位有效）
        string manStr;
        if (mantissaDecimals > 0)
            manStr = mantissa.ToString($"F{mantissaDecimals}", CultureInfo.InvariantCulture);
        else
            manStr = mantissa.ToString("0.####", CultureInfo.InvariantCulture);
        if (double.TryParse(manStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed == 0)
            manStr = mantissa.ToString("0.####", CultureInfo.InvariantCulture);
        // 去掉多余尾随 0（但保留 Ksp 所需精度）
        if (manStr.Contains('.'))
            manStr = manStr.TrimEnd('0').TrimEnd('.');
        if (string.IsNullOrEmpty(manStr)) manStr = "1";
        return $"{manStr}×10{ExponentSuperscript(exp)}";
    }

    private static string ExponentSuperscript(double exp)
    {
        var i = (int)Math.Round(exp);
        var s = i.ToString(CultureInfo.InvariantCulture);
        var sb = new StringBuilder();
        foreach (var c in s)
        {
            sb.Append(c switch
            {
                '-' => '⁻', '+' => '⁺',
                '0' => '⁰', '1' => '¹', '2' => '²', '3' => '³', '4' => '⁴',
                '5' => '⁵', '6' => '⁶', '7' => '⁷', '8' => '⁸', '9' => '⁹',
                _ => c
            });
        }
        return sb.ToString();
    }
}
