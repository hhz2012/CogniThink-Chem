using CogniThink.Chem.Core.Benchmark;
using CogniThink.Chem.Core.Chemistry;
using CogniThink.Chem.Core.Data;
using CogniThink.Chem.Core.Ionic;
using CogniThink.Chem.Core.Math;
using CogniThink.Chem.Core.Reactions;
using CogniThink.Chem.Core.Rules;
using CogniThink.Chem.Core.Stoichiometry;

namespace CogniThink.Chem.Core.Inference;

/// <summary>
/// 水溶液离子平衡推断请求：溶质组成 + 可选题给 pH + 待比较粒子清单；
/// 给出 <see cref="AqueousRequest.Conversion"/> 时走沉淀转化（Ksp）路径。
/// </summary>
public sealed class AqueousRequest
{
    /// <summary>溶质清单（L2 化学式 + 浓度/体积）。</summary>
    public List<SoluteSpec> Solutes { get; set; } = new();

    /// <summary>题给 pH（可空；给出时按题给酸碱性与电荷守恒定序，物料/质子守恒因组成不定而不出）。</summary>
    public string Ph { get; set; } = "";

    /// <summary>题给附加条件（仅存档展示，引擎不据文本推断）。</summary>
    public string Note { get; set; } = "";

    /// <summary>要求比较浓度的粒子清单（L2 离子写法；空 = 不排序）。</summary>
    public List<string> OrderSpecies { get; set; } = new();

    /// <summary>沉淀转化条件（可空）。</summary>
    public AqueousConversionSpec? Conversion { get; set; }
}

/// <summary>水溶液离子平衡推断结果。</summary>
public sealed class AqueousResult
{
    public InferenceStatus Status { get; set; }

    public string Message { get; set; } = "";

    /// <summary>浓度由大到小的分组序列（组内 "=" 相连，组间 ">" 相连；欠定时为空）。</summary>
    public List<string> Order { get; set; } = new();

    /// <summary>守恒等式（电荷/物料/质子；沉淀转化路径为 Ksp 表达式）。</summary>
    public List<string> ConservationEquations { get; set; } = new();

    /// <summary>定量结论（显酸碱性依据 / 转化平衡常数比较）。</summary>
    public string? QuantitativeConclusion { get; set; }

    /// <summary>逐字弱过程方程（电离/水解/转化，均取自反应库；强电解质完全电离式仅入步骤描述，
    /// 因离子方程式拆写校验会把"可溶盐 = 离子"判为应拆未拆）。</summary>
    public List<string> Equations { get; set; } = new();

    public List<InferenceStep> Steps { get; set; } = new();
}

/// <summary>
/// 水溶液离子平衡引擎（"能推断"）：盐类水解 / 弱电解质电离 / 三大守恒 / 粒子浓度比较 / 沉淀转化。
/// 流程（每步落 <see cref="InferenceStep"/>，弱过程方程一律取反应库条目逐字溯源）：
/// <list type="number">
/// <item>溶质分类（electrolytes.json 强酸强碱弱电解质 + IonizationMap 可溶盐拆解），
/// 写出完全电离式并算混合后式量浓度（全程 <see cref="Rational"/> 精确）。</item>
/// <item>弱过程方程：水解查反应库 HYD 条目、电离查 ION 条目、水的电离恒取 RX-PL-ION-005。</item>
/// <item>酸碱性判定：Kh = Kw/Ka（SOL-006，越弱越水解）与竞争电离常数做精确有理比较；
/// 缓冲体系比较 Ka 与 Kh；多元弱酸酸式根比较水解 Kh 与电离 Ka。</item>
/// <item>三大守恒（SOL-007）：电荷 = Σ带系数浓度；物料按共轭阶梯总量比；质子按参考形态得失质子。</item>
/// <item>排序：完全电离物种 > 弱过程物种；同式量浓度并列时由电荷守恒 + 酸碱性定序；
/// 水解产物与主导水离子的相对大小由质子守恒判定；凡定性判据不可判的对（如 NaHCO3 中
/// c(CO3^2-) 与 c(OH^-) 需解联立平衡开方）如实 Underdetermined，绝不含糊输出。</item>
/// </list>
/// 沉淀转化路径（SOL-003/SOL-004）：转化方程取反应库 PPT 条目，
/// K = Ksp(原)/Ksp(新) 用 <see cref="ConstantSpec.Ratio"/> 精确求商比较。
/// 库外物质与拆解不唯一的溶质直接 NoSolution，不猜。
/// </summary>
public static class AqueousEquilibriumEngine
{
    /// <summary>酸碱性判定结果。</summary>
    private enum Acidity
    {
        Acidic,
        Neutral,
        Basic,
    }

    /// <summary>排序分层。</summary>
    private enum Tier
    {
        Full,
        Molecule,
        HydrolysisProduct,
        IonizationProduct,
        Water,
    }

    /// <summary>推断入口。</summary>
    public static AqueousResult Infer(AqueousRequest request, ReactionLibrary? library = null)
    {
        library ??= ReactionLibrary.Default.Value;
        var constants = RuleStore.Load().Query<AcidConstantsData>("acid-constants");
        if (request.Conversion is { } conversion)
        {
            return InferConversion(conversion, constants);
        }

        if (request.Solutes.Count == 0)
        {
            return new AqueousResult
            {
                Status = InferenceStatus.NoSolution,
                Message = "水溶液离子平衡题需给出溶液组成（given.solution.solutes）。",
            };
        }

        return InferComparison(request, constants, library);
    }

    // -----------------------------------------------------------------------
    // 沉淀转化路径（SOL-003 / SOL-004）
    // -----------------------------------------------------------------------

    private static AqueousResult InferConversion(AqueousConversionSpec conv, AcidConstantsData constants)
    {
        var result = new AqueousResult();
        var library = ReactionLibrary.Default.Value;
        var entry = library
            .FindByReactants(new[] { conv.Original, conv.AddedIon })
            .FirstOrDefault(e => e.Products.Contains(conv.Target, StringComparer.Ordinal));
        if (entry is null)
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = $"反应库无 {conv.Original} 被 {conv.AddedIon} 转化为 {conv.Target} 的记录，库外不臆造转化路径。";
            return result;
        }

        result.Equations.Add(entry.Equation);
        result.Steps.Add(new InferenceStep
        {
            Description = $"沉淀转化方程：{entry.Equation}（{conv.Original} 中加入 {conv.AddedIon} 转化为 {conv.Target}）",
            Evidence = entry.Id,
            KnowledgeId = entry.KnowledgeId,
            Source = entry.Source,
        });

        if (!constants.Ksp.TryGetValue(conv.Original, out var kspOriginal)
            || !constants.Ksp.TryGetValue(conv.Target, out var kspTarget))
        {
            result.Status = InferenceStatus.Underdetermined;
            result.Message = $"acid-constants.json 缺少 {conv.Original} 或 {conv.Target} 的 Ksp，无法比较溶解度判定转化方向。";
            return result;
        }

        var ruleKsp = FindRule("SOL-003");
        var exprOriginal = constants.KspExpressions.GetValueOrDefault(conv.Original, $"Ksp({conv.Original})");
        var exprTarget = constants.KspExpressions.GetValueOrDefault(conv.Target, $"Ksp({conv.Target})");
        result.ConservationEquations.Add(exprOriginal);
        result.ConservationEquations.Add(exprTarget);
        result.Steps.Add(new InferenceStep
        {
            Description = $"溶度积与溶解度判据（SOL-003）：{exprOriginal}；{exprTarget}",
            Evidence = "SOL-003",
            KnowledgeId = ruleKsp?.KnowledgeId ?? "",
            Source = ruleKsp?.Source ?? "",
        });

        var k = ConstantSpec.Ratio(kspOriginal, kspTarget);
        var ruleConv = FindRule("SOL-004");
        var proceeds = k > Rational.One;
        result.QuantitativeConclusion = proceeds
            ? $"K = Ksp({conv.Original})/Ksp({conv.Target}) = {k} > 1，{conv.Original} 可转化为更难溶的 {conv.Target}"
            : $"K = Ksp({conv.Original})/Ksp({conv.Target}) = {k} < 1，{conv.Original} 难以转化为 {conv.Target}";
        result.Steps.Add(new InferenceStep
        {
            Description = $"转化平衡常数（SOL-004）：K = Ksp({conv.Original})/Ksp({conv.Target})"
                + $" = {SpecText(kspOriginal)}/{SpecText(kspTarget)} = {k}",
            Evidence = "SOL-004",
            KnowledgeId = ruleConv?.KnowledgeId ?? "",
            Source = ruleConv?.Source ?? "",
        });
        result.Steps.Add(new InferenceStep
        {
            Description = $"结论：{result.QuantitativeConclusion}",
            Evidence = "SOL-004",
            KnowledgeId = ruleConv?.KnowledgeId ?? "",
            Source = ruleConv?.Source ?? "",
        });

        result.Status = InferenceStatus.Solved;
        result.Message = result.QuantitativeConclusion;
        return result;
    }

    // -----------------------------------------------------------------------
    // 粒子浓度比较路径
    // -----------------------------------------------------------------------

    private static AqueousResult InferComparison(AqueousRequest request, AcidConstantsData constants, ReactionLibrary library)
    {
        var result = new AqueousResult();

        // ---- 阶段 1：溶质分类、完全电离式与混合后式量浓度 ----
        var fullIons = new List<string>();
        var ionFormal = new Dictionary<string, Rational>(StringComparer.Ordinal);
        var molecules = new List<string>();
        var moleculeFormal = new Dictionary<string, Rational>(StringComparer.Ordinal);

        var totalVolume = Rational.Zero;
        foreach (var solute in request.Solutes)
        {
            totalVolume += Rational.Parse(solute.VolumeLiters);
        }

        var soluteIonizationEntries = new List<ReactionEntry>();
        foreach (var solute in request.Solutes)
        {
            var body = solute.Species.Trim();
            if (body.Contains('^', StringComparison.Ordinal))
            {
                return NoSolution(result, $"溶质应以化学式给出而非离子：{body}。");
            }

            var formal = MoleCalculator.MolesFromConcentration(
                Rational.Parse(solute.Concentration), Rational.Parse(solute.VolumeLiters)) / totalVolume;

            if (ElectrolyteRules.StrongAcids.Contains(body)
                || ElectrolyteRules.StrongBases.Contains(body)
                || IonizationMap.IsSolubleSalt(body))
            {
                var ions = IonizationMap.GetIons(body);
                if (ions is null)
                {
                    return NoSolution(result, $"溶质 {body} 的离子拆解不唯一（非标准可溶盐/强酸/强碱），引擎不猜。");
                }

                var parts = ions.Select(t => (t.Count > 1 ? t.Count.ToString() : "") + t.Ion);
                result.Steps.Add(new InferenceStep
                {
                    Description = $"{body} 为强电解质，完全电离：{body} = {string.Join(" + ", parts)}"
                        + $"（混合后式量浓度 {formal} mol/L）",
                    Evidence = "electrolytes.json",
                });
                foreach (var (ion, count) in ions)
                {
                    if (!ionFormal.ContainsKey(ion))
                    {
                        fullIons.Add(ion);
                        ionFormal[ion] = Rational.Zero;
                    }

                    ionFormal[ion] += formal * new Rational(count);
                }
            }
            else if (ElectrolyteRules.WeakElectrolytes.Contains(body) && body != "H2O")
            {
                var entry = library.FindByReactants(new[] { body }).FirstOrDefault();
                if (entry is null)
                {
                    return NoSolution(result, $"反应库无弱电解质 {body} 的电离条目，无法写出电离方程。");
                }

                if (!moleculeFormal.ContainsKey(body))
                {
                    molecules.Add(body);
                    moleculeFormal[body] = Rational.Zero;
                }

                moleculeFormal[body] += formal;
                soluteIonizationEntries.Add(entry);
                result.Steps.Add(new InferenceStep
                {
                    Description = $"{body} 为弱电解质，部分电离：{entry.Equation}（混合后式量浓度 {formal} mol/L）",
                    Evidence = entry.Id,
                    KnowledgeId = entry.KnowledgeId,
                    Source = entry.Source,
                });
            }
            else
            {
                return NoSolution(result, $"溶质 {body} 不在高中电解质/溶解性规则范围内，引擎不猜测其水溶液行为。");
            }
        }

        // ---- 共轭阶梯登记：物种 → 阶梯位置与式量浓度 ----
        var acidRefs = new Dictionary<string, SortedSet<int>>(StringComparer.Ordinal);
        var acidFormal = new Dictionary<string, Rational>(StringComparer.Ordinal);
        var baseRefs = new Dictionary<string, SortedSet<int>>(StringComparer.Ordinal);
        var baseFormal = new Dictionary<string, Rational>(StringComparer.Ordinal);

        void Touch(string species, Rational amount)
        {
            var acid = FindLadder(constants.AcidLadders, species);
            if (acid is not null)
            {
                Register(acidRefs, acidFormal, acid.Value.Name, acid.Value.Index, amount);
                return;
            }

            var b = FindLadder(constants.BaseLadders, species);
            if (b is not null)
            {
                Register(baseRefs, baseFormal, b.Value.Name, b.Value.Index, amount);
            }
        }

        foreach (var ion in fullIons)
        {
            Touch(ion, ionFormal[ion]);
        }

        foreach (var molecule in molecules)
        {
            Touch(molecule, moleculeFormal[molecule]);
        }

        // ---- 阶段 2：弱过程方程（水解 HYD / 电离 ION / 水的电离恒取 ION-005） ----
        var hydrolysisEntries = new List<ReactionEntry>();
        var ionizationProductEntries = new List<ReactionEntry>();
        foreach (var (name, refs) in acidRefs)
        {
            var ladder = constants.AcidLadders[name];
            for (var j = refs.Max; j >= 1; j--)
            {
                var entry = library.FindByReactants(new[] { ladder[j], "H2O" }).FirstOrDefault();
                if (entry is not null)
                {
                    hydrolysisEntries.Add(entry);
                }
            }

            if (refs.Count == 1)
            {
                var refIndex = refs.Min;
                for (var j = refIndex + 1; j < ladder.Count; j++)
                {
                    // 溶质分子的电离条目已在阶段 1 收录，避免重复
                    if (molecules.Contains(ladder[j - 1], StringComparer.Ordinal))
                    {
                        continue;
                    }

                    var entry = library.FindByReactants(new[] { ladder[j - 1] })
                        .FirstOrDefault(e => e.Products.Contains(ladder[j], StringComparer.Ordinal));
                    if (entry is not null)
                    {
                        ionizationProductEntries.Add(entry);
                    }
                }
            }
        }

        foreach (var (name, refs) in baseRefs)
        {
            var ladder = constants.BaseLadders[name];
            for (var j = refs.Max; j >= 1; j--)
            {
                var entry = library.FindByReactants(new[] { ladder[j], "H2O" }).FirstOrDefault();
                if (entry is not null)
                {
                    hydrolysisEntries.Add(entry);
                }
            }
        }

        var waterEntry = library.FindByReactants(new[] { "H2O" })
            .FirstOrDefault(e => e.Products.Contains("H^+", StringComparer.Ordinal)
                                 && e.Products.Contains("OH^-", StringComparer.Ordinal));
        if (waterEntry is null)
        {
            return NoSolution(result, "反应库缺少水的电离条目（RX-PL-ION-005）。");
        }

        foreach (var entry in soluteIonizationEntries.Concat(ionizationProductEntries).Concat(hydrolysisEntries).Concat(new[] { waterEntry }))
        {
            result.Equations.Add(entry.Equation);
            result.Steps.Add(new InferenceStep
            {
                Description = $"弱过程方程：{entry.Equation}（{entry.Name}）",
                Evidence = entry.Id,
                KnowledgeId = entry.KnowledgeId,
                Source = entry.Source,
            });
        }

        // ---- 阶段 3：酸碱性判定（题给 pH 优先；否则 Kh = Kw/Ka 与竞争常数精确比较） ----
        var ruleKh = FindRule("SOL-006");
        var phGiven = !string.IsNullOrWhiteSpace(request.Ph);
        Acidity acidity;
        if (phGiven)
        {
            var ph = Rational.Parse(request.Ph);
            var seven = new Rational(7);
            acidity = ph < seven ? Acidity.Acidic : ph > seven ? Acidity.Basic : Acidity.Neutral;
            var phText = acidity switch
            {
                Acidity.Neutral => "溶液显中性：c(H^+) = c(OH^-)",
                Acidity.Acidic => "溶液显酸性：c(H^+) > c(OH^-)",
                _ => "溶液显碱性：c(OH^-) > c(H^+)",
            };
            result.Steps.Add(new InferenceStep
            {
                Description = $"题给 pH = {request.Ph}，{phText}",
                Evidence = "SOL-007",
                KnowledgeId = ruleKh?.KnowledgeId ?? "",
                Source = ruleKh?.Source ?? "",
            });
        }
        else
        {
            var verdict = JudgeAcidity(constants, acidRefs, baseRefs, molecules, result, ruleKh);
            if (verdict is null)
            {
                result.Status = InferenceStatus.Underdetermined;
                result.Message = "缺少所需电离/水解平衡常数，无法判定酸碱性，如实欠定。";
                return result;
            }

            acidity = verdict.Value;
        }

        // ---- 阶段 4：三大守恒（SOL-007） ----
        var ruleCons = FindRule("SOL-007");
        BuildConservations(request, constants, fullIons, ionFormal, molecules, moleculeFormal,
            acidRefs, acidFormal, baseRefs, baseFormal, phGiven, result);
        if (result.ConservationEquations.Count > 0)
        {
            result.Steps.Add(new InferenceStep
            {
                Description = $"三大守恒（SOL-007）：{string.Join("；", result.ConservationEquations)}",
                Evidence = "SOL-007",
                KnowledgeId = ruleCons?.KnowledgeId ?? "",
                Source = ruleCons?.Source ?? "",
            });
        }

        // ---- 阶段 5：粒子浓度排序 ----
        if (request.OrderSpecies.Count > 0)
        {
            var orderStatus = TryBuildOrder(request, constants, fullIons, ionFormal, molecules,
                acidRefs, baseRefs, acidity, phGiven, result);
            if (orderStatus is not null)
            {
                result.Status = orderStatus.Value.Item1;
                result.Message = orderStatus.Value.Item2;
                result.Order.Clear();
                return result;
            }

            result.Status = InferenceStatus.Solved;
            result.Message = "粒子浓度由大到小：" + string.Join(" > ", result.Order);
            result.Steps.Add(new InferenceStep
            {
                Description = "排序结论：" + result.Message,
                Evidence = "SOL-007",
                KnowledgeId = ruleCons?.KnowledgeId ?? "",
                Source = ruleCons?.Source ?? "",
            });
        }
        else
        {
            result.Status = InferenceStatus.Solved;
            result.Message = result.QuantitativeConclusion ?? "已写出弱过程方程与守恒关系。";
        }

        if (result.QuantitativeConclusion is null)
        {
            result.QuantitativeConclusion = acidity switch
            {
                Acidity.Acidic => "溶液显酸性：c(H^+) > c(OH^-)",
                Acidity.Neutral => "溶液显中性：c(H^+) = c(OH^-)",
                _ => "溶液显碱性：c(OH^-) > c(H^+)",
            };
        }

        return result;
    }

    /// <summary>酸碱性判定：缓冲比较 Ka 与 Kh；酸式根比较水解 Kh 与电离 Ka；单一水解离子直接定性与否。</summary>
    private static Acidity? JudgeAcidity(
        AcidConstantsData constants,
        Dictionary<string, SortedSet<int>> acidRefs,
        Dictionary<string, SortedSet<int>> baseRefs,
        List<string> molecules,
        AqueousResult result,
        Rule? ruleKh)
    {
        foreach (var molecule in molecules)
        {
            var acid = FindLadder(constants.AcidLadders, molecule);
            if (acid is null)
            {
                return null;
            }

            var ladder = acid.Value.Ladder;
            var ka = KaForStep(constants, acid.Value.Name, ladder, 1);
            if (ka is null)
            {
                return null;
            }

            if (!acidRefs.TryGetValue(acid.Value.Name, out var refs) || refs.Max == 0)
            {
                result.Steps.Add(new InferenceStep
                {
                    Description = $"{molecule} 为弱酸，电离使溶液显酸性（SOL-006）：Ka({acid.Value.Name}) = {SpecText(ka)}",
                    Evidence = "SOL-006",
                    KnowledgeId = ruleKh?.KnowledgeId ?? "",
                    Source = ruleKh?.Source ?? "",
                });
                return Acidity.Acidic;
            }

            if (refs.Max >= 1)
            {
                // 缓冲：弱酸分子与其共轭碱并存，比较电离 Ka 与水解 Kh = Kw/Ka
                var kh = ConstantSpec.Ratio(constants.Kw, ka);
                var acidic = ka.ToRational() > kh;
                result.Steps.Add(new InferenceStep
                {
                    Description = $"缓冲体系电离与水解竞争（SOL-006）：Ka({acid.Value.Name}) = {SpecText(ka)}"
                        + $" {(acidic ? ">" : "<")} Kh({ladder[1]}) = Kw/Ka = {SpecText(constants.Kw)}/{SpecText(ka)} = {kh}，"
                        + $"电离{(acidic ? "大于" : "小于")}水解，溶液显{(acidic ? "酸" : "碱")}性",
                    Evidence = "SOL-006",
                    KnowledgeId = ruleKh?.KnowledgeId ?? "",
                    Source = ruleKh?.Source ?? "",
                });
                return acidic ? Acidity.Acidic : Acidity.Basic;
            }
        }

        foreach (var (name, refs) in acidRefs)
        {
            var ladder = constants.AcidLadders[name];
            var refIndex = refs.Max;
            var kaForHydrolysis = KaForStep(constants, name, ladder, refIndex);
            if (kaForHydrolysis is null)
            {
                return null;
            }

            var kh = ConstantSpec.Ratio(constants.Kw, kaForHydrolysis);
            if (refs.Count == 1 && refIndex > 0 && refIndex < ladder.Count - 1)
            {
                // 酸式根（如 HCO3^-）：水解 Kh = Kw/Ka1 与电离 Ka2 竞争
                var kaIonize = KaForStep(constants, name, ladder, refIndex + 1);
                if (kaIonize is null)
                {
                    return null;
                }

                var basic = kh > kaIonize.ToRational();
                result.Steps.Add(new InferenceStep
                {
                    Description = $"{ladder[refIndex]} 水解与电离竞争（SOL-006）：Kh = Kw/Ka{refIndex} = {SpecText(constants.Kw)}/{SpecText(kaForHydrolysis)} = {kh}"
                        + $" {(basic ? ">" : "<")} Ka{refIndex + 1} = {SpecText(kaIonize)}，"
                        + $"水解{(basic ? "大于" : "小于")}电离，溶液显{(basic ? "碱" : "酸")}性",
                    Evidence = "SOL-006",
                    KnowledgeId = ruleKh?.KnowledgeId ?? "",
                    Source = ruleKh?.Source ?? "",
                });
                return basic ? Acidity.Basic : Acidity.Acidic;
            }

            result.Steps.Add(new InferenceStep
            {
                Description = $"{ladder[refIndex]} 为弱酸根，水解使溶液显碱性（越弱越水解，SOL-006）："
                    + $"Kh = Kw/Ka{refIndex}({name}) = {SpecText(constants.Kw)}/{SpecText(kaForHydrolysis)} = {kh}",
                Evidence = "SOL-006",
                KnowledgeId = ruleKh?.KnowledgeId ?? "",
                Source = ruleKh?.Source ?? "",
            });
            return Acidity.Basic;
        }

        foreach (var (name, refs) in baseRefs)
        {
            var ladder = constants.BaseLadders[name];
            var kb = constants.Kb.GetValueOrDefault(name);
            if (kb is null)
            {
                return null;
            }

            var kh = ConstantSpec.Ratio(constants.Kw, kb);
            result.Steps.Add(new InferenceStep
            {
                Description = $"{ladder[refs.Max]} 为弱碱阳离子，水解使溶液显酸性（越弱越水解，SOL-006）："
                    + $"Kh = Kw/Kb({name}) = {SpecText(constants.Kw)}/{SpecText(kb)} = {kh}",
                Evidence = "SOL-006",
                KnowledgeId = ruleKh?.KnowledgeId ?? "",
                Source = ruleKh?.Source ?? "",
            });
            return Acidity.Acidic;
        }

        // 强酸强碱盐：不水解，溶液由 pH 路径或水的电离定中性
        result.Steps.Add(new InferenceStep
        {
            Description = "溶质均为强酸强碱完全电离产物，无水解竞争，溶液显中性：c(H^+) = c(OH^-)",
            Evidence = "SOL-006",
            KnowledgeId = ruleKh?.KnowledgeId ?? "",
            Source = ruleKh?.Source ?? "",
        });
        return Acidity.Neutral;
    }

    /// <summary>构造三大守恒：电荷恒出；题给 pH 时组成不定，物料/质子守恒不出；质子守恒要求阶梯参考形态唯一。</summary>
    private static void BuildConservations(
        AqueousRequest request,
        AcidConstantsData constants,
        List<string> fullIons,
        Dictionary<string, Rational> ionFormal,
        List<string> molecules,
        Dictionary<string, Rational> moleculeFormal,
        Dictionary<string, SortedSet<int>> acidRefs,
        Dictionary<string, Rational> acidFormal,
        Dictionary<string, SortedSet<int>> baseRefs,
        Dictionary<string, Rational> baseFormal,
        bool phGiven,
        AqueousResult result)
    {
        static string C(string species, int coefficient = 1) =>
            coefficient == 1 ? $"c({species})" : $"{coefficient}c({species})";

        // 电荷守恒：全部阳离子（含 H^+）= 全部阴离子（含 OH^-）
        var cations = fullIons.Where(i => ChargeOf(i) > 0).ToList();
        var ladderAnions = new List<string>();
        foreach (var (name, refs) in acidRefs)
        {
            var ladder = constants.AcidLadders[name];
            for (var j = ladder.Count - 1; j >= 1; j--)
            {
                if (!ladderAnions.Contains(ladder[j], StringComparer.Ordinal))
                {
                    ladderAnions.Add(ladder[j]);
                }
            }
        }

        var otherAnions = fullIons
            .Where(i => ChargeOf(i) < 0 && i != "OH^-" && !ladderAnions.Contains(i, StringComparer.Ordinal))
            .ToList();
        var left = new List<string>();
        foreach (var ion in cations)
        {
            left.Add(C(ion, ChargeOf(ion)));
        }

        left.Add("c(H^+)");
        var right = new List<string>();
        foreach (var ion in ladderAnions.Concat(otherAnions))
        {
            right.Add(C(ion, -ChargeOf(ion)));
        }

        right.Add("c(OH^-)");
        result.ConservationEquations.Add($"{string.Join(" + ", left)} = {string.Join(" + ", right)}");

        if (phGiven)
        {
            return;
        }

        // 物料守恒：每个被触及的共轭阶梯总量与反离子式量浓度成整数比
        foreach (var (name, _) in acidRefs)
        {
            var ladder = constants.AcidLadders[name];
            var terms = ladder.Select(s => $"c({s})").ToList();
            var cation = fullIons.FirstOrDefault(i => ChargeOf(i) > 0);
            if (cation is null || !acidFormal.TryGetValue(name, out var ladderTotal) || ladderTotal <= Rational.Zero)
            {
                continue;
            }

            var ratio = ionFormal[cation] / ladderTotal;
            result.ConservationEquations.Add(MaterialText(ratio, $"c({cation})", terms));
        }

        foreach (var (name, _) in baseRefs)
        {
            var ladder = constants.BaseLadders[name];
            var terms = ladder.Select(s => $"c({s})").ToList();
            var anion = fullIons.FirstOrDefault(i => ChargeOf(i) < 0 && !ladder.Contains(i, StringComparer.Ordinal));
            if (anion is null || !baseFormal.TryGetValue(name, out var ladderTotal) || ladderTotal <= Rational.Zero)
            {
                continue;
            }

            var ratio = ionFormal[anion] / ladderTotal;
            result.ConservationEquations.Add(MaterialText(ratio, $"c({anion})", terms));
        }

        // 质子守恒：参考形态唯一时按得失质子系数写出
        foreach (var (name, refs) in acidRefs)
        {
            if (refs.Count != 1)
            {
                continue;
            }

            var ladder = constants.AcidLadders[name];
            var refIndex = refs.Min;
            var gained = new List<string>();
            var lost = new List<string>();
            for (var j = ladder.Count - 1; j >= 0; j--)
            {
                if (j == refIndex)
                {
                    continue;
                }

                var coefficient = System.Math.Abs(j - refIndex);
                (j < refIndex ? gained : lost).Add(C(ladder[j], coefficient));
            }

            if (lost.Count == 0)
            {
                result.ConservationEquations.Add(
                    $"c(OH^-) = c(H^+){(gained.Count > 0 ? " + " + string.Join(" + ", gained) : "")}");
            }
            else
            {
                var leftSide = "c(H^+)" + (gained.Count > 0 ? " + " + string.Join(" + ", gained) : "");
                var rightSide = "c(OH^-) + " + string.Join(" + ", lost);
                result.ConservationEquations.Add($"{leftSide} = {rightSide}");
            }
        }

        foreach (var (name, refs) in baseRefs)
        {
            if (refs.Count != 1)
            {
                continue;
            }

            var ladder = constants.BaseLadders[name];
            var refIndex = refs.Max;
            var lost = new List<string>();
            var gained = new List<string>();
            for (var j = 0; j < ladder.Count; j++)
            {
                if (j == refIndex)
                {
                    continue;
                }

                var coefficient = System.Math.Abs(j - refIndex);
                // 碱阶梯索引越大质子越多：j < refIndex 为失质子形态
                (j < refIndex ? lost : gained).Add(C(ladder[j], coefficient));
            }

            var leftSide = "c(H^+)" + (gained.Count > 0 ? " + " + string.Join(" + ", gained) : "");
            var rightSide = "c(OH^-)" + (lost.Count > 0 ? " + " + string.Join(" + ", lost) : "");
            result.ConservationEquations.Add($"{leftSide} = {rightSide}");
        }
    }

    /// <summary>物料守恒文本：ratio = 反离子/阶梯总量，按整数倍写到系数较小的一侧；有系数时才加括号。</summary>
    private static string MaterialText(Rational ratio, string ionTerm, List<string> ladderTerms)
    {
        var joined = string.Join(" + ", ladderTerms);
        var bracketed = ladderTerms.Count > 1 ? "[" + joined + "]" : joined;
        if (ratio == Rational.One)
        {
            return $"{ionTerm} = {joined}";
        }

        if (ratio.IsInteger)
        {
            return $"{ionTerm} = {ratio}{bracketed}";
        }

        var inverse = Rational.One / ratio;
        if (inverse.IsInteger)
        {
            return $"{bracketed} = {inverse}{ionTerm}";
        }

        return $"{ionTerm} = {ratio}·{bracketed}";
    }

    /// <summary>
    /// 排序：完全电离层按式量浓度降序（并列时电荷守恒 + 酸碱性定序）；
    /// 溶质弱酸分子次之；主导水离子 > 水解产物（质子守恒）> 次要水离子。
    /// 电离产物（如 NaHCO3 中的 CO3^2-）与 OH^- 的比较超出定性判据 → 如实欠定。
    /// 返回 null 表示排序成功（result.Order 已填），否则返回 (状态, 消息)。
    /// </summary>
    private static (InferenceStatus, string)? TryBuildOrder(
        AqueousRequest request,
        AcidConstantsData constants,
        List<string> fullIons,
        Dictionary<string, Rational> ionFormal,
        List<string> molecules,
        Dictionary<string, SortedSet<int>> acidRefs,
        Dictionary<string, SortedSet<int>> baseRefs,
        Acidity acidity,
        bool phGiven,
        AqueousResult result)
    {
        static string C(string species) => $"c({species})";

        if (phGiven)
        {
            // 题给 pH 路径：只处理"单一阳离子 + 单一阴离子 + H^+/OH^-"的中和终点判断
            var mainAnions = request.OrderSpecies
                .Where(sp => ChargeOfSafe(sp) < 0 && sp != "OH^-").ToList();
            var mainCations = request.OrderSpecies
                .Where(sp => ChargeOfSafe(sp) > 0 && sp != "H^+").ToList();
            if (mainCations.Count == 1 && mainAnions.Count == 1
                && ChargeOf(mainCations[0]) == -ChargeOf(mainAnions[0]))
            {
                var cat = C(mainCations[0]);
                var an = C(mainAnions[0]);
                if (acidity == Acidity.Neutral)
                {
                    result.Order.Add($"{cat} = {an}");
                    result.Order.Add("c(H^+) = c(OH^-)");
                    result.Steps.Add(new InferenceStep
                    {
                        Description = $"电荷守恒 + 题给中性：c({mainCations[0]}) = c({mainAnions[0]})，c(H^+) = c(OH^-)",
                        Evidence = "SOL-007",
                    });
                }
                else
                {
                    var first = acidity == Acidity.Acidic ? an : cat;
                    var second = acidity == Acidity.Acidic ? cat : an;
                    result.Order.Add(first);
                    result.Order.Add(second);
                    result.Order.Add(acidity == Acidity.Acidic ? "c(H^+)" : "c(OH^-)");
                    result.Order.Add(acidity == Acidity.Acidic ? "c(OH^-)" : "c(H^+)");
                }

                return null;
            }

            return (InferenceStatus.Underdetermined,
                "题给 pH 但主离子对不满足 1:1 电荷配对，无法仅由电荷守恒定序，如实欠定。");
        }

        var tier = new Dictionary<string, Tier>(StringComparer.Ordinal);
        foreach (var species in request.OrderSpecies)
        {
            if (fullIons.Contains(species, StringComparer.Ordinal))
            {
                tier[species] = Tier.Full;
            }
            else if (molecules.Contains(species, StringComparer.Ordinal))
            {
                tier[species] = Tier.Molecule;
            }
            else if (species is "H^+" or "OH^-")
            {
                tier[species] = Tier.Water;
            }
            else
            {
                var acid = FindLadder(constants.AcidLadders, species);
                var b = acid is null ? FindLadder(constants.BaseLadders, species) : null;
                var refs = acid is not null
                    ? acidRefs.GetValueOrDefault(acid.Value.Name)
                    : b is not null ? baseRefs.GetValueOrDefault(b.Value.Name) : null;
                if (refs is null)
                {
                    return (InferenceStatus.NoSolution, $"粒子 {species} 不在溶液组成的电离/水解物种清单中，引擎不猜。");
                }

                var index = acid is not null ? acid.Value.Index : b!.Value.Index;
                var refIndex = refs.Max;
                var isAcidLadder = acid is not null;
                // 酸阶梯：索引越大质子越少；碱阶梯相反。相对参考形态"失质子"的物种为电离产物。
                var ionized = isAcidLadder ? index > refIndex : index < refIndex;
                tier[species] = ionized ? Tier.IonizationProduct : Tier.HydrolysisProduct;
            }
        }

        var ionizationProducts = request.OrderSpecies.Where(sp => tier[sp] == Tier.IonizationProduct).ToList();
        if (ionizationProducts.Count > 0)
        {
            return (InferenceStatus.Underdetermined,
                $"c({ionizationProducts[0]}) 为电离产物，与 c(OH^-) 的相对大小需解联立平衡方程（含开方），"
                + "超出三大守恒定性判据，如实欠定，不臆断。");
        }

        var hydrolysisProducts = request.OrderSpecies.Where(sp => tier[sp] == Tier.HydrolysisProduct).ToList();
        if (hydrolysisProducts.Count > 1)
        {
            return (InferenceStatus.Underdetermined,
                $"水解产物 {string.Join("、", hydrolysisProducts)} 之间的相对大小无定性判据，如实欠定。");
        }

        // 完全电离层：式量浓度降序；并列时由电荷守恒 + 酸碱性定序
        var full = request.OrderSpecies.Where(sp => tier[sp] == Tier.Full).ToList();
        var ordered = full
            .OrderByDescending(sp => ionFormal[sp], RationalComparer.Instance)
            .ToList();
        for (var i = 0; i + 1 < ordered.Count; i++)
        {
            if (ionFormal[ordered[i]] != ionFormal[ordered[i + 1]])
            {
                continue;
            }

            var a = ordered[i];
            var b = ordered[i + 1];
            if (ChargeOf(a) > 0 == ChargeOf(b) > 0)
            {
                continue; // 同性且等式量浓度：并列等号组，无需定序
            }

            var cation = ChargeOf(a) > 0 ? a : b;
            var anion = ChargeOf(a) > 0 ? b : a;
            if (acidity == Acidity.Neutral)
            {
                continue; // 中性并列：保留 "=" 组
            }

            var anionFirst = acidity == Acidity.Acidic;
            ordered[i] = anionFirst ? anion : cation;
            ordered[i + 1] = anionFirst ? cation : anion;
            result.Steps.Add(new InferenceStep
            {
                Description = $"{a} 与 {b} 式量浓度相同，电荷守恒 + 溶液显{(acidity == Acidity.Acidic ? "酸" : "碱")}性："
                    + $"c({anion}) {(anionFirst ? ">" : "<")} c({cation})",
                Evidence = "SOL-007",
            });
        }

        // 并列等号分组：相邻等式量浓度且同性 → "=" 组
        var groups = new List<List<string>>();
        foreach (var species in ordered)
        {
            if (groups.Count > 0
                && ionFormal[species] == ionFormal[groups[^1][0]]
                && ChargeOf(species) > 0 == ChargeOf(groups[^1][0]) > 0)
            {
                groups[^1].Add(species);
            }
            else
            {
                groups.Add(new List<string> { species });
            }
        }

        foreach (var group in groups)
        {
            result.Order.Add(string.Join(" = ", group.Select(C)));
        }

        if (groups.Count > 0)
        {
            result.Steps.Add(new InferenceStep
            {
                Description = "完全电离物种浓度远高于弱过程物种（电离/水解程度小）",
                Evidence = "SOL-006",
            });
        }

        // 溶质弱酸分子层
        foreach (var molecule in molecules)
        {
            if (request.OrderSpecies.Contains(molecule, StringComparer.Ordinal))
            {
                result.Order.Add(C(molecule));
            }
        }

        // 水离子与水解产物：主导水离子 > 水解产物（质子守恒）> 次要水离子
        var dominant = acidity == Acidity.Basic ? "OH^-" : "H^+";
        var minor = acidity == Acidity.Basic ? "H^+" : "OH^-";
        if (acidity == Acidity.Neutral)
        {
            dominant = "OH^-";
            minor = "H^+";
        }

        if (request.OrderSpecies.Contains(dominant, StringComparer.Ordinal))
        {
            result.Order.Add(C(dominant));
        }

        if (hydrolysisProducts.Count == 1 && request.OrderSpecies.Contains(hydrolysisProducts[0], StringComparer.Ordinal))
        {
            result.Order.Add(C(hydrolysisProducts[0]));
            result.Steps.Add(new InferenceStep
            {
                Description = $"质子守恒给出 c({dominant}) > c({hydrolysisProducts[0]})（水解产物由主导水离子同生但被水的电离稀释比较）",
                Evidence = "SOL-007",
            });
        }

        if (request.OrderSpecies.Contains(minor, StringComparer.Ordinal))
        {
            result.Order.Add(C(minor));
        }

        if (result.Order.Count == 0)
        {
            return (InferenceStatus.Underdetermined, "待比较粒子清单为空或均不可判，如实欠定。");
        }

        return null;
    }

    // -----------------------------------------------------------------------
    // 工具
    // -----------------------------------------------------------------------

    private static (string Name, List<string> Ladder, int Index)? FindLadder(
        Dictionary<string, List<string>> ladders, string species)
    {
        foreach (var (name, ladder) in ladders)
        {
            var index = ladder.IndexOf(species);
            if (index >= 0)
            {
                return (name, ladder, index);
            }
        }

        return null;
    }

    private static void Register(
        Dictionary<string, SortedSet<int>> refs,
        Dictionary<string, Rational> formals,
        string name,
        int index,
        Rational amount)
    {
        if (!refs.TryGetValue(name, out var set))
        {
            set = new SortedSet<int>();
            refs[name] = set;
            formals[name] = Rational.Zero;
        }

        set.Add(index);
        formals[name] += amount;
    }

    private static ConstantSpec? KaForStep(AcidConstantsData constants, string acid, List<string> ladder, int step)
    {
        var key = ladder.Count == 2 ? acid : $"{acid}:Ka{step}";
        return constants.Ka.GetValueOrDefault(key);
    }

    private static int ChargeOf(string species) => Species.Parse(species).Charge;

    private static int ChargeOfSafe(string species)
    {
        try
        {
            return ChargeOf(species);
        }
        catch (FormulaException)
        {
            return 0;
        }
    }

    private static string SpecText(ConstantSpec spec) => $"{spec.Mantissa}×10^{spec.Exponent}";

    private static Rule? FindRule(string id) =>
        ChemDataLoader.LoadRules(DataRoot.Resolve()).FirstOrDefault(r => r.Id == id);

    private static AqueousResult NoSolution(AqueousResult result, string message)
    {
        result.Status = InferenceStatus.NoSolution;
        result.Message = message;
        return result;
    }
}

/// <summary>Rational 降序比较器。</summary>
internal sealed class RationalComparer : IComparer<Rational>
{
    public static readonly RationalComparer Instance = new();

    public int Compare(Rational x, Rational y)
    {
        var diff = x - y;
        return diff > Rational.Zero ? 1 : diff < Rational.Zero ? -1 : 0;
    }
}
