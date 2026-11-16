using CogniThink.Chem.Core.Benchmark;
using CogniThink.Chem.Core.Inference;
using CogniThink.Chem.Core.Math;
using CogniThink.Chem.Core.Solving;
using Xunit;

namespace CogniThink.Chem.Tests;

/// <summary>
/// 端到端求解基准（"能解"）：求解器对全部 42 道基准题推导答案，
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
                Assert.NotNull(expected.SubstanceMap);
                Assert.Equal(expected.SubstanceMap!, solution.SubstanceMap);
                Assert.Equal(expected.Equations, solution.Equations);
                break;

            case ProblemCategory.StoichiometryCalc:
                Assert.NotNull(expected.NumericAnswer);
                Assert.NotNull(solution.NumericValue);
                Assert.Equal(Rational.Parse(expected.NumericAnswer!.Value), solution.NumericValue);
                break;
        }

        // 每个解都必须带可溯源的求解步骤（讲解/引导的原料）。
        Assert.NotEmpty(solution.Steps);
        Assert.All(solution.Steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Description)));
    }

    [Fact]
    public void SolveAll_Covers42Problems()
    {
        var solutions = ProblemSolver.SolveAll(Problems);
        Assert.Equal(42, solutions.Count);
        // 41 题解出；PROD-004（陌生物质）按铁律拒答。
        Assert.Equal(41, solutions.Count(s => s.IsSolved));
        var refused = solutions.Single(s => !s.IsSolved);
        Assert.Equal("PROD-004", refused.ProblemId);
        Assert.Equal(InferenceStatus.NoSolution, refused.Status);
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
