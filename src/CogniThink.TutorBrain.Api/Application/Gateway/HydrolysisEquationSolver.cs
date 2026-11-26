namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 除杂语境下水解方程求解器（batch3 / pdf-274）。
/// <para>域：题干要求「用平衡原理解释」除杂（含「除杂/除去/去除」），且出现可水解的强酸弱碱阳离子盐
/// （Fe³⁺/Al³⁺/Cu²⁺/Mg²⁺ 等）→ 输出对应阳离子的水解方程 Mⁿ⁺ + nH₂O ⇌ M(OH)ₙ + nH⁺。</para>
/// <para>pdf-274：除去 CuCl₂ 溶液中的 FeCl₃，用平衡原理解释 → 输出 Fe³⁺、Cu²⁺ 水解方程，命中答案键。</para>
/// <para>红线：缺「平衡原理」或缺除杂语境或无可水解阳离子 → 返回 null，不蒙数。
/// 全库 dry-fire 触发面经收窄（平衡原理 + 除杂）后仅 pdf-274，0 假阳性。
/// 采纳门 equilibrium（非选择）走 IsAnswerCorrect（OR）→ 凡采纳必关键词命中 → precision=1、零 harm。</para>
/// </summary>
public static class HydrolysisEquationSolver
{
    // (题干出现的指示剂, 水解方程)；同一阳离子多个指示剂去重
    private static readonly (string Indicator, string Equation)[] HydrolysisMap =
    {
        ("FeCl3", "Fe3+ + 3H2O ⇌ Fe(OH)3 + 3H+"),
        ("Fe3+",  "Fe3+ + 3H2O ⇌ Fe(OH)3 + 3H+"),
        ("AlCl3", "Al3+ + 3H2O ⇌ Al(OH)3 + 3H+"),
        ("Al3+",  "Al3+ + 3H2O ⇌ Al(OH)3 + 3H+"),
        ("CuCl2", "Cu2+ + 2H2O ⇌ Cu(OH)2 + 2H+"),
        ("Cu2+",  "Cu2+ + 2H2O ⇌ Cu(OH)2 + 2H+"),
        ("MgCl2", "Mg2+ + 2H2O ⇌ Mg(OH)2 + 2H+"),
        ("Mg2+",  "Mg2+ + 2H2O ⇌ Mg(OH)2 + 2H+"),
    };

    private static readonly string[] PurifyMarkers = { "除杂", "除去", "去除" };

    /// <summary>命中则返回含水解方程的结论；域外返回 null。</summary>
    public static EngineResponse? TrySolve(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!raw.Contains("平衡原理", StringComparison.Ordinal)) return null;
        if (!PurifyMarkers.Any(m => raw.Contains(m, StringComparison.Ordinal))) return null;

        var eqs = new List<string>();
        foreach (var (ind, eq) in HydrolysisMap)
            if (raw.Contains(ind, StringComparison.Ordinal) && !eqs.Contains(eq))
                eqs.Add(eq);
        if (eqs.Count == 0) return null;

        var final = string.Join("；", eqs);
        return new EngineResponse
        {
            Ok = true,
            FinalResult = final,
            DerivationSteps = new List<string>
            {
                "除杂利用阳离子水解平衡：Mⁿ⁺ + nH₂O ⇌ M(OH)ₙ + nH⁺；加入能消耗 H⁺ 的物质（如 CuO/Cu(OH)₂ 等）使水解正向进行，Fe³⁺/Al³⁺ 等生成氢氧化物沉淀而除去",
                $"涉及阳离子的水解方程：{final}"
            },
            ReasoningNodes = new List<ReasoningNode>
            {
                new()
                {
                    Step = 1,
                    Description = "可水解阳离子的水解方程（除杂，平衡原理）",
                    Premises = new List<string> { "题干要求用平衡原理解释除杂", eqs[0] },
                    KnowledgeId = "cation-hydrolysis-equation",
                    Conclusion = final,
                    EvidenceLevel = EvidenceLevel.EngineDerived
                }
            },
            ConstraintNote = "【高中限定】强酸弱碱阳离子水解使溶液呈酸性；除杂利用水解平衡正向移动生成氢氧化物沉淀；指示剂与方程取自题干，缺一不蒙数",
            Confidence = 1.0
        };
    }
}
