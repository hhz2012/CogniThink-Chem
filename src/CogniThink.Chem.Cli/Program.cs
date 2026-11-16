using System.Text;
using System.Text.Json;
using CogniThink.Chem.Core;
using CogniThink.Chem.Core.Benchmark;
using CogniThink.Chem.Core.Common;
using CogniThink.Chem.Core.Data;
using CogniThink.Chem.Core.Explanation;
using CogniThink.Chem.Core.Knowledge;
using CogniThink.Chem.Core.Solving;

Console.OutputEncoding = System.Text.Encoding.UTF8;

if (args.Length > 0 && args[0] == "--demo")
{
    RunDemo();
    return 0;
}

if (args.Length > 0 && args[0] == "--solve")
{
    return RunSolveDemo(args.Length > 1 ? args[1] : null);
}

if (args.Length > 0 && args[0] == "--export-web")
{
    return RunExportWeb(args.Length > 1 ? args[1] : null);
}

if (args.Length > 0 && args[0] == "--verify-real")
{
    return RunVerifyReal();
}

Console.WriteLine($"{ModuleInfo.DisplayName} ({ModuleInfo.Name}) v{ModuleInfo.Version}");
Console.WriteLine("命令：--demo（统一校验演示）　--solve [题目ID]（解+讲+引导 演示，可指定全量基准题库中的单题）　--export-web [输出目录]（导出前端网页数据）　--verify-real（真题求解验证）");
return 0;

/// <summary>
/// 全链路演示：统一入口 ChemicalCheckerService 的典型调用与返回结构示例。
/// 覆盖：正确分子方程式、学生错题（未配平）、离子方程式、碱性介质氧化还原、
/// SMILES 官能团校验、陌生物质超纲标记。
/// </summary>
static void RunDemo()
{
    var cases = new (string Title, CheckRequest Request)[]
    {
        ("正确分子方程式（氢气燃烧）", new CheckRequest { Equation = "2H2 + O2 =点燃= 2H2O" }),
        ("学生错题（未配平的水电解）", new CheckRequest { Equation = "H2O =通电= H2 + O2" }),
        ("离子方程式（硫酸钡沉淀）", new CheckRequest { Equation = "Ba^2+ + SO4^2- = BaSO4↓" }),
        ("离子方程式错题（醋酸误拆为 H^+）", new CheckRequest { Equation = "CH3COO^- + H^+ = Na^+ + Cl^-" }),
        ("碱性介质氧化还原", new CheckRequest
        {
            Equation = "2MnO4^- + 3SO3^2- + H2O = 2MnO2↓ + 3SO4^2- + 2OH^-",
            Medium = "basic"
        }),
        ("SMILES 官能团校验（葡萄糖）", new CheckRequest { Smiles = "OCC(O)C(O)C(O)C(O)C=O" }),
        ("陌生物质（超纲干扰项）", new CheckRequest { Equation = "Xy + O2 = XyO2" })
    };

    foreach (var (title, request) in cases)
    {
        var report = ChemicalCheckerService.Check(request);
        Console.WriteLine($"■ {title}");
        Console.WriteLine($"  输入：{request.Equation ?? request.Smiles}");
        Console.WriteLine($"  结论：{(report.IsValid ? "校验通过" : "校验未通过")}　风险标签：{(report.RiskTag.Length == 0 ? "无" : report.RiskTag)}");
        if (report.ErrorMessage.Length > 0)
        {
            Console.WriteLine($"  致命错误：{report.ErrorMessage}");
        }

        foreach (var item in report.CheckItems)
        {
            var mark = item.Passed ? "√" : "×";
            var detail = item.Detail.Length > 0 ? $"：{item.Detail}" : "";
            Console.WriteLine($"  {mark} {item.CheckName}{detail}");
        }

        Console.WriteLine();
    }
}

/// <summary>
/// 求解演示（"能解、能讲、能指导学生思考"）：
/// 指定题目 ID 时从全量基准题库（含九类）检索并只输出该题；
/// 未指定时五类题各选一题，打印"推断/计算 → 解 → 讲解 → 引导"全链路。
/// </summary>
static int RunSolveDemo(string? singleId)
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

/// <summary>
/// 导出前端网页数据：对全部基准题真实运行"求解 → 讲解 → 引导"全链路，
/// 把题目与引擎产出（解/讲/引导）序列化为 web/data/problems.json，
/// 供 web/ 静态站直接渲染。网页内容全部来自引擎运行结果，不手写。
/// </summary>
static int RunExportWeb(string? outputDir)
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

/// <summary>
/// 真题验证：加载 data/real-exam/real-exam.json（高考真题改编），
/// 用推断引擎逐题求解，与真题标准答案逐字段比对（结论/方程式/成分/对应关系/精确数值），
/// 输出 PASS/FAIL 报告。FAIL 即暴露知识库覆盖缺口或引擎能力边界，如实呈现、不粉饰。
/// </summary>
static int RunVerifyReal()
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
        var (ok, detail) = CompareWithStandardAnswer(problem, solution);
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

/// <summary>引擎结论 vs 真题标准答案逐字段比对，返回（是否一致，差异明细）。</summary>
static (bool Ok, List<string> Detail) CompareWithStandardAnswer(ProblemSpec problem, Solution solution)
{
    var detail = new List<string>();
    var expected = problem.Expected;
    var ok = true;

    switch (problem.Category)
    {
        case ProblemCategory.IonCoexistence:
        {
            var got = solution.Coexistence?.ToString() ?? "(无结论)";
            var want = expected.Verdict?.ToString() ?? "(无)";
            var same = string.Equals(got, want, StringComparison.Ordinal);
            detail.Add($"  共存结论：引擎={got}　标准={want}　{(same ? "一致" : "不一致")}");
            ok &= same;
            break;
        }
        case ProblemCategory.SubstanceIdentification:
        {
            var got = solution.Composition.ToDictionary(kv => kv.Key, kv => kv.Value.ToString(), StringComparer.Ordinal);
            var want = expected.Composition?.ToDictionary(kv => kv.Key, kv => kv.Value.ToString(), StringComparer.Ordinal)
                       ?? new Dictionary<string, string>(StringComparer.Ordinal);
            var same = got.Count == want.Count && want.All(kv => got.TryGetValue(kv.Key, out var v) && v == kv.Value);
            detail.Add($"  成分结论：引擎={{{string.Join("，", got.Select(kv => $"{kv.Key}:{kv.Value}"))}}}");
            detail.Add($"  　　　　　标准={{{string.Join("，", want.Select(kv => $"{kv.Key}:{kv.Value}"))}}}　{(same ? "一致" : "不一致")}");
            ok &= same;
            break;
        }
        case ProblemCategory.ProcessInference:
        {
            var got = solution.SubstanceMap;
            var want = expected.SubstanceMap ?? new Dictionary<string, string>();
            var same = got.Count == want.Count && want.All(kv => got.TryGetValue(kv.Key, out var v) && v == kv.Value);
            detail.Add($"  物质对应：引擎={{{string.Join("，", got.Select(kv => $"{kv.Key}:{kv.Value}"))}}}");
            detail.Add($"  　　　　　标准={{{string.Join("，", want.Select(kv => $"{kv.Key}:{kv.Value}"))}}}　{(same ? "一致" : "不一致")}");
            ok &= same;
            break;
        }
        case ProblemCategory.StoichiometryCalc:
        {
            var want = expected.NumericAnswer;
            if (want is null)
            {
                detail.Add("  标准答案缺少数值结论，无法比对。");
                ok = false;
            }
            else
            {
                var wantValue = CogniThink.Chem.Core.Math.Rational.Parse(want.Value);
                var same = solution.NumericValue is { } v && v == wantValue
                           && string.Equals(solution.NumericUnit, want.Unit, StringComparison.Ordinal);
                detail.Add($"  数值结论：引擎={solution.NumericValue?.ToString() ?? "(无)"} {solution.NumericUnit}　标准={want.Value} {want.Unit}　{(same ? "一致" : "不一致")}");
                ok &= same;
            }

            break;
        }
        case ProblemCategory.Electrochemistry:
        {
            if (!solution.IsSolved)
            {
                detail.Add($"  引擎未解出：{solution.Message}");
                ok = false;
                break;
            }

            var got = solution.Electrodes;
            var want = expected.Electrodes;
            foreach (var (label, g, w) in new (string Label, string? Got, string? Want)[]
            {
                ("负极", got?.Negative, want?.Negative),
                ("正极", got?.Positive, want?.Positive),
                ("阳极", got?.Anode, want?.Anode),
                ("阴极", got?.Cathode, want?.Cathode),
            })
            {
                if (string.IsNullOrWhiteSpace(g) && string.IsNullOrWhiteSpace(w))
                {
                    continue;
                }

                var same = string.Equals(g ?? "", w ?? "", StringComparison.Ordinal);
                detail.Add($"  {label}电极式：引擎={g ?? "(无)"}　标准={w ?? "(无)"}　{(same ? "一致" : "不一致")}");
                ok &= same;
            }

            break;
        }
        case ProblemCategory.ChemicalEquilibrium:
        {
            if (!solution.IsSolved)
            {
                detail.Add($"  引擎未解出：{solution.Message}");
                ok = false;
                break;
            }

            var got = solution.Equilibrium;
            var want = expected.Equilibrium;
            if (got is null || want is null)
            {
                detail.Add("  三段式结论缺失，无法比对。");
                ok = false;
                break;
            }

            var tableSame = got.Table.Count == want.Table.Count
                            && want.Table.All(wr => got.Table.Any(gr =>
                                gr.Species == wr.Species && gr.Start == wr.Start
                                && gr.Change == wr.Change && gr.End == wr.End));
            detail.Add($"  三段式：引擎={{{string.Join("；", got.Table.Select(r => $"{r.Species} 起始{r.Start} 转化{r.Change} 平衡{r.End}"))}}}");
            detail.Add($"  　　　　标准={{{string.Join("；", want.Table.Select(r => $"{r.Species} 起始{r.Start} 转化{r.Change} 平衡{r.End}"))}}}　{(tableSame ? "一致" : "不一致")}");
            ok &= tableSame;

            foreach (var (label, g, w) in new (string Label, string? Got, string? Want)[]
            {
                ("转化率", got.ConversionSpecies is null ? null : $"{got.ConversionSpecies} {got.Conversion}", want.ConversionSpecies is null ? null : $"{want.ConversionSpecies} {want.Conversion}"),
                ("摩尔分数", got.FractionSpecies is null ? null : $"{got.FractionSpecies} {got.Fraction}", want.FractionSpecies is null ? null : $"{want.FractionSpecies} {want.Fraction}"),
                ("K 值", got.KValue, want.KValue),
                ("移动方向", got.Direction, want.Direction),
            })
            {
                if (string.IsNullOrWhiteSpace(g) && string.IsNullOrWhiteSpace(w))
                {
                    continue;
                }

                var same = string.Equals(g ?? "", w ?? "", StringComparison.Ordinal);
                detail.Add($"  {label}：引擎={g ?? "(无)"}　标准={w ?? "(无)"}　{(same ? "一致" : "不一致")}");
                ok &= same;
            }

            break;
        }
        case ProblemCategory.AqueousIonEquilibrium:
        {
            if (!solution.IsSolved)
            {
                detail.Add($"  引擎未解出：{solution.Message}");
                ok = false;
                break;
            }

            var got = solution.Aqueous;
            var want = expected.Aqueous;
            if (got is null || want is null)
            {
                detail.Add("  水溶液结论缺失，无法比对。");
                ok = false;
                break;
            }

            if (want.Order.Count > 0 || got.Order.Count > 0)
            {
                var same = got.Order.SequenceEqual(want.Order, StringComparer.Ordinal);
                detail.Add($"  浓度顺序：引擎={string.Join(" > ", got.Order)}");
                detail.Add($"  　　　　　标准={string.Join(" > ", want.Order)}　{(same ? "一致" : "不一致")}");
                ok &= same;
            }

            if (want.ConservationEquations.Count > 0 || got.ConservationEquations.Count > 0)
            {
                var same = new HashSet<string>(got.ConservationEquations, StringComparer.Ordinal)
                    .SetEquals(want.ConservationEquations);
                detail.Add($"  守恒等式：引擎={{{string.Join("；", got.ConservationEquations)}}}");
                detail.Add($"  　　　　　标准={{{string.Join("；", want.ConservationEquations)}}}　{(same ? "一致" : "不一致")}");
                ok &= same;
            }

            break;
        }
        case ProblemCategory.IndustrialProcess:
        {
            if (!solution.IsSolved)
            {
                detail.Add($"  引擎未解出：{solution.Message}");
                ok = false;
                break;
            }

            var got = solution.SubstanceMap;
            var want = expected.SubstanceMap ?? new Dictionary<string, string>();
            if (want.Count > 0 || got.Count > 0)
            {
                var same = got.Count == want.Count && want.All(kv => got.TryGetValue(kv.Key, out var v) && v == kv.Value);
                detail.Add($"  物质对应：引擎={{{string.Join("，", got.Select(kv => $"{kv.Key}:{kv.Value}"))}}}");
                detail.Add($"  　　　　　标准={{{string.Join("，", want.Select(kv => $"{kv.Key}:{kv.Value}"))}}}　{(same ? "一致" : "不一致")}");
                ok &= same;
            }

            foreach (var (stepNumber, keyword) in expected.Purposes ?? new Dictionary<string, string>())
            {
                var actual = solution.Purposes.TryGetValue(stepNumber, out var p) ? p : "(缺)";
                var hit = actual.Contains(keyword, StringComparison.Ordinal);
                detail.Add($"  步骤{stepNumber}目的：引擎={actual}　标准须含\"{keyword}\"　{(hit ? "一致" : "不一致")}");
                ok &= hit;
            }

            break;
        }
        case ProblemCategory.IonPowderIdentification:
        {
            if (!solution.IsSolved)
            {
                detail.Add($"  引擎未解出：{solution.Message}");
                ok = false;
                break;
            }

            var got = solution.Composition.ToDictionary(kv => kv.Key, kv => kv.Value.ToString(), StringComparer.Ordinal);
            var want = expected.Composition?.ToDictionary(kv => kv.Key, kv => kv.Value.ToString(), StringComparer.Ordinal)
                       ?? new Dictionary<string, string>(StringComparer.Ordinal);
            var same = got.Count == want.Count && want.All(kv => got.TryGetValue(kv.Key, out var v) && v == kv.Value);
            detail.Add($"  成分结论：引擎={{{string.Join("，", got.Select(kv => $"{kv.Key}:{kv.Value}"))}}}");
            detail.Add($"  　　　　　标准={{{string.Join("，", want.Select(kv => $"{kv.Key}:{kv.Value}"))}}}　{(same ? "一致" : "不一致")}");
            ok &= same;
            break;
        }
        case ProblemCategory.OrganicInference:
        {
            if (!solution.IsSolved)
            {
                detail.Add($"  引擎未解出：{solution.Message}");
                ok = false;
                break;
            }

            var got = solution.SubstanceMap;
            var want = expected.SubstanceMap ?? new Dictionary<string, string>();
            var same = got.Count == want.Count && want.All(kv => got.TryGetValue(kv.Key, out var v) && v == kv.Value);
            detail.Add($"  物质对应：引擎={{{string.Join("，", got.Select(kv => $"{kv.Key}:{kv.Value}"))}}}");
            detail.Add($"  　　　　　标准={{{string.Join("，", want.Select(kv => $"{kv.Key}:{kv.Value}"))}}}　{(same ? "一致" : "不一致")}");
            ok &= same;
            break;
        }
        case ProblemCategory.ExperimentalInquiry:
        {
            if (!solution.IsSolved)
            {
                detail.Add($"  引擎未解出：{solution.Message}");
                ok = false;
                break;
            }

            var got = solution.Experiment?.Conclusions ?? new List<string>();
            var want = expected.Experiment?.Conclusions ?? new List<string>();
            var same = got.Count == want.Count && got.SequenceEqual(want, StringComparer.Ordinal);
            detail.Add($"  实验结论：引擎={{{string.Join("；", got)}}}");
            detail.Add($"  　　　　　标准={{{string.Join("；", want)}}}　{(same ? "一致" : "不一致")}");
            ok &= same;
            break;
        }
        default:
        {
            if (!solution.IsSolved)
            {
                detail.Add($"  引擎未解出：{solution.Message}");
                ok = false;
            }

            break;
        }
    }

    if (expected.Equations.Count > 0 || solution.Equations.Count > 0)
    {
        var sameEq = new HashSet<string>(solution.Equations, StringComparer.Ordinal)
            .SetEquals(expected.Equations);
        detail.Add($"  方程式：引擎={{{string.Join("；", solution.Equations)}}}");
        detail.Add($"  　　　　标准={{{string.Join("；", expected.Equations)}}}　{(sameEq ? "一致" : "不一致")}");
        ok &= sameEq;
    }

    return (ok, detail);
}
