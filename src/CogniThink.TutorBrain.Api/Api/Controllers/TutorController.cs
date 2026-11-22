namespace CogniThink.TutorBrain.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using CogniThink.TutorBrain.Api.Contracts;
using CogniThink.TutorBrain.Application;
using CogniThink.TutorBrain.Domain;

[ApiController]
[Route("api/tutor")]
public sealed class TutorController : ControllerBase
{
    private readonly TutorService _tutor;

    public TutorController(TutorService tutor) => _tutor = tutor;

    /// <summary>
    /// 提交一步思考。
    /// 状态机决定：是否推进、是否反问、提示级别、是否通关、是否发放变式。
    /// 判定由领域规则引擎完成，LLM 只负责话术。
    /// </summary>
    [HttpPost("submit-step")]
    public async Task<ActionResult<SubmitStepResponse>> SubmitStep(
        [FromBody] SubmitStepRequest request,
        CancellationToken ct)
    {
        var result = await _tutor.SubmitStepAsync(
            new SubmitStepInput(
                request.StudentId,
                request.ProblemId,
                request.StepIndex,
                request.StepContent),
            ct);

        if (result is null)
            return NotFound(new { message = "题目或步骤不存在，请检查 problemId / stepIndex" });

        var d = result.Decision;
        var response = new SubmitStepResponse(
            Status: d.Status.ToString(),
            Verdict: d.Verdict.ToString(),
            ErrorTag: d.ErrorTag.ToText(),
            HintLevel: d.HintLevel,
            RecommendedAction: d.RecommendedAction.ToString(),
            Message: d.Message,
            RelatedKnowledgeIds: d.RelatedKnowledgeIds.ToList(),
            UnlockedNextStep: d.UnlockedNextStep,
            Completed: d.Completed,
            NextStepIndex: d.NextStepIndex,
            HintText: d.HintText,
            Mastery: result.Mastery is null ? null : Math.Round(result.Mastery.Value, 3),
            CurrentStep: result.CurrentStep,
            TotalSteps: result.TotalSteps,
            WeakPrerequisites: result.WeakPrerequisites.Select(DtoMapper.ToDto).ToList(),
            ChemicalEvidence: result.ChemicalEvidence is null ? null : DtoMapper.ToDto(result.ChemicalEvidence),
            Variant: result.Variant is null ? null : DtoMapper.ToDto(result.Variant),
            StandardReasoningChain: result.StandardReasoningChain?.ToList());

        return Ok(response);
    }

    /// <summary>查看某题的卡壳轨迹（回放每一步判定）</summary>
    [HttpGet("traces/{studentId}/{problemId}")]
    public async Task<IActionResult> GetTraces(string studentId, string problemId, CancellationToken ct)
    {
        var traces = await _tutor.GetStepTracesAsync(studentId, problemId, ct);
        return Ok(traces.Select(t => new
        {
            t.StepIndex,
            t.StepContent,
            Verdict = t.Verdict.ToString(),
            ErrorTag = t.ErrorTag.ToText(),
            t.HintLevel,
            t.Feedback,
            t.CreatedAt
        }));
    }
}
