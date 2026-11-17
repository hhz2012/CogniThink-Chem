using CogniThink.Chem.Core.Balancing;
using CogniThink.Chem.Core.Benchmark;
using CogniThink.Chem.Core.Chemistry;
using CogniThink.Chem.Core.Data;
using CogniThink.Chem.Core.Math;
using CogniThink.Chem.Core.Parsing;
using CogniThink.Chem.Core.Reactions;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.Chem.Core.Inference;

/// <summary>电化学推断请求：装置类型、电极、电解质、介质与（可空）总反应。</summary>
public sealed class ElectrochemRequest
{
    /// <summary>"galvanic"（原电池）/ "electrolytic"（电解池）。</summary>
    public string Type { get; set; } = "";

    /// <summary>电极材料（L2 写法）。原电池按题给 [负极, 正极]，电解池按 [阳极, 阴极]；惰性电极写 "C"/"Pt"。</summary>
    public List<string> Electrodes { get; set; } = new();

    /// <summary>电解质溶液/熔融物（L2 写法），如 "CuSO4"、"NaCl"。</summary>
    public string Electrolyte { get; set; } = "";

    /// <summary>介质："acidic" / "basic"；其它（如熔融体系）不适用水溶液离子-电子法。</summary>
    public string Medium { get; set; } = "";

    /// <summary>总反应（可空）。给出时以此拆解半反应；为空时按电极+电解质检索知识库 ELC 条目。</summary>
    public string Overall { get; set; } = "";

    /// <summary>通入气体燃料/氧化剂（燃料电池用），仅作记录，不参与拆解。</summary>
    public List<string> GasFeeds { get; set; } = new();
}

/// <summary>电化学推断结果：电极反应式（按装置类型填对应两项）+ 总反应 + 可溯源步骤。</summary>
public sealed class ElectrochemResult
{
    public InferenceStatus Status { get; set; }

    public string Message { get; set; } = "";

    /// <summary>电极反应式（Solved 时按装置类型只填对应两项）。</summary>
    public ElectrodeEquationsSpec? Electrodes { get; set; }

    /// <summary>总反应原文。</summary>
    public string OverallEquation { get; set; } = "";

    public List<InferenceStep> Steps { get; set; } = new();
}

/// <summary>
/// 电化学推断引擎（Hard 电化学：写电极反应式）。知识驱动流程：
/// <list type="number">
/// <item>装置判定（原电池→负/正极，电解池→阴/阳极），依据规则 RED-004。</item>
/// <item>介质门槛：仅酸性/碱性水溶液适用离子-电子法；熔融等体系如实上抛欠定。</item>
/// <item>总反应：题给优先；为空时按"电极+电解质"精确检索知识库 ELC 条目，唯一命中才可用。</item>
/// <item>用 <see cref="RedoxBalancer"/> 拆解氧化/还原半反应；一侧缺失时按电子守恒做
/// "总反应 − 倍数×已知半反应"的减法推导（移项规则见 <see cref="DeriveBySubtraction"/>）。</item>
/// <item>每条电极式过 HalfReaction 模式校验（守恒 + 电子项）。</item>
/// <item>原电池且两电极材料都在金属活动性顺序表中时，核对"活泼者作负极"；矛盾即无解。</item>
/// <item>两半反应按转移电子数缩放相加，与总反应净系数逐项比对，电子守恒闭合才放行。</item>
/// </list>
/// 铁律：每一步结论可溯源（Evidence/KnowledgeId/Source）；推不出时如实 Underdetermined/NoSolution，绝不猜测。
/// </summary>
public static class ElectrochemEngine
{
    private const string ElectronKey = "e^-";

    private static readonly Lazy<Rule?> RedRule = new(() => FindRule("RED-004"));
    private static readonly Lazy<Rule?> ElectronConsRule = new(() => FindRule("CONS-ELEC-001"));
    private static readonly Lazy<List<string>> ActivitySeries = new(() =>
        RuleStore.Load().Query<ActivitySeriesData>("activity-series").Series);

    /// <summary>推断电极反应式。</summary>
    /// <param name="request">装置与总反应规格。</param>
    /// <param name="library">反应库（缺省用全局缓存；仅在未题给总反应时检索）。</param>
    public static ElectrochemResult Infer(ElectrochemRequest request, ReactionLibrary? library = null)
    {
        var result = new ElectrochemResult();
        var galvanic = request.Type == "galvanic";
        var electrolytic = request.Type == "electrolytic";
        if (!galvanic && !electrolytic)
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = $"装置类型只能是 galvanic（原电池）或 electrolytic（电解池）：\"{request.Type}\"";
            return result;
        }

        var oxidName = galvanic ? "负极" : "阳极";
        var redName = galvanic ? "正极" : "阴极";
        result.Steps.Add(new InferenceStep
        {
            Description = galvanic
                ? "装置为原电池：发生氧化反应（失电子）的一极为负极，发生还原反应（得电子）的一极为正极。"
                : "装置为电解池：与电源正极相连、发生氧化反应（失电子）的一极为阳极，发生还原反应（得电子）的一极为阴极。",
            Evidence = $"given.cell.type={request.Type}",
            KnowledgeId = RedRule.Value?.KnowledgeId ?? "",
            Source = RedRule.Value?.Source ?? "",
        });

        if (request.Medium is not ("acidic" or "basic"))
        {
            result.Status = InferenceStatus.Underdetermined;
            result.Message = "介质非酸性/碱性水溶液：熔融体系不适用水溶液离子-电子法机械拆解，引擎无法确定电极反应式。";
            return result;
        }

        // 总反应来源：题给优先，否则知识库 ELC 条目唯一命中。
        string overall;
        string overallEvidence;
        string overallKnowledgeId;
        string overallSource;
        if (!string.IsNullOrWhiteSpace(request.Overall))
        {
            overall = request.Overall.Trim();
            overallEvidence = "given.cell.overall";
            overallKnowledgeId = "";
            overallSource = "";
        }
        else
        {
            library ??= ReactionLibrary.Default.Value;
            var reactants = request.Electrodes.Concat(new[] { request.Electrolyte })
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
            var query = electrolytic ? new List<string> { "通电" } : new List<string>();
            var matches = library.FindByReactants(reactants)
                .Where(e => e.Id.Contains("ELC", StringComparison.Ordinal)
                            && ReactionLibrary.ConditionsSatisfied(query, e.Conditions))
                .ToList();
            if (matches.Count != 1)
            {
                result.Status = InferenceStatus.Underdetermined;
                result.Message = matches.Count == 0
                    ? "题未给出总反应，且知识库中没有与电极+电解质反应物组合唯一匹配的 ELC 条目，无法确定总反应。"
                    : $"题未给出总反应，但知识库匹配到 {matches.Count} 条 ELC 条目，总反应不唯一。";
                return result;
            }

            var entry = matches[0];
            overall = entry.Equation;
            overallEvidence = entry.Id;
            overallKnowledgeId = entry.KnowledgeId;
            overallSource = entry.Source;
        }

        result.Steps.Add(new InferenceStep
        {
            Description = overallEvidence == "given.cell.overall"
                ? $"采用题给总反应：{overall}"
                : $"总反应由知识库条目 {overallEvidence} 检索得到：{overall}",
            Evidence = overallEvidence,
            KnowledgeId = overallKnowledgeId,
            Source = overallSource,
        });

        // 氧化还原拆解（离子-电子法）。
        var analysis = RedoxBalancer.SplitHalfReactions(overall, request.Medium);
        if (!analysis.Success)
        {
            result.Status = InferenceStatus.Underdetermined;
            result.Message = $"总反应氧化还原拆解失败：{analysis.FailReason}";
            return result;
        }

        if (!analysis.IsRedox)
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = "总反应中不存在元素氧化数变化，不是氧化还原反应，无电极反应可拆。";
            return result;
        }

        if (!analysis.ElectronConservation)
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = "总反应失电子总数与得电子总数不相等，与电子守恒矛盾，装置描述不成立。";
            return result;
        }

        var oxidation = analysis.OxidationHalf;
        var reduction = analysis.ReductionHalf;
        if (oxidation is null && reduction is null)
        {
            result.Status = InferenceStatus.Underdetermined;
            result.Message = $"氧化、还原半反应均无法由离子-电子法直接拆解：{analysis.FailReason}";
            return result;
        }

        result.Steps.Add(new InferenceStep
        {
            Description = $"按离子-电子法（{(request.Medium == "basic" ? "碱性" : "酸性")}介质）拆解总反应，转移电子数 {analysis.ElectronsTransferred}："
                          + (oxidation is not null && reduction is not null
                              ? $"氧化半反应 {oxidation.Expression}；还原半反应 {reduction.Expression}。"
                              : $"直接推得{(oxidation is not null ? "氧化" : "还原")}半反应 {(oxidation?.Expression ?? reduction!.Expression)}，另一侧拆解失败（{analysis.FailReason}），需经减法补全。"),
            Evidence = $"{oxidation?.Expression ?? "（氧化侧未直接推得）"}；{reduction?.Expression ?? "（还原侧未直接推得）"}",
        });

        // 一侧缺失：由电子守恒经减法推得。
        if (oxidation is null || reduction is null)
        {
            var known = oxidation ?? reduction!;
            var missingIsOxidation = oxidation is null;
            var factor = analysis.ElectronsTransferred / known.Electrons;
            if (!factor.IsInteger)
            {
                result.Status = InferenceStatus.Underdetermined;
                result.Message = $"减法推缺失半反应时电子倍数非整数（{analysis.ElectronsTransferred} ÷ {known.Electrons}），放弃推导。";
                return result;
            }

            var derived = DeriveBySubtraction(overall, known.Expression, factor, missingIsOxidation);
            if (derived.Expression is null)
            {
                result.Status = InferenceStatus.Underdetermined;
                result.Message = $"减法推缺失半反应失败：{derived.FailReason}";
                return result;
            }

            result.Steps.Add(new InferenceStep
            {
                Description = $"{(missingIsOxidation ? "氧化" : "还原")}半反应由电子守恒经减法推得（总反应 − {factor}×{known.Kind}半反应，移项后）：{derived.Expression}",
                Evidence = $"{overall} − {factor}×({known.Expression})",
            });

            var half = new HalfReactionResult
            {
                Expression = derived.Expression,
                Electrons = derived.Electrons,
                Kind = missingIsOxidation ? "氧化" : "还原",
            };
            if (missingIsOxidation)
            {
                oxidation = half;
            }
            else
            {
                reduction = half;
            }
        }

        // 电极式校验（HalfReaction 模式：守恒 + 电子项）。
        foreach (var (label, equation) in new[] { (oxidName, oxidation!.Expression), (redName, reduction!.Expression) })
        {
            var report = ChemicalCheckerService.Check(new CheckRequest
            {
                Equation = equation,
                Mode = EquationMode.HalfReaction,
                Medium = request.Medium,
            });
            if (!report.IsValid)
            {
                result.Status = InferenceStatus.Underdetermined;
                result.Message = $"{label}反应式 \"{equation}\" 未通过半反应校验：{report.FailureSummary()}";
                return result;
            }
        }

        result.Steps.Add(new InferenceStep
        {
            Description = "两条电极反应式均通过半反应校验（元素与电荷守恒、电子项只在一侧）。",
            Evidence = "ChemicalCheckerService.Check(Mode=HalfReaction)",
        });

        // 原电池：金属活动性顺序核对（两电极材料都精确在表中时）。
        if (galvanic)
        {
            var series = ActivitySeries.Value;
            var first = request.Electrodes.Count > 0 ? request.Electrodes[0] : "";
            var second = request.Electrodes.Count > 1 ? request.Electrodes[1] : "";
            var oxidizedMaterial = FindElectrodeMaterial(oxidation.Expression, first, second);
            if (series.Contains(first) && series.Contains(second) && oxidizedMaterial is not null)
            {
                var other = oxidizedMaterial == first ? second : first;
                var posOx = series.IndexOf(oxidizedMaterial);
                var posOther = series.IndexOf(other);
                if (posOx > posOther)
                {
                    result.Status = InferenceStatus.NoSolution;
                    result.Message = $"电极极性与金属活动性顺序矛盾：发生氧化的 {oxidizedMaterial}（第 {posOx + 1} 位）"
                                     + $"不应晚于 {other}（第 {posOther + 1} 位）作负极。";
                    return result;
                }

                result.Steps.Add(new InferenceStep
                {
                    Description = $"活动性核对：{oxidizedMaterial}（第 {posOx + 1} 位）位于 {other}（第 {posOther + 1} 位）之前，"
                                  + "活泼金属失电子作负极，与题给总反应一致。",
                    Evidence = $"activity-series：{oxidizedMaterial}#{posOx + 1} < {other}#{posOther + 1}",
                    KnowledgeId = RedRule.Value?.KnowledgeId ?? "",
                    Source = RedRule.Value?.Source ?? "",
                });
            }
            else
            {
                result.Steps.Add(new InferenceStep
                {
                    Description = "电极材料为惰性/化合物，极性由题给与总反应决定（不参与活动性顺序核对）。",
                    Evidence = $"given.cell.electrodes=[{string.Join(", ", request.Electrodes)}]",
                });
            }
        }

        // 电子守恒闭合：两半反应按转移电子数缩放相加应与总反应逐项一致。
        var scaleOx = analysis.ElectronsTransferred / oxidation.Electrons;
        var scaleRed = analysis.ElectronsTransferred / reduction.Electrons;
        if (!scaleOx.IsInteger || !scaleRed.IsInteger)
        {
            result.Status = InferenceStatus.Underdetermined;
            result.Message = $"半反应缩放倍数非整数（{scaleOx}、{scaleRed}），无法与总反应核对电子守恒。";
            return result;
        }

        if (!ConservationCloses(overall, oxidation.Expression, scaleOx, reduction.Expression, scaleRed))
        {
            result.Status = InferenceStatus.Underdetermined;
            result.Message = "两电极反应式按电子守恒缩放相加后与总反应不一致，推导链未闭合。";
            return result;
        }

        result.Steps.Add(new InferenceStep
        {
            Description = $"{oxidName}式 ×{scaleOx} 与{redName}式 ×{scaleRed} 相加后电子项恰好消去、与总反应逐项一致，电子守恒闭合。",
            Evidence = $"{scaleOx}×({oxidation.Expression}) + {scaleRed}×({reduction.Expression}) ≡ {overall}",
            KnowledgeId = ElectronConsRule.Value?.KnowledgeId ?? "",
            Source = ElectronConsRule.Value?.Source ?? "",
        });

        result.Electrodes = galvanic
            ? new ElectrodeEquationsSpec { Negative = oxidation.Expression, Positive = reduction.Expression }
            : new ElectrodeEquationsSpec { Anode = oxidation.Expression, Cathode = reduction.Expression };
        result.OverallEquation = overall;
        result.Status = InferenceStatus.Solved;
        result.Message = $"电极反应式推断完成：{oxidName} {oxidation.Expression}；{redName} {reduction.Expression}";
        return result;
    }

    // -----------------------------------------------------------------------
    // 减法推导缺失半反应
    // -----------------------------------------------------------------------

    /// <summary>
    /// 由"总反应净系数向量 − factor×已知半反应净系数向量"推缺失半反应。
    /// 净系数为正置左、为负（绝对值）置右；项序：先总反应该侧原有项（原顺序），
    /// 再总反应对侧翻转项（按对侧顺序），最后仅出现在已知半反应中的项（按半反应左→右顺序）。
    /// 电子项在左为还原、在右为氧化，类型必须与缺失侧一致，否则放弃（不猜）。
    /// </summary>
    private static (string? Expression, Rational Electrons, string? FailReason) DeriveBySubtraction(
        string overall,
        string knownExpression,
        Rational factor,
        bool wantOxidation)
    {
        if (!EquationParser.TryParse(overall, out var overallParsed, out _)
            || !EquationParser.TryParse(knownExpression, out var knownParsed, out _))
        {
            return (null, Rational.Zero, "总反应或已知半反应解析失败");
        }

        var balance = EquationBalancer.Balance(overallParsed!);
        if (!balance.Success)
        {
            return (null, Rational.Zero, $"总反应配平失败：{balance.FailReason}");
        }

        var species = new Dictionary<string, Species>(StringComparer.Ordinal);
        var net = BuildNetVector(overallParsed!, balance.Coefficients, Rational.One, species);
        foreach (var (key, value) in BuildNetVector(knownParsed!, WrittenCoefficients(knownParsed!), factor, species))
        {
            net[key] = net.GetValueOrDefault(key) - value;
        }

        var overallLeftKeys = OrderedKeys(overallParsed!.Left, species);
        var overallRightKeys = OrderedKeys(overallParsed.Right, species);
        var knownLeftKeys = OrderedKeys(knownParsed!.Left, species);
        var knownRightKeys = OrderedKeys(knownParsed.Right, species);
        var overallKeys = new HashSet<string>(overallLeftKeys.Concat(overallRightKeys), StringComparer.Ordinal);

        var leftParts = new List<string>();
        var rightParts = new List<string>();
        CollectSide(leftParts, net, overallLeftKeys, wantThisSide: true);
        CollectSide(leftParts, net, overallRightKeys, wantThisSide: true);
        CollectSideOnlyNotIn(leftParts, net, knownLeftKeys.Concat(knownRightKeys), overallKeys, wantThisSide: true);
        CollectSide(rightParts, net, overallRightKeys, wantThisSide: false);
        CollectSide(rightParts, net, overallLeftKeys, wantThisSide: false);
        CollectSideOnlyNotIn(rightParts, net, knownLeftKeys.Concat(knownRightKeys), overallKeys, wantThisSide: false);

        if (!net.TryGetValue(ElectronKey, out var electronNet) || electronNet.IsZero)
        {
            return (null, Rational.Zero, "减法结果不含电子项，不是半反应");
        }

        var electronsOnLeft = electronNet > Rational.Zero;
        var kind = electronsOnLeft ? "还原" : "氧化";
        var expectKind = wantOxidation ? "氧化" : "还原";
        if (kind != expectKind)
        {
            return (null, Rational.Zero, $"减法结果电子在{(electronsOnLeft ? "左" : "右")}侧（{kind}），与缺失侧类型（{expectKind}）不符");
        }

        return ($"{string.Join(" + ", leftParts)} = {string.Join(" + ", rightParts)}", electronNet.Abs(), null);
    }

    private static void CollectSide(List<string> parts, Dictionary<string, Rational> net, IEnumerable<string> keys, bool wantThisSide)
    {
        foreach (var key in keys)
        {
            var value = net.GetValueOrDefault(key);
            if (wantThisSide ? value > Rational.Zero : value < Rational.Zero)
            {
                parts.Add(FormatTerm(key, value.Abs()));
            }
        }
    }

    private static void CollectSideOnlyNotIn(
        List<string> parts,
        Dictionary<string, Rational> net,
        IEnumerable<string> keys,
        HashSet<string> exclude,
        bool wantThisSide)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            if (exclude.Contains(key) || !seen.Add(key))
            {
                continue;
            }

            var value = net.GetValueOrDefault(key);
            if (wantThisSide ? value > Rational.Zero : value < Rational.Zero)
            {
                parts.Add(FormatTerm(key, value.Abs()));
            }
        }
    }

    private static string FormatTerm(string key, Rational coefficient) =>
        coefficient == Rational.One ? key : $"{coefficient}{key}";

    /// <summary>净系数向量（左正右负），按物种规范键聚合；同时登记键→物种。</summary>
    private static Dictionary<string, Rational> BuildNetVector(
        ParsedEquation equation,
        IReadOnlyList<Rational> coefficients,
        Rational scale,
        Dictionary<string, Species> speciesRegistry)
    {
        var map = new Dictionary<string, Rational>(StringComparer.Ordinal);
        var index = 0;
        foreach (var term in equation.AllTerms)
        {
            var key = term.Species.ToString();
            speciesRegistry.TryAdd(key, term.Species);
            var signed = (index < equation.Left.Count ? coefficients[index] : -coefficients[index]) * scale;
            map[key] = map.GetValueOrDefault(key) + signed;
            index++;
        }

        return map;
    }

    private static List<Rational> WrittenCoefficients(ParsedEquation equation) =>
        equation.AllTerms.Select(t => new Rational(t.Coefficient)).ToList();

    private static List<string> OrderedKeys(IReadOnlyList<ParsedTerm> terms, Dictionary<string, Species> registry)
    {
        var keys = new List<string>();
        foreach (var term in terms)
        {
            var key = term.Species.ToString();
            registry.TryAdd(key, term.Species);
            if (!keys.Contains(key, StringComparer.Ordinal))
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    /// <summary>氧化半反应中作为中性物种出现的电极材料（与题给电极字符串精确相等）。</summary>
    private static string? FindElectrodeMaterial(string oxidationExpression, params string[] electrodes)
    {
        if (!EquationParser.TryParse(oxidationExpression, out var parsed, out _))
        {
            return null;
        }

        foreach (var term in parsed!.AllTerms)
        {
            if (term.Species.Charge != 0)
            {
                continue;
            }

            var text = term.Species.ToString();
            if (electrodes.Any(e => e == text))
            {
                return text;
            }
        }

        return null;
    }

    /// <summary>电子守恒闭合判定：scaleOx×氧化式 + scaleRed×还原式 与总反应净系数逐项一致。</summary>
    private static bool ConservationCloses(
        string overall,
        string oxidationExpression,
        Rational scaleOx,
        string reductionExpression,
        Rational scaleRed)
    {
        if (!EquationParser.TryParse(overall, out var overallParsed, out _))
        {
            return false;
        }

        var overallEq = overallParsed!;
        var balance = EquationBalancer.Balance(overallEq);
        if (!balance.Success)
        {
            return false;
        }

        var registry = new Dictionary<string, Species>(StringComparer.Ordinal);
        var diff = BuildNetVector(overallEq, balance.Coefficients, Rational.One, registry);
        if (!EquationParser.TryParse(oxidationExpression, out var oxParsed, out _)
            || !EquationParser.TryParse(reductionExpression, out var redParsed, out _))
        {
            return false;
        }

        var oxEq = oxParsed!;
        var redEq = redParsed!;
        foreach (var (key, value) in BuildNetVector(oxEq, WrittenCoefficients(oxEq), scaleOx, registry))
        {
            diff[key] = diff.GetValueOrDefault(key) - value;
        }

        foreach (var (key, value) in BuildNetVector(redEq, WrittenCoefficients(redEq), scaleRed, registry))
        {
            diff[key] = diff.GetValueOrDefault(key) - value;
        }

        return diff.Values.All(v => v.IsZero);
    }

    private static Rule? FindRule(string id) =>
        ChemDataLoader.LoadRules(DataRoot.Resolve()).FirstOrDefault(r => r.Id == id);
}
