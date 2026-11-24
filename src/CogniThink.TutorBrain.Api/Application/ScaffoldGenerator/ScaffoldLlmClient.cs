namespace CogniThink.TutorBrain.Application.ScaffoldGenerator;

using CogniThink.TutorBrain.Application.Agent;

// ============================================================
// Task 3: 生成器专用 LLM 文本客户端抽象。
// 复用项目既有 LlmProxyService（同一 HttpClient / LlmOptions / 超时配置），不新建 API 通道。
// 之所以再抽一层接口：LlmProxyService 是 sealed class，无法直接 mock——
// TR-3.1 要求「mock LLM 断言输出只进 releases 不进 correct」，故接口化仅为可测试。
// 红线：LLM 只写文案，不产 correct；任何调用失败返回 null，由调用方回退模板，绝不抛错。
// ============================================================

public interface IScaffoldLlmClient
{
    bool IsAvailable { get; }

    Task<string?> ChatAsync(
        string systemPrompt,
        string userPrompt,
        float temperature,
        int maxTokens,
        CancellationToken ct = default);
}

/// <summary>
/// 适配既有 <see cref="LlmProxyService"/>，复用其全部配置与错误处理（任何失败 → null）。
/// 生成器不感知底层 HTTP / 鉴权细节。
/// </summary>
public sealed class LlmProxyScaffoldClient : IScaffoldLlmClient
{
    private readonly LlmProxyService _llm;

    public LlmProxyScaffoldClient(LlmProxyService llm) => _llm = llm;

    public bool IsAvailable => _llm.IsAvailable;

    public Task<string?> ChatAsync(string systemPrompt, string userPrompt, float temperature, int maxTokens, CancellationToken ct = default)
        => _llm.ChatAsync(systemPrompt, userPrompt, temperature, maxTokens, ct);
}
