namespace CogniThink.TutorBrain.Application.ScaffoldGenerator;

using CogniThink.Chem.Core.Balancing;

// ============================================================
// Task 1: 链选择器 —— 题目 → ScaffoldModuleKind。
// MVP 只映射 redox-reasoning（引擎真值最齐全）；其余模块 Task 8 推广。
// 映射优先级：引擎判定（redox.Success && IsRedox）> 题干关键词。
// 无法映射 → null（整题不发布）。映射表可单测、可扩展。
// ============================================================

public sealed class ModuleSelector : IModuleSelector
{
    public ScaffoldModuleKind? Select(QuestionCase @case, EngineArtifacts artifacts)
    {
        // 1) 引擎氧化还原判定是权威信号
        if (EngineArtifactsBuilder.GetRedox(artifacts) is { Success: true, IsRedox: true })
            return ScaffoldModuleKind.RedoxReasoning;

        // 2) 题干关键词兜底（引擎未命中时，给氧化还原题一次机会）
        var q = (@case.Question ?? string.Empty);
        if (ContainsRedoxKeyword(q))
            return ScaffoldModuleKind.RedoxReasoning;

        // 其余模块（ionic-system / aqueous-equilibrium / sequential-reaction / electrochem-reasoning）
        // Task 8 推广；当前未知 → null（整题不发布，绝不硬塞不匹配的链）。
        return null;
    }

    private static bool ContainsRedoxKeyword(string q)
    {
        foreach (var kw in RedoxKeywords)
            if (q.Contains(kw, StringComparison.Ordinal))
                return true;
        return false;
    }

    // 可扩展：后续推广到其它模块时，在此追加关键词或新增判定分支。
    private static readonly string[] RedoxKeywords =
    {
        "氧化", "还原", "氧化剂", "还原剂", "电子", "化合价", "价态",
        "氧化性", "还原性", "被氧化", "被还原", "歧化", "归中"
    };
}
