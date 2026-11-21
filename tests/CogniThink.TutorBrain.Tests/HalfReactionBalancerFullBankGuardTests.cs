using System.Linq;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// 半反应配平器全库护栏（Spec T8）：无 LLM、始终运行。
/// 遍历全题库，断言每条非 null 半反应输出对其所在题 IsAnswerCorrect=true（precision=1 的硬保证）；
/// 并断言 pdf-304 / pdf-313 两个 GAIN 目标必须命中（否则漏接路由 / KB）。
/// 任何不满足即阻塞发布。
/// </summary>
public class HalfReactionBalancerFullBankGuardTests
{
    [Fact]
    public void FullBankGuard_HalfReaction_EveryNonNullOutput_IsAnswerCorrect()
    {
        var cases = GatewayVsLlmEvaluationService.BuildTestCases();

        // JSON 加载守卫：若 pdf_questions.json 损坏，BuildTestCases 会吞异常返回空集 → 必须失败而非空过
        Assert.True(cases.Count > 700, $"pdf_questions.json 加载异常：仅 {cases.Count} 条（预期 ~757）");

        var fired = new System.Collections.Generic.List<string>();
        foreach (var c in cases)
        {
            var req = new EngineRequest
            {
                RawText = c.Question,
                TaskType = LlmGatewayService.ClassifyTaskDeterministic(c.Question)
            };
            var resp = HalfReactionBalancer.TrySolve(req);
            if (resp is null) continue;

            fired.Add(c.Id);
            Assert.True(
                GatewayVsLlmEvaluationService.IsAnswerCorrect(resp.FinalResult, c),
                $"[BLOCK] 半反应输出未判正 → {c.Id}：output=\"{resp.FinalResult}\"，keywords=[{string.Join(" | ", c.AnswerKeywords)}]");
        }

        // GAIN 目标必须命中（T8.2 扫描确认仅此两题触发）
        Assert.Contains("pdf-304", fired);
        Assert.Contains("pdf-313", fired);
    }

    [Theory]
    [InlineData("pdf-313", "[B(OH)4]^- + 8e^- + 4H2O = BH4^- + 8OH^-")]
    [InlineData("pdf-304", "CO-2e^-+4OH^-=CO3^2-+2H2O")]
    public void FirmTargets_OutputIsCorrect(string id, string expected)
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == id);
        var resp = HalfReactionBalancer.TrySolve(
            new EngineRequest { RawText = c.Question, TaskType = LlmGatewayService.ClassifyTaskDeterministic(c.Question) });
        Assert.NotNull(resp);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(resp!.FinalResult, c),
            $"{id} 输出未判正：output=\"{resp!.FinalResult}\"，keywords=[{string.Join(" | ", c.AnswerKeywords)}]");
        Assert.Contains(expected, resp.FinalResult);
    }

    [Fact]
    public void Negative_ChoiceTaskType_ReturnsNull()
    {
        // 选择题一律不介入（保留既有选项校验路径，零回归）
        var resp = HalfReactionBalancer.TrySolve(new EngineRequest
        {
            RawText = "CO-空气燃料电池中 a 电极的电极反应式为？",
            TaskType = "choice"
        });
        Assert.Null(resp);
    }

    [Fact]
    public void Negative_NoElectrodeKeyword_ReturnsNull()
    {
        // 无「电极反应」意图 → 不介入
        var resp = HalfReactionBalancer.TrySolve(new EngineRequest
        {
            RawText = "写出反应的化学方程式：Fe 与 O2 点燃生成 Fe3O4",
            TaskType = "fill"
        });
        Assert.Null(resp);
    }

    [Fact]
    public void Negative_AmbiguousAcidAndBase_ReturnsNull()
    {
        // 介质歧义（酸 + 碱同时出现）→ 萃取式返回 null，不蒙数
        var resp = HalfReactionBalancer.TrySolve(new EngineRequest
        {
            RawText = "在酸性和碱性条件下 MnO4^- 转化为 Mn^2+ 的电极反应式",
            TaskType = "fill"
        });
        Assert.Null(resp);
    }

    [Fact]
    public void Negative_NoPairNoMediumNoKb_ReturnsNull()
    {
        // 仅有「电极反应」意图但无显式氧化还原对 / 介质 / 不命中 KB → null
        var resp = HalfReactionBalancer.TrySolve(new EngineRequest
        {
            RawText = "写出该装置中 a 电极的电极反应式（电解质溶液为 NaCl）",
            TaskType = "fill"
        });
        Assert.Null(resp);
    }
}
