using CogniThink.Chem.Core.Chemistry;
using CogniThink.Chem.Core.Math;

namespace CogniThink.Chem.Core.Balancing;

/// <summary>
/// 单个物种的氧化数分配结果。同一元素可有多个氧化数（如 NH4NO3 中的 N：-3 与 +5）。
/// </summary>
public sealed class OxidationAssignment
{
    public bool Success { get; init; }

    /// <summary>失败或需要人工介入时的说明。</summary>
    public string? Note { get; init; }

    /// <summary>元素 → (氧化数, 该氧化数下的原子个数) 列表。</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<(Rational Value, int Count)>> States { get; init; } =
        new Dictionary<string, IReadOnlyList<(Rational, int)>>();
}

/// <summary>
/// 氧化数分析器（高中规则）。分配顺序：
/// <list type="number">
/// <item>特例表精确命中（过氧化物、金属氢化物、FeS2）。</item>
/// <item>单质：氧化数 = 电荷 / 原子数（Fe→0、Na+→+1、O2→0）。</item>
/// <item>常规规则：O = -2、H = +1、恒定化合价元素（F/IA/IIA/Al/Zn/Ag）、
/// 无氧卤化物与硫化物规则（不含 O 时 Cl/Br/I = -1、S = -2）。</item>
/// <item>离子团拆解：仍有未知元素的物种拆分为已知离子团（NO3/SO4/NH4 等）与单元素，
/// 可区分同元素不同氧化数（NH4NO3 中 N：-3/+5），并求解多未知元素盐（Cu(NO3)2 → Cu +2）。</item>
/// <item>兜底：仅剩唯一未知元素时由"代数和 = 电荷"解出（可为分数，如 Fe3O4 中 Fe = +8/3）。</item>
/// </list>
/// 高中适用边界：不涉及有机氧化数逐碳计算、配合物中心离子拆分。
/// 易错点：
/// <list type="bullet">
/// <item>Fe3O4 返回平均氧化数 +8/3，电子计数按总变化量计算仍然正确。</item>
/// <item>离子团拆解后仍有多个未知元素（如 FeAsS）→ 返回失败，引擎不猜测。</item>
/// </list>
/// </summary>
public static class OxidationStateAnalyzer
{
    // 过氧化物（O = -1）。来源：人教版必修一·钠及其化合物。
    private static readonly HashSet<string> Peroxides = new(StringComparer.Ordinal)
    {
        "H2O2", "Na2O2", "K2O2", "BaO2", "CaO2"
    };

    // 金属氢化物（H = -1）。来源：人教版必修一·氢化物常识。
    private static readonly HashSet<string> MetalHydrides = new(StringComparer.Ordinal)
    {
        "NaH", "KH", "CaH2", "LiH", "BaH2"
    };

    // 化合物中化合价恒定的常见元素（高中口诀级规则）。
    // 来源：人教版必修一·化合价规则：F 恒 -1；IA 族 +1；IIA 族 +2；Al +3；Zn +2；Ag +1。
    private static readonly Dictionary<string, Rational> FixedStates = new(StringComparer.Ordinal)
    {
        ["F"] = new Rational(-1),
        ["Li"] = Rational.One,
        ["Na"] = Rational.One,
        ["K"] = Rational.One,
        ["Rb"] = Rational.One,
        ["Cs"] = Rational.One,
        ["Be"] = new Rational(2),
        ["Mg"] = new Rational(2),
        ["Ca"] = new Rational(2),
        ["Sr"] = new Rational(2),
        ["Ba"] = new Rational(2),
        ["Al"] = new Rational(3),
        ["Zn"] = new Rational(2),
        ["Ag"] = Rational.One
    };

    // 特例表：直接给出每个元素的 (氧化数, 原子数) 列表。
    // FeS2（黄铁矿，工业制硫酸背景）：Fe +2、S -1。
    private static readonly Dictionary<string, Dictionary<string, List<(Rational Value, int Count)>>> SpecialTable =
        new(StringComparer.Ordinal)
        {
            ["FeS2"] = new()
            {
                ["Fe"] = new() { (new Rational(2), 1) },
                ["S"] = new() { (new Rational(-1), 2) }
            }
        };

    // 常见原子团（离子团）：团名 → (团电荷, 团内元素氧化数)。
    // 来源：人教版必修一/选择性必修一常见原子团化合价口诀。
    // 团内氧化数之和 = 团电荷，拆解后直接并入整体代数和校验。
    private static readonly Dictionary<string, (int Charge, (string Element, Rational Value, int Count)[] States)> IonGroups =
        new(StringComparer.Ordinal)
        {
            ["NO3"] = (-1, new[] { ("N", new Rational(5), 1), ("O", new Rational(-2), 3) }),
            ["NO2"] = (-1, new[] { ("N", new Rational(3), 1), ("O", new Rational(-2), 2) }),
            ["SO4"] = (-2, new[] { ("S", new Rational(6), 1), ("O", new Rational(-2), 4) }),
            ["SO3"] = (-2, new[] { ("S", new Rational(4), 1), ("O", new Rational(-2), 3) }),
            ["CO3"] = (-2, new[] { ("C", new Rational(4), 1), ("O", new Rational(-2), 3) }),
            ["HCO3"] = (-1, new[] { ("H", Rational.One, 1), ("C", new Rational(4), 1), ("O", new Rational(-2), 3) }),
            ["OH"] = (-1, new[] { ("O", new Rational(-2), 1), ("H", Rational.One, 1) }),
            ["NH4"] = (1, new[] { ("N", new Rational(-3), 1), ("H", Rational.One, 4) }),
            ["MnO4"] = (-1, new[] { ("Mn", new Rational(7), 1), ("O", new Rational(-2), 4) }),
            ["ClO3"] = (-1, new[] { ("Cl", new Rational(5), 1), ("O", new Rational(-2), 3) }),
            ["ClO"] = (-1, new[] { ("Cl", Rational.One, 1), ("O", new Rational(-2), 1) }),
            ["PO4"] = (-3, new[] { ("P", new Rational(5), 1), ("O", new Rational(-2), 4) }),
            ["SiO3"] = (-2, new[] { ("Si", new Rational(4), 1), ("O", new Rational(-2), 3) }),
            ["AlO2"] = (-1, new[] { ("Al", new Rational(3), 1), ("O", new Rational(-2), 2) })
        };

    private static readonly string[] IonGroupsByLength =
        IonGroups.Keys.OrderByDescending(k => k.Length).ToArray();

    /// <summary>
    /// 为物种分配氧化数。
    /// </summary>
    /// <param name="species">已解析物种。</param>
    /// <returns>分配结果；无法确定时 Success=false 并附说明。</returns>
    public static OxidationAssignment Assign(Species species)
    {
        var formulaKey = species.Body;

        if (SpecialTable.TryGetValue(formulaKey, out var special))
        {
            return Ok(special.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<(Rational, int)>)kv.Value.Select(t => (t.Value, t.Count)).ToList()));
        }

        // 单质或单元素离子
        if (species.Elements.Count == 1)
        {
            var (element, count) = species.Elements.Single();
            var value = new Rational(species.Charge, count);
            return Ok(new Dictionary<string, IReadOnlyList<(Rational, int)>>
            {
                [element] = new List<(Rational, int)> { (value, count) }
            });
        }

        var states = new Dictionary<string, IReadOnlyList<(Rational Value, int Count)>>(StringComparer.Ordinal);
        var knownSum = Rational.Zero;

        foreach (var (element, count) in species.Elements)
        {
            if (element == "O")
            {
                var v = Peroxides.Contains(formulaKey) ? new Rational(-1) : new Rational(-2);
                states["O"] = new List<(Rational, int)> { (v, count) };
                knownSum += v * new Rational(count);
            }
            else if (element == "H")
            {
                var v = MetalHydrides.Contains(formulaKey) ? new Rational(-1) : Rational.One;
                states["H"] = new List<(Rational, int)> { (v, count) };
                knownSum += v * new Rational(count);
            }
            else if (FixedStates.TryGetValue(element, out var fixedState))
            {
                states[element] = new List<(Rational, int)> { (fixedState, count) };
                knownSum += fixedState * new Rational(count);
            }
        }

        // 卤化物/硫化物规则：物种不含氧时 Cl/Br/I = -1、S = -2（高中常见规则）。
        // 含氧物种（ClO^-、ClO3^-、SO3^2- 等）不在此固定，走离子团拆解或平均氧化数路径。
        if (!species.Elements.ContainsKey("O"))
        {
            foreach (var (element, count) in species.Elements)
            {
                if (states.ContainsKey(element))
                {
                    continue;
                }

                var anionValue = element switch
                {
                    "Cl" or "Br" or "I" => new Rational(-1),
                    "S" => new Rational(-2),
                    _ => (Rational?)null
                };
                if (anionValue is null)
                {
                    continue;
                }

                states[element] = new List<(Rational, int)> { (anionValue.Value, count) };
                knownSum += anionValue.Value * new Rational(count);
            }
        }

        var unknowns = species.Elements.Keys.Where(e => !states.ContainsKey(e)).ToList();
        if (unknowns.Count == 0)
        {
            if (knownSum != new Rational(species.Charge))
            {
                return Fail($"氧化数规则冲突：{species.Formula} 按常规规则代数和 {knownSum} ≠ 电荷 {species.Charge}，可能为超纲特例");
            }

            return Ok(states);
        }

        // 优先尝试离子团拆解：可区分同一物种内同元素的不同氧化数（NH4NO3：-3/+5），
        // 也能处理多未知元素盐（Cu(NO3)2）。拆解失败且仅剩 1 个未知元素 → 兜底平均氧化数（Fe3O4：+8/3）。
        var viaGroups = AssignViaIonGroups(species, unknowns);
        if (viaGroups.Success)
        {
            return viaGroups;
        }

        if (unknowns.Count > 1)
        {
            return Fail($"{species.Formula} 含多种未知氧化数元素（{string.Join("、", unknowns)}），超出引擎规则，需人工指定");
        }

        var unknown = unknowns[0];
        var atomCount = species.Elements[unknown];
        var value2 = (new Rational(species.Charge) - knownSum) / new Rational(atomCount);
        states[unknown] = new List<(Rational, int)> { (value2, atomCount) };
        return Ok(states);
    }

    /// <summary>
    /// 离子团拆解：把 Body 拆分为已知离子团（NO3/SO4/NH4/OH 等）与单元素 token，
    /// 并入团内氧化数后再决定剩余元素。
    /// 仅当拆解完整覆盖 Body、原子总数核对一致且代数和匹配电荷时才采纳，否则如实失败，不猜测。
    /// </summary>
    /// <param name="species">待分配物种。</param>
    /// <param name="unknowns">常规规则无法确定的元素列表（用于失败说明）。</param>
    private static OxidationAssignment AssignViaIonGroups(Species species, List<string> unknowns)
    {
        if (!TryTokenize(species.Body, out var tokens, out var tokenError))
        {
            return Fail($"{species.Formula} 离子团拆解失败（{tokenError}），未知氧化数元素：{string.Join("、", unknowns)}");
        }

        if (!tokens.Any(t => t.Group is not null))
        {
            return Fail($"{species.Formula} 未找到已知离子团");
        }

        // 从零重建（团内氧化数含 O/H，复用上一轮结果会重复计数）
        var states = new Dictionary<string, List<(Rational Value, int Count)>>(StringComparer.Ordinal);
        var knownSum = Rational.Zero;
        var pending = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var (group, element, multiplier) in tokens)
        {
            if (group is not null)
            {
                foreach (var (innerElement, value, count) in IonGroups[group].States)
                {
                    var total = count * multiplier;
                    AddState(states, innerElement, value, total);
                    knownSum += value * new Rational(total);
                }
            }
            else
            {
                var value = DetermineElementValue(species.Body, element!);
                if (value is null)
                {
                    pending[element!] = pending.GetValueOrDefault(element!) + multiplier;
                }
                else
                {
                    AddState(states, element!, value.Value, multiplier);
                    knownSum += value.Value * new Rational(multiplier);
                }
            }
        }

        // 一致性校验：拆解后各元素原子总数必须与解析结果一致，防止分词器误判
        var totals = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (element, list) in states)
        {
            totals[element] = list.Sum(t => t.Count);
        }

        foreach (var (element, count) in pending)
        {
            totals[element] = totals.GetValueOrDefault(element) + count;
        }

        if (totals.Count != species.Elements.Count ||
            totals.Any(kv => !species.Elements.TryGetValue(kv.Key, out var expected) || expected != kv.Value))
        {
            return Fail($"{species.Formula} 离子团拆解结果与化学式原子数不符，团划分不可信");
        }

        if (pending.Count == 0)
        {
            if (knownSum != new Rational(species.Charge))
            {
                return Fail($"{species.Formula} 离子团拆解后代数和 {knownSum} ≠ 电荷 {species.Charge}，可能为超纲特例");
            }

            return Ok(states.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<(Rational, int)>)kv.Value.ToList()));
        }

        if (pending.Count == 1)
        {
            var (unknown, atomCount) = pending.Single();
            var value = (new Rational(species.Charge) - knownSum) / new Rational(atomCount);
            AddState(states, unknown, value, atomCount);
            return Ok(states.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<(Rational, int)>)kv.Value.ToList()));
        }

        return Fail($"{species.Formula} 离子团拆解后仍含多种未知氧化数元素（{string.Join("、", pending.Keys)}），超出引擎规则");
    }

    /// <summary>
    /// 将 Body 从左到右拆分为"离子团 / 单元素" token，支持括号与计数（Cu(NO3)2 → Cu×1、NO3×2）。
    /// 离子团按最长优先匹配，避免 CO3 被误拆为 C + O3。
    /// </summary>
    private static bool TryTokenize(
        string body,
        out List<(string? Group, string? Element, int Multiplier)> tokens,
        out string error) =>
        TokenizeRange(body, 0, body.Length, out tokens, out error);

    private static bool TokenizeRange(
        string body,
        int start,
        int end,
        out List<(string? Group, string? Element, int Multiplier)> tokens,
        out string error)
    {
        tokens = new List<(string?, string?, int)>();
        error = "";
        var pos = start;

        while (pos < end)
        {
            var ch = body[pos];
            if (ch == '(')
            {
                var close = FindMatchingParen(body, pos, end);
                if (close < 0)
                {
                    error = "括号不配对";
                    return false;
                }

                if (!TokenizeRange(body, pos + 1, close, out var inner, out error))
                {
                    return false;
                }

                pos = close + 1;
                if (!ReadCount(body, ref pos, out var parenCount))
                {
                    error = "括号后计数非法";
                    return false;
                }

                foreach (var (g, e, m) in inner)
                {
                    tokens.Add((g, e, m * parenCount));
                }
            }
            else if (ch is >= 'A' and <= 'Z')
            {
                string? matchedGroup = null;
                foreach (var g in IonGroupsByLength)
                {
                    if (end - pos >= g.Length && string.CompareOrdinal(body, pos, g, 0, g.Length) == 0)
                    {
                        matchedGroup = g;
                        break;
                    }
                }

                if (matchedGroup is not null)
                {
                    pos += matchedGroup.Length;
                    if (!ReadCount(body, ref pos, out var groupCount))
                    {
                        error = $"离子团 {matchedGroup} 后计数非法";
                        return false;
                    }

                    tokens.Add((matchedGroup, null, groupCount));
                }
                else
                {
                    var symbol = ch.ToString();
                    pos++;
                    if (pos < end && body[pos] is >= 'a' and <= 'z')
                    {
                        symbol += body[pos];
                        pos++;
                    }

                    if (!ReadCount(body, ref pos, out var elementCount))
                    {
                        error = $"元素 {symbol} 后计数非法";
                        return false;
                    }

                    tokens.Add((null, symbol, elementCount));
                }
            }
            else
            {
                error = $"无法识别的字符 '{ch}'";
                return false;
            }
        }

        return true;
    }

    private static int FindMatchingParen(string body, int open, int end)
    {
        var depth = 0;
        for (var i = open; i < end; i++)
        {
            if (body[i] == '(')
            {
                depth++;
            }
            else if (body[i] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>读取紧邻计数（缺省为 1；显式 0 视为非法）。</summary>
    private static bool ReadCount(string body, ref int pos, out int count)
    {
        var start = pos;
        while (pos < body.Length && body[pos] is >= '0' and <= '9')
        {
            pos++;
        }

        if (start == pos)
        {
            count = 1;
            return true;
        }

        return int.TryParse(body[start..pos], out count) && count > 0;
    }

    private static void AddState(
        Dictionary<string, List<(Rational Value, int Count)>> states,
        string element,
        Rational value,
        int count)
    {
        if (!states.TryGetValue(element, out var list))
        {
            list = new List<(Rational, int)>();
            states[element] = list;
        }

        list.Add((value, count));
    }

    /// <summary>
    /// 单元素 token 的氧化数判定：FixedStates / O(-2，过氧化物例外) / H(+1，金属氢化物例外) /
    /// 无氧时 Cl/Br/I = -1、S = -2。无法确定返回 null。
    /// </summary>
    private static Rational? DetermineElementValue(string formulaKey, string element)
    {
        if (element == "O")
        {
            return Peroxides.Contains(formulaKey) ? new Rational(-1) : new Rational(-2);
        }

        if (element == "H")
        {
            return MetalHydrides.Contains(formulaKey) ? new Rational(-1) : Rational.One;
        }

        if (FixedStates.TryGetValue(element, out var fixedState))
        {
            return fixedState;
        }

        if (!formulaKey.Contains('O'))
        {
            return element switch
            {
                "Cl" or "Br" or "I" => new Rational(-1),
                "S" => new Rational(-2),
                _ => null
            };
        }

        return null;
    }

    private static OxidationAssignment Ok(Dictionary<string, IReadOnlyList<(Rational Value, int Count)>> states) =>
        new() { Success = true, States = states };

    private static OxidationAssignment Fail(string note) =>
        new() { Success = false, Note = note };
}
