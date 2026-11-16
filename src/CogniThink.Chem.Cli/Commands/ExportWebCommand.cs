using System.Text;
using System.Text.Json;
using CogniThink.Chem.Core;
using CogniThink.Chem.Core.Benchmark;
using CogniThink.Chem.Core.Data;
using CogniThink.Chem.Core.Explanation;
using CogniThink.Chem.Core.Solving;

namespace CogniThink.Chem.Cli.Commands;

/// <summary>
/// --export-web 命令：导出前端网页数据。
/// </summary>
internal static class ExportWebCommand
{
    /// <summary>
    /// 导出前端网页数据：对全部基准题真实运行"求解 → 讲解 → 引导"全链路，
    /// 把题目与引擎产出（解/讲/引导）序列化为 web/data/problems.json，
    /// 供 web/ 静态站直接渲染。网页内容全部来自引擎运行结果，不手写。
    /// </summary>
    public static int Run(string? outputDir)
    {
        var dataRoot = DataRoot.Resolve();
        var repoRoot = Path.GetDirectoryName(dataRoot)!;
        var targetDir = Path.GetFullPath(string.IsNullOrWhiteSpace(outputDir)
            ? Path.Combine(repoRoot, "web", "data")
            : outputDir);
        Directory.CreateDirectory(targetDir);

        var problems = BenchmarkLoader.LoadAll(dataRoot);
        var knowledge = ChemDataLoader.LoadKnowledge(dataRoot);
        var explainer = new Explainer(knowledge);

        var records = new List<object>();
        foreach (var problem in problems)
        {
            var solution = ProblemSolver.Solve(problem);
            var explanation = explainer.Explain(solution);
            var guide = GuideGenerator.Generate(solution, problem, knowledge);

            records.Add(new
            {
                problem = new
                {
                    problem.Id,
                    problem.Category,
                    categoryName = Explainer.CategoryName(problem.Category),
                    problem.Difficulty,
                    problem.ProblemText,
                    problem.Source,
                    problem.Tags,
                    question = new { problem.Question.Ask, problem.Question.Targets },
                    given = new
                    {
                        problem.Given.Species,
                        problem.Given.Conditions,
                        problem.Given.Medium,
                        problem.Given.CandidateSpecies,
                        problem.Given.SingleEntity,
                        steps = problem.Given.Steps.Select(s => new
                        {
                            s.Step, s.From, s.To, s.Reagents, s.Conditions, s.Phenomenon
                        }),
                        numericFacts = problem.Given.NumericFacts.Select(f => new
                        {
                            f.Kind, f.Species, f.Value, f.Unit
                        }),
                        experiment = problem.Given.Experiment is { } ex ? new
                        {
                            ex.Mode, ex.Targets, ex.Operations, ex.Phenomena, ex.CandidateSpecies
                        } : null,
                    },
                },
                solution = new
                {
                    status = solution.Status.ToString(),
                    isSolved = solution.IsSolved,
                    solution.Message,
                    solution.Equations,
                    solution.Composition,
                    solution.SubstanceMap,
                    coexistence = solution.Coexistence?.ToString(),
                    numericValue = solution.NumericValue?.ToString(),
                    solution.NumericUnit,
                    solution.NumericSpecies,
                    solution.Electrodes,
                    solution.Equilibrium,
                    solution.Aqueous,
                    solution.Purposes,
                    solution.Experiment,
                    steps = solution.Steps.Select(s => new
                    {
                        s.Order, s.Description, s.Evidence, s.KnowledgeId, s.Source
                    }),
                },
                explanation = new
                {
                    explanation.AnswerSummary,
                    steps = explanation.Steps.Select(s => new
                    {
                        s.Order, s.Text, s.Evidence, s.KnowledgeId, s.KnowledgeName, s.Source
                    }),
                    explanation.References,
                    explanation.FullText,
                },
                guide = new
                {
                    guide.Levels,
                    guide.FullText,
                },
            });
        }

        var manifest = new
        {
            engine = new
            {
                name = ModuleInfo.Name,
                displayName = ModuleInfo.DisplayName,
                version = ModuleInfo.Version,
            },
            generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            knowledgeCount = knowledge.Count,
            total = records.Count,
            categories = Enum.GetValues<ProblemCategory>()
                .Select(c => new
                {
                    category = c.ToString(),
                    name = Explainer.CategoryName(c),
                    count = problems.Count(p => p.Category == c),
                }),
            problems = records,
        };

        var options = new JsonSerializerOptions(ChemDataLoader.Options)
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        var file = Path.Combine(targetDir, "problems.json");
        File.WriteAllText(file, JsonSerializer.Serialize(manifest, options), new UTF8Encoding(false));

        Console.WriteLine($"已导出 {records.Count} 道题（解+讲+引导）→ {file}");
        foreach (var c in manifest.categories)
        {
            Console.WriteLine($"  {c.name}：{c.count} 题");
        }

        return 0;
    }
}