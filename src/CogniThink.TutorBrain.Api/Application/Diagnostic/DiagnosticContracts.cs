namespace CogniThink.TutorBrain.Application.Diagnostic;

using CogniThink.TutorBrain.Domain;
using CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 推理检查点：把一步推理拆成可逐一考核的知识点节点。
/// 学生按顺序回答，第一个答错的节点即定位为知识盲区。
/// </summary>
public sealed class ReasoningCheckpoint
{
    /// <summary>检查点序号（从 1 开始）</summary>
    public int Index { get; set; }

    /// <summary>对应知识点 Id</summary>
    public string KnowledgeId { get; set; } = string.Empty;

    /// <summary>知识点名称</summary>
    public string KnowledgeName { get; set; } = string.Empty;

    /// <summary>检查点类型：前置 / 核心 / 推理</summary>
    public CheckpointType Type { get; set; }

    /// <summary>向学生提出的诊断问题</summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>判定用关键词（逗号分隔），空表示仅做概念确认</summary>
    public string? ExpectedKeywords { get; set; }

    /// <summary>该检查点的讲解内容（核心结论，尽量简短）</summary>
    public string Explanation { get; set; } = string.Empty;

    /// <summary>思考方法：教学生"怎么想"，而不是直接给答案</summary>
    public string ThinkingStrategy { get; set; } = string.Empty;

    /// <summary>分析框架：引导学生按步骤思考的问题链</summary>
    public List<string> AnalysisFramework { get; set; } = new();

    /// <summary>常见误区：提醒学生避免的典型错误</summary>
    public string CommonMisconception { get; set; } = string.Empty;

    /// <summary>化学证据类型：无 / 物质颜色 / 反应方程 / 共存判断</summary>
    public EvidenceType EvidenceType { get; set; }

    /// <summary>证据查询用的物质/离子/反应关键词</summary>
    public string? EvidenceKey { get; set; }

    /// <summary>P3-3：链接到网关标准推理链中同 knowledge_id 的推导节点（无匹配则 null；已排除 Trend）。加性字段，不参与判定。</summary>
    public ReasoningNode? LinkedReasoningNode { get; set; }
}

public enum CheckpointType
{
    /// <summary>前置知识点检查</summary>
    Prerequisite = 0,
    /// <summary>核心知识点检查</summary>
    Core = 1,
    /// <summary>本步推理结论检查</summary>
    Reasoning = 2
}

public enum EvidenceType
{
    None = 0,
    SubstanceColor = 1,
    Reaction = 2,
    Coexistence = 3
}

/// <summary>
/// 一道题某一步的完整推理结构：前置 → 核心 → 推理结论。
/// </summary>
public sealed class ReasoningStructure
{
    public string ProblemId { get; set; } = string.Empty;
    public int StepIndex { get; set; }
    public string StepPrompt { get; set; } = string.Empty;
    public List<ReasoningCheckpoint> Checkpoints { get; set; } = new();
}

/// <summary>诊断会话状态</summary>
public enum DiagnosticStatus
{
    /// <summary>诊断进行中</summary>
    InProgress = 0,
    /// <summary>已定位知识盲区</summary>
    GapFound = 1,
    /// <summary>已讲解，等待学生重试原步骤</summary>
    Explained = 2,
    /// <summary>诊断完成（学生掌握或重试通过）</summary>
    Completed = 3
}

/// <summary>学生对某个检查点的回答记录</summary>
public sealed class CheckpointAnswer
{
    public int CheckpointIndex { get; set; }
    public string Answer { get; set; } = string.Empty;
    public StepVerdict Verdict { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// 诊断会话存储（单例）：跨请求保存诊断会话。
/// 诊断是短暂交互，无需持久化到数据库，内存存储即可。
/// </summary>
public sealed class DiagnosticSessionStore
{
    private readonly Dictionary<string, DiagnosticSession> _sessions = new();
    private readonly object _lock = new();

    public void Store(DiagnosticSession session)
    {
        lock (_lock) { _sessions[session.SessionId] = session; }
    }

    public DiagnosticSession? Get(string sessionId)
    {
        lock (_lock) { return _sessions.TryGetValue(sessionId, out var s) ? s : null; }
    }

    public bool TryGetValue(string sessionId, out DiagnosticSession session)
    {
        lock (_lock)
        {
            var found = _sessions.TryGetValue(sessionId, out var s);
            session = s!;
            return found;
        }
    }
}

/// <summary>
/// 诊断会话：跟踪学生在推理结构上的逐项作答情况。
/// 会话存储在内存中（诊断是短暂交互，不需持久化）。
/// </summary>
public sealed class DiagnosticSession
{
    public string SessionId { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string ProblemId { get; set; } = string.Empty;
    public int StepIndex { get; set; }

    public ReasoningStructure Structure { get; set; } = new();
    public List<CheckpointAnswer> Answers { get; set; } = new();
    public DiagnosticStatus Status { get; set; } = DiagnosticStatus.InProgress;

    /// <summary>当前考核到的检查点序号</summary>
    public int CurrentCheckpointIndex { get; set; } = 1;

    /// <summary>定位到的盲区检查点（第一个答错的）</summary>
    public int? GapCheckpointIndex { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>开始诊断的返回</summary>
public sealed record DiagnosticStartResult(
    string SessionId,
    ReasoningStructure Structure,
    ReasoningCheckpoint CurrentCheckpoint,
    string Message);

/// <summary>提交诊断回答的返回</summary>
public sealed record DiagnosticAnswerResult(
    StepVerdict Verdict,
    string Message,
    DiagnosticAction Action,
    ReasoningCheckpoint? NextCheckpoint,
    CheckpointExplanation? Explanation,
    int? GapCheckpointIndex,
    DiagnosticStatus SessionStatus);

public enum DiagnosticAction
{
    /// <summary>继续下一个检查点</summary>
    NextCheckpoint = 0,
    /// <summary>讲解盲区知识点</summary>
    Explain = 1,
    /// <summary>全部检查点通过，可重试原步骤</summary>
    ReadyToRetry = 2,
    /// <summary>原步骤重试通过，诊断完成</summary>
    Completed = 3
}

/// <summary>知识点讲解内容</summary>
public sealed class CheckpointExplanation
{
    public string KnowledgeId { get; set; } = string.Empty;
    public string KnowledgeName { get; set; } = string.Empty;
    public CognitiveLevel Level { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public List<string> Evidence { get; set; } = new();
    public List<string> RelatedReactions { get; set; } = new();

    /// <summary>思考方法：教学生"怎么想"</summary>
    public string ThinkingStrategy { get; set; } = string.Empty;

    /// <summary>分析步骤：引导学生一步步思考</summary>
    public List<string> AnalysisSteps { get; set; } = new();

    /// <summary>常见误区：提醒学生避免的典型错误</summary>
    public string CommonMisconception { get; set; } = string.Empty;

    /// <summary>迁移提示：引导学生把思考方法用到类似题目</summary>
    public string TransferHint { get; set; } = string.Empty;
}
