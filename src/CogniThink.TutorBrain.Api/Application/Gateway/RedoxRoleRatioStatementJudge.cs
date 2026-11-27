using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Balancing;
using CogniThink.Chem.Core.Chemistry;
using CogniThink.Chem.Core.Math;
using CogniThink.Chem.Core.Parsing;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 氧化还原角色 / 比值陈述判定原语（spec redox-statement-judge-pdf075 FR-3 / Task 4）：判定
/// "X 是氧化剂/还原剂/既是氧化剂又是还原剂/只作氧化剂/氧化产物/还原产物" 与
/// "被氧化与被还原的物质的量之比为 a:b" 类陈述。
/// <para>真值源：<see cref="RedoxBalancer.SplitHalfReactions"/> 的氧化态变化 + 配平系数；
/// 产物侧辅以 <see cref="OxidationStateAnalyzer.Assign"/> 逐物种氧化态。引擎计算是唯一真值源。</para>
/// <para>红线（不蒙数、零假阳性）：标签方程不可解析配平 / 物种歧义 / 非歧化的跨元素比值（物种与原子
/// 比值语义不一致）→ 一律 <see cref="Verdict.Undecided"/>。比值仅在单一变价元素（歧化）时判定，
/// 此时元素物质的量比 == 被氧化/被还原物质的量比。</para>
/// </summary>
internal sealed class RedoxRoleRatioStatementJudge : IStatementJudge
{
    public Verdict Judge(string stem, string statement)
    {
        if (string.IsNullOrEmpty(stem) || string.IsNullOrEmpty(statement))
            return Verdict.Undecided;

        var s = RedoxStatementPrimitives.NormalizeText(statement);

        if (TryParseRole(s, out var speciesToken, out var role)
            && speciesToken is not null && role.HasValue)
            return EvaluateRole(stem, statement, speciesToken, role.Value);

        if (TryParseRatio(s, out var ox, out var red) && ox.HasValue && red.HasValue)
            return EvaluateRatio(stem, statement, ox.Value, red.Value);

        return Verdict.Undecided;
    }

    // ===================== 角色 =====================

    private enum RoleKind { Oxidant, Reductant, Both, OxidantOnly, ReductantOnly, OxidationProduct, ReductionProduct }

    private static readonly Regex BothRe = new(
        @"([A-Za-z][A-Za-z0-9()^+\-]*)\s*既\s*是\s*(氧化剂|还原剂)\s*又\s*是\s*(氧化剂|还原剂)",
        RegexOptions.Compiled);

    private static readonly Regex OnlyRe = new(
        @"([A-Za-z][A-Za-z0-9()^+\-]*)\s*只\s*作\s*(氧化剂|还原剂)", RegexOptions.Compiled);

    private static readonly Regex ProductRe = new(
        @"([A-Za-z][A-Za-z0-9()^+\-]*)\s*(?:是|为|作)\s*(氧化产物|还原产物)", RegexOptions.Compiled);

    private static readonly Regex PlainRe = new(
        @"([A-Za-z][A-Za-z0-9()^+\-]*)\s*(?:是|为|作)\s*(氧化剂|还原剂)", RegexOptions.Compiled);

    private static bool TryParseRole(string s, out string? species, out RoleKind? role)
    {
        species = null; role = null;

        var mBoth = BothRe.Match(s);
        if (mBoth.Success)
        {
            var k1 = mBoth.Groups[2].Value; var k2 = mBoth.Groups[3].Value;
            if (k1 == k2) return false;           // 同为氧化或同为还原 → 不是"既是又是"
            species = mBoth.Groups[1].Value; role = RoleKind.Both; return true;
        }
        var mOnly = OnlyRe.Match(s);
        if (mOnly.Success)
        {
            species = mOnly.Groups[1].Value;
            role = mOnly.Groups[2].Value == "氧化剂" ? RoleKind.OxidantOnly : RoleKind.ReductantOnly;
            return true;
        }
        var mProd = ProductRe.Match(s);
        if (mProd.Success)
        {
            species = mProd.Groups[1].Value;
            role = mProd.Groups[2].Value == "氧化产物" ? RoleKind.OxidationProduct : RoleKind.ReductionProduct;
            return true;
        }
        var mPlain = PlainRe.Match(s);
        if (mPlain.Success)
        {
            species = mPlain.Groups[1].Value;
            role = mPlain.Groups[2].Value == "氧化剂" ? RoleKind.Oxidant : RoleKind.Reductant;
            return true;
        }
        return false;
    }

    private static Verdict EvaluateRole(string stem, string statement, string speciesToken, RoleKind role)
    {
        Species sp;
        try { sp = Species.Parse(speciesToken); }
        catch (FormulaException) { return Verdict.Undecided; }
        if (string.IsNullOrEmpty(sp.Body)) return Verdict.Undecided;

        var equation = ResolveEquation(stem, statement);
        if (equation is null) return Verdict.Undecided;
        if (!EquationParser.TryParse(equation, out var parsed, out _) || parsed is null)
            return Verdict.Undecided;

        var terms = parsed.AllTerms.ToList();
        var leftCount = parsed.Left.Count;
        var leftIdx = new List<int>(); var rightIdx = new List<int>();
        for (var i = 0; i < terms.Count; i++)
        {
            if (!string.Equals(terms[i].Species.Body, sp.Body, StringComparison.Ordinal)) continue;
            if (i < leftCount) leftIdx.Add(i); else rightIdx.Add(i);
        }
        var isProductRole = role is RoleKind.OxidationProduct or RoleKind.ReductionProduct;
        // 反应物角色要求物种只在左侧；产物角色要求只在右侧；两侧都出现 → 歧义不判
        if (isProductRole)
        {
            if (rightIdx.Count == 0 || leftIdx.Count > 0) return Verdict.Undecided;
        }
        else
        {
            if (leftIdx.Count == 0 || rightIdx.Count > 0) return Verdict.Undecided;
        }

        var rx = RedoxBalancer.SplitHalfReactions(equation);
        if (!rx.Success) return Verdict.Undecided;
        // 非氧化还原反应：没有任何物质被氧化/还原，相关角色陈述均为假
        if (!rx.IsRedox) return Verdict.False;

        var oxidizedInX = rx.Changes.Any(c => c.IsOxidation && sp.Elements.ContainsKey(c.Element));
        var reducedInX = rx.Changes.Any(c => !c.IsOxidation && sp.Elements.ContainsKey(c.Element));
        // 物种不含任何变价元素 → 旁观物种，不是氧化剂/还原剂/氧化/还原产物
        if (!oxidizedInX && !reducedInX)
            return Verdict.False;

        return role switch
        {
            RoleKind.Oxidant => reducedInX ? Verdict.True : Verdict.False,
            RoleKind.Reductant => oxidizedInX ? Verdict.True : Verdict.False,
            RoleKind.Both => (oxidizedInX && reducedInX) ? Verdict.True : Verdict.False,
            RoleKind.OxidantOnly => (reducedInX && !oxidizedInX) ? Verdict.True : Verdict.False,
            RoleKind.ReductantOnly => (oxidizedInX && !reducedInX) ? Verdict.True : Verdict.False,
            RoleKind.OxidationProduct => EvaluateProductRole(sp, rx, oxidized: true),
            RoleKind.ReductionProduct => EvaluateProductRole(sp, rx, oxidized: false),
            _ => Verdict.Undecided
        };
    }

    private static Verdict EvaluateProductRole(Species sp, RedoxAnalysis rx, bool oxidized)
    {
        var assign = OxidationStateAnalyzer.Assign(sp);
        if (!assign.Success) return Verdict.Undecided;

        foreach (var (element, _) in sp.Elements)
        {
            var changes = rx.Changes.Where(c => c.Element == element).ToList();
            if (changes.Count == 0) continue;

            // FromText/ToText 是该元素在对应侧的「全部」氧化态集合（非单一半反应的跃迁），
            // 歧化时同一元素的氧化/还原两条 change 共享这组文本。据此分别筛选氧化/还原目标态：
            //   氧化产物 → 产物中该元素的态属于「右侧高于某左侧」的态；还原产物 → 右侧低于某左侧。
            var fromStates = ParseStates(changes.First().FromText);
            var toStates = ParseStates(changes.First().ToText);
            if (fromStates is null || toStates is null
                || fromStates.Count == 0 || toStates.Count == 0) return Verdict.Undecided;

            if (!assign.States.TryGetValue(element, out var xStates) || xStates.Count == 0)
                return Verdict.Undecided;

            var xSet = new HashSet<int>();
            foreach (var (v, _) in xStates)
            {
                if (!v.IsInteger) return Verdict.Undecided;
                xSet.Add((int)v.Num);
            }

            var targets = new HashSet<int>();
            foreach (var t in toStates)
            {
                foreach (var f in fromStates)
                {
                    if (oxidized ? t > f : t < f) { targets.Add(t); break; }
                }
            }
            if (xSet.Overlaps(targets)) return Verdict.True;
        }
        return Verdict.False;
    }

    // ===================== 比值（仅单一变价元素 = 歧化，避免物种/原子比歧义） =====================

    private static bool TryParseRatio(string s, out int? oxidized, out int? reduced)
    {
        oxidized = null; reduced = null;
        var m = Regex.Match(s,
            @"被氧化\s*(?:与|和)\s*被还原(?:的)?(?:物质的量)?(?:之比|比)(?:为|是)?\s*(\d+)\s*[:：]\s*(\d+)");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var a) && int.TryParse(m.Groups[2].Value, out var b))
        { oxidized = a; reduced = b; return true; }
        var m2 = Regex.Match(s,
            @"被还原\s*(?:与|和)\s*被氧化(?:的)?(?:物质的量)?(?:之比|比)(?:为|是)?\s*(\d+)\s*[:：]\s*(\d+)");
        if (m2.Success && int.TryParse(m2.Groups[1].Value, out var b2) && int.TryParse(m2.Groups[2].Value, out var a2))
        { oxidized = a2; reduced = b2; return true; }
        return false;
    }

    private static Verdict EvaluateRatio(string stem, string statement, int claimedOx, int claimedRed)
    {
        var equation = ResolveEquation(stem, statement);
        if (equation is null) return Verdict.Undecided;

        var rx = RedoxBalancer.SplitHalfReactions(equation);
        if (!rx.Success || !rx.IsRedox || !rx.ElectronConservation) return Verdict.Undecided;

        var elements = rx.Changes.Select(c => c.Element).Distinct(StringComparer.Ordinal).ToList();
        if (elements.Count != 1) return Verdict.Undecided;   // 跨元素 → 物种/原子比歧义，不判
        var element = elements[0];

        if (!EquationParser.TryParse(equation, out var parsed, out _) || parsed is null)
            return Verdict.Undecided;
        var bal = EquationBalancer.Balance(parsed);
        if (!bal.Success) return Verdict.Undecided;

        var terms = parsed.AllTerms.ToList();
        var leftCount = parsed.Left.Count;
        // 取该元素左右两侧氧化数的多重集（系数 × 原子数），排序后逐位配对，
        // 统计被氧化（右 > 左）与被还原（右 < 左）的原子物质的量之比。
        // 不依赖 Core 的 OxidationChange.FromText/ToText（歧化时为该侧全集，非单一半反应跃迁）。
        var leftVals = CollectElementValues(terms, bal.Coefficients, leftCount, element, isLeft: true);
        var rightVals = CollectElementValues(terms, bal.Coefficients, leftCount, element, isLeft: false);
        if (leftVals.Count == 0 || leftVals.Count != rightVals.Count) return Verdict.Undecided;
        leftVals.Sort(); rightVals.Sort();

        long oxidizedAtoms = 0, reducedAtoms = 0;
        for (var k = 0; k < leftVals.Count; k++)
        {
            var diff = rightVals[k] - leftVals[k];
            if (diff > 0) oxidizedAtoms++;
            else if (diff < 0) reducedAtoms++;
        }
        if (oxidizedAtoms == 0 || reducedAtoms == 0) return Verdict.Undecided;

        var (trueOx, trueRed) = SimplifyIntPair(oxidizedAtoms, reducedAtoms);
        return trueOx == claimedOx && trueRed == claimedRed ? Verdict.True : Verdict.False;
    }

    // ===================== 共用 =====================

    private static string? ResolveEquation(string stem, string statement)
    {
        var label = RedoxStatementPrimitives.FindLabelRef(statement);
        if (label is not null)
        {
            var labeled = RedoxStatementPrimitives.ResolveLabeled(stem, label.Value);
            if (labeled is not null) return labeled;
        }
        return RedoxStatementPrimitives.ResolveUnique(stem);
    }

    private static HashSet<int>? ParseStates(string text)
    {
        var set = new HashSet<int>();
        foreach (var part in text.Split(',', '，', ' '))
        {
            var t = part.Trim().TrimStart('+');
            if (t.Length == 0) continue;
            if (!int.TryParse(t, out var v)) return null;
            set.Add(v);
        }
        return set.Count == 0 ? null : set;
    }

    // 收集指定元素在某一侧（左/右）的氧化数多重集：系数 × 原子数重复该值。
    private static List<int> CollectElementValues(
        List<ParsedTerm> terms, Rational[] coeff, int leftCount, string element, bool isLeft)
    {
        var vals = new List<int>();
        for (var i = 0; i < terms.Count; i++)
        {
            if ((i < leftCount) != isLeft) continue;
            var c = coeff[i];
            if (c.Den != 1 || c.IsZero) return new List<int>();   // 非整数系数 → 多重集语义不明，交由上层判 Undecided
            long coef = c.Num;
            var assign = OxidationStateAnalyzer.Assign(terms[i].Species);
            if (!assign.Success) continue;
            if (!assign.States.TryGetValue(element, out var list) || list.Count == 0) continue;
            foreach (var (v, cnt) in list)
            {
                if (!v.IsInteger) continue;
                for (var j = 0; j < coef * cnt; j++) vals.Add((int)v.Num);
            }
        }
        return vals;
    }

    // 把 (ox, red) 两个正整数化为最简正整数对
    private static (int ox, int red) SimplifyIntPair(long a, long b)
    {
        if (a <= 0 || b <= 0) return (0, 0);
        long g = Gcd(a, b);
        return ((int)(a / g), (int)(b / g));
    }

    private static long Gcd(long a, long b) { a = Math.Abs(a); b = Math.Abs(b); while (b != 0) { a %= b; (a, b) = (b, a); } return a; }
}
