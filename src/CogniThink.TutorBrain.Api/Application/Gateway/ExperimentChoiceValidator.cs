namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// Tier 3：实验安全/操作/装置、电化学腐蚀、周期律、化学变化、方程式正误、计量概念等
/// 选择题确定性校验器（事实库查表，batch2 choice 子集）。
/// <para>真值源：<c>data/highschool/experiment-facts.json</c>。</para>
/// <para>
/// 仅当全部选项都能被事实库干净命中（每选项恰好 1 条）且答案唯一时断言；
/// 任一选项无法干净判定或不唯一则返回 null。红线：不蒙数、零假阳性。
/// </para>
/// </summary>
public static class ExperimentChoiceValidator
{
    private const string FactKey = "experiment-facts";
    private const string SourceTag = "data/highschool/experiment-facts.json";

    /// <summary>尝试对一道实验/综合选择题唯一求解；不能唯一判定返回 null。</summary>
    public static ChoiceVerifier.Outcome? TryDecide(EngineRequest req)
        => FactChoiceMatcher.TryDecide(req, FactKey, SourceTag);
}
