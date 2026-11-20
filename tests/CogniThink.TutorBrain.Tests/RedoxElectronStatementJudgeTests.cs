using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// RedoxElectronStatementJudge 护栏（spec FR-4 / Task 4）。
/// 红线：仅判定电子转移数陈述；方程式未配平 / 非氧化还原 / 目标物种歧义或不含变价元素 → Undecided；
/// 不蒙数、零假阳性；接入聚合器后不破坏既有判定（TR-4.3）。
/// </summary>
public class RedoxElectronStatementJudgeTests
{
    private static readonly RedoxElectronStatementJudge Judge = new();

    // ===== TR-4.1：配平反应的电子转移陈述正确判定（True / False）=====

    [Fact]
    public void PerMolProduct_Correct_ReturnsTrue()
    {
        // 2H2 + O2 = 2H2O：总转移 4 e-，H2O 系数 2 → 每 mol H2O 转移 2 e-
        var stem = "反应：2H2 + O2 = 2H2O，下列说法正确的是";
        Assert.Equal(Verdict.True, Judge.Judge(stem, "每生成1 mol H2O转移2 mol电子"));
    }

    [Fact]
    public void PerMolProduct_Wrong_ReturnsFalse()
    {
        var stem = "反应：2H2 + O2 = 2H2O";
        Assert.Equal(Verdict.False, Judge.Judge(stem, "每生成1 mol H2O转移4 mol电子")); // 真值 2
    }

    [Fact]
    public void PerMolReactant_Correct_ReturnsTrue()
    {
        // 每消耗 1 mol O2（系数 1）→ 4 e-
        var stem = "反应：2H2 + O2 = 2H2O";
        Assert.Equal(Verdict.True, Judge.Judge(stem, "每消耗1 mol O2转移4 mol电子"));
    }

    [Fact]
    public void NaForm_Correct_ReturnsTrue()
    {
        // "转移电子数为 2NA" 归一化为 2 mol 电子
        var stem = "反应：2H2 + O2 = 2H2O";
        Assert.Equal(Verdict.True, Judge.Judge(stem, "每生成1 mol H2O转移电子数为2NA"));
    }

    [Fact]
    public void BareNa_Correct_ReturnsTrue()
    {
        // 无数值的 "NA" → 1 mol 电子；这里用 1 e- 的反应：2Na + Cl2 = 2NaCl（Na 0→+1，2 e-；Cl 0→-1，2 e-；每 mol NaCl 1 e-）
        var stem = "反应：2Na + Cl2 = 2NaCl";
        Assert.Equal(Verdict.True, Judge.Judge(stem, "每生成1 mol NaCl转移电子数为NA"));
    }

    [Fact]
    public void KMnO4_Cl2_Correct_ReturnsTrue()
    {
        // 2KMnO4 + 16HCl = 2KCl + 2MnCl2 + 5Cl2 + 8H2O：总 10 e-，Cl2 系数 5 → 每 mol 2 e-
        var stem = "反应：2KMnO4 + 16HCl = 2KCl + 2MnCl2 + 5Cl2 + 8H2O";
        Assert.Equal(Verdict.True, Judge.Judge(stem, "每生成1 mol Cl2转移2 mol电子"));
    }

    [Fact]
    public void KMnO4_Cl2_Wrong_ReturnsFalse()
    {
        var stem = "反应：2KMnO4 + 16HCl = 2KCl + 2MnCl2 + 5Cl2 + 8H2O";
        Assert.Equal(Verdict.False, Judge.Judge(stem, "每生成1 mol Cl2转移4 mol电子")); // 真值 2
    }

    [Fact]
    public void MassBased_Correct_ReturnsTrue()
    {
        // 36 g H2O = 2 mol；声称转移 4 mol e- → 每 mol 2 e-（真值 2）→ True
        var stem = "反应：2H2 + O2 = 2H2O";
        Assert.Equal(Verdict.True, Judge.Judge(stem, "每生成36 g H2O转移4 mol电子"));
    }

    // 带反应标签的多反应题干（pdf-075 形式）：能定位到指定反应并计算
    [Fact]
    public void LabeledReaction_ResolvesAndJudgesTrue()
    {
        var stem = "反应方程式如下：\nⅰ. 2XeF2 + 2H2O = 2Xe + O2 + 4HF\nⅱ. 2XeF2 + 4OH^- = 2Xe + O2 + 4F^- + 2H2O\n下列说法错误的是";
        // 反应ⅱ：Xe +2→0（2×2=4 得电子），O -2→0（2×2=4 失电子），总 4 e-，O2 系数 1 → 4
        Assert.Equal(Verdict.True, Judge.Judge(stem, "反应ⅱ中每生成1 mol O2转移4 mol电子"));
    }

    // ===== TR-4.2：不可判输入返回 Undecided（不蒙数）=====

    [Fact]
    public void NotRedox_ReturnsUndecided()
    {
        var stem = "反应：NaOH + HCl = NaCl + H2O";
        Assert.Equal(Verdict.Undecided, Judge.Judge(stem, "每生成1 mol H2O转移2 mol电子"));
    }

    [Fact]
    public void Unbalanceable_ReturnsUndecided()
    {
        // H2O = H2O2 无法配平（O 1 vs 2）
        var stem = "反应：H2O = H2O2";
        Assert.Equal(Verdict.Undecided, Judge.Judge(stem, "每生成1 mol H2O2转移1 mol电子"));
    }

    [Fact]
    public void TargetNotInEquation_ReturnsUndecided()
    {
        var stem = "反应：2H2 + O2 = 2H2O";
        Assert.Equal(Verdict.Undecided, Judge.Judge(stem, "每生成1 mol CO2转移2 mol电子"));
    }

    [Fact]
    public void SpectatorSpecies_NoChangingElement_ReturnsUndecided()
    {
        // H2O 中 H/O 氧化态均未变 → 不含变价元素 → 不判（避免无意义比值）
        var stem = "反应：2KMnO4 + 16HCl = 2KCl + 2MnCl2 + 5Cl2 + 8H2O";
        Assert.Equal(Verdict.Undecided, Judge.Judge(stem, "每生成1 mol H2O转移2 mol电子"));
    }

    [Fact]
    public void NonElectronStatement_ReturnsUndecided()
    {
        var stem = "反应：2H2 + O2 = 2H2O";
        Assert.Equal(Verdict.Undecided, Judge.Judge(stem, "水是氧化物"));
    }

    [Fact]
    public void NoEquationInStem_ReturnsUndecided()
    {
        Assert.Equal(Verdict.Undecided, Judge.Judge("某反应的说法正确的是", "每生成1 mol O2转移4 mol电子"));
    }

    // ===== 真实题面探针：pdf-075（处理 OCR 上标 / 表格多反应）=====

    [Fact]
    public void RealPdf075_OptionB_JudgedChemicallyTrue()
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().FirstOrDefault(x => x.Id == "pdf-075");
        if (c is null) return; // 数据缺失时跳过（不硬失败）
        // 化学真值：反应ⅱ每生成 1 mol O2 转移 4 mol 电子 → True。
        // 注意：pdf-075 存储 answer 与化学真值冲突（已知数据缺陷，见 data-defect-register），
        // 本探针只验证原语的化学计算正确性，不强制解题。
        Assert.Equal(Verdict.True, Judge.Judge(c.Question, "反应ⅱ中每生成1 mol O2转移4 mol电子"));
    }

    // ===== 全库护栏：接入聚合器后永不与既有事实判定矛盾（只能更保守）=====

    [Fact]
    public void FullBank_RedoxJudge_NeverContradicts_ExistingDecided()
    {
        // 对全库被既有事实校验器采纳的题，逐选项核对：若 redox 原语给出确定结论，
        // 必须与 TryDecide 的判定一致；允许更保守（Undecided），绝不反向。
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
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

                    var v = Judge.Judge(stem, optText);
                    if (v == Verdict.Undecided) continue;
                    var actual = v == Verdict.True;
                    if (actual != expected)
                        contradictions.Add($"{c.Id} opt {L}: TryDecide={(expected ? "正确" : "错误")}, RedoxJudge={v}");
                }
            }
        }

        Assert.Empty(contradictions);
    }

    // ===== 全库护栏：MultiSelect / Counting 任一被采纳结论仍判分正确（接入 redox 后 precision=1.0）=====

    [Fact]
    public void FullBank_AfterRedoxWiring_MultiSelectAndCounting_NoWrongAdoption()
    {
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        var wrong = new List<string>();
        foreach (var c in cases)
        {
            var req = new EngineRequest { RawText = c.Question };
            var ms = MultiSelectChoiceValidator.TryDecide(req);
            if (ms is not null && !GatewayVsLlmEvaluationService.IsAnswerCorrect(ms.FinalResult, c))
                wrong.Add($"MS {c.Id}: {ms.FinalResult} (answer={c.Answer})");
            var ct = CountingChoiceValidator.TryDecide(req);
            if (ct is not null && !GatewayVsLlmEvaluationService.IsAnswerCorrect(ct.FinalResult, c))
                wrong.Add($"CT {c.Id}: {ct.FinalResult} (answer={c.Answer})");
        }
        Assert.Empty(wrong);
    }
}
