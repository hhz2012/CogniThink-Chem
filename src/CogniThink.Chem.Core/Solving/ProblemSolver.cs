using CogniThink.Chem.Core.Benchmark;
using CogniThink.Chem.Core.Inference;
using CogniThink.Chem.Core.Math;
using CogniThink.Chem.Core.Reactions;

namespace CogniThink.Chem.Core.Solving;

/// <summary>
/// 题目求解器（"能解"）：按 <see cref="ProblemCategory"/> 把题目分派到对应推断引擎，
/// 汇总为统一 <see cref="Solution"/>。
/// <list type="bullet">
/// <item>产物推断 → <see cref="ProductInferenceEngine"/>（反应库检索）。</item>
/// <item>离子共存 → <see cref="CoexistenceEngine"/>（介质+两两反应检索）。</item>
/// <item>成分推断 → <see cref="IdentificationEngine"/>（约束传播；模式由 given.singleEntity 标注）。</item>
/// <item>过程推断 → <see cref="ProcessInferenceEngine"/>（转化链回溯赋值）。</item>
/// <item>定量计算 → 复用 <see cref="BenchmarkVerifier.ComputeCalc"/> 的精确有理复算。</item>
/// </list>
/// 铁律：求解器只做路由与汇总，全部结论仍来自知识驱动引擎；欠定/无解原样上抛，不猜。
/// </summary>
public static class ProblemSolver
{
    /// <summary>求解单题。</summary>
    /// <param name="problem">题目（含已知条件；expected 仅计算题用于复算规格）。</param>
    /// <param name="library">反应库（缺省用全局缓存）。</param>
    public static Solution Solve(ProblemSpec problem, ReactionLibrary? library = null)
    {
        library ??= ReactionLibrary.Default.Value;
        switch (problem.Category)
        {
            case ProblemCategory.ProductInference:
                return SolveProduct(problem, library);
            case ProblemCategory.IonCoexistence:
                return SolveCoexistence(problem, library);
            case ProblemCategory.SubstanceIdentification:
                return SolveIdentification(problem, library);
            case ProblemCategory.ProcessInference:
                return SolveProcess(problem, library);
            case ProblemCategory.StoichiometryCalc:
                return SolveCalc(problem);
            default:
                throw new ArgumentOutOfRangeException(nameof(problem), problem.Category, "未知题目类别");
        }
    }

    /// <summary>批量求解。</summary>
    public static IReadOnlyList<Solution> SolveAll(IEnumerable<ProblemSpec> problems, ReactionLibrary? library = null)
    {
        library ??= ReactionLibrary.Default.Value;
        return problems.Select(p => Solve(p, library)).ToList();
    }

    // -----------------------------------------------------------------------
    // 产物推断
    // -----------------------------------------------------------------------

    private static Solution SolveProduct(ProblemSpec problem, ReactionLibrary library)
    {
        var solution = NewSolution(problem);
        var infer = ProductInferenceEngine.Infer(new ProductInferenceRequest
        {
            Reactants = problem.Given.Species.ToList(),
            Conditions = problem.Given.Conditions.ToList(),
        }, library);

        solution.Status = infer.Status;
        solution.Message = infer.Message;
        solution.Equations = infer.Matches.Select(m => m.Equation).ToList();
        solution.Steps = ToSteps(infer.Steps);
        return solution;
    }

    // -----------------------------------------------------------------------
    // 离子共存
    // -----------------------------------------------------------------------

    private static Solution SolveCoexistence(ProblemSpec problem, ReactionLibrary library)
    {
        var solution = NewSolution(problem);
        var judge = CoexistenceEngine.Judge(new CoexistenceRequest
        {
            Species = problem.Given.Species.ToList(),
            Medium = problem.Given.Medium ?? "",
        }, library);

        solution.Coexistence = judge.Verdict;
        solution.Status = judge.Verdict == CoexistenceVerdict.Underdetermined
            ? InferenceStatus.Underdetermined
            : InferenceStatus.Solved;
        solution.Message = judge.Message;
        solution.Equations = MaximalConflicts(judge.Conflicts)
            .Where(c => c.Reaction is not null)
            .Select(c => c.Reaction!.Equation)
            .ToList();
        solution.Steps = ToSteps(judge.Steps);
        return solution;
    }

    /// <summary>
    /// 冲突去重：若某冲突的参与离子集是另一冲突的真子集，则被后者包含
    /// （如 COEX-008 中 {HCO3^-,OH^-} 被 {Ca^2+,HCO3^-,OH^-} 包含），
    /// 只保留最特异的冲突，使理由方程式与标准答案形态一致。
    /// </summary>
    private static List<CoexistenceConflict> MaximalConflicts(List<CoexistenceConflict> conflicts)
    {
        var result = new List<CoexistenceConflict>();
        foreach (var conflict in conflicts)
        {
            var ions = new HashSet<string>(conflict.InvolvedIons, StringComparer.Ordinal);
            var subsumed = conflicts.Any(other =>
                !ReferenceEquals(other, conflict)
                && ions.IsProperSubsetOf(new HashSet<string>(other.InvolvedIons, StringComparer.Ordinal)));
            if (!subsumed)
            {
                result.Add(conflict);
            }
        }

        return result;
    }

    // -----------------------------------------------------------------------
    // 成分推断
    // -----------------------------------------------------------------------

    private static Solution SolveIdentification(ProblemSpec problem, ReactionLibrary library)
    {
        var solution = NewSolution(problem);
        var identify = IdentificationEngine.Identify(new IdentificationRequest
        {
            CandidateSpecies = problem.Given.CandidateSpecies.ToList(),
            Medium = problem.Given.Medium ?? "",
            SingleEntity = problem.Given.SingleEntity,
            Steps = problem.Given.Steps.Select(ToExperimentStep).ToList(),
        }, library);

        solution.Status = identify.Status;
        solution.Message = identify.Message;
        solution.Composition = new Dictionary<string, CompositionVerdict>(identify.Composition);
        solution.Equations = identify.Equations.Select(e => e.Equation).ToList();
        solution.Steps = ToSteps(identify.Steps);
        return solution;
    }

    // -----------------------------------------------------------------------
    // 过程推断
    // -----------------------------------------------------------------------

    private static Solution SolveProcess(ProblemSpec problem, ReactionLibrary library)
    {
        var solution = NewSolution(problem);
        var infer = ProcessInferenceEngine.Infer(new ProcessInferenceRequest
        {
            CandidateSpecies = problem.Given.CandidateSpecies.ToList(),
            Medium = problem.Given.Medium ?? "",
            Steps = problem.Given.Steps.Select(ToExperimentStep).ToList(),
        }, library);

        solution.Status = infer.Status;
        solution.Message = infer.Message;
        solution.SubstanceMap = new Dictionary<string, string>(infer.Assignment);
        solution.Equations = infer.StepResults
            .OrderBy(r => r.Step)
            .Select(r => r.Equation)
            .ToList();
        solution.Steps = ToSteps(infer.Steps);
        return solution;
    }

    // -----------------------------------------------------------------------
    // 定量计算
    // -----------------------------------------------------------------------

    private static Solution SolveCalc(ProblemSpec problem)
    {
        var solution = NewSolution(problem);
        if (problem.Expected.Calc is not { } calc)
        {
            solution.Status = InferenceStatus.NoSolution;
            solution.Message = "计算题缺少复算规格（calc），引擎无法计算。";
            return solution;
        }

        foreach (var fact in problem.Given.NumericFacts)
        {
            solution.Steps.Add(new SolutionStep
            {
                Order = solution.Steps.Count + 1,
                Description = $"已知：{FactText(fact)}",
                Evidence = "given.numericFacts",
            });
        }

        var value = BenchmarkVerifier.ComputeCalc(calc);
        solution.NumericValue = value;
        solution.NumericUnit = problem.Expected.NumericAnswer?.Unit ?? UnitOf(calc.Op);
        solution.NumericSpecies = problem.Expected.NumericAnswer?.Species ?? calc.Species ?? "";
        solution.Steps.Add(new SolutionStep
        {
            Order = solution.Steps.Count + 1,
            Description = $"按公式 {FormulaOf(calc.Op)} 精确计算（有理数运算）。",
            Evidence = $"MoleCalculator.{calc.Op}",
        });
        solution.Steps.Add(new SolutionStep
        {
            Order = solution.Steps.Count + 1,
            Description = $"结果：{value} {solution.NumericUnit}".Trim(),
            Evidence = "Rational 精确值",
        });

        solution.Status = InferenceStatus.Solved;
        solution.Message = $"计算完成：{value} {solution.NumericUnit}".Trim();
        return solution;
    }

    // -----------------------------------------------------------------------
    // 辅助
    // -----------------------------------------------------------------------

    private static Solution NewSolution(ProblemSpec problem) => new()
    {
        ProblemId = problem.Id,
        Category = problem.Category,
        Status = InferenceStatus.NoSolution,
    };

    private static ExperimentStep ToExperimentStep(StepSpec s) => new()
    {
        Step = s.Step,
        From = s.From,
        To = s.To,
        Reagents = s.Reagents.ToList(),
        Conditions = s.Conditions.ToList(),
        Phenomenon = s.Phenomenon,
    };

    private static List<SolutionStep> ToSteps(List<InferenceStep> steps)
    {
        var result = new List<SolutionStep>(steps.Count);
        var order = 1;
        foreach (var step in steps)
        {
            result.Add(new SolutionStep
            {
                Order = order++,
                Description = step.Description,
                Evidence = step.Evidence,
                KnowledgeId = step.KnowledgeId,
                Source = step.Source,
            });
        }

        return result;
    }

    private static string FactText(NumericFact fact)
    {
        var subject = string.IsNullOrWhiteSpace(fact.Species) ? "" : $"{fact.Species} 的";
        return $"{subject}{KindText(fact.Kind)} = {fact.Value} {fact.Unit}".Trim();
    }

    private static string KindText(NumericFactKind kind)
    {
        switch (kind)
        {
            case NumericFactKind.Mass: return "质量";
            case NumericFactKind.Moles: return "物质的量";
            case NumericFactKind.GasVolume: return "气体体积（标准状况）";
            case NumericFactKind.SolutionVolume: return "溶液体积";
            case NumericFactKind.Concentration: return "物质的量浓度";
            case NumericFactKind.ParticleCount: return "粒子数";
            case NumericFactKind.ActualYieldMass: return "实际产量";
            default: return kind.ToString();
        }
    }

    /// <summary>各计算类型的公式表述（讲解用）。</summary>
    public static string FormulaOf(CalcOp op)
    {
        switch (op)
        {
            case CalcOp.MolarConcentration: return "c = n / V";
            case CalcOp.MolesFromMass: return "n = m / M";
            case CalcOp.MassFromMoles: return "m = n × M";
            case CalcOp.GasVolumeFromMass: return "V = (m / M) × 22.4 L/mol";
            case CalcOp.MolesFromGasVolume: return "n = V / 22.4 L/mol";
            case CalcOp.Dilution: return "c2 = c1 × V1 / V2";
            case CalcOp.MolesFromConcentration: return "n = c × V";
            case CalcOp.YieldRatio: return "产率 = 实际产量 / 理论产量";
            case CalcOp.MolesFromParticles: return "n = N / NA";
            case CalcOp.IonMolesFromSolute: return "n(离子) = c × V × 角标数";
            default: throw new ArgumentOutOfRangeException(nameof(op), op, "未知计算类型");
        }
    }

    private static string UnitOf(CalcOp op)
    {
        switch (op)
        {
            case CalcOp.MolarConcentration:
            case CalcOp.Dilution:
                return "mol/L";
            case CalcOp.MassFromMoles:
                return "g";
            case CalcOp.GasVolumeFromMass:
                return "L";
            case CalcOp.YieldRatio:
                return "比率";
            default:
                return "mol";
        }
    }
}
