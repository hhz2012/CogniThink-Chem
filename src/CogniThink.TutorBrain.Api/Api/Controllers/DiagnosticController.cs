namespace CogniThink.TutorBrain.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using CogniThink.TutorBrain.Application.Diagnostic;

/// <summary>
/// 诊断导学 API：
/// 学生答错或跳过某步时，把该步推理链拆成知识检查点逐项诊断，
/// 定位盲区后给出讲解（含化学证据），再让学生重试。
/// </summary>
[ApiController]
[Route("api/diagnostic")]
public sealed class DiagnosticController : ControllerBase
{
    private readonly DiagnosticTeachingEngine _engine;

    public DiagnosticController(DiagnosticTeachingEngine engine) => _engine = engine;

    /// <summary>开始诊断：返回该步的完整推理结构与第一个检查点问题</summary>
    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartDiagnosticRequest req, CancellationToken ct)
    {
        var result = await _engine.StartAsync(req.StudentId, req.ProblemId, req.StepIndex, ct);
        if (result is null)
            return NotFound(new { message = "题目或步骤不存在，无法构建推理结构。" });

        return Ok(new
        {
            sessionId = result.SessionId,
            totalCheckpoints = result.Structure.Checkpoints.Count,
            structure = result.Structure.Checkpoints.Select(c => new
            {
                index = c.Index,
                type = c.Type.ToString(),
                knowledgeId = c.KnowledgeId,
                knowledgeName = c.KnowledgeName,
                question = c.Question
            }),
            currentCheckpoint = MapCheckpoint(result.CurrentCheckpoint),
            message = result.Message
        });
    }

    /// <summary>提交对当前检查点的回答</summary>
    [HttpPost("answer")]
    public IActionResult Answer([FromBody] AnswerDiagnosticRequest req)
    {
        var result = _engine.SubmitAnswer(req.SessionId, req.Answer);
        if (result is null)
            return NotFound(new { message = "诊断会话不存在或已过期。" });

        return Ok(new
        {
            verdict = result.Verdict.ToString(),
            message = result.Message,
            action = result.Action.ToString(),
            sessionStatus = result.SessionStatus.ToString(),
            gapCheckpointIndex = result.GapCheckpointIndex,
            nextCheckpoint = result.NextCheckpoint is null ? null : MapCheckpoint(result.NextCheckpoint),
            explanation = result.Explanation is null ? null : MapExplanation(result.Explanation)
        });
    }

    /// <summary>按知识点 Id 获取讲解内容</summary>
    [HttpGet("explain/{knowledgeId}")]
    public IActionResult Explain(string knowledgeId)
    {
        var explanation = _engine.ExplainKnowledge(knowledgeId);
        if (explanation is null)
            return NotFound(new { message = "知识点不存在。" });

        return Ok(MapExplanation(explanation));
    }

    /// <summary>讲解后重试原步骤</summary>
    [HttpPost("retry")]
    public IActionResult Retry([FromBody] RetryDiagnosticRequest req)
    {
        var result = _engine.RetryStep(req.SessionId, req.Answer);
        if (result is null)
            return NotFound(new { message = "诊断会话不存在。" });

        return Ok(new
        {
            verdict = result.Verdict.ToString(),
            message = result.Message,
            action = result.Action.ToString(),
            sessionStatus = result.SessionStatus.ToString(),
            explanation = result.Explanation is null ? null : MapExplanation(result.Explanation)
        });
    }

    /// <summary>查询诊断会话当前状态</summary>
    [HttpGet("session/{sessionId}")]
    public IActionResult GetSession(string sessionId)
    {
        var session = _engine.GetSession(sessionId);
        if (session is null)
            return NotFound(new { message = "诊断会话不存在。" });

        return Ok(new
        {
            sessionId = session.SessionId,
            problemId = session.ProblemId,
            stepIndex = session.StepIndex,
            status = session.Status.ToString(),
            currentCheckpointIndex = session.CurrentCheckpointIndex,
            gapCheckpointIndex = session.GapCheckpointIndex,
            totalCheckpoints = session.Structure.Checkpoints.Count,
            answered = session.Answers.Count
        });
    }

    private static object MapCheckpoint(ReasoningCheckpoint c) => new
    {
        index = c.Index,
        type = c.Type.ToString(),
        knowledgeId = c.KnowledgeId,
        knowledgeName = c.KnowledgeName,
        question = c.Question,
        evidenceType = c.EvidenceType.ToString(),
        linkedReasoningNode = c.LinkedReasoningNode
    };

    private static object MapExplanation(CheckpointExplanation e) => new
    {
        knowledgeId = e.KnowledgeId,
        knowledgeName = e.KnowledgeName,
        level = e.Level.ToString(),
        description = e.Description,
        detail = e.Detail,
        evidence = e.Evidence,
        relatedReactions = e.RelatedReactions,
        // 教方法、教思考相关字段
        thinkingStrategy = e.ThinkingStrategy,
        analysisSteps = e.AnalysisSteps,
        commonMisconception = e.CommonMisconception,
        transferHint = e.TransferHint
    };
}

public sealed record StartDiagnosticRequest(string StudentId, string ProblemId, int StepIndex);
public sealed record AnswerDiagnosticRequest(string SessionId, string Answer);
public sealed record RetryDiagnosticRequest(string SessionId, string Answer);
