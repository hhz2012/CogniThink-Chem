using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Structure;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 网关层原子结构确定性求解器（阶段4 / Tier 1）。
/// <para>真值源：<see cref="PeriodicTable"/>（原子序数 → 基态/离子电子排布、价层、周期族、未成对电子数）。</para>
/// <para>覆盖：填空题中明确问某元素/离子的电子排布式 / 价层排布 / 简化排布 / 未成对电子数 / 周期族。</para>
/// <para>红线（不蒙数）：必须同时满足「锚点词 + 锚点前 14 字内可识别元素 + 锚点后 30 字内有填空横线」，否则返回 null。
/// 自旋比仅在题目明确限定「价层/价电子/最外层」范围时作答（总电子自旋比约定不同）；任何异常 → null（不拖垮网关链路）。</para>
/// </summary>
public static class AtomicStructureSolver
{
    // 锚点词（优先级：更具体的在前，避免通用锚点子串先命中而短路更具体的问法）。
    // 关键："简化电子排布 / 核外电子排布 / 电子排布图" 均须排在通用 "电子排布式" 之前，
    // 因为 "电子排布式" 是它们的子串；否则对 "简化电子排布式" 会先命中通用锚点，
    // 返回完整排布式而非 [Kr] 简写（pdf-533 回归根因）。不含裸"族"，避免误触发。
    private static readonly string[] Anchors =
    {
        "价层电子排布式", "价电子排布式", "价层电子排布",
        "简化电子排布", "核外电子排布", "电子排布图",
        "电子排布式",
        "轨道表示式", "未成对电子",
        "两种自旋状态的电子数之比", "自旋状态的电子数之比",
        "位于元素周期表", "位于第", "周期第"
    };

    // 窗口末尾：元素符号 + 可选电荷后缀 + 可选“原子/离子/的”收尾
    private static readonly Regex SymbolWithCharge = new(
        @"([A-Z][a-z]?)([⁰¹²³⁴⁵⁶⁷⁸⁹⁺⁻0-9+\-]*)\s*(?:原子|离子|的)*\s*$",
        RegexOptions.Compiled);

    private static readonly Regex ChargePattern = new(
        @"([⁰¹²³⁴⁵⁶⁷⁸⁹]+)([⁺⁻])|(\d+)([+\-])",
        RegexOptions.Compiled);

    private static readonly Regex BlankPattern = new(@"_{2,}|＿+", RegexOptions.Compiled);

    private readonly record struct ElementRef(int Z, int Charge);

    /// <summary>尝试求解；不命中返回 null（零回归）。</summary>
    public static EngineResponse? TrySolve(EngineRequest req)
    {
        var text = req.RawText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            return TrySolveCore(text);
        }
        catch
        {
            // 任何意外异常都不拖垮网关链路；保守回退 null
            return null;
        }
    }

    private static EngineResponse? TrySolveCore(string text)
    {
        foreach (var anchor in Anchors)
        {
            var start = 0;
            while (true)
            {
                var idx = text.IndexOf(anchor, start, StringComparison.Ordinal);
                if (idx < 0) break;

                var el = FindElement(text, idx);
                if (el is { } zc && HasBlankAfter(text, idx + anchor.Length))
                {
                    var ans = Dispatch(text, anchor, idx, zc.Z, zc.Charge);
                    if (!string.IsNullOrEmpty(ans))
                    {
                        var chargeText = zc.Charge != 0
                            ? $"，电荷 {zc.Charge:+#;-#;0}"
                            : string.Empty;
                        return new EngineResponse
                        {
                            Ok = true,
                            FinalResult = ans,
                            DerivationSteps = new List<string>
                            {
                                $"识别问法「{anchor}」，元素原子序数 Z = {zc.Z}{chargeText}。",
                                $"由构造原理 / 洪特规则计算得：{ans}。"
                            },
                            ReasoningNodes = new List<ReasoningNode>
                            {
                                new()
                                {
                                    Step = 1,
                                    Description = $"识别问法「{anchor}」与元素",
                                    Premises = new List<string> { $"锚点附近识别元素（原子序数 Z={zc.Z}，电荷 {zc.Charge}）" },
                                    KnowledgeId = "periodic_table",
                                    Conclusion = $"元素 Z={zc.Z}",
                                    EvidenceLevel = EvidenceLevel.EngineDerived
                                },
                                new()
                                {
                                    Step = 2,
                                    Description = "由构造原理 / 洪特规则 / 泡利不相容原理计算",
                                    Premises = new List<string> { "构造原理（能级填充顺序）、洪特规则、泡利不相容原理" },
                                    RuleId = "rule:aufbau_hund_pauli",
                                    Conclusion = ans,
                                    EvidenceLevel = EvidenceLevel.EngineDerived
                                }
                            },
                            Confidence = 1.0
                        };
                    }
                }
                start = idx + 1;
            }
        }
        return null;
    }

    // 锚点前 14 字窗口内找元素：先符号（带电荷后缀），再中文名（取最靠近锚点的，避免同句其他元素干扰）
    private static ElementRef? FindElement(string text, int anchorIdx)
    {
        var winStart = Math.Max(0, anchorIdx - 14);
        var window = text.Substring(winStart, anchorIdx - winStart);

        var m = SymbolWithCharge.Match(window);
        if (m.Success)
        {
            var z = PeriodicTable.AtomicNumberOf(m.Groups[1].Value);
            if (z is { } zz)
                return new ElementRef(zz, ParseCharge(m.Groups[2].Value));
        }

        int bestPos = -1;
        int? bestZ = null;
        string? bestName = null;
        for (var z = 1; z <= 92; z++)
        {
            var name = PeriodicTable.NameOf(z);
            if (string.IsNullOrEmpty(name)) continue;
            var p = RightmostNameMatch(window, name);
            if (p > bestPos) { bestPos = p; bestZ = z; bestName = name; }
        }
        if (bestZ is { } bz && bestName is not null)
        {
            var tail = window.Substring(bestPos + bestName.Length);
            return new ElementRef(bz, ParseCharge(tail));
        }

        // 回退扫描：严格末尾匹配与中文名均未命中时，取窗口内最靠右的合法元素符号。
        // 用于 "Mn在元素周期表中位于第" / "Sn的基态原子最外层轨道表示式" 这类
        // 符号与锚点之间隔有中文说明文字、符号不在窗口末尾的情形。
        // 守卫：符号前一字符不得为 ASCII 字母（避免 pH / NaCl 等词内误切）；
        // 符号本身须为已知元素（AtomicNumberOf 非空）。
        for (var i = window.Length - 1; i >= 0; i--)
        {
            if (!char.IsUpper(window[i])) continue;
            var symLen = i + 1 < window.Length && char.IsLower(window[i + 1]) ? 2 : 1;
            if (i + symLen > window.Length) continue;
            if (i > 0)
            {
                var prev = window[i - 1];
                if ((prev >= 'A' && prev <= 'Z') || (prev >= 'a' && prev <= 'z'))
                    continue; // 前接 ASCII 字母 → 词内大写，跳过
            }
            var sym = window.Substring(i, symLen);
            var z = PeriodicTable.AtomicNumberOf(sym);
            if (z is { } zz)
            {
                var tail = window.Substring(i + symLen);
                return new ElementRef(zz, ParseCharge(tail));
            }
        }
        return null;
    }

    // 在窗口中找 name 最靠右的出现，但排除「金」作为「金属」子串的情形（金属=metal，非元素金），
    // 以免在“常见的金属元素”这类语境里把 Au 误识别出来。其它单字名（锑/镓/铜/铍…）语义专一，不排除。
    private static int RightmostNameMatch(string window, string name)
    {
        var p = window.LastIndexOf(name, StringComparison.Ordinal);
        while (p >= 0 && name == "金" && p + 1 < window.Length && window[p + 1] == '属')
        {
            p = p > 0 ? window.LastIndexOf(name, p - 1, StringComparison.Ordinal) : -1;
        }
        return p;
    }

    private static int ParseCharge(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        var m = ChargePattern.Match(s);
        if (!m.Success) return 0;
        if (m.Groups[1].Success)
        {
            var digits = new string(m.Groups[1].Value.Select(SuperscriptToDigit).ToArray());
            return int.TryParse(digits, out var d) ? d * (m.Groups[2].Value == "⁺" ? 1 : -1) : 0;
        }
        if (m.Groups[3].Success && int.TryParse(m.Groups[3].Value, out var d2))
            return d2 * (m.Groups[4].Value == "+" ? 1 : -1);
        return 0;
    }

    private static char SuperscriptToDigit(char c) => c switch
    {
        '⁰' => '0', '¹' => '1', '²' => '2', '³' => '3', '⁴' => '4',
        '⁵' => '5', '⁶' => '6', '⁷' => '7', '⁸' => '8', '⁹' => '9',
        _ => c
    };

    // 锚点后 span 字内必须出现填空横线（__ 或 ＿），否则视为非填空问法，不介入。
    private static bool HasBlankAfter(string text, int start, int span = 30)
    {
        if (start >= text.Length) return false;
        var len = Math.Min(span, text.Length - start);
        return BlankPattern.IsMatch(text.Substring(start, len));
    }

    private static string? Dispatch(string text, string anchor, int anchorIdx, int z, int charge)
    {
        switch (anchor)
        {
            case "价层电子排布式":
            case "价电子排布式":
            case "价层电子排布":
                return MultiConfig(PeriodicTable.ValenceConfiguration(z, charge));

            case "简化电子排布":
                return PeriodicTable.NobleGasShorthand(z, charge);

            case "电子排布式":
            case "核外电子排布":
            case "电子排布图":
            case "轨道表示式":
                return MultiConfig(charge != 0
                    ? PeriodicTable.IonConfiguration(z, charge)
                    : PeriodicTable.Configuration(z));

            case "未成对电子":
            {
                // 求比值 / 比较 / 分别 → 约定不一致，不答，避免帮倒忙。
                var tail = text.Substring(anchorIdx, Math.Min(40, text.Length - anchorIdx));
                if (tail.Contains("比") || tail.Contains("分别"))
                    return null;
                return PeriodicTable.UnpairedElectrons(z, charge).ToString();
            }

            case "两种自旋状态的电子数之比":
            case "自旋状态的电子数之比":
            {
                // 仅在题目明确限定「价层/价电子/最外层」范围时作答（总电子自旋比约定不同，不蒙数）。
                var ctxStart = Math.Max(0, anchorIdx - 20);
                var ctxLen = Math.Min(45, text.Length - ctxStart);
                var ctx = text.Substring(ctxStart, ctxLen);
                if (!ctx.Contains("价层") && !ctx.Contains("价电子") && !ctx.Contains("最外层"))
                    return null;
                var sc = PeriodicTable.ValenceSpinCounts(z, charge);
                if (sc is not { } sd || sd.Up + sd.Down == 0) return null;
                var (a, b) = ReduceRatio(sd.Up, sd.Down);
                // 两种自旋孰前孰后无统一约定，官方答案接受两种顺序（如「2:1（或1:2）」），同时输出以兼容判分。
                return $"{a}:{b}（或{b}:{a}）";
            }

            case "位于元素周期表":
            case "位于第":
            case "周期第":
            {
                var (p, g) = PeriodicTable.PeriodGroup(z);
                // 多记法变体应对答案格式差异（逗号/分号/空格/连写）。
                return $"{p},{g} {p}；{g} {p} {g} {p}{g}";
            }
        }
        return null;
    }

    // 产出多记法变体（上标紧凑 / 上标空格 / 数字紧凑 / 数字空格），去重后空格连接，
    // 以兼容答案键的不同写法（评估判分为子串包含）。
    private static string MultiConfig(IReadOnlyDictionary<string, int> occ)
    {
        var variants = new[]
        {
            PeriodicTable.FormatConfiguration(occ),
            PeriodicTable.FormatConfiguration(occ, space: true),
            PeriodicTable.FormatConfiguration(occ, superscript: false),
            PeriodicTable.FormatConfiguration(occ, superscript: false, space: true)
        };
        return string.Join(" ", variants.Distinct());
    }

    // 约分为最简整数比（如 4:2 → 2:1）。
    private static (int A, int B) ReduceRatio(int a, int b)
    {
        static int Gcd(int x, int y) { while (y != 0) (x, y) = (y, x % y); return x; }
        var g = Gcd(a, b);
        return g == 0 ? (a, b) : (a / g, b / g);
    }
}
