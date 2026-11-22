using System.Text.RegularExpressions;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// P1-2 多部分大题：(N) 编号标记检测与子问切分的共享工具。
/// 标记口径与 MultiSubCorrectnessSolver.SubMarker 一致：仅匹配「括号 + 纯数字 + 括号」，
/// 天然排除 (2025·北京卷) / [Cu(NH3)2] / (FeSO4·7H2O) 等非子问标记。
/// 网关与评估侧共用：检测器决定是否触发分发，评估侧据此对聚合结论走 AND 判分。
/// </summary>
public static class MultiPartUtility
{
    /// <summary>多部分分发聚合结论的 task_type 标记；评估侧据此走 AND 判分与采纳门。</summary>
    public const string MultiPartTaskType = "multi_part";

    // 仅匹配 (1)（1） 这类纯数字编号；不匹配 (2025·...)（数字后非闭括号）、[Cu(NH3)2]（括号内非纯数字）。
    private static readonly Regex SubMarker = new(@"[(（]\s*(\d+)\s*[)）]", RegexOptions.Compiled);

    /// <summary>是否为多部分大题：(N) 数字编号标记 ≥ 2 个。</summary>
    public static bool IsMultiPart(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return SubMarker.Matches(text).Count >= 2;
    }

    /// <summary>
    /// 按 (N) 标记切分子问，返回有序 [(序号, 子问文本)]。
    /// 子问文本已剥去前导 (N) 标记（便于逐子问求解）；序号保留用于聚合时重排。
    /// 标记前的大题引语不计入子问。非多部分文本返回空列表。
    /// </summary>
    public static List<(int Index, string Text)> SplitSubQuestions(string? text)
    {
        var result = new List<(int, string)>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var matches = SubMarker.Matches(text);
        for (int i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            if (!int.TryParse(m.Groups[1].Value, out var idx)) continue;
            int start = m.Index + m.Length;    // 跳过 "(N)" 标记本身（等价于 Match.End，兼容目标框架）
            int end = (i + 1 < matches.Count) ? matches[i + 1].Index : text.Length;
            if (end <= start) continue;
            var seg = text[start..end].Trim();
            if (seg.Length == 0) continue;
            result.Add((idx, seg));
        }
        return result;
    }
}
