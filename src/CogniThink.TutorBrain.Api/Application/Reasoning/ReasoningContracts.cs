namespace CogniThink.TutorBrain.Application.Reasoning;

using CogniThink.TutorBrain.Domain;

/// <summary>单步推理判定结果（由规则引擎产出，LLM 不参与）</summary>
public sealed record StepEvaluation(
    StepVerdict Verdict,
    ErrorTag ErrorTag,
    string Reason,
    IReadOnlyList<string> MissingKeywords)
{
    public static StepEvaluation Ungradable(string reason) =>
        new(StepVerdict.Ungradable, ErrorTag.None, reason, Array.Empty<string>());
}

/// <summary>领域推理引擎抽象（化学规则引擎实现）</summary>
public interface IReasoningEngine
{
    Subject Subject { get; }

    Task<StepEvaluation> EvaluateAsync(
        Problem problem,
        ProblemStep step,
        string studentStep,
        CancellationToken cancellationToken = default);
}

/// <summary>文本规范化工具：全半角、箭头、空格、上下标</summary>
public static class AnswerNormalizer
{
    private static readonly Dictionary<char, char> FullWidth = new()
    {
        ['＋'] = '+', ['－'] = '-', ['＝'] = '=', ['（'] = '(', ['）'] = ')',
        ['，'] = ',', ['。'] = '.', ['：'] = ':', ['；'] = ';', ['∠'] = '∠',
        ['△'] = '△', ['→'] = '→', ['↑'] = '↑', ['↓'] = '↓'
    };

    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var chars = input.Trim()
            .Select(c => FullWidth.TryGetValue(c, out var mapped)
                ? mapped
                : c >= 'Ａ' && c <= 'Ｚ' ? (char)(c - 0xFEE0)
                : c >= 'ａ' && c <= 'ｚ' ? (char)(c - 0xFEE0)
                : c >= '０' && c <= '９' ? (char)(c - 0xFEE0)
                : c)
            .ToArray();
        var text = new string(chars);
        text = text.Replace("＝", "=")
            .Replace("⟶", "→")
            .Replace("—>", "→")
            .Replace("=>", "→")
            .Replace("↑", string.Empty)
            .Replace("↓", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("\t", string.Empty);
        return text.ToLowerInvariant();
    }

    /// <summary>
    /// 分割关键词，支持同义词分组。
    /// 约定：
    /// - "/" 分隔同义词（同组内命中一个即可），如 "KSCN/硫氰化钾"
    /// - "," 分隔必需要素组（每组都需要命中），如 "Fe3+/铁离子,含有"
    /// 返回 List<string[]>，每个 string[] 是一个同义词组。
    /// </summary>
    public static List<string[]> SplitKeywordGroups(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new List<string[]>();
        var groups = raw.Split(new[] { ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string[]>();
        foreach (var g in groups)
        {
            var synonyms = g.Split(new[] { '/', '、' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(k => Normalize(k))
                .Where(k => k.Length > 0)
                .ToArray();
            if (synonyms.Length > 0) result.Add(synonyms);
        }
        return result;
    }

    public static string[] SplitKeywords(string? raw)
    {
        return SplitKeywordGroups(raw).SelectMany(g => g).ToArray();
    }
}
