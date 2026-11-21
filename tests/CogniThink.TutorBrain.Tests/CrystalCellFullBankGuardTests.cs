using System.Linq;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// 晶胞轨道全库护栏（Spec: crystal-cell-validator, AC-2 / NFR-2）。
/// 无 LLM、始终运行：遍历全题库，断言每条非 null 晶胞输出对其所在题 IsAnswerCorrect=true。
/// 任何不满足即阻塞发布（precision=1 的硬保证）。
/// </summary>
public class CrystalCellFullBankGuardTests
{
    [Fact]
    public void FullBankGuard_Crystal_EveryNonNullOutput_IsAnswerCorrect()
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
            var resp = CrystalCellValidator.TrySolve(req, c.ImageAssets);
            if (resp is null) continue;

            fired.Add(c.Id);
            Assert.True(
                GatewayVsLlmEvaluationService.IsAnswerCorrect(resp.FinalResult, c),
                $"[BLOCK] 晶胞输出未判正 → {c.Id}：output=\"{resp.FinalResult}\"，keywords=[{string.Join(" | ", c.AnswerKeywords)}]");
        }

        // 确定 GAIN 目标必须命中（否则漏接资产/路由）
        Assert.Contains("pdf-170", fired);
        Assert.Contains("pdf-206", fired);
        Assert.Contains("pdf-222", fired);
        Assert.Contains("pdf-178", fired);
        Assert.Contains("pdf-203", fired);
        Assert.Contains("pdf-204", fired);
    }

    [Theory]
    [InlineData("pdf-170", "4")]
    [InlineData("pdf-203", "4")]
    [InlineData("pdf-204", "2:1")]
    [InlineData("pdf-206", "4")]
    [InlineData("pdf-206", "V2/4V1")]
    [InlineData("pdf-222", "4.96×10^23 / (N_A · a^3)")]
    [InlineData("pdf-178", "12")]
    [InlineData("pdf-178", "2.87×10^23 / (N_A · a^3)")]
    public void FirmTargets_OutputContains_ExpectedKeyword(string id, string keywordFragment)
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == id);
        var resp = CrystalCellValidator.TrySolve(
            new EngineRequest { RawText = c.Question, TaskType = LlmGatewayService.ClassifyTaskDeterministic(c.Question) },
            c.ImageAssets);
        Assert.NotNull(resp);
        Assert.Contains(keywordFragment, resp!.FinalResult);
    }

    [Fact]
    public void Pdf178_Unlocked_OfficialTrace_FiresAndIsCorrect()
    {
        // pdf-178 官方溯源已找到（page_060 num 4 第(6)小问）→ 改键 + 录入 Mn₃Sb 资产 → 求解器应命中且判正
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-178");
        var resp = CrystalCellValidator.TrySolve(
            new EngineRequest { RawText = c.Question, TaskType = LlmGatewayService.ClassifyTaskDeterministic(c.Question) },
            c.ImageAssets);
        Assert.NotNull(resp);
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(resp!.FinalResult, c),
            $"pdf-178 输出未判正：output=\"{resp!.FinalResult}\"，keywords=[{string.Join(" | ", c.AnswerKeywords)}]");
        Assert.Contains("12", resp.FinalResult);
    }
}
