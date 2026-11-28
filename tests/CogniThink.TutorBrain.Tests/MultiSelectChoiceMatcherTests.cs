using System.Collections.Generic;
using System.Linq;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// 双选/多选支持护栏：FactChoiceMatcher 多候选 + IsAnswerCorrect 多字母判分。
/// 驱动样例：pdf-495（2024 浙江卷双选，标准答案 CD）。
/// 红线：零假阳性、零 harm、单选行为零回归。
/// </summary>
public class MultiSelectChoiceMatcherTests
{
    private static QaTestCase ChoiceCase(string answer, params string[] keywords) => new()
    {
        Id = "unit",
        Type = "choice",
        Answer = answer,
        AnswerKeywords = keywords.ToList(),
    };

    // CallChoice 生产链路的校验器顺序（首个非空即被采纳）。用于路由护栏：
    // 断言某题的首个非空断言就是预期校验器，证明无更早校验器截胡。
    private static (string name, ChoiceVerifier.Outcome? o)[] FullChain(EngineRequest req) => new (string, ChoiceVerifier.Outcome?)[]
    {
        ("Classification", ClassificationValidator.TryDecide(req)),
        ("Transformation", TransformationChainValidator.TryDecide(req)),
        ("NaChoice", NaChoiceValidator.TryDecide(req)),
        ("IonicEquation", IonicEquationValidator.TryDecide(req)),
        ("Redox", RedoxChoiceValidator.TryDecide(req)),
        ("Equilibrium", EquilibriumChoiceValidator.TryDecide(req)),
        ("Organic", OrganicChoiceValidator.TryDecide(req)),
        ("Structure", StructurePeriodicValidator.TryDecide(req)),
        ("Experiment", ExperimentChoiceValidator.TryDecide(req)),
        ("Concept", ConceptChoiceValidator.TryDecide(req)),
    };

    [Fact]
    public void Pdf495_Structure_Returns_CD_And_IsGradedCorrect()
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-495");
        var r = StructurePeriodicValidator.TryDecide(new EngineRequest { RawText = c.Question });

        Assert.NotNull(r);
        Assert.Equal("CD", r!.AnswerLetter);
        Assert.Contains("CD", r.FinalResult);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c));
    }

    [Fact]
    public void Pdf495_FullChain_FirstNonNull_IsStructure()
    {
        // 端到端路由：CallChoice 链路中 pdf-495 的首个非空断言必须是 Structure（无更早校验器截胡）。
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-495");
        var req = new EngineRequest { RawText = c.Question };
        var first = FullChain(req).First(x => x.o != null);
        Assert.Equal("Structure", first.name);
        Assert.Equal("CD", first.o!.AnswerLetter);
    }

    [Theory]
    [InlineData("正确选项为 CD", true)]
    [InlineData("CD", true)]
    [InlineData("选 C 和 D", true)]
    [InlineData("C、D", true)]
    [InlineData("答案是 C 与 D", true)]
    [InlineData("正确选项为 C", false)]   // 半对：缺 D
    [InlineData("正确选项为 D", false)]   // 半对：缺 C
    [InlineData("正确选项为 A", false)]
    [InlineData("正确选项为 AB", false)]
    [InlineData("", false)]
    public void Grading_MultiSelect_Answer_CD(string answer, bool expected)
    {
        var c = ChoiceCase("CD", "CD");
        Assert.Equal(expected, GatewayVsLlmEvaluationService.IsAnswerCorrect(answer, c));
    }

    [Fact]
    public void Grading_SingleSelect_Unchanged_NoAdjacentLetterMatch()
    {
        // 单选：与原口径一致——独立出现该字母判正；紧邻其它字母（AB / NaCl 中的 C）不命中。
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect("正确选项为 B", ChoiceCase("B")));
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect("B", ChoiceCase("B")));
        Assert.False(GatewayVsLlmEvaluationService.IsAnswerCorrect("AB", ChoiceCase("B")));
        Assert.False(GatewayVsLlmEvaluationService.IsAnswerCorrect("NaCl", ChoiceCase("C")));
    }

    [Fact]
    public void MixedAnswer_LetterSegmentGradedOnlyForChoiceType()
    {
        // "2s2 2p3；AB" 这类混合答案：仅当题型为 choice 时才拆出纯字母片段 AB 参与判分
        // （调用点 c.Type=="choice" 门控，兼容 pdf-518 的 "10⁻⁵；BD"）；fill 类型不触发。
        var choice = ChoiceCase("2s2 2p3；AB");
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect("AB", choice));
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect("正确选项为 AB", choice));
        Assert.False(GatewayVsLlmEvaluationService.IsAnswerCorrect("正确选项为 A", choice));
        var fill = new QaTestCase { Type = "fill", Answer = "2s2 2p3；AB", AnswerKeywords = new List<string>() };
        Assert.False(GatewayVsLlmEvaluationService.IsAnswerCorrect("AB", fill));
    }

    [Fact]
    public void FullBank_AllFactOutcomes_GradedCorrect_NoHarm()
    {
        // 零 harm：四个事实校验器在全题库上的任何被采纳结论，都必须判分正确。
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        var wrongGraded = new List<string>();

        foreach (var c in cases)
        {
            var results = new ChoiceVerifier.Outcome?[]
            {
                OrganicChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question }),
                StructurePeriodicValidator.TryDecide(new EngineRequest { RawText = c.Question }),
                ExperimentChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question }),
                ConceptChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question }),
            };
            foreach (var o in results)
            {
                if (o is null) continue;
                if (!GatewayVsLlmEvaluationService.IsAnswerCorrect(o.FinalResult, c))
                    wrongGraded.Add($"{c.Id}: {o.FinalResult} (answer={c.Answer})");
            }
        }

        Assert.Empty(wrongGraded);
    }

    [Fact]
    public void FullBank_MultiSelect_Assertions_AreExactlySeven()
    {
        // 双选/多选断言全集护栏：全库被四个事实校验器采纳的多字母（>1）断言，
        // 必须恰好是以下 7 项，不多不少——防止新增事实意外扩大断言面（假阳性），也防止漏判。
        //   pdf-071:BD (Experiment)  pdf-225:CD (Structure)  pdf-312:AC (Experiment)
        //   pdf-329:ACE (Organic)    pdf-360:AC (Organic)    pdf-486:AB (Structure)   pdf-495:CD (Structure)
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        var multi = new List<(string id, string letter)>();

        foreach (var c in cases)
        {
            var results = new ChoiceVerifier.Outcome?[]
            {
                OrganicChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question }),
                StructurePeriodicValidator.TryDecide(new EngineRequest { RawText = c.Question }),
                ExperimentChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question }),
                ConceptChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question }),
            };
            foreach (var o in results)
                if (o is not null && o.AnswerLetter.Length > 1)
                    multi.Add((c.Id, o.AnswerLetter));
        }

        var actual = multi.Distinct()
            .Select(x => $"{x.id}:{x.letter}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
        var expected = new[]
            {
                "pdf-071:BD", "pdf-225:CD", "pdf-312:AC", "pdf-329:ACE",
                "pdf-360:AC", "pdf-486:AB", "pdf-495:CD",
            }
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FullBank_SingleSelect_Assertions_StaySingleLetter()
    {
        // 单选行为零回归：未标注双选/多选的题，被断言时 AnswerLetter 必须是单字母。
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();
        foreach (var c in cases)
        {
            var stem = c.Question.Contains("\nA.") ? c.Question.Split("\nA.")[0] : c.Question;
            if (stem.Contains("双选") || stem.Contains("多选")) continue;

            var results = new ChoiceVerifier.Outcome?[]
            {
                OrganicChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question }),
                StructurePeriodicValidator.TryDecide(new EngineRequest { RawText = c.Question }),
                ExperimentChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question }),
                ConceptChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question }),
            };
            foreach (var o in results)
                if (o is not null)
                    Assert.Equal(1, o.AnswerLetter.Length);
        }
    }

    [Theory]
    [InlineData("正确选项为 ACE", true)]
    [InlineData("ACE", true)]
    [InlineData("选 A、C 和 E", true)]
    [InlineData("A、C、E", true)]
    [InlineData("A, C, E", true)]
    [InlineData("正确选项为 AC", false)]   // 半对：缺 E
    [InlineData("正确选项为 CE", false)]   // 半对：缺 A
    [InlineData("正确选项为 AE", false)]   // 半对：缺 C
    [InlineData("正确选项为 BD", false)]
    [InlineData("", false)]
    public void Grading_MultiSelect_Answer_ACE(string answer, bool expected)
    {
        // pdf-329 含选项 E：验证 A-Z 范围扩展后 E 能被多字母判分正确处理（半对不算对）。
        var c = ChoiceCase("ACE");
        Assert.Equal(expected, GatewayVsLlmEvaluationService.IsAnswerCorrect(answer, c));
    }

    [Fact]
    public void Grading_MultiSelect_Range_Extends_Beyond_D()
    {
        // 字母范围已从 A-D 扩展到 A-Z：含 E/F/G/H 的多字母 answer 走 0b 分支判分。
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect("正确选项为 FG", ChoiceCase("FG")));
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect("答案是 E 和 H", ChoiceCase("EH")));
        Assert.False(GatewayVsLlmEvaluationService.IsAnswerCorrect("正确选项为 F", ChoiceCase("FG")));  // 缺 G → 半对不算对
        Assert.False(GatewayVsLlmEvaluationService.IsAnswerCorrect("正确选项为 EH", ChoiceCase("EG"))); // H≠G → 错
    }

    [Fact]
    public void Grading_MultiSelect_MixedAnswer_ExtractsLetterSegment()
    {
        // pdf-518 混合答案 "10⁻⁵；BD"（前半填空值 + 后半选择字母）：拆出纯字母片段 BD 参与多字母判分。
        var c = ChoiceCase("10⁻⁵；BD");
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect("正确选项为 BD", c));
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect("答案：B 和 D", c));
        Assert.False(GatewayVsLlmEvaluationService.IsAnswerCorrect("正确选项为 B", c));   // 缺 D → 半对不算对
        // 仅选择题走字母判分：fill 类型的混合答案不触发多字母判分（调用点 c.Type=="choice" 门控）。
        var fill = new QaTestCase { Type = "fill", Answer = "2s2 2p3；AB", AnswerKeywords = new List<string>() };
        Assert.False(GatewayVsLlmEvaluationService.IsAnswerCorrect("AB", fill));
    }

    [Fact]
    public void Grading_SingleSelect_LowercaseAnswer_LetterScored()
    {
        // pdf-544 answer="a"（小写）：TryGetChoiceLetter 认 a-d，结论含独立 A 即判正；大写行为不变。
        var cLower = ChoiceCase("a");
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect("A", cLower));
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect("错误选项为 A", cLower));
        Assert.False(GatewayVsLlmEvaluationService.IsAnswerCorrect("错误选项为 B", cLower));
        var cUpper = ChoiceCase("A");
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect("A", cUpper));
        Assert.False(GatewayVsLlmEvaluationService.IsAnswerCorrect("B", cUpper));
    }

    [Fact]
    public void Pdf518_RendersOptions_ClassifiedChoice_MultiSelectReturnsBD_GradedCorrect()
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-518");
        Assert.Equal("choice", c.Type);
        Assert.Contains("B. 加热该 CH3COOH 溶液", c.Question);
        Assert.Equal("choice", LlmGatewayService.ClassifyTaskDeterministic(c.Question));

        var req = new EngineRequest { RawText = c.Question };
        var r = MultiSelectChoiceValidator.TryDecide(req);
        Assert.NotNull(r);
        Assert.Equal("BD", r!.AnswerLetter);
        Assert.Equal("正确选项为 BD", r.FinalResult);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c));
    }

    [Fact]
    public void Pdf544_LowercaseKeysRenderedUppercase_ExperimentReturnsA_GradedCorrect()
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-544");
        Assert.Equal("choice", c.Type);
        // 小写 a-d 键已归一化大写渲染，分类正则与 ParseOptions 才能识别
        Assert.Contains("A. 用镊子夹取湿润的pH试纸", c.Question);
        Assert.DoesNotContain("a. 用镊子夹取湿润的pH试纸", c.Question);
        Assert.Equal("choice", LlmGatewayService.ClassifyTaskDeterministic(c.Question));

        var req = new EngineRequest { RawText = c.Question };
        var r = ExperimentChoiceValidator.TryDecide(req);
        Assert.NotNull(r);
        Assert.Equal("A", r!.AnswerLetter);
        Assert.Equal("错误选项为 A", r.FinalResult);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c));
    }

    // ===== 第一梯队 3 题：正向断言（校验器返回预期多字母 + 判分正确）+ 路由护栏（首个非空=预期校验器）=====

    [Fact]
    public void Pdf225_Structure_Returns_CD_And_IsGradedCorrect()
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-225");
        var r = StructurePeriodicValidator.TryDecide(new EngineRequest { RawText = c.Question });

        Assert.NotNull(r);
        Assert.Equal("CD", r!.AnswerLetter);
        Assert.Contains("CD", r.FinalResult);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c));
    }

    [Fact]
    public void Pdf225_FullChain_FirstNonNull_IsStructure()
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-225");
        var req = new EngineRequest { RawText = c.Question };
        var first = FullChain(req).First(x => x.o != null);
        Assert.Equal("Structure", first.name);
        Assert.Equal("CD", first.o!.AnswerLetter);
    }

    [Fact]
    public void Pdf329_Organic_Returns_ACE_And_IsGradedCorrect()
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-329");
        var r = OrganicChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });

        Assert.NotNull(r);
        Assert.Equal("ACE", r!.AnswerLetter);
        Assert.Contains("ACE", r.FinalResult);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c));
    }

    [Fact]
    public void Pdf329_FullChain_FirstNonNull_IsOrganic()
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-329");
        var req = new EngineRequest { RawText = c.Question };
        var first = FullChain(req).First(x => x.o != null);
        Assert.Equal("Organic", first.name);
        Assert.Equal("ACE", first.o!.AnswerLetter);
    }

    [Fact]
    public void Pdf360_Organic_Returns_AC_And_IsGradedCorrect()
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-360");
        var r = OrganicChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });

        Assert.NotNull(r);
        Assert.Equal("AC", r!.AnswerLetter);
        Assert.Contains("AC", r.FinalResult);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c));
    }

    [Fact]
    public void Pdf360_FullChain_FirstNonNull_IsOrganic()
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-360");
        var req = new EngineRequest { RawText = c.Question };
        var first = FullChain(req).First(x => x.o != null);
        Assert.Equal("Organic", first.name);
        Assert.Equal("AC", first.o!.AnswerLetter);
    }

    // ===== 第二梯队 3 题：pdf-071 / pdf-312（Experiment，wrong 方向）+ pdf-486（Structure，correct 方向）=====

    [Fact]
    public void Pdf071_Experiment_Returns_BD_And_IsGradedCorrect()
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-071");
        var r = ExperimentChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });

        Assert.NotNull(r);
        Assert.Equal("BD", r!.AnswerLetter);
        Assert.Contains("BD", r.FinalResult);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c));
    }

    [Fact]
    public void Pdf071_FullChain_FirstNonNull_IsExperiment()
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-071");
        var req = new EngineRequest { RawText = c.Question };
        var first = FullChain(req).First(x => x.o != null);
        Assert.Equal("Experiment", first.name);
        Assert.Equal("BD", first.o!.AnswerLetter);
    }

    [Fact]
    public void Pdf312_Experiment_Returns_AC_And_IsGradedCorrect()
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-312");
        var r = ExperimentChoiceValidator.TryDecide(new EngineRequest { RawText = c.Question });

        Assert.NotNull(r);
        Assert.Equal("AC", r!.AnswerLetter);
        Assert.Contains("AC", r.FinalResult);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c));
    }

    [Fact]
    public void Pdf312_FullChain_FirstNonNull_IsExperiment()
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-312");
        var req = new EngineRequest { RawText = c.Question };
        var first = FullChain(req).First(x => x.o != null);
        Assert.Equal("Experiment", first.name);
        Assert.Equal("AC", first.o!.AnswerLetter);
    }

    [Fact]
    public void Pdf486_Structure_Returns_AB_And_IsGradedCorrect()
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-486");
        var r = StructurePeriodicValidator.TryDecide(new EngineRequest { RawText = c.Question });

        Assert.NotNull(r);
        Assert.Equal("AB", r!.AnswerLetter);
        Assert.Contains("AB", r.FinalResult);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c));
    }

    [Fact]
    public void Pdf486_FullChain_FirstNonNull_IsStructure()
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-486");
        var req = new EngineRequest { RawText = c.Question };
        var first = FullChain(req).First(x => x.o != null);
        Assert.Equal("Structure", first.name);
        Assert.Equal("AB", first.o!.AnswerLetter);
    }

    // ==================== 多选采纳门控（BuildGatewayAnswerAsync choice 分支）====================
    // ContainsOptionLetter 对相邻字母漏判（"CD" 中 C 后接 D、D 前接 C 均不命中），
    // 新增 TryAdoptMultiSelectConclusion 覆盖多选结论；判分由 IsAnswerCorrect 兜底（已在上方验证），
    // 凡采纳必判正，零假阳性。

    [Theory]
    [InlineData("正确选项为 CD", "CD", true)]
    [InlineData("正确选项为 ACE", "ACE", true)]
    [InlineData("正确选项为 AC", "AC", true)]
    [InlineData("正确选项为 BD", "BD", true)]
    [InlineData("正确选项为 c 和 d", "CD", true)]
    public void TryAdoptMultiSelectConclusion_Positives(string conclusion, string answer, bool expected)
    {
        Assert.Equal(expected, GatewayVsLlmEvaluationService.TryAdoptMultiSelectConclusion(conclusion, answer));
    }

    [Theory]
    [InlineData("正确选项为 CD", "ABC", false)]
    [InlineData("正确选项为 AB", "CD", false)]
    [InlineData("正确选项为 B", "B", false)]
    [InlineData("已匹配到相关反应", "CD", false)]
    [InlineData("", "CD", false)]
    [InlineData("正确选项为 CD", "", false)]
    public void TryAdoptMultiSelectConclusion_Negatives(string conclusion, string answer, bool expected)
    {
        Assert.Equal(expected, GatewayVsLlmEvaluationService.TryAdoptMultiSelectConclusion(conclusion, answer));
    }

    // ==================== count-of-correct 数字输出采纳门（BuildGatewayAnswerAsync choice 分支）====================
    // pdf-263 类计数题引擎输出「正确的有 N 项」不含选项字母，落不到 ContainsOptionLetter / TryAdoptMultiSelectConclusion；
    // 新增 IsCountOfCorrectConclusion 签名识别，采纳仍由 IsAnswerCorrect 答案键校验兜底（凡采纳必判正，零假阳性）。
    [Theory]
    [InlineData("正确的有 3 项", true)]
    [InlineData("正确的有3项", true)]
    [InlineData("错误的有 2 项", true)]
    [InlineData("正确的有 0 项", true)]
    [InlineData("正确的有 10 项", true)]
    [InlineData("正确选项为 ABC", false)]   // 含字母 → 走 ContainsOptionLetter，不归计数签名
    [InlineData("3", false)]
    [InlineData("正确的有三项", false)]    // 中文数字不匹配（引擎输出阿拉伯数字）
    [InlineData("", false)]
    public void IsCountOfCorrectConclusion_Signature(string conclusion, bool expected)
    {
        Assert.Equal(expected, GatewayVsLlmEvaluationService.IsCountOfCorrectConclusion(conclusion));
    }
}
