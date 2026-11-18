namespace CogniThink.TutorBrain.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using CogniThink.TutorBrain.Application.Evaluation;

/// <summary>
/// 化学规则引擎 vs 大模型 判定准确率对比评估 API。
/// </summary>
[ApiController]
[Route("api/evaluation")]
public sealed class EvaluationController : ControllerBase
{
    private readonly EngineVsLlmEvaluationService _evaluation;
    private readonly GatewayVsLlmEvaluationService _gatewayEvaluation;

    public EvaluationController(
        EngineVsLlmEvaluationService evaluation,
        GatewayVsLlmEvaluationService gatewayEvaluation)
    {
        _evaluation = evaluation;
        _gatewayEvaluation = gatewayEvaluation;
    }

    /// <summary>运行引擎 vs 大模型 对比评估</summary>
    [HttpGet("engine-vs-llm")]
    public async Task<IActionResult> EngineVsLlm(CancellationToken ct)
    {
        var summary = await _evaluation.EvaluateAsync(ct);

        return Ok(new
        {
            totalCases = summary.TotalCases,
            engineAccuracy = Math.Round(summary.EngineAccuracy, 4),
            engineCorrect = summary.EngineCorrect,
            llmAccuracy = Math.Round(summary.LlmAccuracy, 4),
            llmCorrect = summary.LlmCorrect,
            llmUngradable = summary.LlmUngradable,
            llmAvailable = summary.LlmAvailable,
            bothCorrect = summary.BothCorrect.Count,
            engineOnlyCorrect = summary.EngineOnlyCorrect.Count,
            llmOnlyCorrect = summary.LlmOnlyCorrect.Count,
            bothWrong = summary.BothWrong.Count,
            engineOnlyCorrectCases = summary.EngineOnlyCorrect.Select(MapCase),
            llmOnlyCorrectCases = summary.LlmOnlyCorrect.Select(MapCase),
            bothWrongCases = summary.BothWrong.Select(MapCase)
        });
    }

    /// <summary>查看测试集</summary>
    [HttpGet("test-cases")]
    public IActionResult TestCases()
    {
        var cases = _evaluation.BuildTestCases();
        return Ok(cases.Select(c => new
        {
            problemId = c.ProblemId,
            stepIndex = c.StepIndex,
            stepPrompt = c.StepPrompt,
            studentAnswer = c.StudentAnswer,
            expectedVerdict = c.ExpectedVerdict.ToString(),
            note = c.Note
        }));
    }

    private static object MapCase(JudgeCaseResult r) => new
    {
        problemId = r.Case.ProblemId,
        stepIndex = r.Case.StepIndex,
        studentAnswer = r.Case.StudentAnswer,
        expected = r.Case.ExpectedVerdict.ToString(),
        engineVerdict = r.EngineVerdict.ToString(),
        llmVerdict = r.LlmVerdict.ToString(),
        llmReason = r.LlmReason,
        note = r.Case.Note
    };

    /// <summary>网关模式（LLM解析→引擎计算）vs 纯LLM 准确率对比</summary>
    [HttpGet("gateway-vs-llm")]
    public async Task<IActionResult> GatewayVsLlm(CancellationToken ct)
    {
        var summary = await _gatewayEvaluation.EvaluateAsync(ct);

        return Ok(new
        {
            totalCases = summary.TotalCases,
            pureLlmAccuracy = Math.Round(summary.PureLlmAccuracy, 4),
            pureLlmCorrect = summary.PureLlmCorrect,
            gatewayAccuracy = Math.Round(summary.GatewayAccuracy, 4),
            gatewayCorrect = summary.GatewayCorrect,
            gatewayEngineUsed = summary.GatewayEngineUsed,
            llmAvailable = summary.LlmAvailable,
            bothCorrect = summary.BothCorrect.Count,
            gatewayOnlyCorrect = summary.GatewayOnlyCorrect.Count,
            pureLlmOnlyCorrect = summary.PureLlmOnlyCorrect.Count,
            bothWrong = summary.BothWrong.Count,
            bothCorrectCases = summary.BothCorrect.Select(MapQaCase),
            gatewayOnlyCorrectCases = summary.GatewayOnlyCorrect.Select(MapQaCase),
            pureLlmOnlyCorrectCases = summary.PureLlmOnlyCorrect.Select(MapQaCase),
            bothWrongCases = summary.BothWrong.Select(MapQaCase)
        });
    }

    private static object MapQaCase(QaCaseResult r) => new
    {
        id = r.Case.Id,
        question = r.Case.Question,
        expected = r.Case.ExpectedAffirmative.HasValue
            ? (r.Case.ExpectedAffirmative.Value ? "肯定/能" : "否定/不能")
            : "关键词: " + string.Join(",", r.Case.AnswerKeywords),
        note = r.Case.Note,
        pureLlmAnswer = r.PureLlmAnswer,
        pureLlmCorrect = r.PureLlmCorrect,
        taskType = r.TaskType,
        gatewayConclusion = r.GatewayConclusion,
        gatewayOk = r.GatewayOk,
        gatewayAnswer = r.GatewayAnswer,
        gatewayCorrect = r.GatewayCorrect,
        gatewayUsedEngine = r.GatewayUsedEngine
    };
}
