using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 多选判定 fallback（spec FR-1 / Task 2）：在 <c>CallChoice</c> 瀑布链末端、
/// <see cref="ChoiceVerifier.TryDecide"/> 兜底之前，仅当前面所有单选校验器都返回 null 时触发。
/// 不依赖题干「双选/多选」显式标记。
/// <para>
/// 逐选项调用 <see cref="IStatementJudge"/>（事实库匹配 + 计数；Task 4 接入氧化还原电子计数）；
/// 问法方向="选正确"且 ≥2 个选项被确定为真（或"选错误"且 ≥2 个被确定为假）→ 返回拼接字母串（如 "AC"），
/// 走既有 <c>TryAdoptMultiSelectConclusion</c> + <c>IsAnswerCorrect</c> 判分；任一选项不可判
/// （判定器返回 Undecided）或满足方向的候选不足 2 项 → 返回 null，不蒙数。
/// </para>
/// <para>
/// 红线：零假阳性、零 harm；置于末端 fallback，不抢在既有单选校验器之前，已解题字节级不变（TR-2.1）。
/// </para>
/// </summary>
internal static class MultiSelectChoiceValidator
{
    private const string ConstraintNote =
        "【高中限定】多选 fallback：逐选项经事实库 / 计数 / 氧化还原电子计数（Task 4）确定判定；" +
        "任一选项不可判或满足方向的候选不足 2 项则不臆断。不依赖题干「双选/多选」标记。";

    // 选错误项的问法标记（与 FactChoiceMatcher.WrongDir 同口径，避免性质选择题误伤）。
    // 「不规范」全库仅 pdf-544 题干命中，零暴露面。
    // 「不能完成」全库仅 pdf-406 题干命中（气体发生收集装置，单选选不能完成的 C 项）；
    // 加入后 MultiSelect 按选错方向仅 C 为假（候选 1 < 2 → 返回 null），避免 EF-406 事实
    // 使 A/B/D 判真而被误凑成多选 ABD（harm）。与 FactChoiceMatcher.WrongDir 保持同步。
    private static readonly Regex WrongDir = new(
        @"错误|不正确|不相符|不符|不具有对应|不能完成|不能达到|无法达到|不能实现|不能用来|不能用|不属于|不合理|不可行|不能说明|不能证明|不能用于|没有关联|不规范",
        RegexOptions.Compiled);

    // 计数题问法：题干问「正确的有几项/几个/个数」。命中时若选项均为具体陈述（非「N 项/N 个/全部」），
    // 则在逐选项可判后输出计数数字 N（如「正确的有 3 项」），而非字母串，以匹配数字答案。
    // 「N 项/N 个」选项形式由前置 CountingChoiceValidator 路径一覆盖，此处排除避免重复。
    private static readonly Regex CountOfCorrectQ = new(
        @"正确的有\s*(几|[0-9]+)\s*(项|个)|正确的有(几项|几个)|正确的个数|下列叙述正确的有",
        RegexOptions.Compiled);
    private static readonly Regex NumericCountOption = new(
        @"^\s*[0-9一二三四五六七八九十]+\s*(项|个)\s*$|全部", RegexOptions.Compiled);

    // 聚合真值源：事实库（四个库）+ 计数 + 氧化还原电子转移计数（Task 4）
    // + 氧还反应计数（FR-2）+ 氧还角色/比值（FR-3）
    // + 结构/周期律/化学键比较原语（电子排布 / VSEPR 键角 / 晶体类型 / 配位键）。
    // internal：供 StatementChoiceValidator 共享，保证单选择陈述路径（FR-4）与多选 fallback
    // 逐选项判定口径完全一致，二者仅在候选数阈值（1 vs ≥2）上不同。
    internal static readonly CompositeStatementJudge StatementJudge =
        new(new FactBaseStatementJudge(), new CountingStatementJudge(), new RedoxElectronStatementJudge(),
            new RedoxCountStatementJudge(), new RedoxRoleRatioStatementJudge(),
            new ElectronConfigStatementJudge(), new VseprBondAngleStatementJudge(),
            new CrystalTypeStatementJudge(), new CoordinateBondStatementJudge());

    /// <summary>问法方向：题干要求选「错误/不正确…」项 → true（选 verdict==False 的选项）。与 WrongDir 同口径。</summary>
    internal static bool AskWrong(string stem) => WrongDir.IsMatch(stem);

    /// <summary>尝试多选求解；不能干净判定返回 null（任一选项 Undecided 或候选不足 2 项）。</summary>
    public static ChoiceVerifier.Outcome? TryDecide(EngineRequest req)
    {
        var rawText = req.RawText ?? string.Empty;
        var (stem, opts) = ChoiceVerifier.ParseOptions(rawText);
        if (opts.Count < 2) return null;

        var askWrong = WrongDir.IsMatch(stem);
        var letters = opts.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

        var verdicts = new Dictionary<string, Verdict>(letters.Count);
        var reasons = new List<string>(letters.Count);
        // B-LlmMain T2b：收集每选项命中的事实 Id（经 CompositeStatementJudge.JudgeWithIds；引擎子判决返回空 id，R4）。
        var optionHits = new List<ChoiceReasoningNodes.OptionHit>();
        foreach (var L in letters)
        {
            var (v, hitIds) = StatementJudge.JudgeWithIds(stem, opts[L]);
            if (v == Verdict.Undecided) return null;   // 任一选项不可判 → 整题 null，不蒙数
            verdicts[L] = v;
            reasons.Add($"{L}：{(v == Verdict.True ? "正确" : "错误")}（IStatementJudge 判定）");
            optionHits.Add(new ChoiceReasoningNodes.OptionHit(L, hitIds ?? new List<string>()));
        }

        var candidates = askWrong
            ? letters.Where(L => verdicts[L] == Verdict.False).ToList()
            : letters.Where(L => verdicts[L] == Verdict.True).ToList();

        // 计数题数字输出：题干问「正确的有几项」且选项为具体陈述（非「N 项/N 个」）→ 输出计数 N，
        // 以匹配数字答案（如 pdf-263 answer="3"）。复用上方已完成的逐选项判定（任一不可判已 return null），不蒙数。
        if (LooksLikeCountOfCorrectQuestion(stem, opts))
        {
            var n = candidates.Count;
            var countDir = askWrong ? "错误" : "正确";
            var countResult = $"{countDir}的有 {n} 项";
            return new ChoiceVerifier.Outcome(
                AnswerLetter: n.ToString(),
                FinalResult: countResult,
                Reasons: reasons,
                ConstraintNote: ConstraintNote)
            {
                ReasoningNodes = ChoiceReasoningNodes.Build(optionHits, countResult)
            };
        }

        if (candidates.Count < 2) return null;   // 多选需 ≥2 候选；不足 → 不蒙数

        var letter = string.Concat(candidates.OrderBy(L => L, StringComparer.Ordinal));
        var dirWord = askWrong ? "错误" : "正确";
        var finalResult = $"{dirWord}选项为 {letter}";
        return new ChoiceVerifier.Outcome(
            AnswerLetter: letter,
            FinalResult: finalResult,
            Reasons: reasons,
            ConstraintNote: ConstraintNote)
        {
            ReasoningNodes = ChoiceReasoningNodes.Build(optionHits, finalResult)
        };
    }

    /// <summary>计数题数字输出触发：题干问「正确的有几项/个数」且选项非「N 项/N 个/全部」形式。</summary>
    private static bool LooksLikeCountOfCorrectQuestion(string stem, Dictionary<string, string> opts)
    {
        if (!CountOfCorrectQ.IsMatch(stem)) return false;
        // 选项均为「N 项/N 个/全部」→ 由前置 CountingChoiceValidator 路径一覆盖，不重复处理
        if (opts.Values.All(v => NumericCountOption.IsMatch(v))) return false;
        return true;
    }
}
