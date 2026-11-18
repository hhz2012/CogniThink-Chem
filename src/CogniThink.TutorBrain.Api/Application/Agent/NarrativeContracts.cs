namespace CogniThink.TutorBrain.Application.Agent;

using CogniThink.TutorBrain.Application.Reasoning;
using CogniThink.TutorBrain.Domain;

/// <summary>导学话术场景（策略由状态机决定，本层只负责措辞）</summary>
public enum NarrativeScene
{
    StepCorrect,
    PartialMistake,
    KeyMistake,
    EscalatingHint,
    LevelComplete,
    FormatIssue
}

public sealed record NarrativeRequest(
    NarrativeScene Scene,
    Subject Subject,
    string ProblemTitle,
    int StepIndex,
    string StepPrompt,
    StepEvaluation Evaluation,
    int HintLevel,
    string? HintText,
    IReadOnlyList<string> KnowledgeNames,
    string? VariantHint);

/// <summary>
/// 导学话术生成器。
/// 红线：只生成启发式语言，不判定对错、不直接给出最终答案。
/// </summary>
public interface ITutorNarrativeAgent
{
    Task<string> ComposeAsync(NarrativeRequest request, CancellationToken cancellationToken = default);
}
