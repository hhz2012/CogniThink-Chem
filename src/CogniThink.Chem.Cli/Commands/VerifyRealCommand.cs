using CogniThink.Chem.Core.Benchmark;
using CogniThink.Chem.Core.Data;
using CogniThink.Chem.Core.Explanation;
using CogniThink.Chem.Core.Solving;

namespace CogniThink.Chem.Cli.Commands;

/// <summary>
/// --verify-real 命令：真题求解验证。
/// </summary>
internal static class VerifyRealCommand
{
    /// <summary>
    /// 真题验证：加载 data/real-exam/real-exam.json（高考真题改编），
    /// 用推断引擎逐题求解，与真题标准答案逐字段比对（结论/方程式/成分/对应关系/精确数值），
    /// 输出 PASS/FAIL 报告。FAIL 即暴露知识库覆盖缺口或引擎能力边界，如实呈现、不粉饰。
    /// </summary>
    public static int Run()
    {
        var dataRoot = DataRoot.Resolve();
        var repoRoot = Path.GetDirectoryName(dataRoot)!;
        var file = Path.Combine(repoRoot, "data", "real-exam", "real-exam.json");
        var problems = ChemDataLoader.ReadItems<ProblemSpec>(file);

        var pass = 0;
        var fail = 0;
        foreach (var problem in problems)
        {
            var solution = ProblemSolver.Solve(problem);
            var (ok, detail) = RealExamComparer.Compare(problem, solution);
            if (ok)
            {
                pass++;
            }
            else
            {
                fail++;
            }

            Console.WriteLine($"{(ok ? "PASS" : "FAIL")} [{problem.Id}] {Explainer.CategoryName(problem.Category)}｜难度 {problem.Difficulty}");
            Console.WriteLine($"  题目：{problem.ProblemText}");
            Console.WriteLine($"  出处：{problem.Source}");
            Console.WriteLine($"  引擎：{(solution.IsSolved ? "已解出" : solution.Status.ToString())}　{solution.Message}");
            foreach (var line in detail)
            {
                Console.WriteLine($"  {line}");
            }

            Console.WriteLine();
        }

        Console.WriteLine(new string('=', 60));
        Console.WriteLine($"真题验证汇总：{pass} PASS / {fail} FAIL（共 {problems.Count} 题）");
        return fail == 0 ? 0 : 1;
    }
}