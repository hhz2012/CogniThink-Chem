namespace CogniThink.TutorBrain.Api.Contracts;

using CogniThink.TutorBrain.Application.Chemistry;
using CogniThink.TutorBrain.Domain;
using CogniThink.TutorBrain.Domain.Chemistry;

/// <summary>实体 → DTO 映射（统一控制不泄题）</summary>
public static class DtoMapper
{
    public static ProblemDto ToDto(Problem p) => new(
        p.Id,
        p.Subject.ToString(),
        p.Category,
        p.Title,
        p.Stem,
        p.Difficulty,
        p.Steps.Count,
        p.KnowledgeLinks.Select(k => k.KnowledgeId).ToList(),
        p.Steps.OrderBy(s => s.StepIndex)
            .Select(s => new ProblemStepDto(s.StepIndex, s.Prompt, s.IsKeyStep, s.KnowledgeId))
            .ToList(),
        p.VariantOfId);

    public static ProblemSummaryDto ToSummary(Problem p) => new(
        p.Id,
        p.Subject.ToString(),
        p.Category,
        p.Title,
        p.Difficulty,
        p.Steps.Count,
        p.VariantOfId);

    public static StudentDto ToDto(
        Student s,
        IReadOnlyDictionary<string, string> knowledgeNames)
    {
        var masteries = s.Masteries
            .OrderByDescending(m => m.Value)
            .Select(m => new MasteryDto(
                m.KnowledgeId,
                knowledgeNames.TryGetValue(m.KnowledgeId, out var n) ? n : m.KnowledgeId,
                Math.Round(m.Value, 3),
                m.Attempts,
                m.Corrects))
            .ToList();

        var progress = s.Progress
            .Select(p => new ProgressDto(p.ProblemId, p.Status.ToString(), p.CurrentStep, p.RetryCount))
            .ToList();

        return new StudentDto(s.Id, s.Name, s.CreatedAt, masteries, progress, s.Mistakes.Count);
    }

    public static WeakKnowledgeDto ToDto(Application.WeakKnowledgeItem item) => new(
        item.KnowledgeId,
        item.Name,
        item.Subject.ToString(),
        Math.Round(item.Mastery, 3),
        item.IsPrerequisite);

    public static ChemicalEvidenceDto ToDto(ChemicalQueryResult result) => new(
        result.Query.Type.ToString(),
        result.Conclusion,
        result.Evidence.Distinct().Take(4).ToList(),
        result.Conflicts.Take(4).Select(c => new ChemicalConflictDto(
            c.Ions,
            c.Type.ToString(),
            c.Result,
            c.KnowledgeIds)).ToList(),
        result.RelatedReactions.Take(3).Select(r => new ChemicalReactionDto(
            r.Id,
            r.Equation,
            r.Conditions,
            r.Phenomena,
            r.KnowledgeIds)).ToList());
}
