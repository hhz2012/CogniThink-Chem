namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 单条化学陈述的判定结论。<see cref="Undecided"/> 表示真值源无法干净判定——不蒙数，零假阳性。
/// </summary>
internal enum Verdict
{
    True,
    False,
    Undecided,
}

/// <summary>
/// 可复用的陈述判定原语接口：给定题干语境 <paramref name="stem"/> 与一条陈述
/// <paramref name="statement"/>，返回该陈述是否成立。多选判定器与通用组合/计数求解器共用此接口，
/// 把现有真值源（事实库匹配、计数、氧化还原电子转移等）统一聚合为同一调用口径。
/// <para>
/// 红线（不蒙数、零假阳性）：仅当原语能唯一判定时返回 <see cref="Verdict.True"/> /
/// <see cref="Verdict.False"/>；无法唯一判定一律 <see cref="Verdict.Undecided"/>。
/// </para>
/// <para>
/// 本接口仅作为新增校验器（MultiSelectChoiceValidator / CountingChoiceValidator 扩展）的依赖，
/// 不替换既有单选瀑布链的 <see cref="FactChoiceMatcher.TryDecide"/> 路径——既有行为字节级不变。
/// </para>
/// </summary>
internal interface IStatementJudge
{
    Verdict Judge(string stem, string statement);
}
