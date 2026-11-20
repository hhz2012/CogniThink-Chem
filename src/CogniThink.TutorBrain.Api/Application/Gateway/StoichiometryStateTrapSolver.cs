namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 标况气体摩尔体积陷阱求解器（unknown 路由拦截）：
/// 「标准状况下，22.4L X 含有的分子数是 NA 吗？」——若 X 在标况下不是气体，
/// 则 22.4 L 不为 1 mol，不能用 22.4 L/mol，答案否定。
/// <para>红线：仅当题干精确匹配「标况 + 22.4 + 分子数/NA + 物种在非气态事实表中」时判定；否则 null。
/// 化学事实（标况下物质状态）集中在 NonGasAtStp 表，逻辑不硬编码状态事实。</para>
/// </summary>
public static class StoichiometryStateTrapSolver
{
    /// <summary>标况（0℃、101kPa）下非气态的物质 → 状态说明（事实表，可扩展）。</summary>
    private static readonly Dictionary<string, string> NonGasAtStp = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SO3"] = "固体",    // 熔点 16.8℃，标况下为固态
        ["SO₃"] = "固体",
        ["己烷"] = "液体",   // C6H14，标况下为液态
    };

    public static (string FinalResult, string Note)? TrySolve(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // 护栏：必须是「标况 + 22.4 + 分子数/NA + 疑问」类陷阱问法
        bool isStpTrap = (raw.Contains("标准状况") || raw.Contains("标况")) &&
                         raw.Contains("22.4") &&
                         (raw.Contains("分子数") || raw.Contains("NA") || raw.Contains("Nₐ")) &&
                         raw.Contains("吗");
        if (!isStpTrap) return null;

        foreach (var kv in NonGasAtStp)
        {
            if (raw.Contains(kv.Key))
            {
                return ($"不是。标准状况下 {kv.Key} 是{kv.Value}，22.4 L 不是 1 mol 气体，不能用 22.4 L/mol 计算，故分子数不是 NA。",
                        "【高中限定】标况下非气态物质不适用气体摩尔体积");
            }
        }
        return null;
    }
}
