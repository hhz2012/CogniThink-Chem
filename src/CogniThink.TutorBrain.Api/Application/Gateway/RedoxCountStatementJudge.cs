using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Balancing;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 氧化还原反应计数陈述判定原语（spec redox-statement-judge-pdf075 FR-2 / Task 3）：判定
/// "反应ⅰ～ⅳ中有 N 个氧化还原反应" / "上述反应中氧化还原反应有 N 个" 类陈述的真伪。
/// <para>真值源：对范围内每个带标签方程式调用 <see cref="RedoxBalancer.SplitHalfReactions"/>，
/// 统计 <c>IsRedox</c> 为真的个数，与陈述声称值 N 比对。引擎计算是唯一真值源。</para>
/// <para>红线（不蒙数、零假阳性）：仅当<b>能确定标签范围、范围内每个标签方程都可解析配平、
/// 声称计数 N 可唯一提取</b>时返回 True/False；任一环节不满足 → <see cref="Verdict.Undecided"/>。</para>
/// </summary>
internal sealed class RedoxCountStatementJudge : IStatementJudge
{
    public Verdict Judge(string stem, string statement)
    {
        if (string.IsNullOrEmpty(stem) || string.IsNullOrEmpty(statement))
            return Verdict.Undecided;

        var s = RedoxStatementPrimitives.NormalizeText(statement);

        // 必须提及"氧化还原反应"
        if (!s.Contains("氧化还原反应") && !s.Contains("氧还反应"))
            return Verdict.Undecided;

        // 提取声称计数 N
        var claimed = ParseClaimedCount(s);
        if (claimed is null) return Verdict.Undecided;

        // 确定计数范围：显式标签区间，或"上述/下列/以上/各反应"指代全部标签方程
        var range = ParseRange(s);
        var all = range is null && RefersToAllReactions(s);
        if (range is null && !all) return Verdict.Undecided;

        var labeled = RedoxStatementPrimitives.AllLabeledEquations(stem);
        if (labeled.Count == 0) return Verdict.Undecided;

        List<(int label, string equation)> scope;
        if (range is not null)
        {
            var (a, b) = range.Value;
            var lo = Math.Min(a, b);
            var hi = Math.Max(a, b);
            var expected = Enumerable.Range(lo, hi - lo + 1).ToHashSet();
            var have = labeled.Select(x => x.label).ToHashSet();
            // 范围内任一标签方程不可解析 → 计数不确定，保守不判
            if (!expected.IsSubsetOf(have)) return Verdict.Undecided;
            scope = labeled.Where(x => x.label >= lo && x.label <= hi).ToList();
        }
        else
        {
            scope = labeled;
        }

        if (scope.Count == 0) return Verdict.Undecided;

        // 逐个判定是否氧还；任一方程分析失败 → 整体不确定
        var trueCount = 0;
        foreach (var (_, eq) in scope)
        {
            var rx = RedoxBalancer.SplitHalfReactions(eq);
            if (!rx.Success) return Verdict.Undecided;
            if (rx.IsRedox) trueCount++;
        }

        return trueCount == claimed.Value ? Verdict.True : Verdict.False;
    }

    // ===================== 声称计数解析 =====================

    private static readonly Regex CountBeforeRe = new(
        @"([0-9]+)\s*[个项种步]\s*(?:是|为)?\s*(?:氧化还原|氧还)反应", RegexOptions.Compiled);

    private static readonly Regex CountAfterRe = new(
        @"(?:氧化还原|氧还)反应(?:的)?(?:数目|个数)?(?:有|为|是|共)\s*([0-9]+)\s*[个项种步]?",
        RegexOptions.Compiled);

    private static readonly Dictionary<char, int> ChineseDigits = new()
    {
        ['一'] = 1, ['二'] = 2, ['三'] = 3, ['四'] = 4, ['五'] = 5,
        ['六'] = 6, ['七'] = 7, ['八'] = 8, ['九'] = 9, ['十'] = 10,
    };

    private static int? ParseClaimedCount(string s)
    {
        var m1 = CountBeforeRe.Match(s);
        if (m1.Success && int.TryParse(m1.Groups[1].Value, out var c1)) return c1;
        var m2 = CountAfterRe.Match(s);
        if (m2.Success && int.TryParse(m2.Groups[1].Value, out var c2)) return c2;

        // 中文数字："三个氧化还原反应" / "氧化还原反应有三个"
        var idxKw = s.IndexOf("氧化还原反应", StringComparison.Ordinal);
        if (idxKw < 0) idxKw = s.IndexOf("氧还反应", StringComparison.Ordinal);
        if (idxKw >= 0)
        {
            // 关键词前紧邻"X个"
            for (var i = idxKw - 1; i >= 0 && i >= idxKw - 6; i--)
            {
                if (s[i] == '个' || s[i] == '项' || s[i] == '种' || s[i] == '步')
                {
                    if (i - 1 >= 0 && ChineseDigits.TryGetValue(s[i - 1], out var cd)) return cd;
                }
            }
        }
        return null;
    }

    // ===================== 范围解析 =====================

    // 反应ⅰ～ⅳ / 反应①~④ / 反应(1)~(4) / 反应一到四 / ⅰ—ⅳ 等
    private static readonly Regex RangeRe = new(
        @"([\u2160-\u2179\u2460-\u2469\d一二三四五六七八九十]+)\s*[～~\-—到至]\s*([\u2160-\u2179\u2460-\u2469\d一二三四五六七八九十]+)",
        RegexOptions.Compiled);

    private static (int a, int b)? ParseRange(string s)
    {
        // 只在"反应"上下文附近采信区间，避免把无关的"1~2"误当反应范围
        var fanIdx = s.IndexOf("反应", StringComparison.Ordinal);
        foreach (Match m in RangeRe.Matches(s))
        {
            if (fanIdx >= 0 && m.Index > fanIdx + 8) continue; // 远离"反应"关键词 → 不信
            var a = RedoxStatementPrimitives.LabelValue(m.Groups[1].Value);
            var b = RedoxStatementPrimitives.LabelValue(m.Groups[2].Value);
            if (a is not null && b is not null && a.Value != b.Value)
                return (a.Value, b.Value);
        }
        return null;
    }

    private static bool RefersToAllReactions(string s)
    {
        return s.Contains("上述反应") || s.Contains("下列反应") || s.Contains("以上反应")
            || s.Contains("以下反应") || s.Contains("各反应") || s.Contains("全部反应")
            || s.Contains("这些反应");
    }
}
