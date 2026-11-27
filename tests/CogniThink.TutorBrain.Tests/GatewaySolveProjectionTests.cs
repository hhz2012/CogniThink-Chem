using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// P2-4：学生面视图投影门控回归。
/// <para>核心红线：不泄露未采纳结论 —— Insufficient 绝不向学生面下发 final_result / reasoning / explanation。</para>
/// <para>投影是纯只读函数：不改输入 EngineResponse；不改求解与判分路径（eval 不走 HTTP）。</para>
/// </summary>
public class GatewaySolveProjectionTests
{
    private static GatewayResult MakeResult(EngineResponse resp, string taskType = "equilibrium", string reply = "教学回复")
        => new() { TaskType = taskType, EngineResponse = resp, Reply = reply };

    private static ReasoningNode Node(string desc, EvidenceLevel level = EvidenceLevel.EngineDerived)
        => new() { Step = 1, Description = desc, Conclusion = desc, EvidenceLevel = level };

    // ==================== Confirmed（含 Ok=true 与 Ok=false）====================

    [Fact]
    public void Confirmed_OkTrue_IncludesFinalResultReasoningAndExplanation()
    {
        var resp = new EngineResponse
        {
            Ok = true,
            FinalResult = "x=1",
            ConclusionState = ConclusionState.Confirmed,
            ResultProvenance = "equilibrium",
        };
        resp.ReasoningNodes.Add(Node("依据 A"));
        resp.ExplanationNodes.Add(Node("趋势解释", EvidenceLevel.Trend));

        var view = GatewaySolveProjector.Project(MakeResult(resp));

        Assert.Equal(ConclusionState.Confirmed, view.ConclusionState);
        Assert.Equal("x=1", view.FinalResult);
        Assert.Single(view.ReasoningNodes);
        Assert.Single(view.ExplanationNodes);
        Assert.Equal("equilibrium", view.ResultProvenance);
        Assert.Equal("equilibrium", view.TaskType);
        Assert.Equal("教学回复", view.Reply);
    }

    [Fact]
    public void Confirmed_OkFalse_StillIncludesFinalResult_DeterminedVerdict()
    {
        // Ok=false = 引擎确定地判定学生答案错误，仍属「给出确定结论」（spec CQ-3），可展示裁定
        var resp = new EngineResponse
        {
            Ok = false,
            FinalResult = "错误：方程式电荷不守恒",
            ConclusionState = ConclusionState.Confirmed,
        };

        var view = GatewaySolveProjector.Project(MakeResult(resp));

        Assert.Equal(ConclusionState.Confirmed, view.ConclusionState);
        Assert.NotNull(view.FinalResult);
        Assert.Equal("错误：方程式电荷不守恒", view.FinalResult);
    }

    // ==================== 核心红线：Insufficient 不泄露 ====================

    [Fact]
    public void Insufficient_DoesNotLeakFinalResultReasoningOrExplanation()
    {
        var resp = new EngineResponse
        {
            Ok = null,
            FinalResult = "SHOULD-NOT-LEAK",
            ConstraintNote = "缺少反应物浓度，无法计算",
            ConclusionState = ConclusionState.Insufficient,
        };
        resp.ReasoningNodes.Add(Node("不应泄露的推理"));
        resp.ExplanationNodes.Add(Node("不应泄露的解释"));

        var view = GatewaySolveProjector.Project(MakeResult(resp));

        Assert.Equal(ConclusionState.Insufficient, view.ConclusionState);
        Assert.Null(view.FinalResult);           // 核心：不下发答案
        Assert.Empty(view.ReasoningNodes);       // 不下发推理 DAG
        Assert.Empty(view.ExplanationNodes);     // 不下发解释
        Assert.Empty(view.Candidates);
        Assert.Equal("缺少反应物浓度，无法计算", view.ConstraintNote);
    }

    // ==================== CandidateSet（预留）====================

    [Fact]
    public void CandidateSet_IncludesExplanationButNotFinalResult()
    {
        var resp = new EngineResponse
        {
            Ok = null,
            FinalResult = "SHOULD-NOT-LEAK",
            ConclusionState = ConclusionState.CandidateSet,
        };
        resp.ExplanationNodes.Add(Node("候选解释", EvidenceLevel.Trend));

        var view = GatewaySolveProjector.Project(MakeResult(resp));

        Assert.Equal(ConclusionState.CandidateSet, view.ConclusionState);
        Assert.Null(view.FinalResult);    // 不把任一候选当最终答案泄露
        Assert.Empty(view.Candidates);    // 当前无生产路径，留空
        Assert.Single(view.ExplanationNodes);
        Assert.Empty(view.ReasoningNodes);
    }

    // ==================== 纯只读：不修改输入 ====================

    [Fact]
    public void Projector_DoesNotMutateInputEngineResponse()
    {
        var resp = new EngineResponse
        {
            Ok = true,
            FinalResult = "x=1",
            ConclusionState = ConclusionState.Confirmed,
            ResultProvenance = "concept_choice",
            ConstraintNote = "note",
        };
        resp.ReasoningNodes.Add(Node("原节点"));

        var beforeFinal = resp.FinalResult;
        var beforeOk = resp.Ok;
        var beforeState = resp.ConclusionState;
        var beforeNodes = resp.ReasoningNodes.Count;

        var view = GatewaySolveProjector.Project(MakeResult(resp));

        // 输入未被投影修改
        Assert.Equal(beforeFinal, resp.FinalResult);
        Assert.Equal(beforeOk, resp.Ok);
        Assert.Equal(beforeState, resp.ConclusionState);
        Assert.Equal(beforeNodes, resp.ReasoningNodes.Count);
        // 输出正确
        Assert.Equal("x=1", view.FinalResult);
        Assert.Equal("concept_choice", view.ResultProvenance);
    }

    // ==================== 加性 structured 透传（不改变判决裁剪）====================

    [Fact]
    public void Project_PassesThroughStructuredEnrichment()
    {
        var resp = new EngineResponse
        {
            ConclusionState = ConclusionState.Confirmed,
            FinalResult = "x=1",
        };
        var enriched = new StudentSolveStructured
        {
            Numeric = new NumericSnapshot { Value = 1.0, Unit = "mol/L", Species = "NaOH" }
        };

        var view = GatewaySolveProjector.Project(MakeResult(resp), enriched);

        Assert.Same(enriched, view.Structured);
        Assert.NotNull(view.Structured?.Numeric);
        Assert.Equal(1.0, view.Structured!.Numeric!.Value);
        Assert.Equal("mol/L", view.Structured.Numeric.Unit);
    }

    [Fact]
    public void Insufficient_WithStructured_DoesNotLeakVerdictFields()
    {
        // 核心：加性 structured 不改变判决门控——Insufficient 仍不泄露 final_result / 推理 / 解释
        var resp = new EngineResponse
        {
            FinalResult = "SHOULD-NOT-LEAK",
            ConstraintNote = "缺少反应物浓度",
            ConclusionState = ConclusionState.Insufficient,
        };
        resp.ReasoningNodes.Add(Node("不应泄露的推理"));
        resp.ExplanationNodes.Add(Node("不应泄露的解释"));
        var enriched = new StudentSolveStructured { Numeric = new NumericSnapshot { Value = 1.0 } };

        var view = GatewaySolveProjector.Project(MakeResult(resp), enriched);

        Assert.Equal(ConclusionState.Insufficient, view.ConclusionState);
        Assert.Null(view.FinalResult);
        Assert.Empty(view.ReasoningNodes);
        Assert.Empty(view.ExplanationNodes);
        Assert.Equal("缺少反应物浓度", view.ConstraintNote);
        // structured 作为辅助仍可下发
        Assert.NotNull(view.Structured);
        Assert.Equal(1.0, view.Structured!.Numeric!.Value);
    }

    [Fact]
    public void Project_BackwardCompatible_DefaultStructuredNull()
    {
        // 旧调用方不传 enriched → structured 为 null，不破坏既有契约
        var resp = new EngineResponse { ConclusionState = ConclusionState.Confirmed, FinalResult = "x=1" };
        var view = GatewaySolveProjector.Project(MakeResult(resp));
        Assert.Null(view.Structured);
    }
}
