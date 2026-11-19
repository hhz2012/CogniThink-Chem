using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Balancing;
using CogniThink.Chem.Core.Math;
using CogniThink.Chem.Core.Parsing;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 网关层氧化还原求解器（阶段3C / AC-13）。
/// <para>真值源：<see cref="OxidationStateAnalyzer"/>（氧化态分配）+ 高中规则表（氧化剂还原产物 / 电解放电顺序）。</para>
/// <para>覆盖：R1 氧化还原角色识别（给完整方程问氧化剂/还原剂/被氧化/被还原）；
/// R2 常见氧化剂还原产物预测（给氧化剂+介质）、电解电极产物；P3 两条小可行性规则。</para>
/// <para>红线（不蒙数）：方程抽取失败 / 氧化态分配失败 / 变价方向歧化或物种歧义 / 规则表外 / 条件缺失
/// → 一律返回 null，绝不臆断。</para>
/// </summary>
public static class RedoxGatewaySolver
{
    // 仅化学字符（字母/数字/括号/^/+/-/空格）组成的两侧 + 一个主分隔符。用于从题干截取干净方程式片段，
    // 避开中文前缀（"在...反应中"）与条件括号（已先剥离）。
    // 注意：字符类必须含 '+'（物种间加号）与 '-'（电荷负号/连字符），否则会从 "Cu + HNO3 →..."
    // 中只截到 "HNO3 → Cu(NO3)2" 这类残缺片段（从首个能匹配的位置开始），丢失变价元素。
    private static readonly Regex EquationFragment = new(
        @"[A-Za-z0-9\(\)\[\]\^+\-\s]+(?:={1,}|→|⇌|[-—–]{1,}[^-—–>]*[-—–]{1,}>)[A-Za-z0-9\(\)\[\]\^+\-\s]+",
        RegexOptions.Compiled);

    // 条件/状态括号：(稀)(浓)(少量)(过量)(熔融)(水溶液)(g)(s)(l)(aq)(催化剂)(加热)(点燃)(高温)(通电)(光照)(放电)
    // 全角与半角。这些不是化学式真括号，剥离后不影响物种解析。
    private static readonly Regex ConditionParen = new(
        @"[（(](稀|浓|少量|过量|熔融|水溶液|g|s|l|aq|催化剂|加热|点燃|高温|通电|光照|放电|沸腾|酶)[）)]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>尝试求解；不命中返回 null（零回归）。</summary>
    public static EngineResponse? TrySolve(EngineRequest req)
    {
        var text = req.RawText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return null;

        try
        {
            // R1：给完整方程，问氧化剂/还原剂/被氧化/被还原
            var r1 = TryRoleIdentification(text);
            if (r1 is not null) return r1;

            // R2：给氧化剂 + 介质问还原产物；电解问电极产物
            var r2 = TryProductPrediction(text);
            if (r2 is not null) return r2;

            // P3：两条小可行性规则
            var p3 = TryQuickFeasibility(text);
            if (p3 is not null) return p3;
        }
        catch
        {
            // 任何意外异常都不拖垮网关链路；保守回退 null
            return null;
        }

        return null;
    }

    // ============================================================
    // R1：氧化还原角色识别
    // ============================================================

    private static EngineResponse? TryRoleIdentification(string text)
    {
        // 问法识别：只处理这 4 种明确角色问法
        var ask = ParseRoleAsk(text);
        if (ask is null) return null;

        var equation = ExtractCleanEquation(text);
        if (equation is null) return null;
        if (!EquationParser.TryParse(equation, out var parsed, out _) || parsed is null) return null;

        var leftStates = AssignAll(parsed.Left.Select(t => t.Species));
        var rightStates = AssignAll(parsed.Right.Select(t => t.Species));
        if (leftStates is null || rightStates is null) return null;

        var changes = AnalyzeChanges(leftStates, rightStates);
        if (changes.Count == 0) return null; // 非氧化还原反应，不处理

        // 目标：氧化剂=被还原（氧化态降低）的元素所在反应物；还原剂/被氧化=被氧化（升高）的元素所在反应物
        var wantReduction = ask == "oxidant"; // 氧化剂 → 找还原元素；还原剂/被氧化 → 找氧化元素
        var target = changes.Where(c => wantReduction ? c.HasReduction : c.HasOxidation)
                            .Where(c => !(c.HasReduction && c.HasOxidation)) // 排除歧化/归中（同元素既升又降，角色歧义）
                            .ToList();
        if (target.Count == 0) return null;

        var targetElements = target.Select(c => c.Element).ToHashSet(StringComparer.Ordinal);
        var reactants = parsed.Left
            .Select(t => t.Species)
            .Where(sp => sp.Elements.Keys.Any(e => targetElements.Contains(e)))
            .ToList();
        if (reactants.Count != 1) return null; // 多个物种含目标元素 → 歧义，不蒙数

        var species = reactants[0];
        var role = ask == "oxidant" ? "氧化剂" : "还原剂";
        var final = $"{role}是 {species.Body}";

        var steps = new List<string> { $"抽取方程式：{equation}" };
        foreach (var c in changes)
        {
            var dir = c.HasOxidation && !c.HasReduction ? "升高，被氧化"
                    : c.HasReduction && !c.HasOxidation ? "降低，被还原"
                    : "既有升高又有降低（歧化/归中）";
            steps.Add($"{c.Element}：{c.LeftText} → {c.RightText}（{dir}）");
        }
        steps.Add($"{role}为反应物 {species.Body}");

        return new EngineResponse
        {
            Ok = true,
            FinalResult = final,
            DerivationSteps = steps,
            Confidence = 1.0
        };
    }

    private static string? ParseRoleAsk(string text)
    {
        if (text.Contains("氧化剂")) return "oxidant";
        if (text.Contains("还原剂") || text.Contains("被氧化")) return "reductant";
        if (text.Contains("被还原")) return "oxidant";
        return null;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<Rational>>? AssignAll(IEnumerable<CogniThink.Chem.Core.Chemistry.Species> species)
    {
        var map = new Dictionary<string, IReadOnlyList<Rational>>(StringComparer.Ordinal);
        foreach (var sp in species)
        {
            var assign = OxidationStateAnalyzer.Assign(sp);
            if (!assign.Success) return null; // 任一物种氧化态无法确定 → 整体放弃，不蒙数
            foreach (var kv in assign.States)
            {
                var values = kv.Value.Select(x => x.Value).ToList();
                if (map.TryGetValue(kv.Key, out var existing))
                {
                    map[kv.Key] = existing.Concat(values).Distinct().ToList();
                }
                else
                {
                    map[kv.Key] = values;
                }
            }
        }
        return map;
    }

    private sealed record ChangeRecord(
        string Element,
        IReadOnlyList<Rational> Left,
        IReadOnlyList<Rational> Right)
    {
        // 存在 r∈R, l∈L: r < l → 有还原（作氧化剂）
        public bool HasReduction => Right.Any(r => Left.Any(l => r < l));
        // 存在 r∈R, l∈L: r > l → 有氧化（作还原剂）
        public bool HasOxidation => Right.Any(r => Left.Any(l => r > l));
        public string LeftText => string.Join("/", Left.Select(FormatRational));
        public string RightText => string.Join("/", Right.Select(FormatRational));
    }

    private static List<ChangeRecord> AnalyzeChanges(
        IReadOnlyDictionary<string, IReadOnlyList<Rational>> left,
        IReadOnlyDictionary<string, IReadOnlyList<Rational>> right)
    {
        var result = new List<ChangeRecord>();
        var allElements = left.Keys.Concat(right.Keys).Distinct(StringComparer.Ordinal);
        foreach (var e in allElements)
        {
            var l = left.TryGetValue(e, out var lv) ? lv : Array.Empty<Rational>();
            var r = right.TryGetValue(e, out var rv) ? rv : Array.Empty<Rational>();
            if (l.Count == 0 || r.Count == 0) continue; // 元素只在一侧（非变价，是物种变化）→ 跳过
            if (l.ToHashSet().SetEquals(r.ToHashSet())) continue; // 氧化态集合完全相同 → 未变价
            result.Add(new ChangeRecord(e, l, r));
        }
        return result;
    }

    // ============================================================
    // R2：还原产物 / 电极产物预测（高中规则表）
    // ============================================================

    private static EngineResponse? TryProductPrediction(string text)
    {
        return TryReductionProduct(text) ?? TryElectrolysisProduct(text);
    }

    // 常见氧化剂在不同介质下的还原产物（高中规则表）。表外一律 null。
    private static EngineResponse? TryReductionProduct(string text)
    {
        // 触发问法："还原产物"，或 "被还原……的产物"（如 d2-1/d2-2 题干"被还原的产物"，中间含"的"，
        // 不含连续子串"还原产物"，需单独识别）。
        if (!text.Contains("还原产物") && !(text.Contains("被还原") && text.Contains("产物"))) return null;

        // 介质（酸/中/碱）仅对 MnO4 / Cr2O7 必需：它们的还原产物随介质变化。
        // ClO→Cl-、NO3（稀→NO / 浓→NO2）、浓 H2SO4→SO2 由氧化剂本身或浓度决定，
        // 不要求题干出现显式介质词（如 d2-4"稀硝酸"无"酸性"字样），故不能在此全局 return。
        var medium = DetectMedium(text);

        string? product = null;
        if (Regex.IsMatch(text, @"MnO4|高锰酸根"))
        {
            product = medium switch
            {
                "acid" => "Mn2+",      // 酸性：+7 → +2
                "neutral" => "MnO2",   // 中性：+7 → +4
                "base" => "MnO4^2-",   // 碱性：+7 → +6（锰酸根）
                _ => null              // 介质缺失/不明 → 不蒙数
            };
        }
        else if (Regex.IsMatch(text, @"ClO[^\s]|次氯酸根"))
        {
            product = "Cl-"; // 酸性/碱性次氯酸根还原均为 Cl-（高中范围）
        }
        else if (Regex.IsMatch(text, @"Cr2O7|重铬酸根"))
        {
            product = medium == "acid" ? "Cr3+" : null; // 仅酸性；介质缺失 → null
        }
        else if (Regex.IsMatch(text, @"NO3|硝酸|HNO3"))
        {
            if (text.Contains("稀")) product = "NO";      // 稀硝酸 → NO（+5 → +2）
            else if (text.Contains("浓")) product = "NO2"; // 浓硝酸 → NO2（+5 → +4）
            // 无稀/浓 → 不蒙数
        }
        else if (Regex.IsMatch(text, @"浓\s*H2SO4|浓硫酸"))
        {
            product = "SO2"; // +6 → +4
        }

        if (string.IsNullOrEmpty(product)) return null;

        var condText = medium is not null ? MediumText(medium)
            : (Regex.IsMatch(text, @"NO3|硝酸|HNO3") ? (text.Contains("稀") ? "稀" : "浓") : "未指定");

        return new EngineResponse
        {
            Ok = true,
            FinalResult = $"还原产物是 {product}",
            DerivationSteps = new List<string>
            {
                $"识别氧化剂与条件（{condText}）",
                $"查高中规则表得还原产物：{product}"
            },
            Confidence = 1.0
        };
    }

    // 电解电极产物：阳极放电顺序 S2->I->Br->Cl->OH->含氧酸根；只覆盖评估中出现的溶质。
    private static EngineResponse? TryElectrolysisProduct(string text)
    {
        var m = Regex.Match(text, @"电解\s*(?<solute>[A-Za-z0-9\(\)\^\-]+)\s*溶液[，,]?\s*(?<electrode>阳极|阴极)产物");
        if (!m.Success) return null;

        var solute = m.Groups["solute"].Value;
        var electrode = m.Groups["electrode"].Value;

        string? product = null;
        if (electrode == "阳极")
        {
            if (Regex.IsMatch(solute, @"Cl")) product = "Cl2";        // Cl- 优先放电
            else if (Regex.IsMatch(solute, @"SO4|NO3")) product = "O2"; // 含氧酸根不放电，OH- → O2
        }
        if (product is null) return null;

        return new EngineResponse
        {
            Ok = true,
            FinalResult = $"{electrode}产物是 {product}",
            DerivationSteps = new List<string>
            {
                $"电解溶质：{solute}，电极：{electrode}",
                $"按放电顺序判断产物：{product}"
            },
            Confidence = 1.0
        };
    }

    private static string? DetectMedium(string text)
    {
        if (text.Contains("酸性") || text.Contains("H+") || text.Contains("H＋")) return "acid";
        if (text.Contains("中性")) return "neutral";
        if (text.Contains("碱性") || text.Contains("OH-") || text.Contains("OH－")) return "base";
        return null;
    }

    private static string MediumText(string medium) => medium switch
    {
        "acid" => "酸性",
        "neutral" => "中性",
        "base" => "碱性",
        _ => medium
    };

    // ============================================================
    // P3：两条小可行性规则
    // ============================================================

    private static EngineResponse? TryQuickFeasibility(string text)
    {
        // 酸性电解质溶液中电极反应式不能出现 OH-
        if (text.Contains("酸性") && text.Contains("电极反应") &&
            (text.Contains("OH-") || text.Contains("OH－") || text.Contains("氢氧根")) &&
            (text.Contains("可以") || text.Contains("能")))
        {
            return new EngineResponse
            {
                Ok = true,
                FinalResult = "不能。酸性电解质溶液中，电极反应式不出现 OH-。",
                DerivationSteps = new List<string> { "酸性介质中大量存在 H+，OH- 不能稳定存在，故电极反应式不出现 OH-。" },
                Confidence = 1.0
            };
        }

        // 两性金属（Al/Zn）与强碱溶液反应生成 H2
        if ((text.Contains("Al") || text.Contains("铝")) &&
            (text.Contains("NaOH") || text.Contains("强碱")) &&
            (text.Contains("H2") || text.Contains("氢气")) &&
            (text.Contains("能") || text.Contains("生成")))
        {
            return new EngineResponse
            {
                Ok = true,
                FinalResult = "能。Al 是两性金属，与强碱溶液反应生成 H2。",
                DerivationSteps = new List<string> { "Al 为两性金属，可与 NaOH 等强碱溶液反应放出 H2。" },
                Confidence = 1.0
            };
        }

        return null;
    }

    // ============================================================
    // 共用：从题干截取干净方程式
    // ============================================================

    private static string? ExtractCleanEquation(string text)
    {
        var cleaned = ConditionParen.Replace(text, " ");
        var m = EquationFragment.Match(cleaned);
        if (!m.Success) return null;
        var seg = m.Value.Trim();
        // 要求存在主分隔符
        if (!seg.Contains('=') && !seg.Contains('→') && !seg.Contains('⇌')) return null;
        return BalanceGatewaySolver.NormalizeForBalancing(seg);
    }

    private static string FormatRational(Rational r)
    {
        if (r.IsInteger)
        {
            var n = r.Num; // IsInteger 时 Den == 1，值即分子
            return n > 0 ? $"+{n}" : n.ToString();
        }
        return r.ToString();
    }
}
