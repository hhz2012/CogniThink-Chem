namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 组合判定器：聚合多个 <see cref="IStatementJudge"/> 原语，供多选判定器与组合/计数求解器共用。
/// <para>
/// 语义：任一原语给出确定结论（True/False）则记录；多个原语给出<b>冲突</b>结论 →
/// <see cref="Verdict.Undecided"/>（不偏信任一，不蒙数）；全部原语不命中 →
/// <see cref="Verdict.Undecided"/>。多个原语给出一致结论时取该结论。
/// </para>
/// <para>
/// 本类为新增校验器的依赖，不替换既有单选瀑布链的判定路径，既有行为字节级不变。
/// </para>
/// </summary>
internal sealed class CompositeStatementJudge : IStatementJudge
{
    private readonly IReadOnlyList<IStatementJudge> _judges;

    public CompositeStatementJudge(params IStatementJudge[] judges)
    {
        _judges = judges ?? System.Array.Empty<IStatementJudge>();
    }

    public Verdict Judge(string stem, string statement)
    {
        Verdict? definite = null;
        foreach (var j in _judges)
        {
            var v = j.Judge(stem, statement);
            if (v == Verdict.Undecided) continue;
            if (definite.HasValue && definite.Value != v)
                return Verdict.Undecided;   // 冲突 → 不判，不蒙数
            definite = v;
        }
        return definite ?? Verdict.Undecided;
    }
}
