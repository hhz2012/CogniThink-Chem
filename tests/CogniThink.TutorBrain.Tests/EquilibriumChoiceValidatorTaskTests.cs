using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// Tier 2 / T2：电离平衡 / 胶体 / 电解质概念选择题校验器（EquilibriumChoiceValidator）测试。
/// 真值源：data/highschool/equilibrium-rules.json。
/// 红线：凡校验器给出非 null 结论，必须与题库答案一致（零假阳性）；
/// 任一选项无法被规则唯一命中、或问法方向不可判、或满足方向的选项不唯一 → 返回 null（不蒙数）。
/// </summary>
public class EquilibriumChoiceValidatorTaskTests
{
    private static EngineRequest Req(string raw) => new() { TaskType = "choice", RawText = raw };

    private static string Letter(string? answer)
    {
        var m = Regex.Match(answer ?? "", "[A-D]");
        return m.Success ? m.Value : "";
    }

    /// <summary>
    /// 题库 answer 字段被污染的极少数题：以题面 note 与化学真值为准（已人工核对）。
    /// pdf-008：bank answer="A, B, C, D"（污染），题干问“叙述错误的是”，note 与 llmReference 均确认 C 为错误项。
    /// </summary>
    private static readonly Dictionary<string, string> BankAnswerCorrections = new()
    {
        ["pdf-008"] = "C",
    };

    private static QaTestCase? PdfCase(string id) =>
        GatewayVsLlmEvaluationService.GenPdfQuestions().FirstOrDefault(x => x.Id == id);

    // ---------- 必解真题（20 道 T2 候选，化学真值已交叉核对 note / llmReference） ----------

    [Theory]
    [InlineData("pdf-005", "C")]   // 碳纳米管/C60 同素异形体（colloid）
    [InlineData("pdf-007", "C")]   // 分散系（colloid）
    [InlineData("pdf-008", "C")]   // 胶体制备（colloid）——题库 answer 字段污染为 A，化学真值为 C
    [InlineData("pdf-011", "B")]   // 不涉及化学变化（水制冰）
    [InlineData("pdf-033", "D")]   // 电解质概念辨析
    [InlineData("pdf-036", "C")]   // 导电能力变化最大（蒸馏水）
    [InlineData("pdf-091", "C")]   // 物质性质（硝酸银棕色瓶）
    [InlineData("pdf-096", "B")]   // Na2CS3 溶液显碱性
    [InlineData("pdf-124", "A")]   // 氯水+Na2SO3，pH 减小
    [InlineData("pdf-131", "A")]   // 不能证明 Cl2 与水反应（液面上升）
    [InlineData("pdf-264", "B")]   // 促进水的电离且呈碱性（Na2CO3）
    [InlineData("pdf-266", "A")]   // 盐酸/醋酸比较：pH 相等则 c(CH3COO-)=c(Cl-)
    [InlineData("pdf-268", "A")]   // 25℃ 说法：醋酸与盐酸产 H2 量基本相同
    [InlineData("pdf-270", "D")]   // 25℃ 说法：Na2CO3 溶液水电离出的 H+ 量
    [InlineData("pdf-276", "B")]   // 不涉及水解（硫酸滴定 NaOH）
    [InlineData("pdf-277", "C")]   // NaHCO3 vs Na2CO3：前者 c(OH-) 大于后者（错误）
    [InlineData("pdf-441", "B")]   // 化学与生活：小苏打作食品膨松剂
    [InlineData("pdf-444", "A")]   // 厨房化学：洗洁精去油污是乳化非水解
    [InlineData("pdf-452", "B")]   // 生命过程：蛋白质只能被蛋白酶水解（错误）
    [InlineData("pdf-513", "B")]   // 中和等 pH 等体积氨水与 NaOH 所需 HCl 相同（错误）
    public void 必解真题_被正确解出(string id, string expected)
    {
        var tc = PdfCase(id);
        Assert.True(tc is not null, $"题库中找不到 {id}");
        var outcome = EquilibriumChoiceValidator.TryDecide(Req(tc!.Question));
        Assert.True(outcome is not null, $"{id} 应被平衡/概念校验器解出，但返回 null");
        Assert.Equal(expected, outcome!.AnswerLetter);
    }

    // ---------- 诚实 null：不可唯一判定必须返回 null ----------

    [Fact]
    public void 不可判定的真题_保守返回null()
    {
        // pdf-006：K3C60 离子化合物/超导，选项含结构与周期律推理，无法被规则唯一命中 → null
        var tc = PdfCase("pdf-006");
        Assert.NotNull(tc);
        Assert.Null(EquilibriumChoiceValidator.TryDecide(Req(tc!.Question)));
    }

    [Fact]
    public void 问法方向不可判_保守返回null()
    {
        // 题干既无“正确/的是”也无“错误/不正确”等方向词 → 不臆断
        var raw = "下列说法的是（ ）\nA. 醋酸是弱电解质\nB. 盐酸是强电解质\nC. NaCl 是盐\nD. 蔗糖是非电解质";
        Assert.Null(EquilibriumChoiceValidator.TryDecide(Req(raw)));
    }

    [Fact]
    public void 选项含规则库外物质_保守返回null()
    {
        // 问法可判，但选项涉及规则库未覆盖的陈述，任一选项零命中 → 整题 null（不蒙数）
        var raw = "(★)下列说法正确的是（ ）\nA. Xyz 酸是强酸\nB. Abc 碱是强碱\nC. Def 盐是正盐\nD. Ghi 氧化物是酸性氧化物";
        Assert.Null(EquilibriumChoiceValidator.TryDecide(Req(raw)));
    }

    // ---------- 全题库零假阳性护栏（精确率 100%） ----------

    [Fact]
    public void Guardrail_全题库_平衡概念校验器无假阳性()
    {
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        Assert.NotEmpty(cases);
        var solved = new List<string>();
        var mismatches = new List<string>();
        foreach (var c in cases)
        {
            var outcome = EquilibriumChoiceValidator.TryDecide(Req(c.Question));
            if (outcome is null) continue;
            // 凡给出结论，必须是选择题且答案字母与题库一致（污染题走人工核对的真值表）
            var expected = BankAnswerCorrections.TryGetValue(c.Id, out var corr) ? corr : Letter(c.Answer);
            if (expected.Length == 0)
            {
                mismatches.Add($"{c.Id} 校验器给出 {outcome.AnswerLetter}，但题库答案无可比对字母（答案='{c.Answer}'）");
                continue;
            }
            if (outcome.AnswerLetter != expected)
                mismatches.Add($"{c.Id} 校验器给出 {outcome.AnswerLetter}，题库答案 {expected}（答案原文='{c.Answer}'）");
            else
                solved.Add($"{c.Id}->{outcome.AnswerLetter}");
        }
        Assert.True(mismatches.Count == 0, "存在假阳性（校验器错选）：\n" + string.Join("\n", mismatches));
        // 至少应解出 20 道 T2 候选，证明覆盖生效
        Assert.True(solved.Count >= 20, $"仅解出 {solved.Count} 道，期望 ≥20：{string.Join(",", solved)}");
    }
}
