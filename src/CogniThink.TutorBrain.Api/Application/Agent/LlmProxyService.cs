namespace CogniThink.TutorBrain.Application.Agent;

using System.IO;
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
    /// 当前配置模型是否支持视觉输入（多模态）。按模型名是否含 "vl" 判定，
    /// 如 qwen-vl-plus / qwen3-vl-plus / qwen-vl-max。纯文本模型返回 false。
    /// </summary>
    public bool SupportsVision =>
        !string.IsNullOrWhiteSpace(_options.Model)
        && _options.Model.Contains("vl", StringComparison.OrdinalIgnoreCase);

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

    /// <summary>
    /// 带配图调用 LLM。仅当 <see cref="SupportsVision"/> 为 true 且提供了图片时才走多模态，
    /// 否则自动回退到纯文本 <see cref="ChatAsync"/>，对纯文本模型完全无感。
    /// </summary>
    /// <param name="imagePaths">图片相对路径（相对 data/ 目录，如 pdf_images/p007_n04_1.png）。</param>
    public async Task<string?> ChatWithImagesAsync(
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<string>? imagePaths,
        float temperature = 0.3f,
        int maxTokens = 800,
        CancellationToken ct = default)
    {
        if (!IsAvailable) return null;
        if (!SupportsVision || imagePaths is null || imagePaths.Count == 0)
            return await ChatAsync(systemPrompt, userPrompt, temperature, maxTokens, ct);

        var content = new List<object> { new { type = "text", text = userPrompt } };
        foreach (var rel in imagePaths)
        {
            if (string.IsNullOrWhiteSpace(rel)) continue;
            var full = Path.Combine(AppContext.BaseDirectory, "data", rel);
            if (!File.Exists(full)) continue;
            byte[] bytes;
            try { bytes = await File.ReadAllBytesAsync(full, ct); }
            catch { continue; }
            if (bytes.Length == 0) continue;
            var b64 = Convert.ToBase64String(bytes);
            var mime = rel.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
            content.Add(new { type = "image_url", image_url = new { url = $"data:{mime};base64,{b64}" } });
        }

        // 没有任何可用图片：退回纯文本
        if (content.Count == 1)
            return await ChatAsync(systemPrompt, userPrompt, temperature, maxTokens, ct);

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
                    new { role = "user", content }
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
