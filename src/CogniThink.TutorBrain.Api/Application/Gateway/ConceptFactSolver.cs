using System;
using System.Collections.Generic;
using System.Linq;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 网关层概念事实确定性求解器（阶段6-B / AC-24）。
/// <para>真值源：<c>data/highschool/concept-facts.json</c>（唯一），不调用 LLM 推理。</para>
/// <para>覆盖：concept / unknown 中的纯事实题——离子检验试剂/方法、离子水溶液颜色、概念定义、晶体熔点比较。</para>
/// <para>红线（不蒙数）：归一化题干后逐条匹配触发条件，<b>唯一命中</b>才返回结论；
/// 0 命中（查不到）或 &gt;1 命中（歧义）一律返回 <c>null</c>。评估侧仅采纳 <c>IsAnswerCorrect</c> 通过的结论。
/// 逻辑零硬编码化学事实：所有触发条件与答案均在 JSON。</para>
/// </summary>
public static class ConceptFactSolver
{
    // 事实库进程内缓存（RuleStore 也缓存，但跨实例不共享；此处静态 Lazy 保证全进程只加载一次）。
    private static readonly Lazy<ConceptFactData> Data = new(
        () => RuleStore.Load().Query<ConceptFactData>("concept-facts"));

    /// <summary>尝试求解；不命中返回 null（零回归）。</summary>
    public static EngineResponse? TrySolve(EngineRequest req)
    {
        var text = !string.IsNullOrWhiteSpace(req.RawText) ? req.RawText : (req.StudentAnswer ?? string.Empty);
        if (string.IsNullOrWhiteSpace(text)) return null;

        // 选择题由专门的选择校验器链路处理；概念事实求解器不介入选择题形态的文本，
        // 避免选项/题干中的化学关键词（如 Fe3+ + 颜色）误触发概念事实，造成跨求解器交叉污染。
        var (_, opts) = ChoiceVerifier.ParseOptions(text);
        if (opts.Count >= 2) return null;

        try
        {
            var norm = Normalize(text);
            ConceptFactEntry? match = null;
            var count = 0;
            foreach (var e in Data.Value.Entries)
            {
                if (!Matches(e, norm)) continue;
                match = e;
                if (++count > 1) return null; // 歧义 → 不蒙数
            }
            if (count != 1 || match is null) return null; // 0 命中 → 查不到

            return new EngineResponse
            {
                Ok = true,
                FinalResult = match.Answer,
                DerivationSteps = match.Derivation,
                ReasoningNodes = new List<ReasoningNode>
                {
                    new()
                    {
                        Step = 1,
                        Description = "概念事实库唯一命中",
                        Premises = new List<string> { "题干归一化后唯一匹配触发条件（RequiredAll 全中且 RequiredAny 命中，无歧义）" },
                        KnowledgeId = string.IsNullOrEmpty(match.Id) ? null : match.Id,
                        Conclusion = match.Answer,
                        EvidenceLevel = EvidenceLevel.KnowledgeFact
                    }
                },
                ConstraintNote = string.IsNullOrEmpty(match.Constraint)
                    ? "【概念事实库】高中限定"
                    : match.Constraint,
                Confidence = 1.0
            };
        }
        catch
        {
            // 任何意外异常都不拖垮网关链路；保守回退 null（零回归）
            return null;
        }
    }

    /// <summary>触发条件匹配：RequiredAll 全部命中 且 RequiredAny（若非空）至少命中其一。</summary>
    private static bool Matches(ConceptFactEntry e, string norm)
    {
        foreach (var r in e.RequiredAll)
            if (string.IsNullOrEmpty(r) || !norm.Contains(r, StringComparison.Ordinal))
                return false;

        if (e.RequiredAny.Count > 0)
        {
            var any = false;
            foreach (var r in e.RequiredAny)
            {
                if (string.IsNullOrEmpty(r)) continue;
                if (norm.Contains(r, StringComparison.Ordinal)) { any = true; break; }
            }
            if (!any) return false;
        }
        return true;
    }

    /// <summary>
    /// 归一化题干：上下标字符→数字、全角→半角、去电荷记号 ^、转小写，
    /// 使 Fe³⁺/Fe3+/Fe^3+ 归一为同一形式，与 JSON 中 required 子串对齐。
    /// </summary>
    private static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var sb = new System.Text.StringBuilder(input.Length);
        foreach (var c in input)
        {
            char nc = c switch
            {
                '₀' => '0', '₁' => '1', '₂' => '2', '₃' => '3', '₄' => '4',
                '₅' => '5', '₆' => '6', '₇' => '7', '₈' => '8', '₉' => '9',
                '⁰' => '0', '¹' => '1', '²' => '2', '³' => '3', '⁴' => '4',
                '⁵' => '5', '⁶' => '6', '⁷' => '7', '⁸' => '8', '⁹' => '9',
                '⁺' => '+', '⁻' => '-',
                '－' => '-', '−' => '-', '﹣' => '-',
                _ => c
            };
            if (nc >= 0xFF01 && nc <= 0xFF5E) nc = (char)(nc - 0xFEE0); // 全角 ASCII → 半角
            if (nc == 0x3000) nc = ' ';                                    // 全角空格
            if (nc == '^') continue;                                       // 去电荷记号：SO4^2- → SO42-
            sb.Append(nc);
        }
        return sb.ToString().ToLowerInvariant();
    }
}
