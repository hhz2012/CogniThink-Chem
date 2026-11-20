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
    private static readonly Regex WrongDir = new(
        @"错误|不正确|不相符|不符|不具有对应|不能达到|无法达到|不能实现|不能用来|不属于|不合理|不可行|不能说明|不能证明|不能用于|没有关联",
        RegexOptions.Compiled);

    // 聚合真值源：事实库（四个库）+ 计数 + 氧化还原电子转移计数（Task 4）。
    private static readonly CompositeStatementJudge Judge =
        new(new FactBaseStatementJudge(), new CountingStatementJudge(), new RedoxElectronStatementJudge());

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
        foreach (var L in letters)
        {
            var v = Judge.Judge(stem, opts[L]);
            if (v == Verdict.Undecided) return null;   // 任一选项不可判 → 整题 null，不蒙数
            verdicts[L] = v;
            reasons.Add($"{L}：{(v == Verdict.True ? "正确" : "错误")}（IStatementJudge 判定）");
        }

        var candidates = askWrong
            ? letters.Where(L => verdicts[L] == Verdict.False).ToList()
            : letters.Where(L => verdicts[L] == Verdict.True).ToList();

        if (candidates.Count < 2) return null;   // 多选需 ≥2 候选；不足 → 不蒙数

        var letter = string.Concat(candidates.OrderBy(L => L, StringComparer.Ordinal));
        var dirWord = askWrong ? "错误" : "正确";
        return new ChoiceVerifier.Outcome(
            AnswerLetter: letter,
            FinalResult: $"{dirWord}选项为 {letter}",
            Reasons: reasons,
            ConstraintNote: ConstraintNote);
    }
}
