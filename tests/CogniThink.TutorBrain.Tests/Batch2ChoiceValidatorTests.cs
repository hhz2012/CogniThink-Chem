using System.Collections.Generic;
using System.Linq;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// batch2 choice 子集测试：直接调用 ExperimentChoiceValidator.TryDecide 断言。
/// 真值以 llmReference 反推 + 化学核对得到，集中在 ExperimentTruth 字典里（单一事实源）。
/// 红线：零假阳性——校验器对“未列入真值表”的题返回非 null 即判失败；
/// 且不得与 OrganicChoiceValidator / StructurePeriodicValidator 在同一题上同时断言（路由无二义）。
/// 扫描范围：GatewayVsLlmEvaluationService.BuildTestCases() 全题库（与生产完全一致）。
/// </summary>
public class Batch2ChoiceValidatorTests
{
    private static readonly List<QaTestCase> AllCases = GatewayVsLlmEvaluationService.BuildTestCases();

    public static readonly IReadOnlyDictionary<string, string> ExperimentTruth = new Dictionary<string, string>
    {
        ["pdf-019"] = "D",
        ["pdf-230"] = "C",
        ["pdf-314"] = "B",
        ["pdf-315"] = "D",
        ["pdf-318"] = "C",
        ["pdf-395"] = "C",
        ["pdf-460"] = "A",
        ["pdf-492"] = "B",
        // pdf-544：题干「不规范的是」（WrongDir 选非），Experiment 校验器选 verdict==false 的 A（湿润试纸不规范）
        ["pdf-544"] = "A",
        ["pdf-094"] = "D",
        ["pdf-455"] = "D",

        ["pdf-298"] = "C",
        ["pdf-308"] = "D",
        ["pdf-309"] = "C",
        ["pdf-311"] = "D",
        ["pdf-149"] = "A",
        ["pdf-404"] = "A",
        ["pdf-529"] = "C",
        ["pdf-063"] = "C",
        ["pdf-064"] = "B",
        ["pdf-408"] = "B",

        // 双选：题干「错误的是」，Experiment 校验器按 wrong 方向选 verdict==false 的选项
        ["pdf-071"] = "BD",
        ["pdf-312"] = "AC"
    };

    public static IEnumerable<object[]> ExperimentCases =>
        ExperimentTruth.Select(kv => new object[] { kv.Key, kv.Value });

    [Theory]
    [MemberData(nameof(ExperimentCases))]
    public void Experiment_Solves_KnownQuestion(string id, string expected)
    {
        var r = ExperimentChoiceValidator.TryDecide(Req(id));
        Assert.NotNull(r);
        Assert.Equal(expected, r!.AnswerLetter);
    }

    [Fact]
    public void Experiment_ZeroFalsePositive_AcrossAllBuildTestCases()
    {
        foreach (var c in AllCases)
        {
            var r = ExperimentChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });
            if (r is null) continue;
            Assert.True(ExperimentTruth.TryGetValue(c.Id, out var exp),
                $"[零假阳性] ExperimentChoiceValidator 对未列入真值表的题 {c.Id} 返回 {r.AnswerLetter}");
            Assert.Equal(exp, r.AnswerLetter);
        }
    }

    [Fact]
    public void Experiment_Coverage_MatchesTruthTable()
    {
        var solved = AllCases.Count(c => ExperimentChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question }) != null);
        Assert.Equal(ExperimentTruth.Count, solved);
    }

    [Fact]
    public void Experiment_AdoptedAnswers_AreGradedCorrect_AcrossAllBuildTestCases()
    {
        // 红线：凡 ExperimentChoiceValidator 给出结论（必含选项字母 → 被采纳门采纳），
        // 其结论必须被真实判分 IsAnswerCorrect 判为正确，否则即为 harm（采纳了错误答案）。
        foreach (var c in AllCases)
        {
            var r = ExperimentChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });
            if (r is null) continue;
            Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c),
                $"[harm] {c.Id} 结论 [{r.FinalResult}] 未被判为正确（type={c.Type}, answer=[{c.Answer}]）");
        }
    }

    [Fact]
    public void Experiment_NoOverlap_WithOrganicAndStructure()
    {
        foreach (var c in AllCases)
        {
            var e = ExperimentChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });
            if (e is null) continue;
            var o = OrganicChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });
            var s = StructurePeriodicValidator.TryDecide(new EngineRequest { RawText = c.Question });
            Assert.False(o != null,
                $"[路由二义] Experiment 与 Organic 同时断言 {c.Id}: exp={e.AnswerLetter}, org={o?.AnswerLetter}");
            Assert.False(s != null,
                $"[路由二义] Experiment 与 Structure 同时断言 {c.Id}: exp={e.AnswerLetter}, str={s?.AnswerLetter}");
        }
    }

    private static EngineRequest Req(string id)
    {
        var c = AllCases.FirstOrDefault(x => x.Id == id);
        Assert.NotNull(c);
        return new EngineRequest { RawText = c!.Question };
    }
}
