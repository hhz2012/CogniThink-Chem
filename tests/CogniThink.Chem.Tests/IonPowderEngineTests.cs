using CogniThink.Chem.Core.Benchmark;
using CogniThink.Chem.Core.Data;
using CogniThink.Chem.Core.Inference;
using Xunit;

namespace CogniThink.Chem.Tests;

// ===========================================================================
// 离子粉末推断引擎（复用 IdentificationEngine 混合成分模式）：
// 4 道溶液连续离子推断 + 4 道固体粉末混合物分步现象推断，
// 断言三态结论（一定存在/一定不存在/不能确定）、方程式全部落到知识库条目、
// 焰色反应与沉淀酸溶分类能力，以及全负证据边界题的如实欠定。
// ===========================================================================

public class IonPowderEngineTests
{
    private static readonly IReadOnlyList<ProblemSpec> Problems =
        BenchmarkLoader.LoadCategory(ProblemCategory.IonPowderIdentification);

    public static IEnumerable<object[]> ProblemCases() => Problems.Select(p => new object[] { p.Id });

    private static ProblemSpec ById(string id) => Problems.Single(p => p.Id == id);

    private static IdentificationResult Run(ProblemSpec problem) =>
        IdentificationEngine.Identify(new IdentificationRequest
        {
            CandidateSpecies = problem.Given.CandidateSpecies,
            SingleEntity = problem.Given.SingleEntity,
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

    /// <summary>八题逐候选比对三态结论，方程式与 expected 集合一致（题面即基准，实证回填）。</summary>
    [Theory]
    [MemberData(nameof(ProblemCases))]
    public void 离子粉末推断_三态结论与标准答案一致(string caseId)
    {
        var problem = ById(caseId);
        var result = Run(problem);
        var boundary = problem.Tags.Contains("边界");
        Assert.Equal(boundary ? InferenceStatus.Underdetermined : InferenceStatus.Solved, result.Status);

        Assert.NotNull(problem.Expected.Composition);
        Assert.Equal(problem.Given.CandidateSpecies.Count, result.Composition.Count);
        foreach (var kv in problem.Expected.Composition!)
        {
            Assert.True(result.Composition.TryGetValue(kv.Key, out var actual),
                $"{caseId}: 候选 {kv.Key} 缺少判定");
            Assert.Equal(kv.Value.ToString(), actual.ToString());
        }

        Assert.Equal(
            problem.Expected.Equations.OrderBy(x => x, StringComparer.Ordinal),
            result.Equations.Select(e => e.Equation).OrderBy(x => x, StringComparer.Ordinal));
    }

    /// <summary>每题推理链完整：步骤非空、叙述非空；凡给出方程式证据的步骤必须带可溯源知识点。</summary>
    [Theory]
    [MemberData(nameof(ProblemCases))]
    public void 离子粉末推断_推理步骤可溯源(string caseId)
    {
        var result = Run(ById(caseId));
        Assert.NotEmpty(result.Steps);
        Assert.All(result.Steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Description)));

        var knowledgeIds = ChemDataLoader.LoadKnowledge(DataRoot.Resolve())
            .Search().Select(k => k.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var step in result.Steps.Where(s => s.Evidence.Length > 0))
        {
            Assert.False(string.IsNullOrWhiteSpace(step.KnowledgeId),
                $"{caseId}: 证据「{step.Evidence}」缺少知识点归属");
            Assert.True(knowledgeIds.Contains(step.KnowledgeId),
                $"{caseId}: 知识点 {step.KnowledgeId} 不在知识库中");
            Assert.False(string.IsNullOrWhiteSpace(step.Source));
        }
    }

    /// <summary>焰色试验（透过蓝色钴玻璃的紫色火焰）直接把 K^+ 判为一定存在。</summary>
    [Fact]
    public void 离子粉末推断_焰色反应判定钾离子一定存在()
    {
        Assert.Equal(CompositionVerdict.Must, Run(ById("IONP-H01")).Composition["K^+"]);
        Assert.Equal(CompositionVerdict.Must, Run(ById("IONP-H02")).Composition["K^+"]);
        Assert.Equal(CompositionVerdict.Must, Run(ById("IONP-H07")).Composition["KNO3"]);
    }

    /// <summary>沉淀酸溶分类：部分溶→碳酸盐与硫酸盐并存；全部溶→仅碳酸盐沉淀（硫酸钠转入负证据排除）。</summary>
    [Fact]
    public void 离子粉末推断_沉淀酸溶分类判定粉末成分()
    {
        var partial = Run(ById("IONP-H05"));
        Assert.Equal(InferenceStatus.Solved, partial.Status);
        Assert.Equal(CompositionVerdict.Must, partial.Composition["K2CO3"]);
        Assert.Equal(CompositionVerdict.Must, partial.Composition["Na2SO4"]);
        Assert.Equal(CompositionVerdict.Must, partial.Composition["BaCl2"]);

        var full = Run(ById("IONP-H06"));
        Assert.Equal(CompositionVerdict.Must, full.Composition["CaCl2"]);
        Assert.Equal(CompositionVerdict.Must, full.Composition["Na2CO3"]);
        Assert.Equal(CompositionVerdict.MustNot, full.Composition["Na2SO4"]);
        Assert.Contains(full.Equations.Select(e => e.Equation),
            eq => eq == "CaCO3 + 2H^+ = Ca^2+ + H2O + CO2↑");
    }

    /// <summary>必要性判定：可解释现象但非必需的候选降为"不能确定"（可能存在），不得虚判为一定存在。</summary>
    [Fact]
    public void 离子粉末推断_非必要正证据降级为可能存在()
    {
        // CO3^2- 与 SO4^2- 都能解释"不溶于稀硝酸的白色沉淀"（酸存在时碳酸钡不可信），仅 SO4^2- 必需。
        Assert.Equal(CompositionVerdict.Maybe, Run(ById("IONP-H01")).Composition["CO3^2-"]);
        // Cl^- 已由确定存在的 BaCl2 提供，NaCl 无法单独确认。
        Assert.Equal(CompositionVerdict.Maybe, Run(ById("IONP-H08")).Composition["NaCl"]);
        // K^+ 已由确定存在的 K2CO3 提供，KNO3 无法单独确认。
        Assert.Equal(CompositionVerdict.Maybe, Run(ById("IONP-H05")).Composition["KNO3"]);
    }

    /// <summary>边界题：全部为负证据时只能排除、不能确定任何成分，引擎必须如实欠定而非臆造阳离子结论。</summary>
    [Fact]
    public void 离子粉末推断_全负证据边界题如实欠定()
    {
        var result = Run(ById("IONP-H04"));
        Assert.Equal(InferenceStatus.Underdetermined, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
        Assert.Empty(result.Equations);
        Assert.Equal(CompositionVerdict.MustNot, result.Composition["CO3^2-"]);
        Assert.Equal(CompositionVerdict.MustNot, result.Composition["SO4^2-"]);
        Assert.Equal(CompositionVerdict.MustNot, result.Composition["Cl^-"]);
        Assert.Equal(CompositionVerdict.Maybe, result.Composition["Na^+"]);
        Assert.Equal(CompositionVerdict.Maybe, result.Composition["K^+"]);
        Assert.Equal(CompositionVerdict.Maybe, result.Composition["NO3^-"]);
    }

    /// <summary>足量试剂消耗跟踪：被前步足量酸消耗的 CO3^2- 不得在后步负证据中重复引发误判。</summary>
    [Fact]
    public void 离子粉末推断_足量酸消耗后负证据不误伤()
    {
        var result = Run(ById("IONP-H02"));
        Assert.Equal(InferenceStatus.Solved, result.Status);
        // 第 1 步足量稀硝酸已消耗 CO3^2- 并判其存在；第 2 步"无明显沉淀"只排除 SO4^2-。
        Assert.Equal(CompositionVerdict.Must, result.Composition["CO3^2-"]);
        Assert.Equal(CompositionVerdict.MustNot, result.Composition["SO4^2-"]);
    }
}
