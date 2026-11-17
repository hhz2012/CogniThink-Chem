using CogniThink.Chem.Core.Benchmark;
using CogniThink.Chem.Core.Inference;
using CogniThink.Chem.Core.Math;
using CogniThink.Chem.Core.Solving;
using Xunit;

namespace CogniThink.Chem.Tests;

/// <summary>
/// 端到端求解基准（"能解"）：求解器对全部 103 道基准题推导答案，
/// 断言推导结果与 expected 标准答案一致（此前引擎只会校验、现在能推导）。
/// </summary>
public class ProblemSolverTests
{
    private static readonly IReadOnlyList<ProblemSpec> Problems = BenchmarkLoader.LoadAll();

    public static IEnumerable<object[]> ProblemIds() => Problems.Select(p => new object[] { p.Id });

    private static ProblemSpec ById(string id) => Problems.Single(p => p.Id == id);

    [Theory]
    [MemberData(nameof(ProblemIds))]
    public void Benchmark_SolverDerivesExpectedAnswer(string caseId)
    {
        var problem = ById(caseId);
        var solution = ProblemSolver.Solve(problem);
        var expected = problem.Expected;

        // 边界用例（tags 含"边界"，如熔融体系电化学、无平衡信息的平衡题）：引擎必须如实上抛欠定，不得臆造结论。
        if (problem.Tags.Contains("边界"))
        {
            Assert.Equal(InferenceStatus.Underdetermined, solution.Status);
            Assert.Null(solution.Electrodes);
            Assert.Null(solution.Equilibrium);
            Assert.Null(solution.Aqueous);
            Assert.False(string.IsNullOrWhiteSpace(solution.Message));
            return;
        }

        // PROD-004 为陌生物质（Xy）边界用例：知识库无记录，引擎必须拒答而非臆造。
        if (problem.Category == ProblemCategory.ProductInference && !expected.EngineExpectation.IsValid)
        {
            Assert.Equal(InferenceStatus.NoSolution, solution.Status);
            Assert.Empty(solution.Equations);
            Assert.False(string.IsNullOrWhiteSpace(solution.Message));
            return;
        }

        Assert.True(solution.IsSolved, $"{caseId}: 未解出。{solution.Message}");

        switch (problem.Category)
        {
            case ProblemCategory.ProductInference:
                Assert.Equal(expected.Equations, solution.Equations);
                break;

            case ProblemCategory.IonCoexistence:
                Assert.NotNull(expected.Verdict);
                Assert.Equal(
                    expected.Verdict == CoexistVerdict.Coexist,
                    solution.Coexistence == CoexistenceVerdict.Coexist);
                AssertSetEqual(expected.Equations, solution.Equations);
                break;

            case ProblemCategory.SubstanceIdentification:
            case ProblemCategory.IonPowderIdentification:
                Assert.NotNull(expected.Composition);
                Assert.Equal(expected.Composition!.Count, solution.Composition.Count);
                foreach (var kv in expected.Composition!)
                {
                    Assert.True(solution.Composition.TryGetValue(kv.Key, out var actual),
                        $"{caseId}: 候选 {kv.Key} 缺少判定");
                    Assert.Equal(kv.Value.ToString(), actual.ToString());
                }

                AssertSetEqual(expected.Equations, solution.Equations);
                break;

            case ProblemCategory.ProcessInference:
            case ProblemCategory.OrganicInference:
                Assert.NotNull(expected.SubstanceMap);
                Assert.Equal(expected.SubstanceMap!, solution.SubstanceMap);
                Assert.Equal(expected.Equations, solution.Equations);
                break;

            case ProblemCategory.StoichiometryCalc:
                Assert.NotNull(expected.NumericAnswer);
                Assert.NotNull(solution.NumericValue);
                Assert.Equal(Rational.Parse(expected.NumericAnswer!.Value), solution.NumericValue);
                break;

            case ProblemCategory.Electrochemistry:
                Assert.NotNull(expected.Electrodes);
                Assert.NotNull(solution.Electrodes);
                Assert.Equal(expected.Electrodes!.Negative, solution.Electrodes!.Negative);
                Assert.Equal(expected.Electrodes.Positive, solution.Electrodes.Positive);
                Assert.Equal(expected.Electrodes.Anode, solution.Electrodes.Anode);
                Assert.Equal(expected.Electrodes.Cathode, solution.Electrodes.Cathode);
                AssertSetEqual(expected.Equations, solution.Equations);
                break;

            case ProblemCategory.ChemicalEquilibrium:
                Assert.NotNull(expected.Equilibrium);
                Assert.NotNull(solution.Equilibrium);
                var want = expected.Equilibrium!;
                var got = solution.Equilibrium!;
                Assert.Equal(want.Table.Count, got.Table.Count);
                for (var i = 0; i < want.Table.Count; i++)
                {
                    Assert.Equal(want.Table[i].Species, got.Table[i].Species);
                    Assert.Equal(want.Table[i].Start, got.Table[i].Start);
                    Assert.Equal(want.Table[i].Change, got.Table[i].Change);
                    Assert.Equal(want.Table[i].End, got.Table[i].End);
                }

                Assert.Equal(want.ConversionSpecies, got.ConversionSpecies);
                Assert.Equal(want.Conversion, got.Conversion);
                Assert.Equal(want.FractionSpecies, got.FractionSpecies);
                Assert.Equal(want.Fraction, got.Fraction);
                Assert.Equal(want.KValue, got.KValue);
                Assert.Equal(want.Direction, got.Direction);
                AssertSetEqual(expected.Equations, solution.Equations);
                break;

            case ProblemCategory.IndustrialProcess:
                Assert.NotNull(expected.SubstanceMap);
                Assert.Equal(expected.SubstanceMap!, solution.SubstanceMap);
                Assert.Equal(expected.Equations, solution.Equations);
                foreach (var (stepNumber, keyword) in expected.Purposes ?? new Dictionary<string, string>())
                {
                    Assert.True(solution.Purposes.TryGetValue(stepNumber, out var actual),
                        $"{caseId}: 步骤 {stepNumber} 缺少目的输出");
                    Assert.Contains(keyword, actual);
                }

                break;

            case ProblemCategory.AqueousIonEquilibrium:
                Assert.NotNull(expected.Aqueous);
                Assert.NotNull(solution.Aqueous);
                Assert.Equal(expected.Aqueous!.Order, solution.Aqueous!.Order);
                AssertSetEqual(expected.Aqueous.ConservationEquations, solution.Aqueous.ConservationEquations);
                AssertSetEqual(expected.Equations, solution.Equations);
                break;

            case ProblemCategory.ExperimentalInquiry:
                Assert.NotNull(expected.Experiment);
                Assert.NotNull(solution.Experiment);
                Assert.Equal(expected.Experiment!.Conclusions, solution.Experiment!.Conclusions);
                break;
        }

        // 每个解都必须带可溯源的求解步骤（讲解/引导的原料）。
        Assert.NotEmpty(solution.Steps);
        Assert.All(solution.Steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Description)));
    }

    [Fact]
    public void SolveAll_CoversAllProblems()
    {
        var solutions = ProblemSolver.SolveAll(Problems);
        Assert.Equal(130, solutions.Count);
        // 122 题解出；PROD-004（陌生物质）按铁律拒答，ELEC-H06（熔融体系）、EQ-H08（无平衡信息）、
        // AQ-H04（NaHCO3 全粒子排序需解联立平衡）、PROCF-H06（浸出液铁价态未记录）、
        // IONP-H04（全负证据无法确定任何成分）、ORG-H08（乙烯/乙炔双解）、
        // EXP-H08（石灰水变浑浊为 CO2/SO2 共有现象，多义）如实欠定。
        Assert.Equal(122, solutions.Count(s => s.IsSolved));
        var unsolved = solutions.Where(s => !s.IsSolved).ToList();
        Assert.Equal(8, unsolved.Count);
        Assert.Equal(InferenceStatus.NoSolution, unsolved.Single(s => s.ProblemId == "PROD-004").Status);
        Assert.Equal(InferenceStatus.Underdetermined, unsolved.Single(s => s.ProblemId == "ELEC-H06").Status);
        Assert.Equal(InferenceStatus.Underdetermined, unsolved.Single(s => s.ProblemId == "EQ-H08").Status);
        Assert.Equal(InferenceStatus.Underdetermined, unsolved.Single(s => s.ProblemId == "AQ-H04").Status);
        Assert.Equal(InferenceStatus.Underdetermined, unsolved.Single(s => s.ProblemId == "PROCF-H06").Status);
        Assert.Equal(InferenceStatus.Underdetermined, unsolved.Single(s => s.ProblemId == "IONP-H04").Status);
        Assert.Equal(InferenceStatus.Underdetermined, unsolved.Single(s => s.ProblemId == "ORG-H08").Status);
        Assert.Equal(InferenceStatus.Underdetermined, unsolved.Single(s => s.ProblemId == "EXP-H08").Status);
    }

    [Fact]
    public void Solve_CalcProblem_ReportsFormulaAndFacts()
    {
        var solution = ProblemSolver.Solve(ById("CALC-001"));
        Assert.True(solution.IsSolved);
        Assert.Equal("mol/L", solution.NumericUnit);
        Assert.Contains(solution.Steps, s => s.Description.Contains("c = n / V"));
        Assert.Contains(solution.Steps, s => s.Description.Contains("已知"));
    }

    [Fact]
    public void Solve_CalcWithoutSpec_ReturnsNoSolution()
    {
        var source = ById("CALC-001");
        var problem = new ProblemSpec
        {
            Id = source.Id,
            Category = ProblemCategory.StoichiometryCalc,
            Given = source.Given,
            Expected = new ExpectedSpec { Calc = null },
        };
        var solution = ProblemSolver.Solve(problem);
        Assert.Equal(InferenceStatus.NoSolution, solution.Status);
    }

    private static void AssertSetEqual(List<string> expected, List<string> actual)
    {
        Assert.Equal(
            expected.OrderBy(x => x, StringComparer.Ordinal),
            actual.OrderBy(x => x, StringComparer.Ordinal));
    }
}
