namespace CogniThink.TutorBrain.Application.ScaffoldGenerator;

using System.Text;
using System.Text.Json;

// ============================================================
// Task 3: 文案生成器。LLM 写 releases 四档；LLM 不可用 / 输出不合规 / 超时 → 回退模板。
// 红线（结构 + 提示词 + 校验三重保证，LLM 绝不碰 correct）：
//   1. 结构隔离：本类只产出 StepScaffoldReleases（hint / probe / scaffold / reveal），
//      不含任何 correct 字段——结构上 LLM 输出无法灌进 correct；correct 只来自引擎（IStepTruthExtractor）。
//   2. 提示词强约束：把 correct 作为「已知事实」注入上下文，禁止 LLM 改写 / 否定 / 写入文案，
//      信息不足用通用引导语，禁止编造题干外化学事实（呼应「强上下文注入 + 字段级约束」经验）。
//   3. 任何异常 / 空 / 不合规 JSON → 整步回退模板，四档齐全永不崩。
// ============================================================

public sealed class ReleasesGenerator : IReleasesGenerator
{
    private readonly IScaffoldLlmClient _llm;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true,
    };

    /// <param name="llm">null 或不可用时恒走模板回退（离线可用）。</param>
    public ReleasesGenerator(IScaffoldLlmClient? llm = null)
        => _llm = llm ?? new NullScaffoldLlmClient();

    public async Task<StepScaffoldReleases> GenerateAsync(
        AtomicTaskKind kind,
        StepCorrect? correct,
        EngineArtifacts artifacts,
        CancellationToken ct = default)
    {
        artifacts ??= new EngineArtifacts();
        var fallback = ScaffoldReleaseTemplates.BuildFallback(kind, correct, artifacts);
        if (!_llm.IsAvailable) return fallback;

        string? raw;
        try
        {
            raw = await _llm
                .ChatAsync(SystemPrompt(), UserPrompt(kind, correct, artifacts), 0.3f, 600, ct)
                .ConfigureAwait(false);
        }
        catch
        {
            return fallback;
        }
        if (string.IsNullOrWhiteSpace(raw)) return fallback;

        return TryParseReleases(raw) ?? fallback;
    }

    // ---------- LLM 输出解析（字段级合规校验，不合规即整体回退） ----------

    private static StepScaffoldReleases? TryParseReleases(string raw)
    {
        var json = ExtractJsonObject(raw);
        if (json is null) return null;

        ReleasesDto? dto;
        try { dto = JsonSerializer.Deserialize<ReleasesDto>(json, JsonOpts); }
        catch { return null; }
        if (dto is null) return null;

        // 四档齐全且非空；probe.options 非空——任一缺失即整体回退（不发布半截文案）
        if (string.IsNullOrWhiteSpace(dto.Hint)) return null;
        if (dto.Probe is null || string.IsNullOrWhiteSpace(dto.Probe.Stem) || dto.Probe.Options is not { Count: > 0 }) return null;
        if (dto.Scaffold is null
            || string.IsNullOrWhiteSpace(dto.Scaffold.Correct?.Title)
            || string.IsNullOrWhiteSpace(dto.Scaffold.Wrong?.Title)) return null;
        if (dto.Reveal is null || string.IsNullOrWhiteSpace(dto.Reveal.Title) || string.IsNullOrWhiteSpace(dto.Reveal.Body)) return null;

        return new StepScaffoldReleases
        {
            Hint = dto.Hint.Trim(),
            Probe = new DiscriminationQuestion
            {
                Stem = dto.Probe.Stem.Trim(),
                Options = dto.Probe.Options.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()).ToList(),
            },
            Scaffold = new ContrastCard
            {
                Correct = new ContrastSide { Title = dto.Scaffold.Correct!.Title.Trim(), Detail = dto.Scaffold.Correct.Detail ?? "" },
                Wrong = new ContrastSide { Title = dto.Scaffold.Wrong!.Title.Trim(), Detail = dto.Scaffold.Wrong.Detail ?? "" },
            },
            Reveal = new RuleDrawer { Title = dto.Reveal.Title.Trim(), Body = dto.Reveal.Body.Trim() },
        };
    }

    /// <summary>从模型输出里抠出第一个 { ... } JSON 对象（模型偶尔包 ```json 或前后带说明）。</summary>
    private static string? ExtractJsonObject(string raw)
    {
        int start = raw.IndexOf('{');
        int end = raw.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        return raw.Substring(start, end - start + 1);
    }

    // ---------- 提示词 ----------

    private static string SystemPrompt() => """
你是高中化学阶梯式学习脚手架的文案撰写助手。你的唯一任务是为指定的一步练习撰写「阶梯释放文案」。
严格规则：
1. 你只能输出文案，禁止输出、改写或推断该步的正确答案（correct）。正确答案已作为「已知事实」提供，仅用于让文案贴合，你不得质疑或否定它，也不得把它写进任何文案字段。
2. 只允许输出一个 JSON 对象，字段严格为：hint（字符串）、probe（对象：stem 字符串 + options 字符串数组）、scaffold（对象：correct{title,detail} + wrong{title,detail}）、reveal（对象：title 字符串 + body 字符串）。不得输出其它字段、不得输出 JSON 以外的文本。
3. 文案必须基于「题干 + 已知事实 + 引擎推理节点」撰写；信息不足时用通用引导语，禁止编造题干未提及的化学事实、数值或物种。
4. hint：给答错学生的第一层提示（不直接给答案）；probe：一道辨析题（stem + 2~4 个 options）帮学生定位误区；scaffold：正反对比卡片（correct 侧=正确思路，wrong 侧=常见错误思路）；reveal：该步核心规则/方法的揭示。
5. 全部用中文，面向高中生，简洁。
""";

    private static string UserPrompt(AtomicTaskKind kind, StepCorrect? correct, EngineArtifacts artifacts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("【题干】").AppendLine(artifacts?.Case?.Question ?? "");
        sb.AppendLine($"【当前步骤】{kind} —— {ScaffoldReleaseTemplates.Title(kind)}");
        sb.AppendLine($"目标：{ScaffoldReleaseTemplates.Goal(kind)}");
        sb.AppendLine($"常见误区：{ScaffoldReleaseTemplates.Pitfall(kind)}");

        if (correct is not null)
        {
            var correctSet = ScaffoldGeneratorUtils.CorrectSet(correct);
            if (correctSet.Count > 0)
            {
                sb.AppendLine("【该步已知事实（正确答案，仅作贴合参考，禁止写入文案或否定）】");
                sb.AppendLine(string.Join("、", correctSet.OrderBy(x => x, StringComparer.Ordinal)));
            }
        }

        var ctx = ScaffoldReleaseTemplates.ReasoningContext(artifacts);
        if (!string.IsNullOrWhiteSpace(ctx)) sb.Append(ctx);

        sb.AppendLine("请输出该步的阶梯释放文案 JSON。");
        return sb.ToString();
    }

    // ---------- 离线无 LLM 时的空实现（构造器默认） ----------

    private sealed class NullScaffoldLlmClient : IScaffoldLlmClient
    {
        public bool IsAvailable => false;
        public Task<string?> ChatAsync(string s, string u, float t, int m, CancellationToken ct) => Task.FromResult<string?>(null);
    }

    // ---------- LLM 输出 JSON DTO（PropertyNameCaseInsensitive 已开，免显式映射） ----------

    private sealed class ReleasesDto
    {
        public string? Hint { get; set; }
        public ProbeDto? Probe { get; set; }
        public ScaffoldDto? Scaffold { get; set; }
        public RevealDto? Reveal { get; set; }
    }
    private sealed class ProbeDto { public string? Stem { get; set; } public List<string>? Options { get; set; } }
    private sealed class ScaffoldDto { public SideDto? Correct { get; set; } public SideDto? Wrong { get; set; } }
    private sealed class SideDto { public string? Title { get; set; } public string? Detail { get; set; } }
    private sealed class RevealDto { public string? Title { get; set; } public string? Body { get; set; } }
}
