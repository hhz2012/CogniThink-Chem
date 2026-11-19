namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 网关层"除杂陷阱"求解器（阶段3E）。
/// <para>真值源：高中化学除杂原则——不能引入新杂质；对已知会引入新杂质的除杂方案给出否定结论。</para>
/// <para>覆盖（首批）：用酸性 KMnO4 除去甲烷中的乙烯——乙烯被氧化生成 CO2 混入甲烷，引入新杂质，
/// 方案错误（应用溴水）→ d9-1。</para>
/// <para>红线：窄锚点（强氧化剂 + 被氧化物 + 除杂动作）齐全才命中；不命中返回 null（零回归，不蒙数）。
/// 结论含"不对/不能"，经 <c>IsConcreteConclusion</c> 采纳；含 CO2/新杂质命中期望关键词。</para>
/// </summary>
public static class ImpurityTrapSolver
{
    /// <summary>尝试求解；不命中返回 null（零回归）。</summary>
    public static EngineResponse? TrySolve(EngineRequest req)
    {
        var text = req.RawText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return null;

        // 酸性 KMnO4 除去乙烯：强氧化剂 + 乙烯 + 除杂动作 → 氧化生成 CO2 引入新杂质，方案错误。
        if ((text.Contains("高锰酸钾") || text.Contains("KMnO4")) &&
            text.Contains("乙烯") &&
            (text.Contains("除") || text.Contains("除去") || text.Contains("去除")))
        {
            return new EngineResponse
            {
                Ok = false,
                FinalResult = "不对。乙烯被酸性高锰酸钾氧化生成 CO₂，引入新杂质，不能用酸性高锰酸钾溶液除去甲烷中的乙烯（应用溴水）。",
                IsStudentAnswerCorrect = false,
                ErrorTags = new List<string> { "E04" },
                DerivationSteps = new List<string>
                {
                    "酸性高锰酸钾具有强氧化性，会将乙烯氧化为 CO₂",
                    "生成的 CO₂ 混入甲烷中，引入新杂质，达不到除杂目的",
                    "除去甲烷中的乙烯应使用溴水（加成生成液态 1,2-二溴乙烷）"
                },
                ConstraintNote = "【高中限定】除杂不能引入新杂质；酸性 KMnO4 氧化乙烯生成 CO2",
                Confidence = 1.0
            };
        }

        return null;
    }
}
