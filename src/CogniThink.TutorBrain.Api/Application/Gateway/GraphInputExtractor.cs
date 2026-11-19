using System.Text.RegularExpressions;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 从题干渲染文本中为 graph 引擎提取输入：选项物质组、问法极性、候选物种。
/// 红线：只做字符串解析，不做任何化学推断；解析不出时返回空，由调用方如实放弃（不蒙数）。
/// </summary>
public static class GraphInputExtractor
{
    // 行首选项标记：A. / B．/ C、（与 ChoiceVerifier / 确定性分类器保持一致）
    private static readonly Regex OptionMarker = new(@"(^|\n)\s*(?<letter>[A-D])[\.．、]\s", RegexOptions.Compiled);

    // 化学式 token：大写字母开头 + 字母/数字/括号/上下标/电荷
    private static readonly Regex SpeciesToken = new(@"^[A-Z][A-Za-z0-9()²³₀-₉^+\-]*$", RegexOptions.Compiled);

    /// <summary>解析选择题选项，返回 选项字母 → 物质组（按出现顺序）。</summary>
    public static Dictionary<string, List<string>> ExtractOptionGroups(string rawText)
    {
        var result = new Dictionary<string, List<string>>();
        if (string.IsNullOrWhiteSpace(rawText)) return result;

        var matches = OptionMarker.Matches(rawText);
        for (var i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            var letter = m.Groups["letter"].Value;
            var start = m.Index + m.Length;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : rawText.Length;
            var text = rawText[start..end].Trim();
            var species = text.Split(new[] { '、', '，', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => SpeciesToken.IsMatch(s))
                .Distinct()
                .ToList();
            result[letter] = species;
        }
        return result;
    }

    /// <summary>
    /// 问法极性：选"符合/正确"项 → feasible（取唯一可行项）；
    /// 选"不符合/错误"项 → infeasible（取唯一不可行项）。判不出返回 "?"。
    /// 否定词优先检测，避免"不符合"因含"符合"被误判。
    /// </summary>
    public static string AskPolarity(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "?";
        if (text.Contains("不符合") || text.Contains("错误") || text.Contains("不正确")
            || text.Contains("不能") || text.Contains("无法"))
            return "infeasible";
        if (text.Contains("符合") || text.Contains("正确") || text.Contains("可以") || text.Contains("能"))
            return "feasible";
        return "?";
    }

    /// <summary>
    /// 从文本中提取候选物种（Assign 模式用）。过滤掉图占位标签与选项字母 A-D。
    /// 仅当题干枚举了候选物种时才有效；否则返回不足，调用方应放弃（不蒙数）。
    /// </summary>
    public static List<string> ExtractCandidates(string text, IEnumerable<string>? graphLabels = null)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return result;
        var labels = new HashSet<string>(graphLabels ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
        var tokens = Regex.Split(text, @"[^A-Za-z0-9()²³₀-₉^+\-]+");
        foreach (var tok in tokens)
        {
            if (string.IsNullOrEmpty(tok)) continue;
            if (!SpeciesToken.IsMatch(tok)) continue;
            if (tok.Length == 1 && tok[0] >= 'A' && tok[0] <= 'D') continue; // 选项字母
            if (labels.Contains(tok)) continue;                              // 图占位标签
            if (!result.Contains(tok)) result.Add(tok);
        }
        return result;
    }
}
