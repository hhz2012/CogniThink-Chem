using CogniThink.Chem.Core.Common;
using CogniThink.Chem.Core.Math;
using CogniThink.Chem.Core.Stoichiometry;

namespace CogniThink.Chem.Core.Benchmark;

/// <summary>单个干扰项的引擎校验结果。</summary>
public sealed class DistractorOutcome
{
    public string DistractorId { get; init; } = "";

    /// <summary>是否按预期被捕获（或按预期放行——边界样例）。</summary>
    public bool CaughtAsExpected { get; set; }

    public CheckReport Report { get; init; } = new();
}

/// <summary>
/// 单题测试输出（统一规范）。Passed 为计算属性：Mismatches 为空即通过。
/// 供 xUnit 断言与后续接入 LLM 层时替换答案来源复用。
/// </summary>
public sealed class BenchmarkResult
{
    public string CaseId { get; init; } = "";

    public ProblemCategory Category { get; init; }

    /// <summary>整体结论：无任何不符项即通过。</summary>
    public bool Passed => Mismatches.Count == 0;

    /// <summary>不符原因（引擎实际行为与题目预期不一致的逐条说明）。</summary>
    public List<string> Mismatches { get; } = new();

    /// <summary>每条标准方程式的引擎报告（按 expected.equations 顺序）。</summary>
    public List<CheckReport> AnswerReports { get; } = new();

    /// <summary>每个干扰项的校验结果（按 distractors 顺序）。</summary>
    public List<DistractorOutcome> DistractorOutcomes { get; } = new();

    /// <summary>计算题复算明细（"op: 计算值 vs 预期值"），非计算题为空。</summary>
    public string CalcDetail { get; set; } = "";
}

/// <summary>
/// 基准验证器：对单题执行"标准答案引擎校验 + 干扰项捕获校验 + 计算题精确复算"。
/// 职责边界：推断（猜产物/成分/过程）属于 LLM 层；离线测试中 expected 字段即标准答案，
/// 本验证器只确认引擎对标准答案与典型错解的行为符合预期。
/// </summary>
public static class BenchmarkVerifier
{
    /// <summary>验证单题。</summary>
    /// <param name="problem">题目（含标准答案与干扰项）。</param>
    /// <returns>统一测试输出 <see cref="BenchmarkResult"/>。</returns>
    public static BenchmarkResult Verify(ProblemSpec problem)
    {
        var result = new BenchmarkResult { CaseId = problem.Id, Category = problem.Category };

        VerifyExpectedEquations(problem, result);
        VerifyDistractors(problem, result);
        VerifyCalc(problem, result);

        return result;
    }

    /// <summary>批量验证。</summary>
    public static IReadOnlyList<BenchmarkResult> VerifyAll(IEnumerable<ProblemSpec> problems) =>
        problems.Select(Verify).ToList();

    /// <summary>标准方程式逐条过统一入口，与 engineExpectation 比对。</summary>
    private static void VerifyExpectedEquations(ProblemSpec problem, BenchmarkResult result)
    {
        var expectation = problem.Expected.EngineExpectation;
        foreach (var equation in problem.Expected.Equations)
        {
            var report = ChemicalCheckerService.Check(new CheckRequest
            {
                Equation = equation,
                Medium = problem.Given.Medium
            });
            result.AnswerReports.Add(report);

            if (report.IsValid != expectation.IsValid)
            {
                result.Mismatches.Add(
                    $"标准方程式 \"{equation}\"：IsValid={report.IsValid}，预期 {expectation.IsValid}。{report.FailureSummary()}");
            }

            if (report.RiskTag != expectation.RiskTag)
            {
                result.Mismatches.Add(
                    $"标准方程式 \"{equation}\"：风险标签 \"{report.RiskTag}\"，预期 \"{expectation.RiskTag}\"");
            }

            foreach (var name in expectation.MustPass)
            {
                if (!report.CheckItems.Any(i => i.CheckName == name && i.Passed))
                {
                    result.Mismatches.Add($"标准方程式 \"{equation}\"：缺少通过的检查项 \"{name}\"");
                }
            }

            foreach (var name in expectation.MustFail)
            {
                if (!report.CheckItems.Any(i => i.CheckName == name && !i.Passed))
                {
                    result.Mismatches.Add($"标准方程式 \"{equation}\"：缺少失败的检查项 \"{name}\"");
                }
            }
        }
    }

    /// <summary>干扰项逐个过统一入口：预期捕获的必须按指定检查项失败；边界样例预期放行。</summary>
    private static void VerifyDistractors(ProblemSpec problem, BenchmarkResult result)
    {
        foreach (var distractor in problem.Distractors)
        {
            var report = ChemicalCheckerService.Check(new CheckRequest
            {
                Equation = distractor.Equation,
                Medium = problem.Given.Medium
            });
            var outcome = new DistractorOutcome { DistractorId = distractor.Id, Report = report };
            var ok = report.IsValid == distractor.ExpectValid;

            if (!distractor.ExpectValid)
            {
                foreach (var name in distractor.ExpectFailChecks)
                {
                    if (!report.CheckItems.Any(i => i.CheckName == name && !i.Passed))
                    {
                        ok = false;
                        result.Mismatches.Add($"干扰项 {distractor.Id}：未按预期在检查项 \"{name}\" 上失败。{report}");
                    }
                }

                if (distractor.ExpectRiskTag.Length > 0 && report.RiskTag != distractor.ExpectRiskTag)
                {
                    ok = false;
                    result.Mismatches.Add(
                        $"干扰项 {distractor.Id}：风险标签 \"{report.RiskTag}\"，预期 \"{distractor.ExpectRiskTag}\"");
                }
            }

            if (!ok && distractor.ExpectValid)
            {
                result.Mismatches.Add($"干扰项 {distractor.Id}（边界样例，预期放行）：引擎判错。{report.FailureSummary()}");
            }

            outcome.CaughtAsExpected = ok;
            result.DistractorOutcomes.Add(outcome);
        }
    }

    /// <summary>计算题：按 calc 规格调用 MoleCalculator 精确复算，与 numericAnswer 有理数比对。</summary>
    private static void VerifyCalc(ProblemSpec problem, BenchmarkResult result)
    {
        if (problem.Expected.Calc is not { } calc || problem.Expected.NumericAnswer is not { } answer)
        {
            return;
        }

        var computed = ComputeCalc(calc);
        var expected = Rational.Parse(answer.Value);
        result.CalcDetail = $"{calc.Op}: 计算值 {computed}，预期 {expected}（{answer.Unit}）";
        if (computed != expected)
        {
            result.Mismatches.Add($"计算复算不一致：{result.CalcDetail}");
        }
    }

    /// <summary>
    /// 按 <see cref="CalcSpec.Op"/> 分派到 <see cref="MoleCalculator"/> 的对应方法。
    /// 全程 <see cref="Rational"/> 精确运算；缺失字段抛 <see cref="InvalidOperationException"/>（数据集错误，应被测试暴露）。
    /// </summary>
    public static Rational ComputeCalc(CalcSpec spec)
    {
        switch (spec.Op)
        {
            case CalcOp.MolarConcentration:
                return MoleCalculator.MolarConcentration(
                    Need(spec.Moles, "moles"), Need(spec.SolutionVolumeLiters, "solutionVolumeLiters"));

            case CalcOp.MolesFromMass:
                return MoleCalculator.Moles(Need(spec.MassGrams, "massGrams"), NeedSpecies(spec));

            case CalcOp.MassFromMoles:
                return Need(spec.Moles, "moles") * MoleCalculator.MolarMass(NeedSpecies(spec));

            case CalcOp.GasVolumeFromMass:
                return MoleCalculator.Moles(Need(spec.MassGrams, "massGrams"), NeedSpecies(spec))
                       * MoleCalculator.StandardMolarVolume;

            case CalcOp.MolesFromGasVolume:
                return MoleCalculator.MolesFromGasVolume(Need(spec.GasVolumeLiters, "gasVolumeLiters"));

            case CalcOp.Dilution:
                return MoleCalculator.Dilute(
                    Need(spec.Concentration1, "concentration1"),
                    Need(spec.Volume1Liters, "volume1Liters"),
                    Need(spec.Volume2Liters, "volume2Liters"));

            case CalcOp.MolesFromConcentration:
                return MoleCalculator.MolesFromConcentration(
                    Need(spec.Concentration, "concentration"), Need(spec.SolutionVolumeLiters, "solutionVolumeLiters"));

            case CalcOp.YieldRatio:
                return MoleCalculator.Yield(
                    Need(spec.ActualMassGrams, "actualMassGrams"), Need(spec.TheoreticalMassGrams, "theoreticalMassGrams"));

            case CalcOp.MolesFromParticles:
                return MoleCalculator.MolesFromParticles(Need(spec.ParticleMantissa, "particleMantissa"), spec.ParticleExponent);

            case CalcOp.IonMolesFromSolute:
                return MoleCalculator.MolesFromConcentration(
                           Need(spec.Concentration, "concentration"), Need(spec.SolutionVolumeLiters, "solutionVolumeLiters"))
                       * Need(spec.IonSubscript, "ionSubscript");

            default:
                throw new ArgumentOutOfRangeException(nameof(spec), spec.Op, "未知计算类型");
        }
    }

    private static Rational Need(string? value, string field) =>
        value is null
            ? throw new InvalidOperationException($"CalcSpec 缺少字段 {field}")
            : Rational.Parse(value);

    private static string NeedSpecies(CalcSpec spec) =>
        string.IsNullOrWhiteSpace(spec.Species)
            ? throw new InvalidOperationException("CalcSpec 缺少字段 species")
            : spec.Species!;
}
