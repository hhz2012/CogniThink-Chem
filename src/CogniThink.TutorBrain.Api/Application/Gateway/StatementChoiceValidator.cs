using System;
using System.Collections.Generic;
using System.Linq;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 单选择陈述校验路径（spec redox-statement-judge-pdf075 FR-4 / Task 6）：逐选项经
/// <see cref="IStatementJudge"/>（共享 <see cref="MultiSelectChoiceValidator.StatementJudge"/>，含事实库 /
/// 计数 / 氧还电子计数 / 氧还反应计数 / 氧还角色比值 / 结构周期律）判定真伪。
/// <para>
/// 与 <see cref="MultiSelectChoiceValidator"/> 共用同一套逐选项判定与问法方向口径，仅在候选数阈值上不同：
/// <b>全部</b>选项非 <see cref="Verdict.Undecided"/> 且满足问法方向的候选<b>唯一</b> → 返回该字母；
/// 候选 ≥2 → 返回 <c>null</c>（交由 MultiSelect 拼接多答案）；任一选项 Undecided 或候选为 0 → <c>null</c>。
/// </para>
/// <para>
/// 红线（不蒙数、零假阳性）：任一选项不可判即整题弃判；不抢跑在既有单选校验器之前，已解题字节级不变。
/// 接在 <c>StoichiometryChoiceValidator</c> 之后、<c>MultiSelectChoiceValidator</c> 之前。
/// </para>
/// </summary>
internal static class StatementChoiceValidator
{
    private const string ConstraintNote =
        "【高中限定】单选择陈述路径：逐选项经事实库 / 计数 / 氧化还原电子计数 / 氧还反应计数 / 氧还角色比值 / 结构周期律原语判定；" +
        "任一选项不可判或满足问法方向的候选不唯一则不臆断。候选≥2 时交多选 fallback。";

    /// <summary>
    /// 逐选项经共享聚合判定器判定；全部非 Undecided 且满足问法方向的候选唯一 → 返回该字母；否则 null。
    /// </summary>
    public static ChoiceVerifier.Outcome? TryDecide(EngineRequest req)
    {
        var raw = req.RawText ?? string.Empty;
        var (stem, opts) = ChoiceVerifier.ParseOptions(raw);
        if (opts.Count < 2) return null;

        var askWrong = MultiSelectChoiceValidator.AskWrong(stem);
        var letters = opts.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

        var verdicts = new Dictionary<string, Verdict>(letters.Count);
        var reasons = new List<string>(letters.Count);
        var optionHits = new List<ChoiceReasoningNodes.OptionHit>(letters.Count);
        foreach (var L in letters)
        {
            var (v, hitIds) = MultiSelectChoiceValidator.StatementJudge.JudgeWithIds(stem, opts[L]);
            if (v == Verdict.Undecided) return null;   // 任一选项不可判 → 整题 null，不蒙数
            verdicts[L] = v;
            reasons.Add($"{L}：{(v == Verdict.True ? "正确" : "错误")}（IStatementJudge 判定）");
            optionHits.Add(new ChoiceReasoningNodes.OptionHit(L, hitIds ?? new List<string>()));
        }

        // 候选阈值：单选要求恰好唯一；≥2 交 MultiSelect；0 不蒙数。
        var candidates = askWrong
            ? letters.Where(L => verdicts[L] == Verdict.False).ToList()
            : letters.Where(L => verdicts[L] == Verdict.True).ToList();
        if (candidates.Count != 1) return null;

        var letter = candidates[0];
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
}
