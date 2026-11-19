using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// Tier 2 / Task 17：物质转化链校验器测试。
/// 红线：凡校验器给出非 null 结论，必须与题库答案一致（零假阳性）；无法唯一判定必须返回 null。
/// 逻辑精确复刻 _evalscratch/verify_t2t.py（已在全题库扫描验证 0 假阳性）。
/// </summary>
public class TransformationChainValidatorTests
{
    private static EngineRequest Req(string raw) => new() { TaskType = "choice", RawText = raw };

    private static string Letter(string? answer)
    {
        var m = Regex.Match(answer ?? "", "[A-D]");
        return m.Success ? m.Value : "";
    }

    // ---------- 必解真题（AC-17：pdf-013/015/016 被解出；pdf-017 诚实 null 或 ==A） ----------

    [Theory]
    [InlineData("pdf-013", "D")]
    [InlineData("pdf-015", "B")]
    [InlineData("pdf-016", "D")]
    public void 必解真题_被正确解出(string id, string expected)
    {
        var tc = GatewayVsLlmEvaluationService.GenPdfQuestions().FirstOrDefault(x => x.Id == id);
        Assert.NotNull(tc);
        var outcome = TransformationChainValidator.TryDecide(Req(tc!.Question));
        Assert.True(outcome is not null, $"{id} 应被转化链校验器解出，但返回 null");
        Assert.Equal(expected, outcome!.AnswerLetter);
    }

    [Fact]
    public void pdf017_无唯一解_诚实返回null或解为A()
    {
        // pdf-017：A、D 均可行（侯氏制碱 D 也成立）→ 无唯一 → 应返回 null。
        // 诚实行为：不得硬选 A；若将来规则库变动使其有唯一解，也必须是 A（不能是别的）。
        var tc = GatewayVsLlmEvaluationService.GenPdfQuestions().FirstOrDefault(x => x.Id == "pdf-017");
        Assert.NotNull(tc);
        var outcome = TransformationChainValidator.TryDecide(Req(tc!.Question));
        if (outcome is not null)
            Assert.Equal("A", outcome.AnswerLetter);
    }

    // ---------- TR-17 单元逻辑锁（构造可控题干，覆盖各判定分支） ----------

    [Fact]
    public void TR17_1_能实现方向_唯一可行项()
    {
        // A 全链可行；B 断于 S→SO3；C 断于 Fe→Fe2O3（高温得 Fe3O4）；D 断于 NO→HNO3（缺 O2）
        var raw = "下列物质间的转化能实现的是（ ）\n" +
                  "A. Na --(H2O)--> NaOH --(CO2)--> Na2CO3\n" +
                  "B. S --(O2,点燃)--> SO3 --(H2O)--> H2SO4\n" +
                  "C. Fe --(H2O,高温)--> Fe2O3 --(CO,高温)--> Fe\n" +
                  "D. NO --(H2O)--> HNO3 --(Cu)--> NO";
        var o = TransformationChainValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("A", o!.AnswerLetter);
    }

    [Fact]
    public void TR17_2_不能实现方向_唯一断链项()
    {
        // B 断于 S→SO3；A/C/D 全链可行 → 唯一断链为 B
        var raw = "下列物质间的转化不能实现的是（ ）\n" +
                  "A. Na --(H2O)--> NaOH --(CO2)--> Na2CO3\n" +
                  "B. S --(O2,点燃)--> SO3 --(H2O)--> H2SO4\n" +
                  "C. Fe --(Cl2,点燃)--> FeCl3 --(Fe)--> FeCl2\n" +
                  "D. CaCO3 --(高温)--> CaO --(H2O)--> Ca(OH)2";
        var o = TransformationChainValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("B", o!.AnswerLetter);
    }

    [Fact]
    public void TR17_3_裸三物质格式_被正确解析()
    {
        // pdf-015 式：选项为 "甲、乙、丙" 无箭头；B 断于 K2CO3→KNO3（无规则）
        var raw = "甲、乙、丙有如图所示的转化关系（“→”表示反应一步实现）。下列各组物质按照甲、乙、丙的顺序不符合要求的是（ ）\n" +
                  "A. Na、NaOH、NaCl\n" +
                  "B. KOH、K2CO3、KNO3\n" +
                  "C. CaO、Ca(OH)2、CaCO3\n" +
                  "D. Fe、FeCl2、Fe(OH)2";
        var o = TransformationChainValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("B", o!.AnswerLetter);
    }

    [Fact]
    public void TR17_4_选项无法解析为链_保守返回null()
    {
        // 选项 A 不是链（无箭头、无顿号）→ parse_chain 返回 null → 整题不臆断
        var raw = "下列转化能实现的是（ ）\n" +
                  "A. 氯化钠是盐\n" +
                  "B. S --(O2,点燃)--> SO3 --(H2O)--> H2SO4\n" +
                  "C. Fe --(Cl2,点燃)--> FeCl3 --(Fe)--> FeCl2\n" +
                  "D. CaCO3 --(高温)--> CaO --(H2O)--> Ca(OH)2";
        Assert.Null(TransformationChainValidator.TryDecide(Req(raw)));
    }

    [Fact]
    public void TR17_5_纯条件箭头陷阱_不误判()
    {
        // 关键：Na2CO3 --(△)--> NaOH 必须判 False（其实需要 Ca(OH)2，不是加热分解）。
        // A 断于 Fe2O3→Fe(OH)3；B 断于 MgCl2 溶液蒸干水解；C 全链可行；D 断于 Na2CO3--△-->NaOH。
        // 若条件箭头误判，D 会变可行 → C/D 不唯一 → null；正确行为是唯一 C。
        var raw = "下列物质转化能实现的是（ ）\n" +
                  "A. Fe2O3 --(H2O)--> Fe(OH)3 --(HCl)--> FeCl3\n" +
                  "B. MgCl2溶液 --(蒸干)--> 无水MgCl2 --(熔融电解)--> Mg\n" +
                  "C. CaCO3 --(高温)--> CaO --(H2O)--> Ca(OH)2\n" +
                  "D. Na2CO3 --(△)--> NaOH --(CO2)--> Na2CO3";
        var o = TransformationChainValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("C", o!.AnswerLetter);
    }

    [Fact]
    public void TR17_6_酸浓度陷阱_稀硫酸不与铜反应()
    {
        // Cu + 稀硫酸 不可行（规则要求 浓H2SO4+△）；Fe + 稀硫酸 可行。
        // 不能实现方向：A 是唯一断链（Cu+稀硫酸不反应）。
        var raw = "下列转化不能实现的是（ ）\n" +
                  "A. Cu --(稀硫酸)--> CuSO4 --(NaOH)--> Cu(OH)2\n" +
                  "B. Fe --(稀硫酸)--> FeSO4 --(NaOH)--> Fe(OH)2\n" +
                  "C. Mg --(稀盐酸)--> MgCl2 --(NaOH)--> Mg(OH)2\n" +
                  "D. Al --(稀盐酸)--> AlCl3 --(NaOH)--> Al(OH)3";
        var o = TransformationChainValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("A", o!.AnswerLetter);
    }

    [Fact]
    public void TR17_7_规则库步数不少于50()
    {
        Assert.True(TransformationChainValidator.RuleCount >= 50,
            $"规则库步数 {TransformationChainValidator.RuleCount}，期望 ≥50");
    }

    // ---------- 新增 t2t 题库全部可解（AC-17：≥20 新转化链题全解 + 答对） ----------

    [Fact]
    public void 新增t2t题_全部被转化链校验器正确解出()
    {
        var cases = GatewayVsLlmEvaluationService.GenTier2Questions()
            .Where(x => x.Id.StartsWith("t2t-"))
            .ToList();
        Assert.True(cases.Count >= 20, $"t2t 题数量 {cases.Count}，期望 ≥20");
        var fails = new List<string>();
        foreach (var c in cases)
        {
            var outcome = TransformationChainValidator.TryDecide(Req(c.Question));
            var expected = Letter(c.Answer);
            if (outcome is null || outcome.AnswerLetter != expected)
                fails.Add($"{c.Id}(期望 {expected}, 实际 {(outcome?.AnswerLetter ?? "null")})");
        }
        Assert.True(fails.Count == 0, "t2t 题未全对：\n" + string.Join("\n", fails));
    }

    // ---------- 全题库零假阳性护栏（AC-19 精确率 100%、零回归） ----------

    [Fact]
    public void Guardrail_全题库_转化链校验器无假阳性()
    {
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        Assert.NotEmpty(cases);
        var solved = new List<string>();
        var mismatches = new List<string>();
        foreach (var c in cases)
        {
            var outcome = TransformationChainValidator.TryDecide(Req(c.Question));
            if (outcome is null) continue;
            var expected = Letter(c.Answer);
            if (expected.Length == 0)
            {
                mismatches.Add($"{c.Id} 校验器给出 {outcome.AnswerLetter}，但题库答案无可比对字母（答案='{c.Answer}'）");
                continue;
            }
            if (outcome.AnswerLetter != expected)
                mismatches.Add($"{c.Id} 校验器给出 {outcome.AnswerLetter}，题库答案 {expected}");
            else
                solved.Add($"{c.Id}->{outcome.AnswerLetter}");
        }
        Assert.True(mismatches.Count == 0, "存在假阳性（转化链校验器错选）：\n" + string.Join("\n", mismatches));
        // 至少应解出 1 道，证明覆盖生效（含新题与真题）
        Assert.True(solved.Count >= 1, $"仅解出 {solved.Count} 道：{string.Join(",", solved)}");
    }
}
