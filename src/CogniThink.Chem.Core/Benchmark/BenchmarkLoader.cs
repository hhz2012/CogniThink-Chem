using CogniThink.Chem.Core.Data;

namespace CogniThink.Chem.Core.Benchmark;

/// <summary>
/// 标准测试集加载器。数据位于 data/benchmark/problems/*.json，
/// 每个文件为 <see cref="DataFile{T}"/> 结构（version + items），复用 <see cref="ChemDataLoader"/> 的
/// camelCase/字符串枚举/注释容忍选项。
/// </summary>
public static class BenchmarkLoader
{
    /// <summary>测试集相对数据根目录的子路径。</summary>
    public const string RelativeDirectory = "benchmark/problems";

    /// <summary>类别 → 文件名映射（文件名即类别的 kebab-case）。</summary>
    public static readonly IReadOnlyDictionary<ProblemCategory, string> CategoryFiles =
        new Dictionary<ProblemCategory, string>
        {
            [ProblemCategory.ProductInference] = "product-inference.json",
            [ProblemCategory.SubstanceIdentification] = "substance-identification.json",
            [ProblemCategory.ProcessInference] = "process-inference.json",
            [ProblemCategory.IonCoexistence] = "ion-coexistence.json",
            [ProblemCategory.StoichiometryCalc] = "stoichiometry.json",
            [ProblemCategory.Electrochemistry] = "hard-electrochemistry.json",
            [ProblemCategory.ChemicalEquilibrium] = "hard-equilibrium.json",
            [ProblemCategory.AqueousIonEquilibrium] = "hard-aqueous-equilibrium.json",
            [ProblemCategory.IndustrialProcess] = "hard-process-flow.json",
            [ProblemCategory.IonPowderIdentification] = "hard-ion-powder.json",
            [ProblemCategory.OrganicInference] = "hard-organic.json",
            [ProblemCategory.ExperimentalInquiry] = "hard-experiment.json"
        };

    /// <summary>加载全部类别的题目（按文件名排序，类别内保持文件顺序）。</summary>
    /// <param name="dataRoot">数据根目录；null 时按 <see cref="DataRoot.Resolve"/> 定位。</param>
    public static IReadOnlyList<ProblemSpec> LoadAll(string? dataRoot = null)
    {
        var root = ResolveDirectory(dataRoot);
        var problems = new List<ProblemSpec>();
        foreach (var file in System.IO.Directory.EnumerateFiles(root, "*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            problems.AddRange(ChemDataLoader.ReadItems<ProblemSpec>(file));
        }

        return problems;
    }

    /// <summary>加载指定类别的题目。</summary>
    public static IReadOnlyList<ProblemSpec> LoadCategory(ProblemCategory category, string? dataRoot = null) =>
        ChemDataLoader.ReadItems<ProblemSpec>(Path.Combine(ResolveDirectory(dataRoot), CategoryFiles[category]));

    private static string ResolveDirectory(string? dataRoot) =>
        Path.Combine(DataRoot.Resolve(dataRoot), RelativeDirectory);

    /// <summary>
    /// Schema 自检：不依赖引擎结果，只校验数据集自身的结构一致性。
    /// 返回问题描述列表（空 = 全部合格）。规则：
    /// <list type="bullet">
    /// <item>id 全局唯一且非空；problemText、source 非空。</item>
    /// <item>各类别必备字段：产物推断需 given.species + equations；成分推断与离子粉末推断需 candidateSpecies + steps + composition；
    /// 过程推断需 steps + substanceMap + equations；离子共存需 species + verdict；计算题需 numericFacts + calc + numericAnswer；
    /// 电化学需 cell + electrodes；化学平衡需 equilibrium.equation + expected.equilibrium；
    /// 水溶液离子平衡需 solution.solutes + expected.aqueous；工业流程需 steps +（purposes 或 substanceMap）；
    /// 有机推断需 candidateSpecies + steps + substanceMap + equations（tags 含"边界"的如实欠定样例豁免后两项）；
    /// 实验探究需 experiment.mode（tags 含"边界"的如实欠定样例豁免 conclusions）。</item>
    /// <item>干扰项：id 唯一、equation 非空；预期被捕获（expectValid=false）时必须给出 expectFailChecks。</item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<string> ValidateSchema(IEnumerable<ProblemSpec> problems)
    {
        var issues = new List<string>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var problem in problems)
        {
            var where = problem.Id.Length > 0 ? problem.Id : "(无编号)";

            if (problem.Id.Length == 0)
            {
                issues.Add("存在无编号题目");
            }
            else if (!seenIds.Add(problem.Id))
            {
                issues.Add($"编号重复：{problem.Id}");
            }

            if (problem.ProblemText.Length == 0)
            {
                issues.Add($"{where}：problemText 为空");
            }

            if (problem.Source.Length == 0)
            {
                issues.Add($"{where}：source 为空（规则库铁律：知识必须可溯源）");
            }

            switch (problem.Category)
            {
                case ProblemCategory.ProductInference:
                    if (problem.Given.Species.Count == 0)
                    {
                        issues.Add($"{where}：产物推断必须给出 given.species（反应物）");
                    }

                    if (problem.Expected.Equations.Count == 0)
                    {
                        issues.Add($"{where}：产物推断必须给出 expected.equations");
                    }

                    break;

                case ProblemCategory.SubstanceIdentification:
                case ProblemCategory.IonPowderIdentification:
                    if (problem.Given.CandidateSpecies.Count == 0)
                    {
                        issues.Add($"{where}：成分推断必须给出 given.candidateSpecies（可能物质列表）");
                    }

                    if (problem.Given.Steps.Count == 0)
                    {
                        issues.Add($"{where}：成分推断必须给出 given.steps（实验过程）");
                    }

                    if (problem.Expected.Composition is not { Count: > 0 })
                    {
                        issues.Add($"{where}：成分推断必须给出 expected.composition");
                    }

                    break;

                case ProblemCategory.ProcessInference:
                    if (problem.Given.Steps.Count == 0)
                    {
                        issues.Add($"{where}：过程推断必须给出 given.steps（转化步骤）");
                    }

                    if (problem.Expected.SubstanceMap is not { Count: > 0 })
                    {
                        issues.Add($"{where}：过程推断必须给出 expected.substanceMap");
                    }

                    if (problem.Expected.Equations.Count == 0)
                    {
                        issues.Add($"{where}：过程推断必须给出 expected.equations");
                    }

                    break;

                case ProblemCategory.IonCoexistence:
                    if (problem.Given.Species.Count == 0)
                    {
                        issues.Add($"{where}：离子共存必须给出 given.species（离子组）");
                    }

                    if (problem.Expected.Verdict is null)
                    {
                        issues.Add($"{where}：离子共存必须给出 expected.verdict");
                    }

                    break;

                case ProblemCategory.StoichiometryCalc:
                    if (problem.Given.NumericFacts.Count == 0)
                    {
                        issues.Add($"{where}：计算题必须给出 given.numericFacts");
                    }

                    if (problem.Expected.Calc is null)
                    {
                        issues.Add($"{where}：计算题必须给出 expected.calc（复算规格）");
                    }

                    if (problem.Expected.NumericAnswer is null)
                    {
                        issues.Add($"{where}：计算题必须给出 expected.numericAnswer");
                    }

                    break;

                case ProblemCategory.Electrochemistry:
                    if (problem.Given.Cell is not { } cell || cell.Electrodes.Count == 0)
                    {
                        issues.Add($"{where}：电化学必须给出 given.cell（装置类型与电极）");
                    }

                    if (problem.Expected.Electrodes is null)
                    {
                        issues.Add($"{where}：电化学必须给出 expected.electrodes（电极反应式）");
                    }

                    break;

                case ProblemCategory.ChemicalEquilibrium:
                    if (problem.Given.Equilibrium is not { } eq || eq.Equation.Length == 0)
                    {
                        issues.Add($"{where}：化学平衡必须给出 given.equilibrium.equation（可逆反应）");
                    }

                    if (problem.Expected.Equilibrium is null)
                    {
                        issues.Add($"{where}：化学平衡必须给出 expected.equilibrium（三段式与结论）");
                    }

                    break;

                case ProblemCategory.AqueousIonEquilibrium:
                    if (problem.Given.Solution is not { } sol || sol.Solutes.Count == 0)
                    {
                        issues.Add($"{where}：水溶液离子平衡必须给出 given.solution.solutes（溶质与用量）");
                    }

                    if (problem.Expected.Aqueous is not { } aq || (aq.Order.Count == 0 && aq.ConservationEquations.Count == 0))
                    {
                        issues.Add($"{where}：水溶液离子平衡必须给出 expected.aqueous（浓度顺序或守恒等式）");
                    }

                    break;

                case ProblemCategory.IndustrialProcess:
                    if (problem.Given.Steps.Count == 0)
                    {
                        issues.Add($"{where}：工业流程必须给出 given.steps（各步操作）");
                    }

                    if (problem.Expected.Purposes is not { Count: > 0 } && problem.Expected.SubstanceMap is not { Count: > 0 })
                    {
                        issues.Add($"{where}：工业流程必须给出 expected.purposes 或 expected.substanceMap");
                    }

                    break;

                case ProblemCategory.OrganicInference:
                    if (problem.Given.CandidateSpecies.Count == 0)
                    {
                        issues.Add($"{where}：有机推断必须给出 given.candidateSpecies（候选有机物列表）");
                    }

                    if (problem.Given.Steps.Count == 0)
                    {
                        issues.Add($"{where}：有机推断必须给出 given.steps（转化步骤）");
                    }

                    // 边界样例（tags 含"边界"，如实欠定）允许缺省 substanceMap/equations
                    if (!problem.Tags.Contains("边界"))
                    {
                        if (problem.Expected.SubstanceMap is not { Count: > 0 })
                        {
                            issues.Add($"{where}：有机推断必须给出 expected.substanceMap");
                        }

                        if (problem.Expected.Equations.Count == 0)
                        {
                            issues.Add($"{where}：有机推断必须给出 expected.equations");
                        }
                    }

                    break;

                case ProblemCategory.ExperimentalInquiry:
                    if (problem.Given.Experiment is not { } exp || exp.Mode.Length == 0)
                    {
                        issues.Add($"{where}：实验探究必须给出 given.experiment.mode（探究模式）");
                    }

                    // 边界样例（tags 含"边界"，如实欠定）允许缺省 conclusions
                    if (!problem.Tags.Contains("边界") && problem.Expected.Experiment is not { Conclusions.Count: > 0 })
                    {
                        issues.Add($"{where}：实验探究必须给出 expected.experiment.conclusions（实验结论）");
                    }

                    break;

                default:
                    issues.Add($"{where}：未知类别 {problem.Category}");
                    break;
            }

            var seenDistractorIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var distractor in problem.Distractors)
            {
                var dWhere = distractor.Id.Length > 0 ? distractor.Id : $"{where}-D?";
                if (distractor.Id.Length == 0 || !seenDistractorIds.Add(distractor.Id))
                {
                    issues.Add($"{where}：干扰项编号缺失或重复：{dWhere}");
                }

                if (distractor.Equation.Length == 0)
                {
                    issues.Add($"{dWhere}：equation 为空");
                }

                if (!distractor.ExpectValid && distractor.ExpectFailChecks.Count == 0)
                {
                    issues.Add($"{dWhere}：预期被捕获的干扰项必须给出 expectFailChecks");
                }
            }
        }

        return issues;
    }
}
