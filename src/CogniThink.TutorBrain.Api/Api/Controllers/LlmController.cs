namespace CogniThink.TutorBrain.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using CogniThink.TutorBrain.Application.Agent;

/// <summary>
/// LLM 代理接口：前端网关通过此接口调用大模型，避免在前端暴露 API Key。
/// 仅做透传，不做任何化学判定。
/// </summary>
[ApiController]
[Route("api/llm")]
public sealed class LlmController : ControllerBase
{
    private readonly LlmProxyService _proxy;

    public LlmController(LlmProxyService proxy)
    {
        _proxy = proxy;
    }

    public sealed record ChatRequest(
        string SystemPrompt,
        string UserPrompt,
        float Temperature = 0.3f,
        int MaxTokens = 800);

    /// <summary>调用大模型，返回生成文本</summary>
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.UserPrompt))
            return BadRequest(new { message = "userPrompt 不能为空" });

        var text = await _proxy.ChatAsync(
            request.SystemPrompt ?? string.Empty,
            request.UserPrompt,
            request.Temperature,
            request.MaxTokens,
            ct);

        if (text is null)
        {
            return StatusCode(503, new
            {
                message = "大模型服务不可用（Llm:Enabled=false 或调用失败）",
                available = _proxy.IsAvailable
            });
        }

        return Ok(new { text, available = true });
    }

    /// <summary>检查 LLM 配置状态</summary>
    [HttpGet("status")]
    public IActionResult Status() => Ok(new { available = _proxy.IsAvailable });
}
