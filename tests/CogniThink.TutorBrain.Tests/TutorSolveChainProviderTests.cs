// P3-3 导学与求解融合：TutorSolveChainProvider / TutorSolveChainGate 单测（AC-2）。
// 核心红线：
// - 仅 Confirmed 且存在可溯源节点时下发；Insufficient/CandidateSet/空 → null（不泄露未采纳结论）。
// - 排除 EvidenceLevel=Trend（不作判定依据）。
// - 纯只读：不改输入 EngineResponse。
// - 缓存：同 stem 第二次取链不回源；异常降级为 null 且不缓存（允许重试）。
using CogniThink.TutorBrain.Application;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

public class TutorSolveChainProviderTests
{
    private static ReasoningNode Node(string desc, EvidenceLevel level = EvidenceLevel.EngineDerived, string? kid = null)
        => new() { Step = 1, Description = desc, Conclusion = desc, EvidenceLevel = level, KnowledgeId = kid };

    private static GatewayResult Result(ConclusionState state, params ReasoningNode[] nodes)
    {
        var resp = new EngineResponse { ConclusionState = state };
        foreach (var n in nodes) resp.ReasoningNodes.Add(n);
        return new GatewayResult { EngineResponse = resp };
    }

    // ==================== 门控（TutorSolveChainGate.Extract）====================

    [Fact]
    public void Confirmed_WithNodes_ReturnsNonTrendNodes()
    {
        var result = Result(ConclusionState.Confirmed,
            Node("推导 A", EvidenceLevel.EngineDerived, "KP-1"),
            Node("趋势解释", EvidenceLevel.Trend, "KP-2"),
            Node("事实 B", EvidenceLevel.KnowledgeFact, "KP-3"));

        var chain = TutorSolveChainGate.Extract(result);

        Assert.NotNull(chain);
        Assert.Equal(2, chain!.Count);
        Assert.DoesNotContain(chain, n => n.EvidenceLevel == EvidenceLevel.Trend);
        Assert.Contains(chain, n => n.Description == "推导 A");
        Assert.Contains(chain, n => n.Description == "事实 B");
    }

    [Fact]
    public void Confirmed_AllTrend_ReturnsNull()
    {
        var result = Result(ConclusionState.Confirmed, Node("仅趋势", EvidenceLevel.Trend));

        var chain = TutorSolveChainGate.Extract(result);

        Assert.Null(chain);
    }

    [Fact]
    public void Confirmed_EmptyNodes_ReturnsNull()
    {
        var chain = TutorSolveChainGate.Extract(Result(ConclusionState.Confirmed));

        Assert.Null(chain);
    }

    [Fact]
    public void Insufficient_WithNodes_ReturnsNull_NoLeak()
    {
        var result = Result(ConclusionState.Insufficient, Node("不应泄露", EvidenceLevel.EngineDerived));

        var chain = TutorSolveChainGate.Extract(result);

        Assert.Null(chain);
    }

    [Fact]
    public void CandidateSet_WithNodes_ReturnsNull()
    {
        var result = Result(ConclusionState.CandidateSet, Node("候选", EvidenceLevel.EngineDerived));

        var chain = TutorSolveChainGate.Extract(result);

        Assert.Null(chain);
    }

    [Fact]
    public void Extract_DoesNotMutateInput()
    {
        var result = Result(ConclusionState.Confirmed, Node("原节点", EvidenceLevel.EngineDerived, "KP-9"));
        var beforeCount = result.EngineResponse.ReasoningNodes.Count;
        var beforeState = result.EngineResponse.ConclusionState;

        _ = TutorSolveChainGate.Extract(result);

        Assert.Equal(beforeCount, result.EngineResponse.ReasoningNodes.Count);
        Assert.Equal(beforeState, result.EngineResponse.ConclusionState);
    }

    // ==================== Provider（缓存 + 降级）====================

    private sealed class StubProvider : TutorSolveChainProvider
    {
        public int CallCount;
        private readonly Func<string, GatewayResult>? _factory;
        private readonly bool _throw;

        public StubProvider(GatewayResult result) : base(null!, new TutorSolveChainCache())
            => _factory = _ => result;

        public StubProvider(Func<string, GatewayResult> factory) : base(null!, new TutorSolveChainCache())
            => _factory = factory;

        public StubProvider(bool throwOnCall) : base(null!, new TutorSolveChainCache())
            => _throw = throwOnCall;

        protected override Task<GatewayResult> CallGatewayAsync(string stem, CancellationToken ct)
        {
            CallCount++;
            if (_throw) throw new InvalidOperationException("gateway down");
            return Task.FromResult(_factory!(stem));
        }
    }

    [Fact]
    public async Task Provider_CacheHit_DoesNotReinvokeGateway()
    {
        var result = Result(ConclusionState.Confirmed, Node("A", EvidenceLevel.EngineDerived));
        var provider = new StubProvider(result);

        var first = await provider.GetChainAsync("stem-x");
        var second = await provider.GetChainAsync("stem-x");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(1, provider.CallCount); // 第二次命中缓存，未回源
    }

    [Fact]
    public async Task Provider_DifferentStem_ReinvokesGateway()
    {
        var provider = new StubProvider(_ => Result(ConclusionState.Confirmed, Node("A")));

        _ = await provider.GetChainAsync("stem-a");
        _ = await provider.GetChainAsync("stem-b");

        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task Provider_NullDefinitive_CachesNull()
    {
        // Insufficient → Extract 返回 null（确定无链）→ 缓存 null，第二次不回源
        var provider = new StubProvider(Result(ConclusionState.Insufficient, Node("X")));

        var first = await provider.GetChainAsync("stem-i");
        var second = await provider.GetChainAsync("stem-i");

        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task Provider_Exception_ReturnsNull_NotCached_Retries()
    {
        var provider = new StubProvider(throwOnCall: true);

        var first = await provider.GetChainAsync("stem-e");
        var second = await provider.GetChainAsync("stem-e"); // 异常未缓存 → 重试

        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task Provider_EmptyStem_ReturnsNull_WithoutCallingGateway()
    {
        var provider = new StubProvider(Result(ConclusionState.Confirmed));

        var chain = await provider.GetChainAsync("   ");

        Assert.Null(chain);
        Assert.Equal(0, provider.CallCount);
    }
}
