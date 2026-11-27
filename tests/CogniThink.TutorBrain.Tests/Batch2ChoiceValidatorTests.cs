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
        ["pdf-312"] = "AC",
        // engine-capability-gap-fix T1：实验安全(正确项=B)+沉淀法定量测NH4Cl热分解率(错项=C，WrongDir)。
        // 事实写入 experiment-facts（EF-545~EF-546）；探针 TOTAL PROBLEMS=0，
        // 4 选项各命中唯一事实，verdict 与官方 answer 一致，与 Organic/Structure 无路由二义。计数 23→25。
        ["pdf-147"] = "B",
        ["pdf-155"] = "C",
        // engine-ext-experiment-operation：粗盐精制"一系列操作"结晶方法选择（EF-416，单选 C）
        ["pdf-416"] = "C",
        // data-repair-engine-exp-batch1：铁与水蒸气实验"说法错误的是"（EF-100，WrongDir 选错=B）；
        // 气体发生收集装置"不能完成的是"（EF-406，WrongDir 新增「不能完成」token，选错=C）。
        // 探针 blast-radius 零 stray-hit，回归仅该两题从空结论翻正，与 Organic/Structure 无路由二义。
        ["pdf-100"] = "B",
        ["pdf-406"] = "C",
        // data-repair-engine-exp-batch2：2024 河北卷"实验操作及现象能得出相应结论的是"（EF-405，选正确=C）。
        // 官方真题结论列经还原：A"H2O具有还原性"错、B"含SO4^2-"错（Ag+/CO3^2-/SO3^2-干扰）、
        // C"碱性Mg(OH)2>Al(OH)3"对、D"增大生成物浓度使平衡逆向移动"错（实为OH-耗H+正向移动）。
        // stemMatch「实验操作及现象能得出」全库唯一，blast-radius 零 stray-hit，回归仅 pdf-405 从空结论翻正。
        ["pdf-405"] = "C",
        // data-repair-engine-exp-batch3：2024 安徽卷羟胺"物质结构或性质的比较，正确的是"（EF-197，选正确=D）。
        // 原题在 pdf_questions.json 中被粘入另一道(海洋资源/N2H4)题干+note、键为垃圾值 A,B,C,D；
        // 清理题干→避免 WrongDir 把 askwrong 误判为 true、修键为 D、回填官方解析(答案册 page_073 num=5)。
        // stemMatch「物质结构或性质的比较」全库唯一，blast-radius 零 stray-hit，回归仅 pdf-197 从空结论翻正。
        ["pdf-197"] = "D"
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
