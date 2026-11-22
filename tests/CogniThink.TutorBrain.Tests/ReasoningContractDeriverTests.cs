using System.Text.Json;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// P2-1 T2：结论三态派生（ConclusionStateDeriver）单元测试。
/// 验证映射正确性 + 派生只读（不改 Ok/FinalResult/NeedMoreCondition → 不影响采纳门，spec CQ-2）
/// + JSON 加性序列化（既有键不变，新键出现，AC-2）。
/// </summary>
public class ReasoningContractDeriverTests
{
    [Fact]
    public void OkTrue_结论非空_Confirmed()
    {
        var resp = new EngineResponse { Ok = true, FinalResult = "电子排布式为[Ar]3d5" };
        Assert.Equal(ConclusionState.Confirmed, ConclusionStateDeriver.Derive(resp));
    }

    [Fact]
    public void OkFalse_引擎确定判错_Confirmed_CQ3()
    {
        var resp = new EngineResponse { Ok = false, FinalResult = "不共存", ErrorTags = { "E07" } };
        Assert.Equal(ConclusionState.Confirmed, ConclusionStateDeriver.Derive(resp));
    }

    [Fact]
    public void OkNull_信息不足_Insufficient()
    {
        var resp = new EngineResponse { Ok = null, FinalResult = "" };
        Assert.Equal(ConclusionState.Insufficient, ConclusionStateDeriver.Derive(resp));
    }

    [Fact]
    public void OkTrue_但结论为空_Insufficient()
    {
        var resp = new EngineResponse { Ok = true, FinalResult = "   " };
        Assert.Equal(ConclusionState.Insufficient, ConclusionStateDeriver.Derive(resp));
    }

    [Fact]
    public void NeedMoreCondition_OkNull_Insufficient()
    {
        var resp = new EngineResponse { Ok = null, NeedMoreCondition = true, ConstraintNote = "需知浓度" };
        Assert.Equal(ConclusionState.Insufficient, ConclusionStateDeriver.Derive(resp));
    }

    [Fact]
    public void 求解器显式置CandidateSet_被保留()
    {
        var resp = new EngineResponse { Ok = null, ConclusionState = ConclusionState.CandidateSet };
        Assert.Equal(ConclusionState.CandidateSet, ConclusionStateDeriver.Derive(resp));
    }

    [Fact]
    public void 全新响应默认_Insufficient()
    {
        Assert.Equal(ConclusionState.Insufficient, ConclusionStateDeriver.Derive(new EngineResponse()));
    }

    [Fact]
    public void 派生只读_不修改Ok_FinalResult_NeedMoreCondition_采纳门输入不变()
    {
        var resp = new EngineResponse
        {
            Ok = true,
            FinalResult = "结论",
            NeedMoreCondition = false,
            ConstraintNote = "note"
        };
        var (okBefore, finalBefore, needBefore, noteBefore) = (resp.Ok, resp.FinalResult, resp.NeedMoreCondition, resp.ConstraintNote);

        _ = ConclusionStateDeriver.Derive(resp);

        Assert.Equal(okBefore, resp.Ok);
        Assert.Equal(finalBefore, resp.FinalResult);
        Assert.Equal(needBefore, resp.NeedMoreCondition);
        Assert.Equal(noteBefore, resp.ConstraintNote);
    }

    [Fact]
    public void JSON序列化_加性新键出现_既有键保留_AC2()
    {
        var resp = new EngineResponse
        {
            Ok = true,
            FinalResult = "答案",
            DerivationSteps = { "步骤1" },
            ConclusionState = ConclusionState.Confirmed,
            ReasoningNodes =
            {
                new ReasoningNode
                {
                    Step = 1,
                    Description = "查表",
                    KnowledgeId = "K-001",
                    Conclusion = "答案",
                    EvidenceLevel = EvidenceLevel.KnowledgeFact
                }
            }
        };

        var json = JsonSerializer.Serialize(resp);

        // 既有键保留（断言键名/ASCII 值；中文值经默认编码器转义，不影响键存在性）
        Assert.Contains("\"final_result\"", json);
        Assert.Contains("\"derivation_steps\"", json);
        Assert.Contains("\"ok\":true", json);
        // 新加性键出现
        Assert.Contains("\"conclusion_state\":\"Confirmed\"", json);
        Assert.Contains("\"reasoning_nodes\"", json);
        Assert.Contains("\"knowledge_id\":\"K-001\"", json);
        Assert.Contains("\"evidence_level\":\"KnowledgeFact\"", json);
    }

    [Fact]
    public void ReasoningNode_枚举序列化为字符串_非数字()
    {
        var node = new ReasoningNode { EvidenceLevel = EvidenceLevel.Trend };
        var json = JsonSerializer.Serialize(node);
        Assert.Contains("\"evidence_level\":\"Trend\"", json);
        Assert.DoesNotContain("\"evidence_level\":3", json);
    }
}
