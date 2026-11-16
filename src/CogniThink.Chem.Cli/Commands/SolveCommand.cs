using CogniThink.Chem.Core.Benchmark;
using CogniThink.Chem.Core.Data;
using CogniThink.Chem.Core.Explanation;
using CogniThink.Chem.Core.Solving;

namespace CogniThink.Chem.Cli.Commands;

/// <summary>
/// --solve 命令：解+讲+引导 演示。
/// </summary>
internal static class SolveCommand
{
    /// <summary>
    /// 求解演示（"能解、能讲、能指导学生思考"）：
    /// 指定题目 ID 时从全量基准题库（含九类）检索并只输出该题；
    /// 未指定时五类题各选一题，打印"推断/计算 → 解 → 讲解 → 引导"全链路。
    /// </summary>
    public static int Run(string? singleId)
    {
        var problems = BenchmarkLoader.LoadAll();
        var knowledge = ChemDataLoader.LoadKnowledge(DataRoot.Resolve());
        var explainer = new Explainer(knowledge);

        List<ProblemSpec> selected;
        if (!string.IsNullOrWhiteSpace(singleId))
        {
            var problem = problems.FirstOrDefault(p =>
                string.Equals(p.Id, singleId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (problem is null)
            {
                Console.WriteLine($"未找到题目 ID \"{singleId}\"：--solve 从全量基准题库（{problems.Count} 题、九类题型）中检索。");
                return 1;
            }

            selected = new List<ProblemSpec> { problem };
        }
        else
        {
            var demoIds = new[] { "PROD-003", "IDENT-006", "PROC-001", "COEX-001", "CALC-003" };
            selected = demoIds.Select(id => problems.Single(p => p.Id == id)).ToList();
        }

        foreach (var problem in selected)
        {
            var solution = ProblemSolver.Solve(problem);
            var explanation = explainer.Explain(solution);
            var guide = GuideGenerator.Generate(solution, problem, knowledge);

            Console.WriteLine(new string('=', 76));
            Console.WriteLine($"[{problem.Id}] {Explainer.CategoryName(problem.Category)}｜难度 {problem.Difficulty}");
            Console.WriteLine($"题目：{problem.ProblemText}");
            Console.WriteLine();

            Console.WriteLine("—— 解 ——");
            Console.WriteLine($"状态：{(solution.IsSolved ? "已解出" : solution.Status.ToString())}");
            Console.WriteLine($"答案：{explanation.AnswerSummary}");
            Console.WriteLine("求解步骤（可溯源）：");
            foreach (var step in solution.Steps)
            {
                var evidence = step.Evidence.Length > 0 ? $"　[{step.Evidence}]" : "";
                Console.WriteLine($"  {step.Order}. {step.Description}{evidence}");
            }

            Console.WriteLine();
            Console.WriteLine("—— 讲 ——");
            Console.WriteLine(explanation.FullText);

            Console.WriteLine();
            Console.WriteLine("—— 引导（苏格拉底式，不直接给答案）——");
            Console.WriteLine(guide.FullText);
            Console.WriteLine();
        }

        return 0;
    }
}