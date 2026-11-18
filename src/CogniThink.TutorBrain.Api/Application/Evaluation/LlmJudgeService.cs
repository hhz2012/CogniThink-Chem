namespace CogniThink.TutorBrain.Application.Evaluation;

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CogniThink.TutorBrain.Application.Agent;
using CogniThink.TutorBrain.Domain;

/// <summary>
/// 大模型判定服务：让 LLM 直接判断学生答案是否正确。
/// 用于和化学规则引擎的判定结果做准确率对比。
/// 判定只输出 Correct / Incorrect / PartiallyCorrect 三选一，不参与教学。
/// </summary>
public sealed class LlmJudgeService
{
    private readonly HttpClient _http;
    private readonly LlmOptions _options;

    public LlmJudgeService(HttpClient http, LlmOptions options)
    {
        _http = http;
        _options = options;
    }

    public bool IsAvailable =>
        _options.Enabled
        && !string.IsNullOrWhiteSpace(_options.BaseUrl)
        && !string.IsNullOrWhiteSpace(_options.ApiKey);

    /// <summary>
    /// 让大模型判定学生答案是否正确。
    /// 返回判定结果及置信度。
    /// </summary>
    public async Task<LlmJudgeResult> JudgeAsync(
        string problemTitle,
        string stepPrompt,
        string studentAnswer,
        CancellationToken ct = default)
    {
        if (!IsAvailable)
        {
            return new LlmJudgeResult
            {
                Available = false,
                Verdict = StepVerdict.Ungradable,
                Reason = "大模型未配置（Llm:Enabled=false 或缺少 BaseUrl/ApiKey）"
            };
        }

        const string systemPrompt =
            "你是高中化学阅卷老师，严格按高中化学评分标准判断学生答案是否正确。\n" +
            "核心原则：高中化学只看关键化学事实是否正确，不苛求表述完整度。题目已提供全部前提，学生只需答出该步的核心结论。\n" +
            "判定标准：\n" +
            "- Correct：关键化学事实正确、方向对。简洁表述完全可以（如写试剂名、现象关键词、结论方向）。例如Fe³⁺检验写'变红''变红色'均算正确，不必写'血红色'。\n" +
            "- PartiallyCorrect：方向大致对但有明显不足——如缺少关键产物、关键条件确实缺失导致结论不成立、或混入部分错误信息。\n" +
            "- Incorrect：结论本身错误或与化学事实相反（如该含Fe³⁺却说不含、该不能共存却说能共存）。不要把'写得太简洁'当成Incorrect。\n" +
            "重要：仅因答案简洁就扣分是错误的；只有真正缺少关键化学要素或结论错误时才降级。\n" +
            "只输出 JSON：{\"verdict\":\"Correct|PartiallyCorrect|Incorrect\",\"reason\":\"简短理由\"}，不要多余文字。";

        var userPrompt =
            $"题目：{problemTitle}\n" +
            $"步骤问题：{stepPrompt}\n" +
            $"学生回答：{studentAnswer}\n" +
            $"请判定该回答是否正确。";

        try
        {
            var url = $"{_options.BaseUrl!.TrimEnd('/')}/chat/completions";
            var payload = new
            {
                model = _options.Model,
                temperature = 0.1,
                max_tokens = 150,
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
            if (!resp.IsSuccessStatusCode)
            {
                return new LlmJudgeResult
                {
                    Available = true,
                    Verdict = StepVerdict.Ungradable,
                    Reason = $"大模型返回错误状态码 {(int)resp.StatusCode}"
                };
            }

            var dto = await resp.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: cts.Token);
            var text = dto?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                return new LlmJudgeResult
                {
                    Available = true,
                    Verdict = StepVerdict.Ungradable,
                    Reason = "大模型返回空内容"
                };
            }

            return ParseVerdict(text);
        }
        catch (Exception ex)
        {
            return new LlmJudgeResult
            {
                Available = true,
                Verdict = StepVerdict.Ungradable,
                Reason = $"大模型调用异常：{ex.Message}"
            };
        }
    }

    private static LlmJudgeResult ParseVerdict(string text)
    {
        // 尝试从 JSON 中解析
        var verdict = StepVerdict.Ungradable;
        var reason = string.Empty;

        // 简单提取：找 verdict 字段
        var verdictMatch = System.Text.RegularExpressions.Regex.Match(
            text, @"verdict""\s*:\s*""(Correct|PartiallyCorrect|Incorrect)""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (verdictMatch.Success)
        {
            verdict = Enum.Parse<StepVerdict>(verdictMatch.Groups[1].Value, ignoreCase: true);
        }
        else
        {
            // 兜底：关键词匹配
            var lower = text.ToLowerInvariant();
            if (lower.Contains("partiallycorrect") || lower.Contains("partial"))
                verdict = StepVerdict.PartiallyCorrect;
            else if (lower.Contains("incorrect") || lower.Contains("错误"))
                verdict = StepVerdict.Incorrect;
            else if (lower.Contains("correct") || lower.Contains("正确"))
                verdict = StepVerdict.Correct;
        }

        // 提取 reason
        var reasonMatch = System.Text.RegularExpressions.Regex.Match(
            text, @"reason""\s*:\s*""([^""]+)""");
        if (reasonMatch.Success) reason = reasonMatch.Groups[1].Value;

        return new LlmJudgeResult
        {
            Available = true,
            Verdict = verdict,
            Reason = reason
        };
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

/// <summary>大模型判定结果</summary>
public sealed class LlmJudgeResult
{
    public bool Available { get; set; }
    public StepVerdict Verdict { get; set; }
    public string Reason { get; set; } = string.Empty;
}
