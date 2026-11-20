namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// Tier 2：有机化学选择题校验器（官能团性质 / 反应类型 / 同分异构 / 同系物 / 高分子 / 材料）。
/// <para>真值源：<c>data/highschool/organic-facts.json</c>。</para>
/// <para>
/// 仅当全部选项都能被事实库干净命中且答案唯一时断言；含结构图、多选、计算或任一选项无法判定时返回 null。
/// 红线：不蒙数、零假阳性。
/// </para>
/// </summary>
public static class OrganicChoiceValidator
{
    private const string FactKey = "organic-facts";
    private const string SourceTag = "data/highschool/organic-facts.json";

    /// <summary>尝试对一道有机化学选择题唯一求解；不能唯一判定返回 null。</summary>
    public static ChoiceVerifier.Outcome? TryDecide(EngineRequest req)
        => FactChoiceMatcher.TryDecide(req, FactKey, SourceTag);
}
