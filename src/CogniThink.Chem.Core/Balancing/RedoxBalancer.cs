using CogniThink.Chem.Core.Chemistry;
using CogniThink.Chem.Core.Common;
using CogniThink.Chem.Core.Math;
using CogniThink.Chem.Core.Parsing;

namespace CogniThink.Chem.Core.Balancing;

/// <summary>某一元素的氧化数变化摘要。</summary>
public sealed class OxidationChange
{
    /// <summary>元素符号。</summary>
    public string Element { get; init; } = "";

    /// <summary>反应物侧氧化数（多个时以逗号分隔，如 "-3, +5"）。</summary>
    public string FromText { get; init; } = "";

    /// <summary>生成物侧氧化数。</summary>
    public string ToText { get; init; } = "";

    /// <summary>该元素总失电子数（氧化，Δ&gt;0）或得电子数（还原，Δ&lt;0）的绝对值。</summary>
    public Rational Electrons { get; init; }

    /// <summary>true = 被氧化（化合价升高）。</summary>
    public bool IsOxidation { get; init; }
}

/// <summary>半反应推导结果。</summary>
public sealed class HalfReactionResult
{
    /// <summary>半反应表达式，如 "MnO4^- + 8H^+ + 5e^- = Mn^2+ + 4H2O"。</summary>
    public string Expression { get; init; } = "";

    /// <summary>转移电子数（正数）。</summary>
    public Rational Electrons { get; init; }

    /// <summary>"氧化" 或 "还原"。</summary>
    public string Kind { get; init; } = "";
}

/// <summary>氧化还原分析结果。</summary>
public sealed class RedoxAnalysis
{
    public bool Success { get; init; }

    /// <summary>是否为氧化还原反应（存在氧化数变化）。</summary>
    public bool IsRedox { get; init; }

    public string? FailReason { get; init; }

    public string RiskTag { get; init; } = RiskTags.None;

    /// <summary>各元素氧化数变化。</summary>
    public IReadOnlyList<OxidationChange> Changes { get; init; } = Array.Empty<OxidationChange>();

    /// <summary>总转移电子数（按配平系数计）。</summary>
    public Rational ElectronsTransferred { get; init; }

    /// <summary>失电子总数 == 得电子总数。</summary>
    public bool ElectronConservation { get; init; }

    /// <summary>氧化半反应（推导失败为 null，原因见 FailReason）。</summary>
    public HalfReactionResult? OxidationHalf { get; init; }

    /// <summary>还原半反应。</summary>
    public HalfReactionResult? ReductionHalf { get; init; }
}

/// <summary>
/// 氧化还原分析器：氧化数判定 + 电子守恒校验 + 半反应拆解（离子-电子法，机器推导）。
/// 流程：配平（需要计量数）→ 逐物种氧化数 → 按元素氧化数多重集排序配对判定氧化/还原与电子数（支持歧化）→
/// 按"被氧化/被还原元素"挑选核心物种 → 零空间法补平 H2O/H^+/OH^-/e^- 得半反应。
/// 高中适用边界：
/// <list type="bullet">
/// <item>半反应推导面向水溶液离子反应；固相/分子内氧化还原（如 KClO3 分解、NH4NO3 分解）
/// 会如实报告"无法机械拆分"，不猜测。</item>
/// <item>介质：acidic 补 H^+/H2O；basic 补 OH^-/H2O。</item>
/// </list>
/// 易错点：分子形式书写的强电解质（如 Cu(NO3)2）含旁观酸根时半反应推导会失败，
/// 应先转写为离子形式再调用（拆写规则见 Ionic 模块）。
/// </summary>
public static class RedoxBalancer
{
    /// <summary>
    /// 分析氧化还原并拆解半反应。
    /// </summary>
    /// <param name="equation">完整方程式（物种必须齐全，引擎不补产物）。</param>
    /// <param name="medium">"acidic"（默认）或 "basic"。</param>
    /// <returns>分析结果；失败时给出原因，不编造。</returns>
    public static RedoxAnalysis SplitHalfReactions(string equation, string medium = "acidic")
    {
        if (medium is not ("acidic" or "basic"))
        {
            return Fail($"介质参数只能是 acidic 或 basic：{medium}", RiskTags.ParseError);
        }

        if (!EquationParser.TryParse(equation, out var parsed, out var parseError))
        {
            return Fail(parseError!, RiskTags.ParseError);
        }

        var balance = EquationBalancer.Balance(parsed!);
        if (!balance.Success)
        {
            return Fail($"无法配平，氧化还原分析中止：{balance.FailReason}", balance.RiskTag);
        }

        var terms = parsed!.AllTerms.ToList();
        var coefficients = balance.Coefficients;

        // 逐物种氧化数
        var assignments = new OxidationAssignment[terms.Count];
        for (var i = 0; i < terms.Count; i++)
        {
            assignments[i] = OxidationStateAnalyzer.Assign(terms[i].Species);
            if (!assignments[i].Success)
            {
                return Fail(assignments[i].Note!, RiskTags.RedoxError);
            }
        }

        // 按元素氧化数多重集配对：两侧各按"系数 × 原子数"展开、排序后逐位比较。
        // 总 Δ 会漏检歧化反应（Cl2 + OH^- 中 Cl 升降相抵、Δ=0），配对可区分同一元素的氧化与还原。
        var elements = terms.SelectMany(t => t.Species.Elements.Keys)
            .Distinct(StringComparer.Ordinal).OrderBy(e => e, StringComparer.Ordinal).ToList();

        var changes = new List<OxidationChange>();
        var oxidizedElements = new HashSet<string>(StringComparer.Ordinal);
        var reducedElements = new HashSet<string>(StringComparer.Ordinal);
        var lostTotal = Rational.Zero;
        var gainedTotal = Rational.Zero;

        foreach (var element in elements)
        {
            var leftValues = CollectValues(terms, assignments, coefficients, parsed.Left.Count, element, isLeft: true);
            var rightValues = CollectValues(terms, assignments, coefficients, parsed.Left.Count, element, isLeft: false);
            if (leftValues.Count != rightValues.Count)
            {
                return Fail($"元素 {element} 两侧原子数不一致，无法分析氧化数变化", RiskTags.ConservationViolation);
            }

            leftValues.Sort();
            rightValues.Sort();

            var lost = Rational.Zero;
            var gained = Rational.Zero;
            for (var k = 0; k < leftValues.Count; k++)
            {
                var diff = rightValues[k] - leftValues[k];
                if (diff.IsNegative)
                {
                    gained -= diff;
                }
                else if (!diff.IsZero)
                {
                    lost += diff;
                }
            }

            if (lost.IsZero && gained.IsZero)
            {
                continue;
            }

            var fromText = SideValuesText(terms, assignments, parsed.Left.Count, element, isLeft: true);
            var toText = SideValuesText(terms, assignments, parsed.Left.Count, element, isLeft: false);
            if (!lost.IsZero)
            {
                oxidizedElements.Add(element);
                lostTotal += lost;
                changes.Add(new OxidationChange
                {
                    Element = element,
                    FromText = fromText,
                    ToText = toText,
                    Electrons = lost,
                    IsOxidation = true
                });
            }

            if (!gained.IsZero)
            {
                reducedElements.Add(element);
                gainedTotal += gained;
                changes.Add(new OxidationChange
                {
                    Element = element,
                    FromText = fromText,
                    ToText = toText,
                    Electrons = gained,
                    IsOxidation = false
                });
            }
        }

        if (changes.Count == 0)
        {
            return new RedoxAnalysis { Success = true, IsRedox = false };
        }

        var analysis = new RedoxAnalysis
        {
            Success = true,
            IsRedox = true,
            Changes = changes,
            ElectronsTransferred = lostTotal,
            ElectronConservation = lostTotal == gainedTotal
        };

        // 半反应拆解（尽力推导，失败不影响上面的电子守恒结论）。
        // claimed 记录已被某条半反应认领的物种（按化学式）及其经由元素，两次调用共享，用于排除跨界物种。
        var claimed = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var oxidation = BuildHalfReaction(parsed, terms, assignments, oxidizedElements, medium, "氧化", oxidationHalf: true, claimed, out var oxidationError);
        var reduction = BuildHalfReaction(parsed, terms, assignments, reducedElements, medium, "还原", oxidationHalf: false, claimed, out var reductionError);

        if (oxidation is not null && reduction is not null && oxidation.Expression == reduction.Expression)
        {
            return new RedoxAnalysis
            {
                Success = true,
                IsRedox = true,
                Changes = changes,
                ElectronsTransferred = lostTotal,
                ElectronConservation = analysis.ElectronConservation,
                FailReason = "氧化剂与还原剂为同一物质且两条半反应重合（如 NH4NO3 自身氧化还原），请人工分析",
                RiskTag = RiskTags.RedoxError
            };
        }

        return new RedoxAnalysis
        {
            Success = true,
            IsRedox = true,
            Changes = changes,
            ElectronsTransferred = lostTotal,
            ElectronConservation = analysis.ElectronConservation,
            OxidationHalf = oxidation,
            ReductionHalf = reduction,
            FailReason = oxidationError ?? reductionError,
            RiskTag = oxidationError is not null || reductionError is not null ? RiskTags.RedoxError : RiskTags.None
        };
    }

    /// <summary>
    /// 展开指定侧某元素的全部氧化数值（按计量系数 × 原子数重复，系数为配平后的正整数），
    /// 供与对侧排序后逐位配对，识别歧化/自身氧化还原。
    /// </summary>
    private static List<Rational> CollectValues(
        List<ParsedTerm> terms,
        OxidationAssignment[] assignments,
        Rational[] coefficients,
        int leftCount,
        string element,
        bool isLeft)
    {
        var values = new List<Rational>();
        for (var i = 0; i < terms.Count; i++)
        {
            if (i < leftCount != isLeft)
            {
                continue;
            }

            if (!assignments[i].States.TryGetValue(element, out var states))
            {
                continue;
            }

            var coefficient = coefficients[i];
            if (!coefficient.IsInteger || coefficient.IsNegative)
            {
                throw new InvalidOperationException($"计量系数应为正整数，实际为 {coefficient}");
            }

            foreach (var (value, count) in states)
            {
                var repeat = (int)(coefficient.Num * count);
                for (var k = 0; k < repeat; k++)
                {
                    values.Add(value);
                }
            }
        }

        return values;
    }

    /// <summary>某侧某元素各氧化数的展示文本（去重升序）。</summary>
    private static string SideValuesText(
        List<ParsedTerm> terms,
        OxidationAssignment[] assignments,
        int leftCount,
        string element,
        bool isLeft)
    {
        var values = new SortedSet<Rational>();
        for (var i = 0; i < terms.Count; i++)
        {
            if (i < leftCount != isLeft)
            {
                continue;
            }

            if (assignments[i].States.TryGetValue(element, out var states))
            {
                foreach (var (value, _) in states)
                {
                    values.Add(value);
                }
            }
        }

        return string.Join(", ", values.Select(v => v.ToString()));
    }

    /// <summary>
    /// 构造一条半反应。
    /// </summary>
    /// <param name="targetElements">本条半反应关注的元素（被氧化或被还原的元素）。</param>
    /// <param name="expectKind">期望类型，用于交叉校验。</param>
    /// <param name="oxidationHalf">true = 构造氧化半反应，false = 还原半反应（决定物种挑选的比较方向）。</param>
    /// <param name="claimed">跨两次调用共享的物种认领表（物种 → 经由元素集合）。</param>
    /// <param name="error">推导失败原因（成功为 null）。</param>
    private static HalfReactionResult? BuildHalfReaction(
        ParsedEquation equation,
        List<ParsedTerm> terms,
        OxidationAssignment[] assignments,
        HashSet<string> targetElements,
        string medium,
        string expectKind,
        bool oxidationHalf,
        Dictionary<string, HashSet<string>> claimed,
        out string? error)
    {
        error = null;
        var leftCount = equation.Left.Count;

        // 挑选核心物种：氧化半反应取"反应物氧化数低于对侧最大值、生成物高于对侧最小值"的物种；
        // 还原半反应方向相反。若物种已被另一条半反应经由不同元素认领，则排除（避免跨界物种污染）。
        var reactantCore = new List<Species>();
        var productCore = new List<Species>();

        SelectCore(terms, assignments, leftCount, targetElements, isReactantSide: true, oxidationHalf, reactantCore, claimed);
        SelectCore(terms, assignments, leftCount, targetElements, isReactantSide: false, oxidationHalf, productCore, claimed);

        // 候选列：核心物种（反应物 σ=+1，生成物 σ=-1）+ H2O + H^+/OH^- + e^-（σ=+1，符号由解决定）
        var candidates = new List<(Species? Species, int Sigma)>();
        foreach (var s in reactantCore)
        {
            candidates.Add((s, +1));
        }

        foreach (var s in productCore)
        {
            candidates.Add((s, -1));
        }

        var water = Species.Parse("H2O");
        var mediumSpecies = Species.Parse(medium == "basic" ? "OH^-" : "H^+");
        var waterIndex = candidates.Count;
        candidates.Add((water, +1));
        var mediumIndex = candidates.Count;
        candidates.Add((mediumSpecies, +1));
        var electronIndex = candidates.Count;
        candidates.Add((null, +1));

        // 行：核心物种出现过的元素 ∪ {H, O}，再加电荷行
        var elements = reactantCore.Concat(productCore).SelectMany(s => s.Elements.Keys)
            .Union(new[] { "H", "O" }, StringComparer.Ordinal)
            .Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList();

        var rows = new List<Rational[]>();
        foreach (var element in elements)
        {
            var row = new Rational[candidates.Count];
            for (var j = 0; j < candidates.Count; j++)
            {
                var (sp, sigma) = candidates[j];
                if (sp is not null && sp.Elements.TryGetValue(element, out var count))
                {
                    row[j] = new Rational(sigma * count);
                }
            }

            rows.Add(row);
        }

        var chargeRow = new Rational[candidates.Count];
        for (var j = 0; j < candidates.Count; j++)
        {
            var (sp, sigma) = candidates[j];
            chargeRow[j] = new Rational(sigma * (sp?.Charge ?? -1)); // 电子电荷 -1
        }

        rows.Add(chargeRow);

        var basis = RationalMatrix.FromRows(rows).NullSpaceBasis();
        if (basis.Count != 1)
        {
            error = $"半反应推导失败（零空间维数 {basis.Count}）：核心物种组合不完整或该反应不适用离子-电子法（常见于固相/分子内氧化还原）";
            return null;
        }

        var vector = basis[0];
        var anchor = reactantCore.Count > 0 ? 0 : productCore.Count > 0 ? reactantCore.Count : -1;
        if (anchor < 0)
        {
            error = "半反应核心物种为空，无法推导";
            return null;
        }

        if (vector[anchor].IsNegative)
        {
            vector = vector.Select(v => -v).ToArray();
        }

        for (var j = 0; j < reactantCore.Count + productCore.Count; j++)
        {
            if (!vector[j].IsNegative && !vector[j].IsZero)
            {
                continue;
            }

            error = $"半反应推导失败：物种 {candidates[j].Species!.Formula} 的系数方向异常";
            return null;
        }

        var integers = EquationBalancer.ToSmallestIntegers(vector);

        string FormatCoefficient(Rational c) => c == Rational.One ? "" : c.ToString();
        var leftParts = new List<string>();
        var rightParts = new List<string>();

        for (var j = 0; j < reactantCore.Count; j++)
        {
            leftParts.Add($"{FormatCoefficient(integers[j])}{reactantCore[j]}");
        }

        for (var j = 0; j < productCore.Count; j++)
        {
            rightParts.Add($"{FormatCoefficient(integers[reactantCore.Count + j])}{productCore[j]}");
        }

        foreach (var (index, sp) in new[] { (waterIndex, water), (mediumIndex, mediumSpecies) })
        {
            var v = integers[index];
            if (v.IsZero)
            {
                continue;
            }

            var part = $"{FormatCoefficient(v.Abs())}{sp}";
            (v.IsNegative ? rightParts : leftParts).Add(part);
        }

        var electronValue = integers[electronIndex];
        if (electronValue.IsZero)
        {
            error = "半反应推导失败：未出现电子项";
            return null;
        }

        var electronPart = $"{FormatCoefficient(electronValue.Abs())}e^-";
        var electronsOnLeft = !electronValue.IsNegative;
        (electronsOnLeft ? leftParts : rightParts).Add(electronPart);

        var kind = electronsOnLeft ? "还原" : "氧化";
        if (kind != expectKind)
        {
            error = $"半反应类型异常：期望{expectKind}半反应，实际推出{kind}半反应（物种挑选可能存在歧义）";
            return null;
        }

        return new HalfReactionResult
        {
            Expression = $"{string.Join(" + ", leftParts)} = {string.Join(" + ", rightParts)}",
            Electrons = electronValue.Abs(),
            Kind = kind
        };
    }

    /// <summary>
    /// 按目标元素挑选一侧的核心物种。
    /// 氧化半反应：反应物侧取"氧化数低于对侧最大值"的物种，生成物侧取"高于对侧最小值"的物种；
    /// 还原半反应方向相反。
    /// 被另一条半反应经由不同元素认领的物种予以排除（同元素认领则共享，如歧化反应的 Cl2）。
    /// </summary>
    private static void SelectCore(
        List<ParsedTerm> terms,
        OxidationAssignment[] assignments,
        int leftCount,
        HashSet<string> targetElements,
        bool isReactantSide,
        bool oxidationHalf,
        List<Species> core,
        Dictionary<string, HashSet<string>> claimed)
    {
        for (var i = 0; i < terms.Count; i++)
        {
            if (i < leftCount != isReactantSide)
            {
                continue;
            }

            var species = terms[i].Species;
            var viaElements = new HashSet<string>(StringComparer.Ordinal);
            foreach (var element in targetElements)
            {
                if (!assignments[i].States.TryGetValue(element, out var states))
                {
                    continue;
                }

                var opposite = OppositeExtreme(terms, assignments, leftCount, element, isReactantSide, oxidationHalf);
                if (opposite is null)
                {
                    continue;
                }

                var oppositeValue = opposite.Value;
                var higher = oxidationHalf == isReactantSide
                    ? states.Any(s => s.Value < oppositeValue)   // 氧化-反应物 / 还原-生成物：取低值侧
                    : states.Any(s => s.Value > oppositeValue);  // 氧化-生成物 / 还原-反应物：取高值侧
                if (higher)
                {
                    viaElements.Add(element);
                }
            }

            if (viaElements.Count == 0)
            {
                continue;
            }

            if (claimed.TryGetValue(species.Formula, out var previous))
            {
                if (previous.Overlaps(viaElements))
                {
                    core.Add(species);
                }
                // 不同元素认领 → 排除（跨界物种）
                continue;
            }

            claimed[species.Formula] = viaElements;
            core.Add(species);
        }
    }

    /// <summary>
    /// 对侧极值。氧化半反应：挑反应物侧时用对侧最大值，挑生成物侧时用对侧最小值；
    /// 还原半反应相反。
    /// </summary>
    private static Rational? OppositeExtreme(
        List<ParsedTerm> terms,
        OxidationAssignment[] assignments,
        int leftCount,
        string element,
        bool selectingReactantSide,
        bool oxidationHalf)
    {
        var wantMax = oxidationHalf == selectingReactantSide;
        Rational? extreme = null;
        for (var i = 0; i < terms.Count; i++)
        {
            var onLeft = i < leftCount;
            if (onLeft == selectingReactantSide)
            {
                continue; // 只看对侧
            }

            if (!assignments[i].States.TryGetValue(element, out var states))
            {
                continue;
            }

            foreach (var (value, _) in states)
            {
                if (extreme is null || (wantMax ? value > extreme.Value : value < extreme.Value))
                {
                    extreme = value;
                }
            }
        }

        return extreme;
    }

    private static RedoxAnalysis Fail(string reason, string riskTag) =>
        new() { Success = false, FailReason = reason, RiskTag = riskTag };
}
