namespace CogniThink.TutorBrain.Application.Agent;

using System.Net.Http.Json;
using System.Text.Json.Serialization;

/// <summary>
/// 通用 LLM 调用代理：供前端网关调用，复用后端配置的 LlmOptions。
/// 不做任何化学判定，只透传 system + user 消息并返回模型文本。
/// </summary>
public sealed class LlmProxyService
{
    private readonly HttpClient _http;
    private readonly LlmOptions _options;

    public LlmProxyService(HttpClient http, LlmOptions options)
    {
        _http = http;
        _options = options;
    }

    public bool IsAvailable =>
        _options.Enabled
        && !string.IsNullOrWhiteSpace(_options.BaseUrl)
        && !string.IsNullOrWhiteSpace(_options.ApiKey);

    /// <summary>
    /// 调用 LLM，返回模型生成的文本。
    /// 失败时返回 null，由调用方决定回退策略。
    /// </summary>
    public async Task<string?> ChatAsync(
        string systemPrompt,
        string userPrompt,
        float temperature = 0.3f,
        int maxTokens = 800,
        CancellationToken ct = default)
    {
        if (!IsAvailable) return null;

        try
        {
            var url = $"{_options.BaseUrl!.TrimEnd('/')}/chat/completions";
            var payload = new
            {
                model = _options.Model,
                temperature,
                max_tokens = maxTokens,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                }
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");
            req.Content = JsonContent.Create(payload);

            var resp = await _http.SendAsync(req, cts.Token);
            if (!resp.IsSuccessStatusCode) return null;

            var dto = await resp.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: cts.Token);
            var text = dto?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("choices")] public List<Choice>? Choices { get; set; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")] public Message? Message { get; set; }
    }

    private sealed class Message
    {
        [JsonPropertyName("content")] public string? Content { get; set; }
    }
}
