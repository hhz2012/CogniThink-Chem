namespace CogniThink.TutorBrain.Application;

using Microsoft.EntityFrameworkCore;
using CogniThink.TutorBrain.Domain;
using CogniThink.TutorBrain.Infrastructure;

/// <summary>
/// 变式生成服务：基于母题模板做可控变式。
/// 原则：不自由编题，不改变正确答案；保证同一知识点、同一考察逻辑。
/// 生成的是"同结构复测变式"（换标题/难度，保留推理链与标准答案）。
/// </summary>
public sealed class VariantService
{
    private readonly TutorDbContext _db;

    public VariantService(TutorDbContext db) => _db = db;

    public async Task<Problem?> GenerateAsync(Problem mother, CancellationToken ct = default)
    {
        var variantId = $"{mother.Id}-V1";

        var existing = await _db.Problems
            .Include(p => p.Steps)
            .Include(p => p.KnowledgeLinks)
            .FirstOrDefaultAsync(p => p.Id == variantId, ct);
        if (existing is not null) return existing;

        var variant = new Problem
        {
            Id = variantId,
            Subject = mother.Subject,
            Category = mother.Category,
            Title = $"{mother.Title} · 变式复测",
            Stem = mother.Stem +
                   "\n\n【变式要求】不看之前的作答记录，从第一步开始完整重推一遍；每一步都要写出依据。",
            AnswerKey = mother.AnswerKey,
            VariantOfId = mother.Id,
            Difficulty = Math.Min(5, mother.Difficulty + 1)
        };

        foreach (var link in mother.KnowledgeLinks)
        {
            variant.KnowledgeLinks.Add(new ProblemKnowledge
            {
                ProblemId = variantId,
                KnowledgeId = link.KnowledgeId,
                IsPrimary = link.IsPrimary
            });
        }

        foreach (var step in mother.Steps)
        {
            variant.Steps.Add(new ProblemStep
            {
                ProblemId = variantId,
                StepIndex = step.StepIndex,
                Prompt = step.Prompt,
                ExpectedExpression = step.ExpectedExpression,
                ExpectedKeywords = step.ExpectedKeywords,
                KnowledgeId = step.KnowledgeId,
                IsKeyStep = step.IsKeyStep,
                HintSelfCheck = step.HintSelfCheck,
                HintSubQuestion = step.HintSubQuestion,
                HintClue = step.HintClue,
                Scaffold = step.Scaffold
            });
        }

        _db.Problems.Add(variant);
        await _db.SaveChangesAsync(ct);
        return variant;
    }
}
