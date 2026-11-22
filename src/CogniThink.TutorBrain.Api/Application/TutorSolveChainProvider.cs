// P3-3 导学与求解融合：导学流按需获取网关「标准推理链」（ReasoningNodes DAG）。
//
// 设计要点（对应 spec §3.1 / §3.5 红线）：
// - 只读：不修改 GatewayResult / EngineResponse；不写回任何网关字段。
// - 门控复用 P2-4：仅 ConclusionState=Confirmed 且存在可溯源节点时下发；
//   Insufficient / CandidateSet / 空 DAG → null（不泄露未采纳结论）。
// - 过滤 EvidenceLevel=Trend（P2-2 世界模型解释，标注「不作判定依据」，不进入导学判分/诊断锚点）。
// - 缓存：按题干 stem 缓存（含 null，表示「已确认无可用链」），避免每次 submit-step 重跑 LLM + 引擎。
// - 降级：取链异常 / 超时 → 返回 null，不抛、不影响导学判分主路径（异常不缓存，允许下次重试）。
namespace CogniThink.TutorBrain.Application;

using System.Collections.Concurrent;
using CogniThink.TutorBrain.Application.Gateway;

/// <summary>标准推理链缓存（进程内单例）。缓存 null 表示「已确认无可用链」，避免重复回源。</summary>
public interface ITutorSolveChainCache
{
    bool TryGet(string stem, out IReadOnlyList<ReasoningNode>? chain);
    void Set(string stem, IReadOnlyList<ReasoningNode>? chain);
}

public sealed class TutorSolveChainCache : ITutorSolveChainCache
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<ReasoningNode>?> _store = new();

    public bool TryGet(string stem, out IReadOnlyList<ReasoningNode>? chain)
        => _store.TryGetValue(stem, out chain);

    public void Set(string stem, IReadOnlyList<ReasoningNode>? chain)
        => _store[stem] = chain;
}

/// <summary>
/// 从网关求解结果中提取可下发的标准推理链（纯函数，便于单测）。
/// 不改输入；仅 Confirmed + 非空 + 排除 Trend 后非空时返回节点，否则 null。
/// </summary>
public static class TutorSolveChainGate
{
    public static IReadOnlyList<ReasoningNode>? Extract(GatewayResult result)
    {
        var resp = result.EngineResponse;
        if (resp.ConclusionState != ConclusionState.Confirmed)
            return null;

        var nodes = resp.ReasoningNodes;
        if (nodes is null || nodes.Count == 0)
            return null;

        // 排除 Trend（世界模型解释，帮助理解但不作判定依据，不进入导学诊断锚点）
        var usable = nodes.Where(n => n.EvidenceLevel != EvidenceLevel.Trend).ToList();
        return usable.Count == 0 ? null : usable;
    }
}

/// <summary>
/// 导学流取链 provider：带缓存的网关 DAG 获取。
/// 取链为非阻塞旁路：失败 / null 时 StandardReasoningChain=null，不影响导学判分主路径。
/// </summary>
public class TutorSolveChainProvider
{
    private readonly LlmGatewayService _gateway;
    private readonly ITutorSolveChainCache _cache;

    public TutorSolveChainProvider(LlmGatewayService gateway, ITutorSolveChainCache cache)
    {
        _gateway = gateway;
        _cache = cache;
    }

    public async Task<IReadOnlyList<ReasoningNode>?> GetChainAsync(string problemStem, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(problemStem))
            return null;

        if (_cache.TryGet(problemStem, out var cached))
            return cached; // 命中（含已缓存的 null）直接返回，不回源

        GatewayResult result;
        try
        {
            result = await CallGatewayAsync(problemStem, ct);
        }
        catch
        {
            // 取链失败降级：不抛、不缓存（允许下次重试），不影响导学判分
            return null;
        }

        var chain = TutorSolveChainGate.Extract(result);
        _cache.Set(problemStem, chain); // 缓存结论（含 null），后续同 stem 不再回源
        return chain;
    }

    // 保护虚方法：便于单测子类覆写以计数 / 注入桩结果，无需 mock LlmGatewayService（不改网关类）。
    protected virtual Task<GatewayResult> CallGatewayAsync(string stem, CancellationToken ct)
        => _gateway.ProcessStudentMessageAsync(stem, ct: ct);
}
