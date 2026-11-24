namespace CogniThink.TutorBrain.Application.ScaffoldGenerator;

using System.Text;
using System.Text.Json;

// ============================================================
// Task 3: LLM 干扰项补充（用户裁决：确定性库 + LLM 补充）。
// 包裹一个基础 IDistractorProvider（默认 StaticDistractorProvider），LLM 可用时追加
// 与题干相关的干扰项候选。红线：
//   - LLM 可能把 correct 当干扰项吐出 → 本类先按 correct 过滤一次（防御），
//     OptionsBuilder 再做最终单点过滤（!correctSet.Contains），双重保险。
//   - LLM 任何失败 / 不可用 / 不合规 → 仅返回基础库结果，绝不抛错、绝不蒙数。
// 注意：IDistractorProvider.GetDistractors 是同步接口（Task 2 已定），而 LLM 调用异步。
// 生成器运行在离线批量控制台（Task 5），无同步上下文，阻塞等待安全；不进入在线热路径。
// ============================================================

public sealed class LlmDistractorProvider : IDistractorProvider
{
    private readonly IDistractorProvider _inner;
    private readonly IScaffoldLlmClient _llm;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true,
    };

    public LlmDistractorProvider(IDistractorProvider inner, IScaffoldLlmClient llm)
    {
        _inner = inner;
        _llm = llm;
    }

    public IReadOnlyList<string> GetDistractors(AtomicTaskKind kind, StepCorrect correct, EngineArtifacts artifacts)
    {
        var result = new HashSet<string>(_inner.GetDistractors(kind, correct, artifacts), StringComparer.Ordinal);
        if (!_llm.IsAvailable) return result.ToList();

        var correctSet = ScaffoldGeneratorUtils.CorrectSet(correct);

        string? raw;
        try
        {
            raw = Task.Run(() => _llm.ChatAsync(SystemPrompt(), UserPrompt(kind, correct, artifacts), 0.2f, 200, default))
                .GetAwaiter().GetResult();
        }
        catch
        {
            return result.ToList();
        }
        if (string.IsNullOrWhiteSpace(raw)) return result.ToList();

        foreach (var d in ParseDistractors(raw))
        {
            if (string.IsNullOrWhiteSpace(d)) continue;
            var v = d.Trim();
            if (correctSet.Contains(v)) continue;   // 防御：LLM 把 correct 吐成干扰项 → 丢弃
            result.Add(v);
        }
        return result.ToList();
    }

    private static List<string> ParseDistractors(string raw)
    {
        int start = raw.IndexOf('[');
        int end = raw.LastIndexOf(']');
        if (start < 0 || end <= start) return new List<string>();
        var json = raw.Substring(start, end - start + 1);
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOpts) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static string SystemPrompt() => """
你是高中化学练习题的干扰项生成助手。为指定步骤生成 2~4 个「似是而非的错误候选项」，用于选择题迷惑学生。
严格规则：
1. 只输出一个 JSON 字符串数组，如 ["H2O","Na+"]，不得输出其它文本。
2. 候选项必须与该步同类（元素 / 物种 / 产物 / 选项字母），且贴合题干。
3. 禁止输出正确答案本身；禁止编造与题干完全无关的内容。
4. 若没有合适的干扰项，输出空数组 []。
""";

    private static string UserPrompt(AtomicTaskKind kind, StepCorrect correct, EngineArtifacts artifacts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("【题干】").AppendLine(artifacts?.Case?.Question ?? "");
        sb.AppendLine($"【步骤】{kind} —— {ScaffoldReleaseTemplates.Title(kind)}");
        var correctSet = ScaffoldGeneratorUtils.CorrectSet(correct);
        sb.AppendLine($"【正确答案（禁止作为干扰项输出）】{string.Join("、", correctSet.OrderBy(x => x, StringComparer.Ordinal))}");
        sb.AppendLine("请输出 2~4 个干扰项的 JSON 字符串数组。");
        return sb.ToString();
    }
}
