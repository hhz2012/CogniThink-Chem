using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// B-Impurity 批验收：ImpurityTrapSolver 化学事实外置 JSON + 命中线程化 ReasoningNodes。
/// <para>红线：FinalResult 与基线字节级相等（零漂移）；命中发 1 节点带 KnowledgeId；域外返回 null（R4 不臆造）。</para>
/// </summary>
public class ImpurityTrapExternalizationTests
{
    private const string ExpectedFinalResult = "不对。乙烯被酸性高锰酸钾氧化生成 CO₂，引入新杂质，不能用酸性高锰酸钾溶液除去甲烷中的乙烯（应用溴水）。";
    private const string TrapId = "impurity-kmno4-ethylene-co2";

    private static EngineRequest Req(string raw) => new() { TaskType = "reaction_check", RawText = raw };

    [Fact]
    public void Hit_FinalResult_ByteEqual_And_Node_Threaded()
    {
        var r = ImpurityTrapSolver.TrySolve(Req("用酸性高锰酸钾溶液除去甲烷中的乙烯，对吗？"));
        Assert.NotNull(r);
        Assert.False(r!.Ok);
        Assert.Equal(ExpectedFinalResult, r.FinalResult);
        Assert.NotNull(r.ReasoningNodes);
        Assert.Single(r.ReasoningNodes);
        var node = r.ReasoningNodes[0];
        Assert.Equal(TrapId, node.KnowledgeId);
        Assert.Equal(ExpectedFinalResult, node.Conclusion);
        Assert.Equal(EvidenceLevel.KnowledgeFact, node.EvidenceLevel);
        Assert.Equal(1, node.Step);
    }

    [Fact]
    public void OutOfRange_NoRemoveAction_IsNull()
    {
        Assert.Null(ImpurityTrapSolver.TrySolve(Req("乙烯能使酸性高锰酸钾溶液褪色吗？")));
    }

    [Fact]
    public void OutOfRange_Brine_IsNull()
    {
        Assert.Null(ImpurityTrapSolver.TrySolve(Req("用溴水除去甲烷中的乙烯，对吗？")));
    }

    [Fact]
    public void OutOfRange_Unrelated_IsNull()
    {
        Assert.Null(ImpurityTrapSolver.TrySolve(Req("KMnO4 受热分解的化学方程式是什么？")));
    }
}
