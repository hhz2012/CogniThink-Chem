namespace CogniThink.TutorBrain.Application;

using Microsoft.EntityFrameworkCore;
using CogniThink.TutorBrain.Application.Chemistry;
using CogniThink.TutorBrain.Domain;
using CogniThink.TutorBrain.Domain.Chemistry;
using CogniThink.TutorBrain.Infrastructure;

public sealed record SubmitStepInput(
    string StudentId,
    string ProblemId,
    int StepIndex,
    string StepContent);

public sealed record SubmitStepResult(
    TutorDecision Decision,
    Problem? Variant,
    IReadOnlyList<WeakKnowledgeItem> WeakPrerequisites,
    double? Mastery,
    int CurrentStep,
    int TotalSteps,
    ChemicalQueryResult? ChemicalEvidence,
    bool Completed);

/// <summary>
/// 导学编排服务：加载题目 → 调状态机 → 更新学情 → 生成变式 → 返回结构化回复
/// </summary>
public sealed class TutorService
{
    private readonly TutorDbContext _db;
    private readonly TutorStateMachine _machine;
    private readonly StudentModelService _students;
    private readonly KnowledgeGraphService _graph;
    private readonly VariantService _variants;
    private readonly ProblemQueryCompiler _queryCompiler;
    private readonly ChemicalInferenceService _chemicalInference;

    public TutorService(
        TutorDbContext db,
        TutorStateMachine machine,
        StudentModelService students,
        KnowledgeGraphService graph,
        VariantService variants,
        ProblemQueryCompiler queryCompiler,
        ChemicalInferenceService chemicalInference)
    {
        _db = db;
        _machine = machine;
        _students = students;
        _graph = graph;
        _variants = variants;
        _queryCompiler = queryCompiler;
        _chemicalInference = chemicalInference;
    }

    public async Task<SubmitStepResult?> SubmitStepAsync(SubmitStepInput input, CancellationToken ct = default)
    {
        await _students.GetOrCreateAsync(input.StudentId, ct);

        var problem = await _db.Problems
            .Include(p => p.Steps)
            .Include(p => p.KnowledgeLinks)
            .FirstOrDefaultAsync(p => p.Id == input.ProblemId, ct);
        if (problem is null) return null;

        var step = problem.Steps.FirstOrDefault(s => s.StepIndex == input.StepIndex);
        if (step is null) return null;

        var state = await _db.TutorStates
            .FirstOrDefaultAsync(s => s.StudentId == input.StudentId && s.ProblemId == input.ProblemId, ct);
        if (state is null)
        {
            state = new TutorState
            {
                StudentId = input.StudentId,
                ProblemId = input.ProblemId,
                Status = TutorStatus.AwaitingStep,
                CurrentStepIndex = 1,
                ConsecutiveStuck = 0,
                HintLevel = 0
            };
            _db.TutorStates.Add(state);
        }

        // 知识点名称（供话术返回，不暴露答案）
        var names = await _graph.GetNameMapAsync(problem.KnowledgeLinks.Select(k => k.KnowledgeId), ct);
        var stepKpName = names.TryGetValue(step.KnowledgeId, out var n) ? n : step.KnowledgeId;
        var knowledgeNames = names.Values.Distinct().Append(stepKpName).Distinct().ToArray();

        // 关键步骤查询知识库：结论与证据进入反馈，但不替代状态机的对错判定。
        var chemicalEvidence = step.IsKeyStep ? BuildChemicalEvidence(problem, step) : null;

        // 状态机判定（领域引擎负责对错，LLM 只负责话术）
        var decision = await _machine.HandleAsync(problem, step, state, knowledgeNames, input.StepContent, ct);

        // 卡壳轨迹
        await _students.AddStepTraceAsync(
            input.StudentId, problem.Id, step.StepIndex, input.StepContent,
            decision.Verdict, decision.ErrorTag, decision.HintLevel, decision.Message, ct);

        // 掌握度与复习计划
        double? masteryValue = null;
        if (decision.Verdict != StepVerdict.Ungradable)
        {
            var isCorrect = decision.Verdict == StepVerdict.Correct;
            var mastery = await _students.UpdateMasteryAsync(input.StudentId, step.KnowledgeId, isCorrect, ct);
            masteryValue = mastery.Value;
            await _students.ScheduleReviewAsync(input.StudentId, step.KnowledgeId, isCorrect, ct);

            if (!isCorrect)
            {
                await _students.RecordMistakeAsync(input.StudentId, problem.Id, decision.ErrorTag, ct);
            }
            else if (decision.Completed)
            {
                await _students.MarkMistakePassedAsync(input.StudentId, problem.Id, ct);
            }
        }

        // 闯关进度
        var currentStep = decision.Completed ? problem.Steps.Count : decision.NextStepIndex;
        await _students.UpdateProgressAsync(
            input.StudentId, problem.Id, currentStep, decision.Completed,
            retried: decision.Status == TutorStatus.EscalatingHint, ct);

        // 通关：生成同知识点变式
        Problem? variant = null;
        if (decision.Completed)
        {
            variant = await _variants.GenerateAsync(problem, ct);
        }

        // 关键错误：定位薄弱前置知识点（导学原则：先补前置）
        var weakPrereq = new List<WeakKnowledgeItem>();
        if (decision.Verdict is StepVerdict.Incorrect or StepVerdict.PartiallyCorrect)
        {
            weakPrereq = await _graph.GetWeakPrerequisitesAsync(input.StudentId, step.KnowledgeId, ct: ct);
        }

        await _db.SaveChangesAsync(ct);

        return new SubmitStepResult(
            decision,
            variant,
            weakPrereq,
            masteryValue,
            currentStep,
            problem.Steps.Count,
            chemicalEvidence,
            decision.Completed);
    }

    private ChemicalQueryResult? BuildChemicalEvidence(Problem problem, ProblemStep step)
    {
        // 只编译题目、当前步骤和已完成步骤的试剂上下文，不把学生回答当作知识库事实。
        var priorContext = problem.Steps
            .Where(s => s.StepIndex < step.StepIndex)
            .OrderBy(s => s.StepIndex)
            .Select(s => s.ExpectedExpression);
        var context = string.Join("\n", new[] { problem.Stem, step.Prompt }.Concat(priorContext));
        var query = _queryCompiler.Compile(context);
        var evaluated = _chemicalInference.Evaluate(query);
        if (query.Type == ChemicalQueryType.Unknown) return null;

        var knowledgeIds = problem.KnowledgeLinks
            .Select(k => k.KnowledgeId)
            .Append(step.KnowledgeId)
            .ToHashSet();
        var candidates = evaluated.RelatedReactions
            .Where(r => r.KnowledgeIds.Any(knowledgeIds.Contains))
            .OrderByDescending(r => r.Reactants.Count(id => query.Substances.Contains(id)))
            .ToList();
        var maxOverlap = candidates.Count == 0
            ? 0
            : candidates[0].Reactants.Count(id => query.Substances.Contains(id));
        var reactions = candidates
            .Where(r => maxOverlap <= 1 || r.Reactants.Count(id => query.Substances.Contains(id)) == maxOverlap)
            .Take(3)
            .ToList();

        // 若反应被知识点过滤，同步移除对应的反应证据，避免展示无关内容。
        var evidence = evaluated.Evidence
            .Where(text => !text.Contains("；现象：") ||
                           reactions.Any(r => text.StartsWith(r.Equation + "；", StringComparison.Ordinal)))
            .Distinct()
            .Take(4)
            .ToList();

        var result = evaluated with { Evidence = evidence, RelatedReactions = reactions };
        return result.Evidence.Count == 0 && result.Conflicts.Count == 0 && result.RelatedReactions.Count == 0
            ? null
            : result;
    }

    public async Task<List<StepTrace>> GetStepTracesAsync(
        string studentId,
        string problemId,
        CancellationToken ct = default) =>
        await _db.StepTraces
            .Where(t => t.StudentId == studentId && t.ProblemId == problemId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);
}
