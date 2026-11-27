namespace CogniThink.TutorBrain.Api.Controllers;

using System.IO;
using Microsoft.AspNetCore.Mvc;
using CogniThink.TutorBrain.Application.Agent;
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
    private readonly LlmProxyService _llm;
    private readonly SolveStructuredEnricher _enricher;

    public GatewayController(LlmGatewayService gateway, LlmProxyService llm, SolveStructuredEnricher enricher)
    {
        _gateway = gateway;
        _llm = llm;
        _enricher = enricher;
    }

    private const string OcrSystemPrompt =
        "你是高中化学题目 OCR 助手。把图片中的化学题目文字准确转写为纯文本。" +
        "要求：1) 完整保留题干、所有选项（A/B/C/D…）、化学公式、角标、单位、反应条件；" +
        "2) 化学式中的下标、上标一律用普通 ASCII 数字表示（如 Na2CO3、H2O、CO2、Ca2+），" +
        "不要输出 ₂ ₃ ⁴ 等 Unicode 上下标字符；" +
        "3) 不要解题、不要给答案、不要添加任何解释、问候、标题或前后缀；" +
        "4) 只输出题目本身的文字。";

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
    /// 追加：HTTP 层 best-effort 富化 structured（数值/守恒/pH/反应轨迹），不进判分路径。
    /// </summary>
    [HttpPost("solve")]
    public async Task<IActionResult> Solve([FromBody] ProcessRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { message = "text 不能为空" });

        var result = await _gateway.ProcessStudentMessageAsync(request.Text, ct: ct);
        var enriched = _enricher.Enrich(result, request.Text);
        return Ok(GatewaySolveProjector.Project(result, enriched));
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

    /// <summary>拍照 OCR：上传题目图片，返回抽取的题干文本（供前端编辑后再求解）。</summary>
    [HttpPost("ocr-photo")]
    [RequestFormLimits(MultipartBodyLengthLimit = 10_000_000)]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> OcrPhoto(IFormFile? file, CancellationToken ct)
    {
        var (ok, bytes, mime, err) = await ReadImageAsync(file, ct);
        if (!ok) return BadRequest(new { message = err });

        var text = await _llm.ChatWithImageBytesAsync(
            OcrSystemPrompt,
            "请提取图片中的高中化学题目文本。",
            new[] { (mime, bytes) },
            temperature: 0.1f, maxTokens: 1200, ct: ct);

        return Ok(new { text = NormalizeOcrText(text), available = _llm.SupportsVision });
    }

    /// <summary>拍照一键解题：OCR 抽题干 → 网关求解 → 返回学生面视图。快速尝鲜，建议核对 OCR 文本。</summary>
    [HttpPost("solve-photo")]
    [RequestFormLimits(MultipartBodyLengthLimit = 10_000_000)]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> SolvePhoto(IFormFile? file, CancellationToken ct)
    {
        var (ok, bytes, mime, err) = await ReadImageAsync(file, ct);
        if (!ok) return BadRequest(new { message = err });

        var extracted = await _llm.ChatWithImageBytesAsync(
            OcrSystemPrompt,
            "请提取图片中的高中化学题目文本。",
            new[] { (mime, bytes) },
            temperature: 0.1f, maxTokens: 1200, ct: ct);

        var normalized = NormalizeOcrText(extracted);
        if (string.IsNullOrWhiteSpace(normalized))
            return Ok(new { ocrText = string.Empty, available = _llm.SupportsVision, note = "未能从图片中识别出题目文本，请换一张清晰图片或手动输入" });

        var result = await _gateway.ProcessStudentMessageAsync(normalized, ct: ct);
        var enriched = _enricher.Enrich(result, normalized);
        return Ok(new { ocrText = normalized, view = GatewaySolveProjector.Project(result, enriched) });
    }

    /// <summary>
    /// OCR 文本归一化兜底：把模型可能输出的 Unicode 上下标/全角数字转成 ASCII，
    /// 保证下游化学引擎（按 ASCII 解析化学式）能识别 Na2CO3 / Ca2+ 等。
    /// </summary>
    private static readonly Dictionary<char, char> OcrCharNorm = new()
    {
        ['₀'] = '0', ['₁'] = '1', ['₂'] = '2', ['₃'] = '3', ['₄'] = '4',
        ['₅'] = '5', ['₆'] = '6', ['₇'] = '7', ['₈'] = '8', ['₉'] = '9',
        ['⁰'] = '0', ['¹'] = '1', ['²'] = '2', ['³'] = '3', ['⁴'] = '4',
        ['⁵'] = '5', ['⁶'] = '6', ['⁷'] = '7', ['⁸'] = '8', ['⁹'] = '9',
        ['⁺'] = '+', ['⁻'] = '-',
        ['０'] = '0', ['１'] = '1', ['２'] = '2', ['３'] = '3', ['４'] = '4',
        ['５'] = '5', ['６'] = '6', ['７'] = '7', ['８'] = '8', ['９'] = '9',
    };

    private static string NormalizeOcrText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            if (OcrCharNorm.TryGetValue(chars[i], out var rep))
                chars[i] = rep;
        return new string(chars).Trim();
    }

    private static async Task<(bool Ok, byte[] Bytes, string Mime, string? Error)> ReadImageAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return (false, Array.Empty<byte>(), string.Empty, "未收到图片文件");
        if (file.Length > 10 * 1024 * 1024)
            return (false, Array.Empty<byte>(), string.Empty, "图片过大（≤10MB）");
        var mime = file.ContentType ?? string.Empty;
        if (!mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return (false, Array.Empty<byte>(), string.Empty, "仅支持图片文件");

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        return (true, ms.ToArray(), mime, null);
    }
}
