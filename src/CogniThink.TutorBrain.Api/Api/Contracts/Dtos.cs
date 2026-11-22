using CogniThink.TutorBrain.Application.Gateway;

namespace CogniThink.TutorBrain.Api.Contracts;

/// <summary>
/// 前端 DTO 约定：
/// - 绝不返回 AnswerKey / ExpectedExpression（防止泄题）
/// - 所有字段为结构化数据，前端负责探险 UI 与推理画布
/// </summary>

public sealed record StudentDto(
    string StudentId,
    string Name,
    DateTime CreatedAt,
    List<MasteryDto> Masteries,
    List<ProgressDto> Progress,
    int MistakeCount);

public sealed record MasteryDto(
    string KnowledgeId,
    string Name,
    double Value,
    int Attempts,
    int Corrects);

public sealed record ProgressDto(
    string ProblemId,
    string Status,
    int CurrentStep,
    int RetryCount);

public sealed record ProblemDto(
    string Id,
    string Subject,
    string Category,
    string Title,
    string Stem,
    int Difficulty,
    int TotalSteps,
    List<string> KnowledgeIds,
    List<ProblemStepDto> Steps,
    string? VariantOfId);

/// <summary>向学生展示的步骤信息（不含标准答案与判定表达式）</summary>
public sealed record ProblemStepDto(
    int StepIndex,
    string Prompt,
    bool IsKeyStep,
    string KnowledgeId);

public sealed record SubmitStepRequest(
    string StudentId,
    string ProblemId,
    int StepIndex,
    string StepContent);

public sealed record SubmitStepResponse(
    string Status,
    string Verdict,
    string ErrorTag,
    int HintLevel,
    string RecommendedAction,
    string Message,
    List<string> RelatedKnowledgeIds,
    bool UnlockedNextStep,
    bool Completed,
    int NextStepIndex,
    string? HintText,
    double? Mastery,
    int CurrentStep,
    int TotalSteps,
    List<WeakKnowledgeDto> WeakPrerequisites,
    ChemicalEvidenceDto? ChemicalEvidence,
    ProblemDto? Variant,
    List<ReasoningNode>? StandardReasoningChain);

public sealed record ChemicalEvidenceDto(
    string QueryType,
    string Conclusion,
    List<string> KeyFacts,
    List<ChemicalConflictDto> Conflicts,
    List<ChemicalReactionDto> RelatedReactions);

public sealed record ChemicalConflictDto(
    List<string> Ions,
    string Type,
    string Result,
    List<string> KnowledgeIds);

public sealed record ChemicalReactionDto(
    string Id,
    string Equation,
    List<string> Conditions,
    List<string> Phenomena,
    List<string> KnowledgeIds);

public sealed record WeakKnowledgeDto(
    string KnowledgeId,
    string Name,
    string Subject,
    double Mastery,
    bool IsPrerequisite);

public sealed record ReviewSuggestionDto(
    string KnowledgeId,
    string KnowledgeName,
    string Reason,
    DateTime DueAt,
    double Mastery);

public sealed record SaveProgressRequest(
    string ProblemId,
    int CurrentStep,
    bool Completed,
    int RetryCount);

public sealed record KnowledgeDto(
    string Id,
    string Name,
    string Subject,
    string Level,
    string Description,
    List<string> Prerequisites,
    List<string> ErrorTags);

public sealed record ProblemSummaryDto(
    string Id,
    string Subject,
    string Category,
    string Title,
    int Difficulty,
    int TotalSteps,
    string? VariantOfId);
