using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// IStatementJudge 统一陈述判定原语地基护栏（Task 1）。
/// 红线：零假阳性 / 零 harm；既有单选瀑布链字节级不变（TR-1.3）；冲突不蒙数（TR-1.2）。
/// </summary>
public class StatementJudgeTests
{
    // ===== TR-1.1：事实库唯一命中 → True/False；无命中 / stem 未限定 → Undecided =====

    // GC-004-B（general-choice-facts）：match=[次磷酸, NaH2PO2, 正盐]，
    // stemMatch=[化学研究中常用的方法, 分类方法正确的是]，verdict=true。
    private const string Stem004 = "化学研究中常用的方法，下列分类方法正确的是";

    [Fact]
    public void FactJudge_UniqueHit_ReturnsTrue()
    {
        var judge = new FactBaseStatementJudge();
        var v = judge.Judge(Stem004, "次磷酸（NaH2PO2）是正盐");
        Assert.Equal(Verdict.True, v);
    }

    [Fact]
    public void FactJudge_NoMatch_ReturnsUndecided()
    {
        var judge = new FactBaseStatementJudge();
        // 含 stem 限定但选项不含任何事实的 match 子串 → 无命中 → Undecided
        var v = judge.Judge(Stem004, "某种不相关物质 Xyz 属于正盐");
        Assert.Equal(Verdict.Undecided, v);
    }

    [Fact]
    public void FactJudge_StemNotQualified_ReturnsUndecided()
    {
        var judge = new FactBaseStatementJudge();
        // 选项命中 match，但题干不含 stemMatch → 不应命中（防跨题误命中）→ Undecided
        var v = judge.Judge("完全无关的另一道题题干", "次磷酸（NaH2PO2）是正盐");
        Assert.Equal(Verdict.Undecided, v);
    }

    // ===== TR-1.1 扩展：计数原语（CountingStatementJudge）=====

    [Fact]
    public void CountingJudge_AlkaneIsomersCorrect_ReturnsTrue()
    {
        var judge = new CountingStatementJudge();
        // C4H10 烷烃构造异构 = 2（正丁烷、异丁烷）
        Assert.Equal(Verdict.True, judge.Judge("", "分子式为 C4H10 的同分异构体有 2 种"));
    }

    [Fact]
    public void CountingJudge_AlkaneIsomersWrong_ReturnsFalse()
    {
        var judge = new CountingStatementJudge();
        Assert.Equal(Verdict.False, judge.Judge("", "分子式为 C4H10 的同分异构体有 3 种"));
    }

    [Fact]
    public void CountingJudge_NonCountingStatement_ReturnsUndecided()
    {
        var judge = new CountingStatementJudge();
        // 不含 "N 种" 断言 → 不是计数陈述 → Undecided
        Assert.Equal(Verdict.Undecided, judge.Judge("", "次磷酸是一元酸"));
    }

    // ===== TR-1.2：组合判定器冲突 → Undecided；一致 → 采纳；全 Undecided → Undecided =====

    private sealed class FixedJudge : IStatementJudge
    {
        private readonly Verdict _v;
        public FixedJudge(Verdict v) => _v = v;
        public Verdict Judge(string stem, string statement) => _v;
    }

    [Fact]
    public void Composite_ConflictingPrimitives_ReturnsUndecided()
    {
        var composite = new CompositeStatementJudge(new FixedJudge(Verdict.True), new FixedJudge(Verdict.False));
        Assert.Equal(Verdict.Undecided, composite.Judge("s", "x"));
    }

    [Fact]
    public void Composite_ConsistentPrimitives_AdoptsVerdict()
    {
        var composite = new CompositeStatementJudge(new FixedJudge(Verdict.True), new FixedJudge(Verdict.True));
        Assert.Equal(Verdict.True, composite.Judge("s", "x"));
    }

    [Fact]
    public void Composite_AllUndecided_ReturnsUndecided()
    {
        var composite = new CompositeStatementJudge(new FixedJudge(Verdict.Undecided), new FixedJudge(Verdict.Undecided));
        Assert.Equal(Verdict.Undecided, composite.Judge("s", "x"));
    }

    [Fact]
    public void Composite_OneDefinitiveOneUndecided_AdoptsDefinitive()
    {
        var composite = new CompositeStatementJudge(new FixedJudge(Verdict.Undecided), new FixedJudge(Verdict.False));
        Assert.Equal(Verdict.False, composite.Judge("s", "x"));
    }

    // ===== TR-1.3：新链路永不与既有 FactChoiceMatcher 真值矛盾（只能更保守）=====

    [Fact]
    public void FactJudge_NeverContradicts_FactChoiceMatcher_OnDecidedQuestions()
    {
        // 对全库中被四个事实校验器任一采纳的题，逐选项核对：新链路 JudgeStatement 若给出确定结论，
        // 必须与 TryDecide 写入 reasons 的判定一致；允许新链路更保守（Undecided），绝不允许反向。
        // 这证明新增 JudgeStatement 路径与既有真值路径无分歧，既有单选行为字节级不变。
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        var judge = new FactBaseStatementJudge();
        var contradictions = new List<string>();

        foreach (var c in cases)
        {
            var req = new EngineRequest { RawText = c.Question };
            var outcomes = new ChoiceVerifier.Outcome?[]
            {
                OrganicChoiceValidator.TryDecide(req),
                StructurePeriodicValidator.TryDecide(req),
                ExperimentChoiceValidator.TryDecide(req),
                ConceptChoiceValidator.TryDecide(req),
            };
            foreach (var o in outcomes)
            {
                if (o is null) continue;
                var (stem, opts) = ChoiceVerifier.ParseOptions(c.Question);
                foreach (var reason in o.Reasons)
                {
                    var m = Regex.Match(reason, @"^([A-Z])：(正确|错误)");
                    if (!m.Success) continue;
                    var L = m.Groups[1].Value;
                    var expected = m.Groups[2].Value == "正确";
                    if (!opts.TryGetValue(L, out var optText)) continue;

                    var v = judge.Judge(stem, optText);
                    if (v == Verdict.Undecided) continue;   // 更保守是安全的
                    var actual = v == Verdict.True;
                    if (actual != expected)
                        contradictions.Add($"{c.Id} opt {L}: TryDecide={(expected ? "正确" : "错误")}, JudgeStatement={v}");
                }
            }
        }

        Assert.Empty(contradictions);
    }
}
