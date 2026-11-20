using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// Tier 2 事实驱动选择题校验器的共享内核（供 OrganicChoiceValidator / StructurePeriodicValidator 复用）。
/// <para>
/// 真值源：<c>data/highschool/*.json</c> 事实库。每条事实给出：
/// <list type="bullet">
/// <item><c>match</c>：归一化后必须全部出现在选项文本中的子串；</item>
/// <item><c>stemMatch</c>：归一化后必须全部出现在题干中的子串（可选，用于把短选项限定到本题，防跨题误命中）；</item>
/// <item><c>verdict</c>：该选项陈述是否成立（true=正确，false=错误）。</item>
/// </list>
/// </para>
/// <para>
/// 红线（不蒙数、零假阳性）：仅当 ① 问法方向可判（选正确项 / 选错误项）；
/// ② 每个选项都恰好命中一条事实（0 条或 ≥2 条都视为无法干净判定）；
/// ③ 满足方向的候选满足题量要求：单选恰好唯一，双选/多选（题干标注「双选」/「多选」）≥2 →
/// 才返回答案字母（双选/多选返回拼接字母串，如 "CD"）。否则返回 null。
/// </para>
/// </summary>
internal static class FactChoiceMatcher
{
    private sealed class FactData
    {
        public string Version { get; set; } = "";
        public string Source { get; set; } = "";
        public List<FactStatement> Statements { get; set; } = new();
    }

    private sealed class FactStatement
    {
        public string Id { get; set; } = "";
        public List<string> Match { get; set; } = new();
        public List<string> StemMatch { get; set; } = new();
        public bool Verdict { get; set; }
        public string Reason { get; set; } = "";
    }

    // 选错误项：题干含这些标记 → 选 verdict==false 的选项。
    // 注意：「不可能共平面的是」「属于极性分子的是」等是选满足某性质的项，属 correct 方向，
    // 故仅匹配明确要求选「错误/不正确/不符/不相符/不能达到」的标记，避免误伤性质选择题。
    // 「不相符」须单列：「不符」是连续两字子串，「不相符」中间隔了「相」字，不会被「不符」命中。
    // 扩展标记均为无歧义的「选非/选错」问法：不属于 / 不合理 / 不可行 / 不能说明 / 不能证明 /
    // 不能用于 / 没有关联。刻意不加宽泛的「无关」「不能」等，避免误伤性质选择题；任何回归由
    // Batch3ChoiceValidatorTests 全量 723 题的零假阳性 / 零伤害护栏兜底。
    private static readonly Regex WrongDir = new(
        @"错误|不正确|不相符|不符|不具有对应|不能达到|无法达到|不能实现|不能用来|不属于|不合理|不可行|不能说明|不能证明|不能用于|没有关联", RegexOptions.Compiled);

    /// <summary>
    /// 选项/题干归一化：先复用 <see cref="ClassificationValidator.Norm"/>（上下标→ASCII、全角→半角、去空白），
    /// 再把 em dash（— U+2014）、en dash（– U+2013）、减号（− U+2212）统一成连字符 '-'。
    /// 原因：题干/选项常出现 C—H / Si—Si 等用破折号书写的化学键，而事实库 match 关键词用 ASCII '-'，
    /// <see cref="ClassificationValidator.Norm"/> 不做这层转换，会导致本应命中的选项漏判。
    /// </summary>
    private static string NormOpt(string s)
    {
        var n = ClassificationValidator.Norm(s);
        return n.Replace('—', '-').Replace('–', '-').Replace('−', '-');
    }

    /// <summary>
    /// 关键词子串匹配，但当关键词以数字开头时要求数字前边界：
    /// 避免 "6种" 命中 "16种"、"2" 命中 "12" 等纯计数选项的跨数字子串歧义，
    /// 否则一个选项会命中多条事实，触发红线整题弃判（如 pdf-393 三取代苯计数）。
    /// 关键词以数字结尾时不设后边界——化学式/离子式（如 2CrO4、C3H8N6）后常接电荷数字，
    /// 不应被误判为同一数字的延续。非数字开头关键词行为与 string.Contains 完全一致。
    /// </summary>
    private static bool OptContains(string text, string keyword)
    {
        if (keyword.Length == 0) return false;
        var kwStartIsDigit = char.IsDigit(keyword[0]);
        var idx = text.IndexOf(keyword, StringComparison.Ordinal);
        while (idx >= 0)
        {
            var prevIsDigit = idx > 0 && char.IsDigit(text[idx - 1]);
            if (!kwStartIsDigit || !prevIsDigit)
                return true;
            idx = text.IndexOf(keyword, idx + 1, StringComparison.Ordinal);
        }
        return false;
    }

    /// <summary>
    /// 用指定事实库对一道选择题唯一求解。不能唯一判定返回 null。
    /// </summary>
    /// <param name="req">题干+选项渲染文本。</param>
    /// <param name="factKey">事实库文件名（不含 .json），位于 data/highschool/ 下。</param>
    /// <param name="sourceTag">写入 ConstraintNote 的来源标签。</param>
    public static ChoiceVerifier.Outcome? TryDecide(EngineRequest req, string factKey, string sourceTag)
    {
        var rawText = req.RawText ?? string.Empty;
        var (stem, opts) = ChoiceVerifier.ParseOptions(rawText);
        if (opts.Count < 2) return null;

        var askDir = WrongDir.IsMatch(stem) ? "wrong" : "correct";

        var facts = RuleStore.Load().Query<FactData>(factKey)?.Statements;
        if (facts is null || facts.Count == 0) return null;

        var stemN = NormOpt(stem);
        var letters = opts.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

        var verdicts = new Dictionary<string, bool>(letters.Count);
        var reasons = new List<string>(letters.Count);
        foreach (var L in letters)
        {
            var optN = NormOpt(opts[L]);
            var matched = facts
                .Where(f => f.Match.Count > 0
                            && f.Match.All(k => OptContains(optN, NormOpt(k)))
                            && f.StemMatch.All(k => OptContains(stemN, NormOpt(k))))
                .ToList();

            // 红线：任一选项无法干净判定（0 条或 ≥2 条命中）→ 整题返回 null，不蒙数。
            if (matched.Count != 1) return null;

            verdicts[L] = matched[0].Verdict;
            reasons.Add($"{L}：{(matched[0].Verdict ? "正确" : "错误")}（{matched[0].Reason}）");
        }

        // 双选/多选：题干显式标注「双选」或「多选」时，允许返回多个满足方向的候选字母；
        // 未标注的题一律按单选处理（候选必须唯一），避免改变既有单选行为。
        var isMultiSelect = Regex.IsMatch(stem, @"双选|多选");
        var candidates = askDir == "correct"
            ? letters.Where(L => verdicts[L]).ToList()
            : letters.Where(L => !verdicts[L]).ToList();

        string letter;
        if (isMultiSelect)
        {
            // 双选/多选：候选 ≥2 才断言；仅 1 个或 0 个 → 事实不足或题目标记存疑，不蒙数。
            if (candidates.Count < 2) return null;
            letter = string.Concat(candidates.OrderBy(L => L, StringComparer.Ordinal));
        }
        else
        {
            if (candidates.Count != 1) return null;   // 单选：唯一解才断言
            letter = candidates[0];
        }

        var dirWord = askDir == "correct" ? "正确" : "错误";
        var constraint = isMultiSelect
            ? $"【高中限定】基于 {sourceTag} 事实库判定（双选/多选）；任一选项无法干净判定或候选不足则不臆断。"
            : $"【高中限定】基于 {sourceTag} 事实库唯一判定；任一选项无法干净判定或不唯一则不臆断。";
        return new ChoiceVerifier.Outcome(
            AnswerLetter: letter,
            FinalResult: $"{dirWord}选项为 {letter}",
            Reasons: reasons,
            ConstraintNote: constraint);
    }
}
