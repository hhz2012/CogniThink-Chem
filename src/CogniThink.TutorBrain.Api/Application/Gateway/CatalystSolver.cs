using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 催化剂识别求解器（reaction_check 兜底）。
/// <para>域：题干设问「催化剂为 ____」，并给出带圈编号（①②…）的多步反应；
/// 由净计量法判别催化剂——先配平循环（使中间体抵消），催化剂满足：净计量为 0 且「先被消耗、后被再生」。</para>
/// <para>红线（不蒙数）：仅处理两步循环（高中主流），须唯一识别出一种催化剂；
/// 循环不可配平 / 候选不唯一 / 物种解析不全 → 返回 null。采纳门必经答案键校验 → precision=1、零 harm。</para>
/// </summary>
public static class CatalystSolver
{
    private const string Arrows = "═→⟶＝";

    private static readonly Regex LeadingCoefRe = new(
        @"^\s*(\d+(?:\.\d+)?)\s*", RegexOptions.Compiled);

    public static EngineResponse? TrySolve(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // 1. 严格触发：设问催化剂 + 存在带圈编号的多步反应
        if (!raw.Contains("催化剂")) return null;
        bool asks = raw.Contains("催化剂为") || raw.Contains("催化剂是") || raw.Contains("作催化剂");
        if (!asks) return null;
        if (raw.IndexOf('①') < 0 || raw.IndexOf('②') < 0) return null;

        // 2. 解析各步反应：物种 → (左侧系数, 右侧系数)
        var steps = ParseSteps(raw);
        if (steps.Count != 2) return null; // 仅处理两步循环（不蒙数）
        var (a, b) = (steps[0], steps[1]);

        // 3. 每物种在每步的净系数 v = 右 − 左（缺省为 0）
        var allSpecies = a.Keys.Union(b.Keys).ToList();
        double V(string s, Dictionary<string, (double l, double r)> step) =>
            step.TryGetValue(s, out var x) ? x.r - x.l : 0d;

        // 4. 跨步骤物种（中间体 / 催化剂 / 可能的介质离子）→ 推导循环计量比 λ1:λ2
        var cross = allSpecies.Where(s => a.ContainsKey(s) && b.ContainsKey(s)).ToList();
        if (cross.Count == 0) return null;

        var ratios = new Dictionary<(long, long), int>();
        foreach (var s in cross)
        {
            var (p, q) = Reduce(-V(s, b), V(s, a)); // λ1·v1 + λ2·v2 = 0 → λ1:λ2 = -v2 : v1
            if (p <= 0 || q <= 0) continue;         // 仅接受正向循环（λ1、λ2 均 > 0）
            ratios[(p, q)] = ratios.TryGetValue((p, q), out var n) ? n + 1 : 1;
        }
        if (ratios.Count == 0) return null;
        // 循环比须由多数跨步骤物种一致给出（中间体与催化剂共享同一比值；
        // 净消耗/生成的介质离子比值不同，自然淘汰）。比值不唯一 → 不蒙数。
        int maxCount = ratios.Values.Max();
        var winners = ratios.Where(kv => kv.Value == maxCount).ToList();
        if (winners.Count != 1) return null;
        var mode = winners[0].Key;
        double λ1 = mode.Item1, λ2 = mode.Item2;

        // 5. 催化剂：净计量为 0 且先消耗（左侧某步）后再生（右侧更晚的步）
        string? catalyst = null;
        int candidateCount = 0;
        foreach (var s in allSpecies)
        {
            double net = λ1 * V(s, a) + λ2 * V(s, b);
            if (Math.Abs(net) > 1e-6) continue;
            int minLeft = MinStepOnLeft(s, a, b);
            int maxRight = MaxStepOnRight(s, a, b);
            if (minLeft > 0 && maxRight > 0 && minLeft < maxRight)
            {
                catalyst ??= s;
                candidateCount++;
            }
        }
        if (candidateCount != 1 || catalyst is null) return null; // 不唯一 → 不蒙数

        string final =
            $"{catalyst} 在反应①中被消耗、反应②中又再生，配平循环后其净计量为 0，" +
            $"反应前后质量与化学性质不变，故为该过程的催化剂。答案：{catalyst}";

        return new EngineResponse
        {
            Ok = true,
            FinalResult = final,
            DerivationSteps = new List<string>
            {
                $"识别两步循环，跨步骤物种：{string.Join("、", cross)}",
                $"循环计量比 λ1:λ2 = {λ1:0.##}:{λ2:0.##}（使中间体抵消）",
                $"净计量为 0 且先消耗后再生的物种唯一：{catalyst}"
            },
            ConstraintNote = "【高中限定】两步循环催化剂识别，已校验循环可配平与候选唯一性",
            Confidence = 1.0
        };
    }

    private static int MinStepOnLeft(string s,
        Dictionary<string, (double l, double r)> a,
        Dictionary<string, (double l, double r)> b)
    {
        int min = int.MaxValue;
        if (a.TryGetValue(s, out var x) && x.l > 0) min = Math.Min(min, 1);
        if (b.TryGetValue(s, out var y) && y.l > 0) min = Math.Min(min, 2);
        return min == int.MaxValue ? 0 : min;
    }

    private static int MaxStepOnRight(string s,
        Dictionary<string, (double l, double r)> a,
        Dictionary<string, (double l, double r)> b)
    {
        int max = 0;
        if (a.TryGetValue(s, out var x) && x.r > 0) max = Math.Max(max, 1);
        if (b.TryGetValue(s, out var y) && y.r > 0) max = Math.Max(max, 2);
        return max;
    }

    /// <summary>解析所有带圈编号的反应步：每步为 物种→(左系数,右系数)。</summary>
    private static List<Dictionary<string, (double l, double r)>> ParseSteps(string text)
    {
        var result = new List<Dictionary<string, (double l, double r)>>();
        var matches = Regex.Matches(text, @"([①-⑳])\s*([^①-⑳\n]+)");
        foreach (Match m in matches)
        {
            var body = m.Groups[2].Value.Replace('＋', '+');
            if (!ContainsArrow(body)) continue;
            var (left, right) = SplitAtArrow(body);
            var map = new Dictionary<string, (double l, double r)>(StringComparer.Ordinal);
            foreach (var (coef, sp) in Tokenize(left))
                map[sp] = (coef, 0);
            foreach (var (coef, sp) in Tokenize(right))
            {
                if (map.TryGetValue(sp, out var ex))
                    map[sp] = (ex.l, coef);
                else
                    map[sp] = (0, coef);
            }
            if (map.Count > 0) result.Add(map);
        }
        return result;
    }

    private static bool ContainsArrow(string s) =>
        s.Contains("===") || s.IndexOfAny(Arrows.ToCharArray()) >= 0 || s.Contains('=');

    private static (string left, string right) SplitAtArrow(string s)
    {
        int idx, len;
        if (s.Contains("===", StringComparison.Ordinal)) { idx = s.IndexOf("===", StringComparison.Ordinal); len = 3; }
        else { idx = s.IndexOfAny(Arrows.ToCharArray()); len = 1; }
        if (idx < 0) { idx = s.IndexOf('='); len = 1; }
        if (idx <= 0) return (s, "");
        return (s.Substring(0, idx), s.Substring(idx + len));
    }

    private static List<(double coef, string species)> Tokenize(string side)
    {
        var list = new List<(double, string)>();
        foreach (var raw in Regex.Split(side, @"\s+\+\s*"))
        {
            var token = raw.Trim();
            if (token.Length == 0) continue;
            double coef = 1d;
            var cm = LeadingCoefRe.Match(token);
            string rest = token;
            if (cm.Success && double.TryParse(cm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var cv))
            {
                coef = cv;
                rest = token.Substring(cm.Length).Trim();
            }
            // 物种归一化：去除内部空白，保证跨步骤同一物种匹配一致
            rest = Regex.Replace(rest, @"\s+", "");
            if (rest.Length == 0) continue;
            list.Add((coef, rest));
        }
        return list;
    }

    /// <summary>约分并规范化比值符号：返回 (p, q)，p>0；不可约时 q 可为负。</summary>
    private static (long, long) Reduce(double a, double b)
    {
        long la = (long)Math.Round(a), lb = (long)Math.Round(b);
        if (la == 0 || lb == 0) return (0, 0);
        long g = Gcd(Math.Abs(la), Math.Abs(lb));
        la /= g; lb /= g;
        if (la < 0) { la = -la; lb = -lb; }
        return (la, lb);
    }

    private static long Gcd(long x, long y) => y == 0 ? x : Gcd(y, x % y);
}
