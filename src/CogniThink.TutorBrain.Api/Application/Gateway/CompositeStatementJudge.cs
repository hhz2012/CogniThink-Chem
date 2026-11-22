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
        => JudgeWithIds(stem, statement).Verdict;

    /// <summary>
    /// 加性扩展：返回聚合判定结论 + 命中事实的 <c>Id</c> 列表（父 spec R4）。
    /// <para>仅 <see cref="FactBaseStatementJudge"/> 命中时携带非空 id（事实库有稳定 <c>Id</c>）；
    /// <see cref="CountingStatementJudge"/> / <see cref="RedoxElectronStatementJudge"/> 属引擎推导、无暴露 id，
    /// 命中时返回空 id 集合（R4：不为引擎推导子判决发节点）。原 <see cref="Judge"/> 签名字节级不变，
    /// 内部委托本方法丢弃 id，既有行为字节级等价。</para>
    /// </summary>
    public (Verdict Verdict, List<string> HitIds) JudgeWithIds(string stem, string statement)
    {
        Verdict? definite = null;
        var hitIds = new List<string>();
        foreach (var j in _judges)
        {
            List<string>? subIds = null;
            Verdict v;
            if (j is FactBaseStatementJudge fb)
            {
                var (vv, ids) = fb.JudgeWithIds(stem, statement);
                v = vv;
                subIds = ids;
            }
            else
            {
                v = j.Judge(stem, statement);   // Counting / RedoxElectron：引擎推导，无暴露 id（R4）
            }
            if (v == Verdict.Undecided) continue;
            if (definite.HasValue && definite.Value != v)
                return (Verdict.Undecided, new List<string>());   // 冲突 → 不判，不蒙数
            definite = v;
            if (subIds is not null) hitIds.AddRange(subIds);
        }
        return (definite ?? Verdict.Undecided, hitIds);
    }
}
