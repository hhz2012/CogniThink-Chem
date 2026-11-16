using CogniThink.Chem.Core.Benchmark;
using CogniThink.Chem.Core.Data;
using CogniThink.Chem.Core.Inference;
using CogniThink.Chem.Core.Rules;
using CogniThink.Chem.Core.Solving;
using Xunit;

namespace CogniThink.Chem.Tests;

/// <summary>
/// 实验探究引擎测试（阶段3）：
/// ① 全部实验探究题经求解器推导的结论与 expected.experiment.conclusions 逐字一致（知识驱动、非硬编码）；
/// ② 推理步骤全部溯源到规则条目（EXP-*）或特征现象索引条目（PH-*），KnowledgeId 在知识库中；
/// ③ 现象多义（EXP-H08）如实欠定，不猜；
/// ④ 未知模式/无匹配条目 → Underdetermined。
/// </summary>
public class ExperimentEngineTests
{
    private static IReadOnlyList<ProblemSpec> Problems =>
        BenchmarkLoader.LoadCategory(ProblemCategory.ExperimentalInquiry);

    [Fact]
    public void Engine_SolvesAllNonBoundaryProblems_WithExpectedConclusions()
    {
        foreach (var problem in Problems)
        {
            var boundary = problem.Tags.Contains("边界");
            var result = ExperimentEngine.Infer(new ExperimentInferenceRequest
            {
                Mode = problem.Given.Experiment!.Mode,
                Targets = problem.Given.Experiment.Targets.ToList(),
                Operations = problem.Given.Experiment.Operations.ToList(),
                Phenomena = problem.Given.Experiment.Phenomena.ToList(),
                CandidateSpecies = problem.Given.Experiment.CandidateSpecies.ToList(),
            });

            var caseId = problem.Id;
            if (boundary)
            {
                Assert.Equal(InferenceStatus.Underdetermined, result.Status);
                Assert.Empty(result.Conclusions);
                Assert.False(string.IsNullOrWhiteSpace(result.Message));
                continue;
            }

            Assert.Equal(InferenceStatus.Solved, result.Status);
            Assert.Equal(problem.Expected.Experiment!.Conclusions, result.Conclusions);
        }
    }

    [Fact]
    public void Engine_Steps_TraceableToRuleOrPhenomenonEntries()
    {
        var ruleIds = ChemDataLoader.LoadRules(DataRoot.Resolve())
            .Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        var phenomenonIds = RuleStore.Load().Query<PhenomenaData>("phenomena").Items
            .Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var knowledgeIds = ChemDataLoader.LoadKnowledge(DataRoot.Resolve())
            .Search().Select(k => k.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var problem in Problems)
        {
            var result = ExperimentEngine.Infer(new ExperimentInferenceRequest
            {
                Mode = problem.Given.Experiment!.Mode,
                Targets = problem.Given.Experiment.Targets.ToList(),
                Operations = problem.Given.Experiment.Operations.ToList(),
                Phenomena = problem.Given.Experiment.Phenomena.ToList(),
                CandidateSpecies = problem.Given.Experiment.CandidateSpecies.ToList(),
            });

            var caseId = problem.Id;
            Assert.NotEmpty(result.Steps);
            Assert.All(result.Steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Description)));

            var evidenceSteps = result.Steps.Where(s => s.Evidence.Length > 0).ToList();
            if (problem.Tags.Contains("边界"))
            {
                // 边界题也必须给出匹配到的条目步（多义说明来自条目证据，而非凭空断言）
                Assert.NotEmpty(evidenceSteps);
            }

            Assert.All(evidenceSteps, step =>
            {
                Assert.True(step.Evidence.StartsWith("EXP-", StringComparison.Ordinal) ||
                            step.Evidence.StartsWith("PH-", StringComparison.Ordinal),
                    $"{caseId}: 证据应为实验规则或特征现象条目 id，不得携带答案原文");
                Assert.True(ruleIds.Contains(step.Evidence) || phenomenonIds.Contains(step.Evidence),
                    $"{caseId}: 条目 {step.Evidence} 不在规则库/现象索引中");
                Assert.False(string.IsNullOrWhiteSpace(step.KnowledgeId));
                Assert.True(knowledgeIds.Contains(step.KnowledgeId),
                    $"{caseId}: 知识点 {step.KnowledgeId} 不在知识库中");
                Assert.False(string.IsNullOrWhiteSpace(step.Source));
            });
        }
    }

    [Fact]
    public void Engine_RepresentativeProblems_HaveMultiStepChains()
    {
        // 代表题：逐对象一步 + 首末流程步，步骤链完整可读。
        foreach (var id in new[] { "EXP-H01", "EXP-H05" })
        {
            var problem = Problems.Single(p => p.Id == id);
            var result = ExperimentEngine.Infer(new ExperimentInferenceRequest
            {
                Mode = problem.Given.Experiment!.Mode,
                Targets = problem.Given.Experiment.Targets.ToList(),
                Operations = problem.Given.Experiment.Operations.ToList(),
                Phenomena = problem.Given.Experiment.Phenomena.ToList(),
                CandidateSpecies = problem.Given.Experiment.CandidateSpecies.ToList(),
            });

            Assert.Equal(InferenceStatus.Solved, result.Status);
            Assert.True(result.Steps.Count >= 4, $"{id}: 步骤链应至少含模式说明、逐对象步与汇总步");
            Assert.True(result.Steps.First().Source == "ExperimentEngine" ||
                        result.Steps.First().Evidence.Length > 0);
        }
    }

    [Fact]
    public void Engine_AmbiguousPhenomenon_StaysUnderdetermined()
    {
        // EXP-H08：石灰水变浑浊为 CO2/SO2 共有现象（PH-006），候选多义 → 如实欠定，不猜。
        var result = ExperimentEngine.Infer(new ExperimentInferenceRequest
        {
            Mode = "phenomenon_conclusion",
            Phenomena = new List<string> { "通入澄清石灰水变浑浊" },
            CandidateSpecies = new List<string> { "CO2", "SO2" },
        });

        Assert.Equal(InferenceStatus.Underdetermined, result.Status);
        Assert.Empty(result.Conclusions);
        Assert.Contains("CO2", result.Message);
        Assert.Contains("SO2", result.Message);
    }

    [Fact]
    public void Engine_UnknownModeOrNoMatch_IsUnderdetermined()
    {
        var unknownMode = ExperimentEngine.Infer(new ExperimentInferenceRequest { Mode = "calorimetry" });
        Assert.Equal(InferenceStatus.Underdetermined, unknownMode.Status);
        Assert.False(string.IsNullOrWhiteSpace(unknownMode.Message));

        // 知识库未收录的气体（如 NO）无法唯一匹配检验条目 → 如实欠定。
        var noMatch = ExperimentEngine.Infer(new ExperimentInferenceRequest
        {
            Mode = "gas_test",
            Targets = new List<string> { "NO" },
        });
        Assert.Equal(InferenceStatus.Underdetermined, noMatch.Status);
        Assert.Empty(noMatch.Conclusions);

        // 空询问清单 → 欠定。
        var empty = ExperimentEngine.Infer(new ExperimentInferenceRequest { Mode = "operation_purpose" });
        Assert.Equal(InferenceStatus.Underdetermined, empty.Status);
    }

    [Fact]
    public void Solver_RoutesExperimentalInquiry_AndReportsMissingGiven()
    {
        var solved = ProblemSolver.Solve(Problems.Single(p => p.Id == "EXP-H04"));
        Assert.Equal(InferenceStatus.Solved, solved.Status);
        Assert.NotNull(solved.Experiment);
        Assert.Equal(2, solved.Experiment!.Conclusions.Count);
        Assert.NotEmpty(solved.Steps);

        // 缺少 given.experiment → 如实 NoSolution，不猜。
        var missing = new ProblemSpec
        {
            Id = "EXP-TMP",
            Category = ProblemCategory.ExperimentalInquiry,
            Difficulty = Difficulty.Hard,
            ProblemText = "缺少探究条件的实验题。",
            Question = new QuestionSpec { Ask = AskType.AnalyzeExperiment },
            Source = "测试",
        };

        var noGiven = ProblemSolver.Solve(missing);
        Assert.Equal(InferenceStatus.NoSolution, noGiven.Status);
        Assert.Null(noGiven.Experiment);
        Assert.Contains("given.experiment", noGiven.Message);
    }
}
