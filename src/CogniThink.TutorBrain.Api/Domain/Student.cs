namespace CogniThink.TutorBrain.Domain;

/// <summary>学生实体：学情模型聚合根</summary>
public class Student
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<KnowledgeMastery> Masteries { get; set; } = new();
    public List<StepTrace> StepTraces { get; set; } = new();
    public List<MistakeRecord> Mistakes { get; set; } = new();
    public List<ReviewSchedule> Reviews { get; set; } = new();
    public List<ChallengeProgress> Progress { get; set; } = new();
}

/// <summary>知识点掌握度（0~1），随做题与复测动态更新</summary>
public class KnowledgeMastery
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string KnowledgeId { get; set; } = string.Empty;

    /// <summary>掌握度 0~1</summary>
    public double Value { get; set; }

    public int Attempts { get; set; }
    public int Corrects { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>卡壳轨迹：每一步作答与系统判定</summary>
public class StepTrace
{
    public long Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string ProblemId { get; set; } = string.Empty;
    public int StepIndex { get; set; }

    /// <summary>学生本步作答原文</summary>
    public string StepContent { get; set; } = string.Empty;

    public StepVerdict Verdict { get; set; }
    public ErrorTag ErrorTag { get; set; }

    /// <summary>本步已放出的提示级别（0~4）</summary>
    public int HintLevel { get; set; }

    /// <summary>系统当时给出的启发话术（用于回放卡壳轨迹）</summary>
    public string Feedback { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>错题集合与复测状态</summary>
public class MistakeRecord
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string ProblemId { get; set; } = string.Empty;
    public ErrorTag ErrorTag { get; set; }

    public DateTime FirstWrongAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastReviewedAt { get; set; }

    /// <summary>复测是否通过</summary>
    public bool Passed { get; set; }
}

/// <summary>
/// 轻量 FSRS 式间隔复习计划
/// </summary>
public class ReviewSchedule
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string KnowledgeId { get; set; } = string.Empty;

    public DateTime DueAt { get; set; } = DateTime.UtcNow;

    /// <summary>当前间隔（天）</summary>
    public int IntervalDays { get; set; } = 1;

    /// <summary>稳定度系数（简易 FSRS，不需要外部库）</summary>
    public double EaseFactor { get; set; } = 2.5;

    public int Repetitions { get; set; }
}

/// <summary>闯关进度</summary>
public class ChallengeProgress
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string ProblemId { get; set; } = string.Empty;

    public ChallengeStatus Status { get; set; } = ChallengeStatus.NotStarted;
    public int CurrentStep { get; set; }
    public int RetryCount { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>某学生某题的导学状态（状态机持久化载体）</summary>
public class TutorState
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string ProblemId { get; set; } = string.Empty;

    public TutorStatus Status { get; set; } = TutorStatus.AwaitingStep;

    /// <summary>已经通过的步骤数（下一步的 StepIndex）</summary>
    public int CurrentStepIndex { get; set; } = 1;

    /// <summary>当前步骤连续卡壳次数</summary>
    public int ConsecutiveStuck { get; set; }

    /// <summary>当前步骤已放出的提示级别 0~4</summary>
    public int HintLevel { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
