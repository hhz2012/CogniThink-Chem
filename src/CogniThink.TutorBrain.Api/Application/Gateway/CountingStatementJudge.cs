namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 计数陈述判定原语：判定 "某分子式的同分异构体/取代物有 N 种" 类断言的真伪。
/// 复用 <see cref="CountingChoiceValidator"/> 既有的构造异构枚举 + counting-facts 查表逻辑
/// （<see cref="CountingChoiceValidator.TryJudgeCountStatement"/>），不改其判定语义。
/// <para>
/// 分子式不可解析 / 官能团不在支持列表 / 事实表歧义 → <see cref="Verdict.Undecided"/>；
/// 非计数断言（不含 "N 种"）→ <see cref="Verdict.Undecided"/>。不蒙数、零假阳性。
/// </para>
/// </summary>
internal sealed class CountingStatementJudge : IStatementJudge
{
    public Verdict Judge(string stem, string statement)
    {
        var v = CountingChoiceValidator.TryJudgeCountStatement(statement);
        return v switch
        {
            true => Verdict.True,
            false => Verdict.False,
            _ => Verdict.Undecided,
        };
    }
}
