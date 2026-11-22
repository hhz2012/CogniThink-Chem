namespace CogniThink.TutorBrain.Application.Gateway;

using System.Text.Json.Serialization;
using CogniThink.Chem.Core.WorldModel;

// ============================================================
// P2-1：统一推理 DAG 与结论三态契约
// 化学引擎是唯一真值源；本契约让推理节点可溯源到 rule/knowledge，
// 结论状态显式三态（确定/候选集/信息不足），为 P2-2（evidence_level）
// 与 P2-4（前端 DAG 展示）提供数据。加性元数据，不改变采纳门与正确性。
// ============================================================

/// <summary>
/// 结论三态（与 Core <c>InferenceStatus</c> 语义对齐，向网关暴露）。
/// 描述「引擎能否给出确定结论」，不描述学生答案对错：
/// <list type="bullet">
/// <item><see cref="Confirmed"/>：引擎给出唯一可溯源结论（含确定地判定学生答案错误，Ok=false）。</item>
/// <item><see cref="CandidateSet"/>：多个并列可能未唯一（预留；当前网关无生产路径）。</item>
/// <item><see cref="Insufficient"/>：信息不足 / 能力缺口，无法确定（对应原静默 null）。</item>
/// </list>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConclusionState
{
    Confirmed,
    CandidateSet,
    Insufficient,
}

/// <summary>
/// 证据强度（路线图 evidence_level；<see cref="Trend"/> 为 P2-2 世界模型预留）。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EvidenceLevel
{
    /// <summary>知识库唯一命中（确定真值）。</summary>
    KnowledgeFact,
    /// <summary>引擎推导（配平 / 计量 / 查表）。</summary>
    EngineDerived,
    /// <summary>候选未唯一（并列可能）。</summary>
    Candidate,
    /// <summary>趋势级（不给绝对值；P2-2 世界模型用，标注「帮助理解，不作判定依据」）。</summary>
    Trend,
}

/// <summary>
/// 统一推理节点：记录「依据什么前提、用哪条规则/知识、推出什么结论、证据强度如何」。
/// 节点 <see cref="Step"/> 从 1 连续编号，保证无跳步；末节点 <see cref="Conclusion"/> == 引擎 FinalResult。
/// 无 knowledge_id/rule_id 命中的求解器不得填充本节点（留空），避免假可溯源（spec R4）。
/// </summary>
public sealed class ReasoningNode
{
    /// <summary>节点序号（从 1 连续，无跳步）。</summary>
    [JsonPropertyName("step")]
    public int Step { get; set; }

    /// <summary>本步叙述（中文）。</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    /// <summary>本步依据的前提（题干条件 / 上一步结论 / 知识条目引用）。</summary>
    [JsonPropertyName("premises")]
    public List<string> Premises { get; set; } = new();

    /// <summary>命中的规则 id（如 data/rules/rules.json 条目；无则 null）。</summary>
    [JsonPropertyName("rule_id")]
    public string? RuleId { get; set; }

    /// <summary>命中的知识点 id（如 data/highschool/*.json 条目；无则 null）—— 与 Core InferenceStep.KnowledgeId 对齐。</summary>
    [JsonPropertyName("knowledge_id")]
    public string? KnowledgeId { get; set; }

    /// <summary>本步推出的中间结论；末节点 = 引擎 FinalResult。</summary>
    [JsonPropertyName("conclusion")]
    public string? Conclusion { get; set; }

    /// <summary>证据强度。</summary>
    [JsonPropertyName("evidence_level")]
    public EvidenceLevel EvidenceLevel { get; set; }
}

/// <summary>
/// P2-1：结论三态派生。从现有信号（Ok / NeedMoreCondition / FinalResult）派生
/// <see cref="ConclusionState"/>，只读不写回 Ok/FinalResult/TaskType，因此不影响采纳门
/// <c>BuildGatewayAnswerAsync</c>（spec CQ-2）。求解器显式置 <see cref="ConclusionState.CandidateSet"/> 时保留。
/// </summary>
public static class ConclusionStateDeriver
{
    public static ConclusionState Derive(EngineResponse resp)
    {
        // 求解器自报候选集 → 保留，不被通用映射覆盖
        if (resp.ConclusionState == ConclusionState.CandidateSet)
            return ConclusionState.CandidateSet;

        // Ok==true 且结论非空 → 确定
        if (resp.Ok == true && !string.IsNullOrWhiteSpace(resp.FinalResult))
            return ConclusionState.Confirmed;

        // Ok==false → 引擎确定地判定学生答案错误，仍属「给出确定结论」（spec CQ-3）
        if (resp.Ok == false)
            return ConclusionState.Confirmed;

        // 其余：Ok==null / NeedMoreCondition / FinalResult 空 → 信息不足 / 能力缺口
        return ConclusionState.Insufficient;
    }
}

/// <summary>
/// P2-2：把 Core 世界模型的趋势级/机理级解释富化到 <see cref="EngineResponse.ExplanationNodes"/>。
/// 只读：仅写新增字段，不改 <see cref="EngineResponse.Ok"/> / <see cref="EngineResponse.FinalResult"/> /
/// TaskType，因此不影响采纳门 <c>BuildGatewayAnswerAsync</c>（spec CQ-5）。所有节点 EvidenceLevel=Trend。
/// </summary>
public static class WorldModelEnricher
{
    public static void Enrich(EngineResponse resp, string questionText)
    {
        var nodes = WorldModel.Explain(questionText, resp.FinalResult);
        resp.ExplanationNodes = nodes.Select(n => new ReasoningNode
        {
            Step = n.Step,
            Description = n.Description,
            Premises = n.Premises,
            RuleId = n.RuleId,
            KnowledgeId = n.KnowledgeId,
            Conclusion = n.Conclusion,
            EvidenceLevel = EvidenceLevel.Trend,
        }).ToList();
    }
}
