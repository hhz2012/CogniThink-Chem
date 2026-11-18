namespace CogniThink.TutorBrain.Application.Agent;

using System.Net.Http.Json;
using System.Text.Json.Serialization;

public sealed class LlmOptions
{
    public bool Enabled { get; set; }
    /// <summary>OpenAI 兼容地址，例如 https://api.example.com/v1</summary>
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    /// <summary>模型名：seed-turbo / qwen-flash 等，可配置切换</summary>
    public string Model { get; set; } = "seed-turbo";
    public int TimeoutSeconds { get; set; } = 8;
}

/// <summary>
/// 大模型话术层：只做自然语言启发，不做判定。
/// 调用失败时自动回退到规则话术，保证导学不中断。
/// </summary>
public sealed class LlmNarrativeAgent : ITutorNarrativeAgent
{
    private readonly HttpClient _http;
    private readonly LlmOptions _options;
    private readonly RuleBasedNarrativeAgent _fallback;

    public LlmNarrativeAgent(HttpClient http, LlmOptions options, RuleBasedNarrativeAgent fallback)
    {
        _http = http;
        _options = options;
        _fallback = fallback;
    }

    public async Task<string> ComposeAsync(NarrativeRequest request, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.BaseUrl) || string.IsNullOrWhiteSpace(_options.ApiKey))
            return await _fallback.ComposeAsync(request, cancellationToken);

        const string systemPrompt =
            "你是高中理科伴思导师。规则：1) 绝不直接给出最终答案；2) 一次只问一个问题或给一条线索；" +
            "3) 先反问、再提示；4) 用户已卡壳多次时才给更具体的线索；5) 输出不超过 80 字；6) 不评判对错细节，只引导思考。";

        var hint = request.HintText is null ? "（本轮无额外线索，请只做启发式反问）" : request.HintText;
        var userPrompt =
            $"学科：化学\n" +
            $"题目：{request.ProblemTitle}\n第 {request.StepIndex} 步：{request.StepPrompt}\n" +
            $"场景：{request.Scene}\n当前提示级别：{request.HintLevel}\n" +
            $"可放出的线索：{hint}\n关联知识点：{string.Join("、", request.KnowledgeNames)}";

        try
        {
            var url = $"{_options.BaseUrl.TrimEnd('/')}/chat/completions";
            var payload = new
            {
                model = _options.Model,
                temperature = 0.4,
                max_tokens = 200,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                }
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");
            req.Content = JsonContent.Create(payload);

            var resp = await _http.SendAsync(req, cts.Token);
            if (!resp.IsSuccessStatusCode) return await _fallback.ComposeAsync(request, cancellationToken);

            var dto = await resp.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: cts.Token);
            var text = dto?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
            return string.IsNullOrWhiteSpace(text)
                ? await _fallback.ComposeAsync(request, cancellationToken)
                : text;
        }
        catch
        {
            return await _fallback.ComposeAsync(request, cancellationToken);
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
