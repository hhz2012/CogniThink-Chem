namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// Tier 2：物质结构与元素周期律 / 化学用语选择题校验器
/// （同位素 / 同素异形体 / 晶体类型 / 化学用语 / 周期律推断 / 分子极性与键角）。
/// <para>真值源：<c>data/highschool/structure-periodic-facts.json</c>。</para>
/// <para>
/// 仅当全部选项都能被事实库干净命中且答案唯一时断言；含晶胞图、谱图、多选、计算或任一选项无法判定时返回 null。
/// 红线：不蒙数、零假阳性。
/// </para>
/// </summary>
public static class StructurePeriodicValidator
{
    private const string FactKey = "structure-periodic-facts";
    private const string SourceTag = "data/highschool/structure-periodic-facts.json";

    /// <summary>尝试对一道结构/周期律/化学用语选择题唯一求解；不能唯一判定返回 null。</summary>
    public static ChoiceVerifier.Outcome? TryDecide(EngineRequest req)
        => FactChoiceMatcher.TryDecide(req, FactKey, SourceTag);
}
