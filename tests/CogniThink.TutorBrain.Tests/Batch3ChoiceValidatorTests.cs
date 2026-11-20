using System.Collections.Generic;
using System.Linq;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// batch3 choice 子集测试：直接调用 ConceptChoiceValidator.TryDecide 断言。
/// 真值以题库 answer + 化学核对得到，集中在 ConceptTruth 字典里（单一事实源）。
/// 红线：零假阳性——校验器对“未列入真值表”的题返回非 null 即判失败；
/// 且不得与 Experiment/Organic/Structure 校验器在同一题上同时断言（路由无二义）；
/// 凡给出结论（含字母→被采纳门采纳）必被真实 IsAnswerCorrect 判为正确（零 harm）。
/// 扫描范围：GatewayVsLlmEvaluationService.BuildTestCases() 全题库（与生产完全一致）。
/// </summary>
public class Batch3ChoiceValidatorTests
{
    private static readonly List<QaTestCase> AllCases = GatewayVsLlmEvaluationService.BuildTestCases();

    public static readonly IReadOnlyDictionary<string, string> ConceptTruth = new Dictionary<string, string>
    {
        ["pdf-004"] = "B",
        ["pdf-118"] = "A",
        ["pdf-245"] = "C",
        ["pdf-372"] = "A",
        ["pdf-396"] = "B",
        ["pdf-442"] = "D",
        ["pdf-451"] = "C",
        ["pdf-493"] = "B",
        ["pdf-496"] = "D",
        ["pdf-497"] = "C",

        // batch3 扩展（第二批）：逐题化学核对 + 题库 answer 一致，零冲突
        ["pdf-034"] = "C",
        ["pdf-052"] = "B",
        ["pdf-086"] = "B",
        ["pdf-102"] = "B",
        ["pdf-129"] = "B",
        ["pdf-343"] = "B",
        ["pdf-407"] = "A",
        ["pdf-411"] = "D",
        ["pdf-440"] = "C",
        ["pdf-446"] = "B",
        ["pdf-503"] = "B",

        // batch3 扩展（第三批）：逐题化学核对 + 题库 answer 一致，零冲突
        // 6 道离子方程式正误判断（IonicEquationValidator 当前无解，由 Concept 事实库兜底判定）
        ["pdf-039"] = "A",
        ["pdf-040"] = "B",
        ["pdf-043"] = "B",
        ["pdf-047"] = "B",
        ["pdf-048"] = "C",
        ["pdf-050"] = "C",
        ["pdf-056"] = "B",
        // 4 道定性判断（化工流程 / 实验结论 / 化学用语）
        ["pdf-157"] = "C",
        ["pdf-339"] = "C",
        ["pdf-435"] = "B",

        // batch3 扩展（第四批）：氧化还原 / 热力学 / 离子平衡 / 化学用语 / 化学与生活定性题，逐题化学核对 + 题库 answer 一致，零冲突。
        // 净新增解出 7 道；pdf-110 经核对在加入事实前已被链路中非 Concept 校验器解出（ChoiceVerifier），Concept 事实冗余，故不纳入本批。
        ["pdf-081"] = "B",
        ["pdf-253"] = "A",
        ["pdf-282"] = "D",
        ["pdf-428"] = "B",
        ["pdf-429"] = "A",
        ["pdf-439"] = "C",
        ["pdf-445"] = "A",

        // batch3 扩展（第五批 / Task 35）：定性题，逐题化学核对 + 题库 answer 一致，零冲突。
        // 依赖 WrongDir 扩展「不属于 / 不合理」后 ask 方向判正（pdf-066 选非、pdf-453 选不合理）。
        ["pdf-066"] = "C",
        ["pdf-109"] = "A",
        ["pdf-285"] = "A",
        ["pdf-334"] = "C",
        ["pdf-402"] = "C",
        ["pdf-453"] = "B",

        // batch3 扩展（第六批 / Task 35）：实验现象 / 超分子 / 冠醚 / 精油结构定性题，逐题化学核对 + 题库 answer 一致，零冲突。
        ["pdf-401"] = "B",
        ["pdf-224"] = "D",
        ["pdf-342"] = "C",
        ["pdf-345"] = "B",

        // batch3 扩展（第七批 / Task 35）：元素推断题，逐题化学核对 + 题库 answer 一致，零冲突。
        ["pdf-229"] = "D",
        ["pdf-231"] = "B",
        ["pdf-234"] = "A",
        ["pdf-235"] = "A",
        ["pdf-236"] = "B",

        // batch3 扩展（第八批 / Task 35）：离子共存 / 实验结论 / 化工流程 / 核反应元素推断 / 分子结构定性题，逐题化学核对 + 题库 answer 一致，零冲突。
        ["pdf-060"] = "D",
        ["pdf-076"] = "C",
        ["pdf-112"] = "C",
        ["pdf-182"] = "A",
        ["pdf-457"] = "D",
        ["pdf-504"] = "C",

        // batch3 扩展（第九批 / Task 35 续）：先修正题库 answer 与化学真值冲突的 7 道，再策展进 Concept。ask=correct 取 verdict==true，ask=wrong 取 verdict==false，逐题独立核对化学真值。
        ["pdf-083"] = "D",
        ["pdf-092"] = "B",
        ["pdf-193"] = "C",
        ["pdf-194"] = "C",
        ["pdf-365"] = "A",
        ["pdf-368"] = "A",
        ["pdf-454"] = "B",

        // batch3 扩展（第十批 / Task 36）：全量审计后策展红线内可安全解的 10 道（含 3 道 answer 冲突修正：pdf-017/045/085）。
        ["pdf-017"] = "D",
        ["pdf-041"] = "A",
        ["pdf-042"] = "B",
        ["pdf-045"] = "C",
        ["pdf-049"] = "D",
        ["pdf-085"] = "D",
        ["pdf-110"] = "C",
        ["pdf-293"] = "C",
        ["pdf-296"] = "C",
        ["pdf-476"] = "D",

        // batch3 扩展（第十一批 / Task 36 续）：全量审计剩余候选中纯文本、定性、note 与 answer 一致的 3 道。
        ["pdf-073"] = "C",
        ["pdf-267"] = "C",
        ["pdf-283"] = "B",

        // batch3 扩展（第十二批 / Task 36 续）：pdf-067 题干已给配平方程式，4 选项 verdict 由化学唯一确定（note 写「C正确」有误，以 answer=C 与化学核对为准）。
        ["pdf-067"] = "C",

        // batch3 扩展（第十三批 / Task 37）：3 道答案矛盾题经官方题源核对后修正并策展。
        // pdf-116（2025 黑吉辽蒙）：答案 A 正确，但选项「解释」方程式列在 OCR 抽取时丢失，已补全；A 的解释方程式配平错误（生成 Cu2O 应为 Cu）。
        // pdf-305（2025 新课标）：存储 answer=A 错误，官方答案为 D（1 mol NO3- 得 8e-，对应 2 mol 而非 4 mol 羟基乙酸盐）。
        // pdf-316（2024 浙江 1 月）：存储 answer=A 错误，官方答案为 C（正极反应应为水还原，NH2- 在水溶液中不能稳定存在）。
        ["pdf-116"] = "A",
        ["pdf-305"] = "D",
        ["pdf-316"] = "C",

        // batch3 扩展（第十四批 / Task 37 续）：全量审计剩余候选中纯文本、可化学唯一判定的 18 道。
        // 含 4 道氧化还原/定性（pdf-068/072/080/082）、11 道 NA 计算（NaChoiceValidator 因含「转移电子/水解」等选项返回 null，由 Concept 事实兜底）、
        // 以及 3 道先经官方题源核对修正存储 answer 后策展的题（pdf-195 D→C、pdf-545 A→B、pdf-474 A→D）。
        // ask=correct 取 verdict==true 项，ask=wrong 取 verdict==false 项；每题 4 选项 verdict 独立核对化学真值，与修正后 answer 一致，零冲突。
        ["pdf-068"] = "C",
        ["pdf-072"] = "D",
        ["pdf-080"] = "B",
        ["pdf-082"] = "D",
        ["pdf-026"] = "D",
        ["pdf-027"] = "B",
        ["pdf-093"] = "A",
        ["pdf-419"] = "D",
        ["pdf-420"] = "B",
        ["pdf-421"] = "B",
        ["pdf-422"] = "A",
        ["pdf-423"] = "A",
        ["pdf-425"] = "A",
        ["pdf-426"] = "D",
        ["pdf-427"] = "A",
        ["pdf-195"] = "C",
        ["pdf-545"] = "B",
        ["pdf-474"] = "D",

        // batch3 扩展（第十五批 / Task 37 续）：全链路 dump 后从 235 道未解中筛出的纯文本、可化学唯一判定的 5 道。
        // pdf-006（K3C60 离子化合物）、pdf-184（核反应守恒）、pdf-294（Ksp 沉淀转化，note 确认）、pdf-424（NA σ 键）、pdf-028（差量法气体计算）。
        // 舍弃 pdf-386（note 自相矛盾、数据损坏）与 pdf-075（存储 answer 与化学真值冲突，待官方题源核对）；其余候选多为图像/结构缺失或选项文本截断，无法零风险策展。
        ["pdf-006"] = "B",
        ["pdf-184"] = "B",
        ["pdf-294"] = "B",
        ["pdf-424"] = "A",
        ["pdf-028"] = "C",

        // batch3 扩展（第十六批 / Task 37 续 第三批）：从文本安全候选中策展的 11 道纯文本、可化学唯一判定题。
        // pdf-123（青花瓷成分·正确）/135（硫的性质·不正确）/136（物质处理方法·正确）/137（C/S 非金属性·正确）
        // /142（通 H2S 离子方程式·存在错误）/144（含硫转化一步实现·正确）/187（Sr/O 同位素·不正确）
        // /189（石墨烯硅烯锗烯·正确）/078（海带提碘·正确）/114（铜冶金污水·错误）/393（苯并呋喃同分异构·正确）。
        // pdf-393 原因「6种」是「16种」子串导致 D 选项串入 A 的事实而弃判；FactChoiceMatcher.OptContains
        //   新增数字前边界后该子串碰撞消除，4 选项各命中唯一事实，verdict 唯一指向 B（三取代苯 10 种扣除 H 得 9 种）。
        // 每题 4 选项 verdict 独立核算化学真值、与存储 answer 一致；stemMatch 均非空。
        ["pdf-123"] = "C",
        ["pdf-135"] = "C",
        ["pdf-136"] = "A",
        ["pdf-137"] = "D",
        ["pdf-142"] = "B",
        ["pdf-144"] = "D",
        ["pdf-187"] = "C",
        ["pdf-189"] = "A",
        ["pdf-078"] = "B",
        ["pdf-114"] = "D",
        ["pdf-393"] = "B"
    };

    public static IEnumerable<object[]> ConceptCases =>
        ConceptTruth.Select(kv => new object[] { kv.Key, kv.Value });

    [Theory]
    [MemberData(nameof(ConceptCases))]
    public void Concept_Solves_KnownQuestion(string id, string expected)
    {
        var r = ConceptChoiceValidator.TryDecide(Req(id));
        Assert.NotNull(r);
        Assert.Equal(expected, r!.AnswerLetter);
    }

    [Fact]
    public void Concept_ZeroFalsePositive_AcrossAllBuildTestCases()
    {
        foreach (var c in AllCases)
        {
            var r = ConceptChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });
            if (r is null) continue;
            Assert.True(ConceptTruth.TryGetValue(c.Id, out var exp),
                $"[零假阳性] ConceptChoiceValidator 对未列入真值表的题 {c.Id} 返回 {r.AnswerLetter}");
            Assert.Equal(exp, r.AnswerLetter);
        }
    }

    [Fact]
    public void Concept_Coverage_MatchesTruthTable()
    {
        var solved = AllCases.Count(c => ConceptChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question }) != null);
        Assert.Equal(ConceptTruth.Count, solved);
    }

    [Fact]
    public void Concept_AdoptedAnswers_AreGradedCorrect_AcrossAllBuildTestCases()
    {
        // 红线：凡 ConceptChoiceValidator 给出结论（必含选项字母 → 被采纳门采纳），
        // 其结论必须被真实判分 IsAnswerCorrect 判为正确，否则即为 harm（采纳了错误答案）。
        foreach (var c in AllCases)
        {
            var r = ConceptChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });
            if (r is null) continue;
            Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c),
                $"[harm] {c.Id} 结论 [{r.FinalResult}] 未被判为正确（type={c.Type}, answer=[{c.Answer}]）");
        }
    }

    [Fact]
    public void Concept_NoOverlap_WithExperimentOrganicAndStructure()
    {
        foreach (var c in AllCases)
        {
            var r = ConceptChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });
            if (r is null) continue;
            var e = ExperimentChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });
            var o = OrganicChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });
            var s = StructurePeriodicValidator.TryDecide(new EngineRequest { RawText = c.Question });
            Assert.False(e != null,
                $"[路由二义] Concept 与 Experiment 同时断言 {c.Id}: con={r.AnswerLetter}, exp={e?.AnswerLetter}");
            Assert.False(o != null,
                $"[路由二义] Concept 与 Organic 同时断言 {c.Id}: con={r.AnswerLetter}, org={o?.AnswerLetter}");
            Assert.False(s != null,
                $"[路由二义] Concept 与 Structure 同时断言 {c.Id}: con={r.AnswerLetter}, str={s?.AnswerLetter}");
        }
    }

    private static EngineRequest Req(string id)
    {
        var c = AllCases.FirstOrDefault(x => x.Id == id);
        Assert.NotNull(c);
        return new EngineRequest { RawText = c!.Question };
    }
}
