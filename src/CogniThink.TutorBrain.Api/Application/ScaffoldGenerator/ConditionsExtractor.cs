namespace CogniThink.TutorBrain.Application.ScaffoldGenerator;

// ============================================================
// Task 1 (P0-2): C 步约束条件提取 —— 对题干做确定性关键词扫描。
// 铁律：只取明确命中的关键词；无命中 → 返回 null（C 降级为只读揭示），绝不猜测。
// 命中多个维度时取优先级最高者作为 C 步所问维度：
//   quantity（用量）> medium（介质）> concentration（浓度）> temperature（温度）。
// ============================================================

/// <summary>C 步约束条件提取结果（有 provenance）。</summary>
internal sealed class ConditionExtraction
{
    public BranchConditions Conditions { get; }
    public string PrimaryDimension { get; }
    public string CorrectKey { get; }
    public string Provenance { get; }

    public ConditionExtraction(BranchConditions conditions, string primaryDimension, string correctKey, string provenance)
    {
        Conditions = conditions;
        PrimaryDimension = primaryDimension;
        CorrectKey = correctKey;
        Provenance = provenance;
    }
}

/// <summary>C 步各维度的固定候选选项目录（key, label），不依赖干扰项生成。</summary>
internal static class ConditionOptionCatalog
{
    public static readonly IReadOnlyDictionary<string, (string Key, string Label)[]> ByDimension =
        new Dictionary<string, (string, string)[]>(StringComparer.Ordinal)
        {
            ["quantity"] = new (string, string)[]
            {
                ("little", "少量"), ("excess", "过量"), ("equal", "等物质的量"), ("none", "不影响"),
            },
            ["medium"] = new (string, string)[]
            {
                ("acidic", "酸性"), ("basic", "碱性"), ("neutral", "中性"),
            },
            ["concentration"] = new (string, string)[]
            {
                ("concentrated", "浓"), ("dilute", "稀"),
            },
            ["temperature"] = new (string, string)[]
            {
                ("heat", "加热/高温"), ("room", "常温"),
            },
        };
}

internal static class ConditionsExtractor
{
    public static ConditionExtraction? Extract(string? question)
    {
        if (string.IsNullOrWhiteSpace(question)) return null;
        var q = question;
        var conditions = new BranchConditions();

        // 优先级 1：用量（少量 / 过量 / 足量 / 等物质的量）
        if (q.Contains("等物质的量", StringComparison.Ordinal))
        {
            conditions.Quantity = "等物质的量";
            return new ConditionExtraction(conditions, "quantity", "equal", "QuestionCase.Question(keyword:等物质的量)");
        }
        if (q.Contains("少量", StringComparison.Ordinal))
        {
            conditions.Quantity = "少量";
            return new ConditionExtraction(conditions, "quantity", "little", "QuestionCase.Question(keyword:少量)");
        }
        if (q.Contains("过量", StringComparison.Ordinal) || q.Contains("足量", StringComparison.Ordinal))
        {
            conditions.Quantity = "过量";
            return new ConditionExtraction(conditions, "quantity", "excess", "QuestionCase.Question(keyword:过量/足量)");
        }

        // 优先级 2：介质（酸性 / 碱性 / 中性）
        if (q.Contains("酸性", StringComparison.Ordinal) || q.Contains("酸溶液", StringComparison.Ordinal))
        {
            conditions.Medium = "酸性";
            return new ConditionExtraction(conditions, "medium", "acidic", "QuestionCase.Question(keyword:酸性)");
        }
        if (q.Contains("碱性", StringComparison.Ordinal) || q.Contains("碱溶液", StringComparison.Ordinal))
        {
            conditions.Medium = "碱性";
            return new ConditionExtraction(conditions, "medium", "basic", "QuestionCase.Question(keyword:碱性)");
        }
        if (q.Contains("中性", StringComparison.Ordinal))
        {
            conditions.Medium = "中性";
            return new ConditionExtraction(conditions, "medium", "neutral", "QuestionCase.Question(keyword:中性)");
        }

        // 优先级 3：浓度（浓 / 稀）
        if (q.Contains("浓", StringComparison.Ordinal))
        {
            conditions.Concentration = "浓";
            return new ConditionExtraction(conditions, "concentration", "concentrated", "QuestionCase.Question(keyword:浓)");
        }
        if (q.Contains("稀", StringComparison.Ordinal))
        {
            conditions.Concentration = "稀";
            return new ConditionExtraction(conditions, "concentration", "dilute", "QuestionCase.Question(keyword:稀)");
        }

        // 优先级 4：温度（加热 / 高温 / 常温）
        if (q.Contains("加热", StringComparison.Ordinal) || q.Contains("高温", StringComparison.Ordinal))
        {
            conditions.Temperature = "加热";
            return new ConditionExtraction(conditions, "temperature", "heat", "QuestionCase.Question(keyword:加热/高温)");
        }
        if (q.Contains("常温", StringComparison.Ordinal) || q.Contains("室温", StringComparison.Ordinal))
        {
            conditions.Temperature = "常温";
            return new ConditionExtraction(conditions, "temperature", "room", "QuestionCase.Question(keyword:常温/室温)");
        }

        return null;
    }
}
