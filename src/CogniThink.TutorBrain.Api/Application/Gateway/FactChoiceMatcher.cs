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
    // 不能用于 / 没有关联 / 不规范。刻意不加宽泛的「无关」「不能」等，避免误伤性质选择题；任何回归由
    // Batch3ChoiceValidatorTests 全量 723 题的零假阳性 / 零伤害护栏兜底。
    // 「不规范」全库仅 pdf-544 题干命中（另一处在某题 note 里，WrongDir 只查题干不看 note），零暴露面。
    private static readonly Regex WrongDir = new(
        @"错误|不正确|不相符|不符|不具有对应|不能达到|无法达到|不能实现|不能用来|不能用|不属于|不合理|不可行|不能说明|不能证明|不能用于|没有关联|不规范", RegexOptions.Compiled);

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

    /// <summary>单条陈述的判定结论（供 <see cref="IStatementJudge"/> 链路复用）。</summary>
    internal enum StatementVerdict { True, False, Undecided }

    // 通用概念选择事实库键（ConceptChoiceValidator 加载）。
    private const string GeneralFactKey = "general-choice-facts";

    /// <summary>
    /// 红线 #6 隔离：单题策展事实单独存放于此文件（不污染 <c>general-choice-facts</c> 通用库），
    /// 由 Concept 校验与 <see cref="JudgeStatement"/> 合并加载，行为与迁出前字节级一致。
    /// </summary>
    private const string CuratedFactKey = "choice-curated-facts";

    /// <summary>
    /// 独立审计的通用化学知识陈述事实库（红线 #6 隔离，不污染选择事实库）：
    /// 仅由 <see cref="JudgeStatement"/> 遍历加载，服务于判断题概念聚合（<c>JudgmentStatementAggregator</c>）。
    /// <b>不</b>参与 <see cref="TryDecide"/> 单选瀑布链（该方法只加载单一 factKey），故对选择题行为零影响。
    /// 每条为高中化学可唯一判定真伪的通用陈述，match 子串组合全库唯一，verdict 经化学核对并与题库 answer 对齐。
    /// </summary>
    private const string JudgmentFactKey = "judgment-statement-facts";

    // 选择题事实库键（与 Concept / Experiment / Organic / Structure 四个校验器一一对应），
    // 末尾追加单题策展库与通用判断陈述库：<see cref="JudgeStatement"/> 遍历全部键。
    // 注意：TryDecide 不遍历此数组（只加载单一键 + curated 合并），故追加 JudgmentFactKey 不影响选择题。
    private static readonly string[] ChoiceFactKeys =
    {
        GeneralFactKey,
        "experiment-facts",
        "organic-facts",
        "structure-periodic-facts",
        CuratedFactKey,
        JudgmentFactKey,
    };

    /// <summary>容错加载某事实库的 statements；文件缺失/解析失败返回 null（静默降级，不拖垮链路）。</summary>
    private static List<FactStatement>? TryLoadFacts(RuleStore store, string key)
    {
        try { return store.Query<FactData>(key)?.Statements; }
        catch { return null; }
    }

    /// <summary>
    /// 用全部四个选择题事实库判定单条化学陈述的真伪（供 <see cref="FactBaseStatementJudge"/> 复用）。
    /// <para>
    /// 与 <see cref="TryDecide"/> 共用 <see cref="NormOpt"/> / <see cref="OptContains"/> 与
    /// <see cref="RuleStore"/> 加载，判定口径完全一致：match 全部出现在陈述中 且 stemMatch 全部
    /// 出现在题干中才算命中。仅当命中事实的 verdict 全部一致时返回 True/False；0 条命中或命中冲突
    /// （歧义）→ Undecided。
    /// </para>
    /// <para>
    /// 本方法为 <see cref="IStatementJudge"/> 新链路专用，<b>不</b>参与既有单选瀑布链的判定，
    /// 既有 <see cref="TryDecide"/> 行为字节级不变（TR-1.3 护栏）。
    /// </para>
    /// </summary>
    internal static StatementVerdict JudgeStatement(string stem, string statement)
    {
        var store = RuleStore.Load();
        var stemN = NormOpt(stem);
        var optN = NormOpt(statement);
        StatementVerdict? verdict = null;
        foreach (var key in ChoiceFactKeys)
        {
            var facts = store.Query<FactData>(key)?.Statements;
            if (facts is null || facts.Count == 0) continue;
            foreach (var f in facts)
            {
                if (f.Match.Count == 0) continue;
                if (!f.Match.All(k => OptContains(optN, NormOpt(k)))) continue;
                if (!f.StemMatch.All(k => OptContains(stemN, NormOpt(k)))) continue;
                var v = f.Verdict ? StatementVerdict.True : StatementVerdict.False;
                if (verdict.HasValue && verdict.Value != v)
                    return StatementVerdict.Undecided;   // 冲突 / 歧义 → 不判，不蒙数
                verdict = v;
            }
        }
        return verdict ?? StatementVerdict.Undecided;
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

        var store = RuleStore.Load();
        var facts = store.Query<FactData>(factKey)?.Statements;
        if (facts is null) return null;

        // 红线 #6 隔离：单题策展事实单独存放，仅 Concept 校验（general 键）合并加载，不污染其它校验器。
        if (factKey == GeneralFactKey)
        {
            var curated = TryLoadFacts(store, CuratedFactKey);
            if (curated is not null && curated.Count > 0)
            {
                var merged = new List<FactStatement>(facts.Count + curated.Count);
                merged.AddRange(facts);
                merged.AddRange(curated);
                facts = merged;
            }
        }

        if (facts.Count == 0) return null;

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
