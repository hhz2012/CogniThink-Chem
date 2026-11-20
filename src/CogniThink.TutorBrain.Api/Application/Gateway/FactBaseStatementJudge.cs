namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 事实库陈述判定原语：用全部四个选择题事实库（general / experiment / organic / structure）
/// 判定单条化学陈述的真伪。与 <see cref="FactChoiceMatcher"/> 共用同一套归一化 / 子串匹配口径
/// （<see cref="FactChoiceMatcher.JudgeStatement"/>），保证新旧链路判定一致。
/// <para>
/// 仅当命中事实的 verdict 全部一致时返回 True/False；0 条命中或命中冲突（歧义）→
/// <see cref="Verdict.Undecided"/>。不蒙数、零假阳性。
/// </para>
/// </summary>
internal sealed class FactBaseStatementJudge : IStatementJudge
{
    public Verdict Judge(string stem, string statement)
    {
        var v = FactChoiceMatcher.JudgeStatement(stem, statement);
        return v switch
        {
            FactChoiceMatcher.StatementVerdict.True => Verdict.True,
            FactChoiceMatcher.StatementVerdict.False => Verdict.False,
            _ => Verdict.Undecided,
        };
    }
}
