namespace CogniThink.TutorBrain.Application.Gateway;

using System.Text.RegularExpressions;
using CogniThink.TutorBrain.Application.Chemistry;

// ============================================================
// solve 投影富化器（仅 HTTP /solve 路径调用，不进判分）。
// 读 GatewayResult 的任务类型与已解析 EngineRequest，加性调用新引擎产出
// 「结构化辅助」段；全程 best-effort，任何异常返回 null，绝不影响 final_result/reply。
// 铁律：不蒙数——只取题给数据的确定性算术或引擎 status=Determined 的结果。
// ============================================================
public sealed class SolveStructuredEnricher
{
    private readonly EquilibriumStateSimulator _equilibrium;
    private readonly ChemicalSimulator _chemical;

    public SolveStructuredEnricher(EquilibriumStateSimulator equilibrium, ChemicalSimulator chemical)
    {
        _equilibrium = equilibrium;
        _chemical = chemical;
    }

    public StudentSolveStructured? Enrich(GatewayResult result, string rawText)
    {
        if (result is null) return null;
        try
        {
            var warnings = new List<string>();
            var structured = new StudentSolveStructured();

            // 1) 定量：题给 n/c/V 任两者 → 确定性算术第三者
            var numeric = BuildNumeric(result.EngineRequest, rawText, warnings);
            if (numeric is not null) structured.Numeric = numeric;

            // 2) 水相 / 沉淀平衡：抽到物种+浓度才调引擎；仅 Determined 才下发
            var eq = TryComputeEquilibrium(rawText, warnings);
            if (eq is not null)
            {
                var aq = MapAqueous(eq);
                if (aq is not null) structured.Aqueous = aq;
                var eqSnap = MapEquilibrium(eq);
                if (eqSnap is not null) structured.Equilibrium = eqSnap;
            }

            // 3) 反应轨迹：仅反应中心类任务 + 解析出反应式两侧才调 ChemicalSimulator；
            //    平衡/计算/概念题即便解析出反应物也不产出轨迹（避免偏离题意，如气相 Kp 题）。
            if (IsReactionCentricTask(result.TaskType))
            {
                var trace = BuildReactionTrace(result.EngineRequest, warnings);
                if (trace is not null) structured.ReactionTrace = trace;
            }

            structured.Warnings = warnings;
            return structured.IsEmpty() ? null : structured;
        }
        catch
        {
            // best-effort：绝不抛入响应管线、绝不影响判决字段
            return null;
        }
    }

    // ==================== 定量算术 ====================

    private static NumericSnapshot? BuildNumeric(EngineRequest req, string rawText, List<string> warnings)
    {
        var known = req.Known;
        var species = GuessSoluteSpecies(rawText);

        if (known.N.HasValue && known.V.HasValue && known.V.Value > 0)
        {
            var c = known.N.Value / known.V.Value;
            return new NumericSnapshot
            {
                Value = Math.Round(c, 6),
                Unit = "mol/L",
                Species = species,
                Formula = "c = n / V"
            };
        }
        if (known.C.HasValue && known.V.HasValue)
        {
            var n = known.C.Value * known.V.Value;
            return new NumericSnapshot
            {
                Value = Math.Round(n, 6),
                Unit = "mol",
                Species = species,
                Formula = "n = c · V"
            };
        }
        if (known.N.HasValue && known.C.HasValue && known.C.Value > 0)
        {
            var v = known.N.Value / known.C.Value;
            return new NumericSnapshot
            {
                Value = Math.Round(v, 6),
                Unit = "L",
                Species = species,
                Formula = "V = n / c"
            };
        }
        return null;
    }

    // ==================== 平衡数值 ====================

    private EquilibriumNumericalResult? TryComputeEquilibrium(string rawText, List<string> warnings)
    {
        var (species, concentration) = ExtractSpeciesConcentration(rawText);
        if (string.IsNullOrWhiteSpace(species) || concentration is null or <= 0)
            return null;

        var input = new ChemicalSystemInput
        {
            // Core 委托（守恒/排序/酸碱性）要求物种同时带 Concentration 与 Volume；
            // 仅给浓度时按 1 L 单物种体系处理（浓度类题的常规约定）。
            Species = new() { new SpeciesInput { Substance = species, Concentration = concentration, Volume = 1.0 } }
        };
        var query = new EquilibriumQuery
        {
            Quantities =
            {
                EquilibriumQuantity.Ph,
                EquilibriumQuantity.IonConcentrations,
                EquilibriumQuantity.ConcentrationOrdering,
                EquilibriumQuantity.Conservations,
                EquilibriumQuantity.AcidityQualitative
            },
            OrderSpecies = ExtractOrderTargets(rawText)
        };

        var result = _equilibrium.Compute(input, query);
        if (result.Status != EquilibriumDetermination.Determined)
        {
            warnings.Add("平衡数据不足，未补算（不蒙数）。");
            return null;
        }
        return result;
    }

    private static AqueousSnapshot? MapAqueous(EquilibriumNumericalResult eq)
    {
        var hasAny = eq.Ph.HasValue
                     || eq.ConservationEquations.Count > 0
                     || eq.ConcentrationOrdering.Count > 0
                     || !string.IsNullOrWhiteSpace(eq.AcidityVerdict)
                     || eq.IonConcentrations.Count > 0;
        if (!hasAny) return null;

        var ordering = eq.ConcentrationOrdering.ToList();
        // 引擎未直接给排序时，用已算离子浓度确定性派生（仅排序，不造数）
        if (ordering.Count == 0 && eq.IonConcentrations.Count >= 2)
        {
            ordering = eq.IonConcentrations
                .OrderByDescending(kv => kv.Value)
                .Select(kv => $"c({kv.Key})")
                .ToList();
        }

        return new AqueousSnapshot
        {
            Ordering = ordering,
            ConservationEquations = eq.ConservationEquations.ToList(),
            Ph = eq.Ph,
            AcidityVerdict = eq.AcidityVerdict,
            IonConcentrations = new Dictionary<string, double>(eq.IonConcentrations, StringComparer.Ordinal)
        };
    }

    private static EquilibriumSnapshot? MapEquilibrium(EquilibriumNumericalResult eq)
    {
        var hasAny = eq.KspResidualConcentration.HasValue
                     || !string.IsNullOrWhiteSpace(eq.KspResidualIon)
                     || !string.IsNullOrWhiteSpace(eq.ConversionConstantK)
                     || eq.ConversionProceeds.HasValue;
        if (!hasAny) return null;
        return new EquilibriumSnapshot
        {
            KspResidualConcentration = eq.KspResidualConcentration,
            KspResidualIon = eq.KspResidualIon,
            ConversionConstantK = eq.ConversionConstantK,
            ConversionProceeds = eq.ConversionProceeds
        };
    }

    // ==================== 反应轨迹 ====================

    /// <summary>仅反应中心类任务才产出反应轨迹，避免给平衡/计算/概念题附加偏离题意的轨迹。</summary>
    private static bool IsReactionCentricTask(string? taskType) =>
        !string.IsNullOrWhiteSpace(taskType) && taskType switch
        {
            "reaction_check" => true,
            "balance" => true,
            "equation_writer" => true,
            "judgment" => true,
            _ => false
        };

    private ReactionTraceSnapshot? BuildReactionTrace(EngineRequest req, List<string> warnings)
    {
        var rxn = req.Reaction;
        var sides = (rxn.Left ?? new()).Concat(rxn.Right ?? new()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        if (sides.Count == 0) return null;

        var input = new ChemicalSystemInput
        {
            Species = sides.Select(s => new SpeciesInput { Substance = s, Moles = 1 }).ToList()
        };

        SimulationTrace trace;
        try { trace = _chemical.Simulate(input); }
        catch { return null; }

        if (trace is null || (trace.Events.Count == 0 && trace.FinalState.Species.Count == 0))
            return null;

        var snap = new ReactionTraceSnapshot { Converged = trace.Converged };
        foreach (var ev in trace.Events)
        {
            snap.Events.Add(new TraceEventDto
            {
                Equation = ev.Equation,
                StageIndex = ev.StageIndex,
                Extent = ev.Extent,
                BasisKnowledgeIds = ev.Basis?.KnowledgeIds?.ToList() ?? new(),
                PriorityRank = ev.Priority?.Rank
            });
        }
        foreach (var sp in trace.FinalState.Species)
        {
            snap.FinalSpecies.Add(new SpeciesAmountDto
            {
                Substance = !string.IsNullOrWhiteSpace(sp.Formula) ? sp.Formula : sp.Name,
                Moles = sp.FinalMoles
            });
        }
        return snap.IsEmpty() ? null : snap;
    }

    // ==================== 确定性文本抽取（仅辅助展示，不回写 EngineRequest） ====================

    // 匹配 "0.1 mol/L CH3COONa" / "0.1mol·L-1 的 CH3COONa" / "0.1 M CH3COONa"
    private static readonly Regex ConcSpeciesRegex = new(
        @"(\d+(?:\.\d+)?)\s*(?:mol/L|mol·L⁻¹|mol·L-1|mol/L|mol\s*L\s*-?\s*1|/L|M)\s*的?\s*([A-Za-z][A-Za-z0-9()+\-]*)",
        RegexOptions.Compiled);

    // 匹配 "0.5 mol NaOH" / "0.5 摩尔 NaOH"
    private static readonly Regex MoleSpeciesRegex = new(
        @"(\d+(?:\.\d+)?)\s*(?:mol|摩尔)\s*的?\s*([A-Za-z][A-Za-z0-9()+\-]*)",
        RegexOptions.Compiled);

    // 匹配 "将 c(Na^+)、c(CH3COO^-)、c(OH^-)、c(H^+) 排序" 中的离子
    private static readonly Regex IonTargetRegex = new(@"c\(\s*([^()\s]+)\s*\)", RegexOptions.Compiled);

    private static (string? species, double? concentration) ExtractSpeciesConcentration(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return (null, null);
        var m = ConcSpeciesRegex.Match(text);
        if (m.Success && double.TryParse(m.Groups[1].Value, out var c))
            return (m.Groups[2].Value, c);
        return (null, null);
    }

    private static string? GuessSoluteSpecies(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = ConcSpeciesRegex.Match(text);
        if (m.Success) return m.Groups[2].Value;
        m = MoleSpeciesRegex.Match(text);
        if (m.Success) return m.Groups[2].Value;
        return null;
    }

    private static List<string> ExtractOrderTargets(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new();
        var list = new List<string>();
        foreach (Match m in IonTargetRegex.Matches(text))
        {
            var ion = m.Groups[1].Value;
            if (!list.Contains(ion)) list.Add(ion);
        }
        return list;
    }
}
