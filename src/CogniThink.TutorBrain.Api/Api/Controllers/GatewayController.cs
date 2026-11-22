namespace CogniThink.TutorBrain.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 化学伴思 LLM 网关接口
/// 学生文本 → LLM 识别任务 → 调用化学引擎 → 生成教学回复
/// </summary>
[ApiController]
[Route("api/gateway")]
public sealed class GatewayController : ControllerBase
{
    private readonly LlmGatewayService _gateway;

    public GatewayController(LlmGatewayService gateway)
    {
        _gateway = gateway;
    }

    public sealed record ProcessRequest(string Text);

    /// <summary>
    /// 网关主入口：输入学生文本，返回任务类型、引擎请求/响应、教学回复
    /// </summary>
    [HttpPost("process")]
    public async Task<IActionResult> Process([FromBody] ProcessRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { message = "text 不能为空" });

        var result = await _gateway.ProcessStudentMessageAsync(request.Text, ct: ct);
        return Ok(result);
    }

    /// <summary>
    /// 学生面求解入口：输入学生文本，返回按 ConclusionState 裁剪的学生面视图，
    /// 不泄露未采纳结论（Insufficient 不下发 final_result / 推理 / 解释）。
    /// P2-4；内部复用 ProcessStudentMessageAsync，仅加只读投影，不改求解与判分。
    /// </summary>
    [HttpPost("solve")]
    public async Task<IActionResult> Solve([FromBody] ProcessRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { message = "text 不能为空" });

        var result = await _gateway.ProcessStudentMessageAsync(request.Text, ct: ct);
        return Ok(GatewaySolveProjector.Project(result));
    }

    /// <summary>仅做输入标准化（识别任务 + 组装引擎请求），不调用引擎</summary>
    [HttpPost("parse")]
    public async Task<IActionResult> Parse([FromBody] ProcessRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { message = "text 不能为空" });

        var req = await _gateway.ParseStudentInputAsync(request.Text, ct: ct);
        return Ok(req);
    }

    /// <summary>获取 System Prompt 内容（供调试）</summary>
    [HttpGet("prompts")]
    public IActionResult GetPrompts() => Ok(new
    {
        parseSystemPrompt = LlmGatewayService.ParseSystemPrompt,
        replySystemPrompt = LlmGatewayService.ReplySystemPrompt
    });
}
