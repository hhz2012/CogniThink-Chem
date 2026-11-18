namespace CogniThink.TutorBrain.Domain;

/// <summary>学科域（本项目仅化学）</summary>
public enum Subject
{
    Chemistry = 1
}

/// <summary>课标认知层级（布鲁姆分类精简版）</summary>
public enum CognitiveLevel
{
    Remember = 1,
    Understand = 2,
    Apply = 3,
    Analyze = 4,
    Evaluate = 5
}

/// <summary>
/// 固定错因标签体系（可扩展）
/// </summary>
public enum ErrorTag
{
    None = 0,
    /// <summary>概念混淆</summary>
    ConceptConfusion = 1,
    /// <summary>条件漏看</summary>
    ConditionMissed = 2,
    /// <summary>推理跳跃</summary>
    ReasoningJump = 3,
    /// <summary>公式记错</summary>
    FormulaMisremembered = 4,
    /// <summary>共存判断错误（化学）</summary>
    CoexistenceJudgement = 5,
    /// <summary>计算失误</summary>
    Calculation = 6,
    /// <summary>审题错误</summary>
    Misread = 7
}

/// <summary>单步判定结果</summary>
public enum StepVerdict
{
    Correct = 0,
    /// <summary>局部小错：方向对但有瑕疵</summary>
    PartiallyCorrect = 1,
    /// <summary>关键错误</summary>
    Incorrect = 2,
    /// <summary>无法判定：格式/信息不足</summary>
    Ungradable = 3
}

/// <summary>导学状态机状态</summary>
public enum TutorStatus
{
    AwaitingStep = 0,
    StepAccepted = 1,
    LocalMistake = 2,
    KeyMistake = 3,
    EscalatingHint = 4,
    Completed = 5
}

/// <summary>建议前端执行的下一步动作（结构化驱动探险 UI）</summary>
public enum RecommendedAction
{
    Proceed = 0,
    SelfCheck = 1,
    AskSubQuestion = 2,
    GiveClue = 3,
    GiveScaffold = 4,
    RetryStep = 5,
    FinishLevel = 6,
    ReviewKnowledge = 7
}

/// <summary>闯关状态</summary>
public enum ChallengeStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2
}

/// <summary>错因标签的中文映射（对外输出）</summary>
public static class ErrorTagText
{
    public static string ToText(this ErrorTag tag) => tag switch
    {
        ErrorTag.ConceptConfusion => "概念混淆",
        ErrorTag.ConditionMissed => "条件漏看",
        ErrorTag.ReasoningJump => "推理跳跃",
        ErrorTag.FormulaMisremembered => "公式记错",
        ErrorTag.CoexistenceJudgement => "共存判断错误",
        ErrorTag.Calculation => "计算失误",
        ErrorTag.Misread => "审题错误",
        _ => "无"
    };
}
