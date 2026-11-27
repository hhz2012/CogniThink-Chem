namespace CogniThink.TutorBrain.Application.Gateway;

using System.Text.Json.Serialization;

// ============================================================
// P2-4：前端接入网关求解 —— 学生面视图投影（只读、加性）
// 把完整 GatewayResult 按 ConclusionState 裁剪为 StudentSolveView：
//   Confirmed   → 下发 final_result + derivation_steps + reasoning_nodes + explanation_nodes（确定结论可溯源）
//   CandidateSet→ 下发 candidates（当前无生产路径，留空）+ explanation_nodes，不下发 final_result
//   Insufficient→ 不下发 final_result / reasoning_nodes / explanation_nodes（不泄露未采纳结论），
//                 仅透传 constraint_note 提示缺什么条件
// 投影是纯函数：不修改传入的 GatewayResult / EngineResponse；不改求解与判分路径。
// eval 走进程内（GatewayVsLlmEvaluationService）不走本 HTTP 端点，故零影响判分。
// ============================================================

/// <summary>学生面求解视图：/api/gateway/solve 的返回体。按 ConclusionState 裁剪，避免泄露未采纳结论。</summary>
public sealed class StudentSolveView
{
    [JsonPropertyName("conclusion_state")]
    public ConclusionState ConclusionState { get; set; }

    [JsonPropertyName("task_type")]
    public string TaskType { get; set; } = "";

    /// <summary>仅 Confirmed 时下发；CandidateSet / Insufficient 为 null（不泄露）。</summary>
    [JsonPropertyName("final_result")]
    public string? FinalResult { get; set; }

    /// <summary>推理 DAG（P2-1），仅 Confirmed 时下发。</summary>
    [JsonPropertyName("reasoning_nodes")]
    public List<ReasoningNode> ReasoningNodes { get; set; } = new();

    /// <summary>引擎逐步推导过程（文字步骤），仅 Confirmed 时下发；空表示该题无逐步推导。</summary>
    [JsonPropertyName("derivation_steps")]
    public List<string> DerivationSteps { get; set; } = new();

    /// <summary>候选集（CandidateSet 时下；当前无生产路径，恒为空，预留）。</summary>
    [JsonPropertyName("candidates")]
    public List<string> Candidates { get; set; } = new();

    /// <summary>趋势级/机理级解释（P2-2）。Confirmed / CandidateSet 时下，前端须标注「帮助理解，不作判定依据」；Insufficient 为空。</summary>
    [JsonPropertyName("explanation_nodes")]
    public List<ReasoningNode> ExplanationNodes { get; set; } = new();

    /// <summary>教学话术（GenerateReplyAsync 已门控，始终下发）。</summary>
    [JsonPropertyName("reply")]
    public string Reply { get; set; } = "";

    /// <summary>仅 Insufficient 时透传：提示还缺什么条件。</summary>
    [JsonPropertyName("constraint_note")]
    public string? ConstraintNote { get; set; }

    /// <summary>结论来源 provenance（P2-3，观测用），始终透传。</summary>
    [JsonPropertyName("result_provenance")]
    public string? ResultProvenance { get; set; }

    /// <summary>结构化辅助数据（数值/水相/平衡/反应轨迹）；加性，仅由 HTTP /solve 层 enricher 填充，不参与判分。为 null 表示无可下发的辅助结构。</summary>
    [JsonPropertyName("structured")]
    public StudentSolveStructured? Structured { get; set; }
}

/// <summary>纯函数投影：GatewayResult → StudentSolveView，按 ConclusionState 裁剪。不修改输入。</summary>
public static class GatewaySolveProjector
{
    public static StudentSolveView Project(GatewayResult result, StudentSolveStructured? enriched = null)
    {
        var resp = result.EngineResponse;
        var state = resp.ConclusionState;

        var view = new StudentSolveView
        {
            ConclusionState = state,
            TaskType = result.TaskType,
            Reply = result.Reply,
            ResultProvenance = resp.ResultProvenance,
            Structured = enriched,
        };

        switch (state)
        {
            case ConclusionState.Confirmed:
                // 引擎给出唯一可溯源结论（含 Ok=false 的确定判错）→ 可展示
                view.FinalResult = resp.FinalResult;
                view.ReasoningNodes = resp.ReasoningNodes;
                view.DerivationSteps = resp.DerivationSteps;
                view.ExplanationNodes = resp.ExplanationNodes;
                break;

            case ConclusionState.CandidateSet:
                // 多候选未唯一：不泄露任一为最终答案；candidates 当前无生产路径，留空
                view.Candidates = new List<string>();
                view.ExplanationNodes = resp.ExplanationNodes;
                break;

            case ConclusionState.Insufficient:
            default:
                // 信息不足 / 能力缺口：绝不展示 final_result / 推理 / 解释，仅提示缺条件
                view.ConstraintNote = resp.ConstraintNote;
                break;
        }

        return view;
    }
}
