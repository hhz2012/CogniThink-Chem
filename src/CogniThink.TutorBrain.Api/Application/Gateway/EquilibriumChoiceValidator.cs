using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// Tier 2 / T2：电离平衡 / 胶体 / 电解质概念选择题逐选项校验。
/// <para>真值源：<c>data/highschool/equilibrium-rules.json</c>（化学陈述→真假）。</para>
/// <para>仅当：① 所有选项都能被规则唯一命中（每个选项恰好匹配一条规则）；
/// ② 问法方向（正确 / 错误）可判；③ 满足方向的选项恰好唯一 → 返回答案字母。否则返回 null。</para>
/// <para>红线：不蒙数、零假阳性。任一选项无法唯一判定则整题不臆断。</para>
/// </summary>
public static class EquilibriumChoiceValidator
{
    private const string ConstraintNote =
        "【高中限定】电离平衡/胶体/电解质概念判定基于 data/highschool/equilibrium-rules.json 事实规则库；" +
        "四个选项均被规则唯一命中且答案唯一时才断言，否则返回 null。";

    private static readonly Lazy<RuleIndex> _index = new(BuildIndex);

    private sealed class RuleIndex
    {
        public List<EquilibriumRule> Rules { get; set; } = new();
    }

    /// <summary>尝试对一道电离平衡/胶体/电解质概念选择题唯一求解；不能唯一判定返回 null。</summary>
    public static ChoiceVerifier.Outcome? TryDecide(EngineRequest req)
    {
        var raw = req.RawText ?? string.Empty;
        var (stem, opts) = ChoiceVerifier.ParseOptions(raw);
        if (opts.Count < 2) return null;

        var ask = AskDir(stem);
        if (ask != "correct" && ask != "wrong") return null;

        var letters = opts.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        var normStem = ClassificationValidator.Norm(stem);
        var idx = _index.Value;

        var verdicts = new Dictionary<string, bool>();
        var reasons = new List<string>();
        // B-LlmMain T2b：收集每选项命中的规则 Id（equilibrium-rules.json 显式 id），用于可溯源推理节点（R4）。
        var optionHits = new List<ChoiceReasoningNodes.OptionHit>();
        foreach (var L in letters)
        {
            var opt = ClassificationValidator.Norm(opts[L]);
            var matched = MatchRules(opt, normStem, idx);
            if (matched.Count != 1) return null;          // 零条或多条命中 → 不臆断
            var rule = matched[0];
            verdicts[L] = rule.Verdict;
            reasons.Add($"{L}. {(rule.Verdict ? "成立" : "不成立")}（{rule.Id}）：{rule.Reason}");
            var hitIds = new List<string>();
            if (!string.IsNullOrWhiteSpace(rule.Id)) hitIds.Add(rule.Id);
            optionHits.Add(new ChoiceReasoningNodes.OptionHit(L, hitIds));
        }

        var candidates = ask == "correct"
            ? letters.Where(l => verdicts[l]).ToList()
            : letters.Where(l => !verdicts[l]).ToList();

        if (candidates.Count != 1) return null;

        var letter = candidates[0];
        var dirWord = ask == "correct" ? "正确" : "错误";
        reasons.Add($"依据平衡/概念事实规则库，{dirWord}选项唯一为 {letter}。");
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

    /// <summary>找出选项命中的全部规则（matchAll 全包含 且 stemAll 全包含）。</summary>
    private static List<EquilibriumRule> MatchRules(string normOpt, string normStem, RuleIndex idx)
    {
        var hits = new List<EquilibriumRule>();
        foreach (var rule in idx.Rules)
        {
            if (rule.MatchAll.Count == 0) continue;
            if (!rule.MatchAll.All(k => normOpt.Contains(ClassificationValidator.Norm(k), StringComparison.Ordinal)))
                continue;
            if (rule.StemAll.Count > 0 &&
                !rule.StemAll.All(k => normStem.Contains(ClassificationValidator.Norm(k), StringComparison.Ordinal)))
                continue;
            hits.Add(rule);
        }
        return hits;
    }

    /// <summary>问法方向：选正确项 / 选错误项；无法判定返回 "?"。</summary>
    private static string AskDir(string stem)
    {
        if (Regex.IsMatch(stem, @"错误|不正确|不涉及|没有涉及|不能证明|不能说明|不属于|不互为|没有关联|无关联"))
            return "wrong";
        if (stem.Contains("的是", StringComparison.Ordinal))
            return "correct";
        return "?";
    }

    private static RuleIndex BuildIndex()
    {
        var data = RuleStore.Load().Query<EquilibriumRuleData>("equilibrium-rules");
        return new RuleIndex { Rules = data.Rules };
    }
}
