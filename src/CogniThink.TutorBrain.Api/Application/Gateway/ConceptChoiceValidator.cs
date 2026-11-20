namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// Tier 3 / batch3：概念辨析、物质性质与用途、化学用语、化学与生活/能源/材料等
/// 纯事实正误选择题确定性校验器（事实库查表）。
/// <para>真值源：<c>data/highschool/general-choice-facts.json</c>。</para>
/// <para>
/// 仅当全部选项都能被事实库干净命中（每选项恰好 1 条）且答案唯一时断言；
/// 任一选项无法干净判定或不唯一则返回 null。红线：不蒙数、零假阳性。
/// 返回 null 时穿透回退原路径，对其余题零影响（零回归由构造保证）。
/// </para>
/// </summary>
public static class ConceptChoiceValidator
{
    private const string FactKey = "general-choice-facts";
    private const string SourceTag = "data/highschool/general-choice-facts.json";

    /// <summary>尝试对一道概念/综合选择题唯一求解；不能唯一判定返回 null。</summary>
    public static ChoiceVerifier.Outcome? TryDecide(EngineRequest req)
        => FactChoiceMatcher.TryDecide(req, FactKey, SourceTag);
}
