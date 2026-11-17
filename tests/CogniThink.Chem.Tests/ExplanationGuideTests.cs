using CogniThink.Chem.Core.Benchmark;
using CogniThink.Chem.Core.Explanation;
using CogniThink.Chem.Core.Solving;
using Xunit;

namespace CogniThink.Chem.Tests;

/// <summary>
/// "能讲"与"能指导学生思考"测试：
/// 讲解必须逐步、含知识点引用与答案小结；引导必须分级且不泄露答案。
/// </summary>
public class ExplanationGuideTests
{
    private static readonly IReadOnlyList<ProblemSpec> Problems = BenchmarkLoader.LoadAll();
    private static readonly Explainer Explainer = new();

    private static List<(ProblemSpec Problem, Solution Solution)> Solved()
    {
        return Problems
            .Select(p => (Problem: p, Solution: ProblemSolver.Solve(p)))
            .Where(x => x.Solution.IsSolved)
            .ToList();
    }

    // ------------------------------------------------------------------
    // 能讲
    // ------------------------------------------------------------------

    [Fact]
    public void Explain_AllSolvedProblems_HasStepsSummaryAndFullText()
    {
        foreach (var (problem, solution) in Solved())
        {
            var explanation = Explainer.Explain(solution);
            Assert.Equal(problem.Id, explanation.ProblemId);
            Assert.NotEmpty(explanation.Steps);
            Assert.False(string.IsNullOrWhiteSpace(explanation.AnswerSummary),
                $"{problem.Id}: 答案小结为空");
            Assert.Contains(problem.Id, explanation.FullText);
            Assert.All(explanation.Steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Text)));
        }
    }

    [Fact]
    public void Explain_KnowledgeDrivenProblems_ReferenceKnowledgePoints()
    {
        // 四类知识驱动题：凡解中引用了反应（方程式非空）的讲解必须给出知识点引用
        // （名称+教材出处）。"能共存"类结论无反应可引，不要求。
        var categories = new[]
        {
            ProblemCategory.ProductInference,
            ProblemCategory.IonCoexistence,
            ProblemCategory.SubstanceIdentification,
            ProblemCategory.ProcessInference,
        };
        var checkedCount = 0;
        foreach (var (problem, solution) in Solved().Where(x =>
                     categories.Contains(x.Problem.Category) && x.Solution.Equations.Count > 0))
        {
            var explanation = Explainer.Explain(solution);
            Assert.True(explanation.References.Count > 0, $"{problem.Id}: 讲解未引用任何知识点");
            Assert.All(explanation.References, r =>
            {
                Assert.False(string.IsNullOrWhiteSpace(r.Name));
                Assert.False(string.IsNullOrWhiteSpace(r.TextbookRef));
            });
            checkedCount++;
        }

        Assert.True(checkedCount >= 20, $"应有足量知识驱动解被检查，实际 {checkedCount}");
    }

    [Fact]
    public void Explain_AnswerSummary_MatchesCategory()
    {
        var byId = Problems.ToDictionary(p => p.Id, p => p);

        var coex = Explainer.Explain(ProblemSolver.Solve(byId["COEX-001"]));
        Assert.Contains("不能大量共存", coex.AnswerSummary);
        Assert.Contains("CO3^2- + 2H^+ = H2O + CO2↑", coex.AnswerSummary);

        var calc = Explainer.Explain(ProblemSolver.Solve(byId["CALC-001"]));
        Assert.Contains("1 mol/L", calc.AnswerSummary);

        var proc = Explainer.Explain(ProblemSolver.Solve(byId["PROC-001"]));
        Assert.Contains("Fe2O3", proc.AnswerSummary);
        Assert.Contains("Fe2O3 + 6HCl = 2FeCl3 + 3H2O", proc.AnswerSummary);

        var ident = Explainer.Explain(ProblemSolver.Solve(byId["IDENT-001"]));
        Assert.Contains("一定存在", ident.AnswerSummary);
        Assert.Contains("一定不存在", ident.AnswerSummary);

        var prod = Explainer.Explain(ProblemSolver.Solve(byId["PROD-001"]));
        Assert.Contains("2Na + Cl2 =点燃= 2NaCl", prod.AnswerSummary);
    }

    [Fact]
    public void Explain_RefusedProblem_ExplainsWhyNotSolved()
    {
        var solution = ProblemSolver.Solve(Problems.Single(p => p.Id == "PROD-004"));
        var explanation = Explainer.Explain(solution);
        Assert.Contains("无法给出产物", explanation.AnswerSummary);
    }

    // ------------------------------------------------------------------
    // 能指导学生思考（伴思：只引导，不给答案）
    // ------------------------------------------------------------------

    [Fact]
    public void Guide_AllProblems_HasSixFrameworkSteps()
    {
        foreach (var problem in Problems)
        {
            var solution = ProblemSolver.Solve(problem);
            var guide = GuideGenerator.Generate(solution, problem);
            Assert.Equal(6, guide.Levels.Count);
            Assert.Equal(new[] { "审题分类", "建立模型", "知识检索", "规则应用", "精确求解", "守恒校验" },
                guide.Levels.Select(l => l.Title).ToArray());
            Assert.All(guide.Levels, l => Assert.NotEmpty(l.Prompts));
            Assert.Contains(problem.Id, guide.FullText);
        }
    }

    [Fact]
    public void Guide_NeverLeaksAnswerEquations()
    {
        foreach (var problem in Problems)
        {
            var solution = ProblemSolver.Solve(problem);
            var guide = GuideGenerator.Generate(solution, problem);
            foreach (var equation in problem.Expected.Equations)
            {
                Assert.DoesNotContain(equation, guide.FullText);
            }
        }
    }

    [Fact]
    public void Guide_NeverLeaksSubstanceAssignments()
    {
        foreach (var problem in Problems.Where(p => p.Category == ProblemCategory.ProcessInference))
        {
            var solution = ProblemSolver.Solve(problem);
            var guide = GuideGenerator.Generate(solution, problem);
            // 过程推断的候选物种已在题面给出，真正的答案是"标签→物质"的对应关系
            // （答案小结格式为 "A 是 Fe2O3"）。因此检查 "{label} 是 " 赋值模式不出现，
            // 而不是检查物质名不出现（否则会误伤知识点摘要中合法出现的化学式，如 Na）。
            foreach (var label in problem.Expected.SubstanceMap!.Keys)
            {
                Assert.DoesNotContain($"{label} 是 ", guide.FullText);
                Assert.DoesNotContain($"是 {label}", guide.FullText);
            }
        }
    }

    [Fact]
    public void Guide_NeverLeaksCompositionOrNumericAnswers()
    {
        // 成分推断的候选物种已在题面 Given.CandidateSpecies 给出，本身不是秘密；
        // 真正的答案是"一定存在/一定不存在"的判定。判定在答案小结中以"一定存在：X"
        // （冒号）形式出现；题面设问中的"一定存在的离子"用"的"而非冒号，不判定。
        var verdictPhrases = new[] { "一定存在：", "一定不存在：", "可能存在：" };
        foreach (var problem in Problems.Where(p =>
                     p.Category is ProblemCategory.SubstanceIdentification or ProblemCategory.IonPowderIdentification))
        {
            var solution = ProblemSolver.Solve(problem);
            var guide = GuideGenerator.Generate(solution, problem);
            foreach (var phrase in verdictPhrases)
            {
                Assert.DoesNotContain(phrase, guide.FullText);
            }
        }

        foreach (var problem in Problems.Where(p => p.Category == ProblemCategory.StoichiometryCalc))
        {
            var solution = ProblemSolver.Solve(problem);
            var guide = GuideGenerator.Generate(solution, problem);
            var answer = problem.Expected.NumericAnswer!;
            Assert.DoesNotContain($"{answer.Value} {answer.Unit}", guide.FullText);
        }
    }

    [Fact]
    public void Guide_CalcProblem_HintsFormulaWithoutValue()
    {
        var problem = Problems.Single(p => p.Id == "CALC-001");
        var guide = GuideGenerator.Generate(ProblemSolver.Solve(problem), problem);
        Assert.Contains("c = n / V", guide.FullText);
        Assert.DoesNotContain("1 mol/L", guide.FullText);
    }
}
