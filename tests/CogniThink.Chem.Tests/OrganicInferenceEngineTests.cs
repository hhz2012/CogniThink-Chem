using CogniThink.Chem.Core.Benchmark;
using CogniThink.Chem.Core.Data;
using CogniThink.Chem.Core.Inference;
using CogniThink.Chem.Core.Reactions;
using Xunit;

namespace CogniThink.Chem.Tests;

// ===========================================================================
// 有机推断引擎（OrganicInferenceEngine）：
// 8 道高考有机转化链推断（烯烃/醇/醛/酸/酯/苯/卤代烃/甲烷连锁取代），
// 断言字母标签赋值与标准答案一致、每步落到 RX-OR-* 知识条目、
// 推理步骤可溯源且不泄露方程式，以及双解边界题如实欠定。
// ===========================================================================

public class OrganicInferenceEngineTests
{
    private static readonly IReadOnlyList<ProblemSpec> Problems =
        BenchmarkLoader.LoadCategory(ProblemCategory.OrganicInference);

    public static IEnumerable<object[]> ProblemCases() => Problems.Select(p => new object[] { p.Id });

    private static ProblemSpec ById(string id) => Problems.Single(p => p.Id == id);

    private static OrganicInferenceResult Run(ProblemSpec problem) =>
        OrganicInferenceEngine.Infer(new OrganicInferenceRequest
        {
            CandidateSpecies = problem.Given.CandidateSpecies,
            Medium = problem.Given.Medium ?? "",
            Steps = problem.Given.Steps.Select(s => new ExperimentStep
            {
                Step = s.Step,
                From = s.From,
                To = s.To,
                Reagents = s.Reagents,
                Conditions = s.Conditions,
                Phenomenon = s.Phenomenon,
            }).ToList(),
        });

    /// <summary>八题逐标签比对赋值、逐字比对各步方程式（题面即基准，探针实证回填）。</summary>
    [Theory]
    [MemberData(nameof(ProblemCases))]
    public void 有机推断_结论与标准答案一致(string caseId)
    {
        var problem = ById(caseId);
        var result = Run(problem);
        var boundary = problem.Tags.Contains("边界");
        Assert.Equal(boundary ? InferenceStatus.Underdetermined : InferenceStatus.Solved, result.Status);
        if (boundary)
        {
            return;
        }

        Assert.NotNull(problem.Expected.SubstanceMap);
        Assert.Equal(problem.Expected.SubstanceMap!, result.Assignment);
        Assert.Equal(
            problem.Expected.Equations,
            result.StepResults.OrderBy(r => r.Step).Select(r => r.Equation));
    }

    /// <summary>推理链完整：叙述非空；凡带证据的步骤，证据必须是存在的 RX-OR-* 条目 id（不含方程式），
    /// 且带可溯源知识点与教材出处。</summary>
    [Theory]
    [MemberData(nameof(ProblemCases))]
    public void 有机推断_推理步骤可溯源且不泄露方程式(string caseId)
    {
        var result = Run(ById(caseId));
        Assert.NotEmpty(result.Steps);
        Assert.All(result.Steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Description)));

        var entryIds = ReactionLibrary.Default.Value.All.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var knowledgeIds = ChemDataLoader.LoadKnowledge(DataRoot.Resolve())
            .Search().Select(k => k.Id).ToHashSet(StringComparer.Ordinal);
        var evidenceSteps = result.Steps.Where(s => s.Evidence.Length > 0).ToList();
        Assert.NotEmpty(evidenceSteps);
        foreach (var step in evidenceSteps)
        {
            Assert.True(step.Evidence.StartsWith("RX-OR-", StringComparison.Ordinal),
                $"{caseId}: 证据应为有机知识条目 id，不得携带方程式原文");
            Assert.True(entryIds.Contains(step.Evidence), $"{caseId}: 条目 {step.Evidence} 不在反应库中");
            Assert.False(string.IsNullOrWhiteSpace(step.KnowledgeId));
            Assert.True(knowledgeIds.Contains(step.KnowledgeId),
                $"{caseId}: 知识点 {step.KnowledgeId} 不在知识库中");
            Assert.False(string.IsNullOrWhiteSpace(step.Source));
        }
    }

    /// <summary>ORG-H01（乙烯—乙醇—乙醛—乙酸—酯四步链）：每步命中唯一 RX-OR 条目，说明文本含条目名。</summary>
    [Fact]
    public void 有机推断_转化链每步命中知识条目()
    {
        var result = Run(ById("ORG-H01"));
        Assert.Equal(InferenceStatus.Solved, result.Status);
        Assert.Equal(4, result.StepResults.Count);
        Assert.All(result.StepResults, r =>
        {
            Assert.NotNull(r.Reaction);
            Assert.StartsWith("RX-OR-", r.Reaction!.Id);
            Assert.False(string.IsNullOrWhiteSpace(r.Equation));
            Assert.Contains(r.Reaction.Name, r.Explanation);
        });
    }

    /// <summary>ORG-H08 边界题：乙烯与乙炔均能解释"溴水褪色 + 催化加氢"，双解必须如实欠定，不得臆造唯一答案。</summary>
    [Fact]
    public void 有机推断_双解边界题如实欠定()
    {
        var result = Run(ById("ORG-H08"));
        Assert.Equal(InferenceStatus.Underdetermined, result.Status);
        Assert.Contains("至少 2 组", result.Message);
    }

    /// <summary>候选预检：出现课内白名单之外的候选物种时，引擎拒答（NoSolution）而非带病推断。</summary>
    [Fact]
    public void 有机推断_超纲候选拒答()
    {
        var result = OrganicInferenceEngine.Infer(new OrganicInferenceRequest
        {
            CandidateSpecies = ["Xy2O3", "C2H4"],
            Steps =
            [
                new ExperimentStep { Step = 1, From = "A", To = "B", Reagents = ["Br2"] },
            ],
        });
        Assert.Equal(InferenceStatus.NoSolution, result.Status);
        Assert.Contains("白名单", result.Message);
    }

    /// <summary>现象约束：题给现象"溴水褪色"指示不饱和烃，饱和烃赋值必须被剪除。</summary>
    [Fact]
    public void 有机推断_现象约束剪除饱和烃()
    {
        var result = OrganicInferenceEngine.Infer(new OrganicInferenceRequest
        {
            CandidateSpecies = ["CH4", "C2H4", "C2H6", "C2H4Br2"],
            Steps =
            [
                new ExperimentStep
                {
                    Step = 1, From = "A", To = "B", Reagents = ["Br2"], Phenomenon = "溴水褪色",
                },
            ],
        });
        Assert.Equal(InferenceStatus.Solved, result.Status);
        Assert.Equal("C2H4", result.Assignment["A"]);
        Assert.Equal("C2H4Br2", result.Assignment["B"]);
    }
}
