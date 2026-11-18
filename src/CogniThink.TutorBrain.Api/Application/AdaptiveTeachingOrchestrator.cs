namespace CogniThink.TutorBrain.Application;

using Microsoft.EntityFrameworkCore;
using CogniThink.TutorBrain.Application.Diagnostic;
using CogniThink.TutorBrain.Domain;
using CogniThink.TutorBrain.Infrastructure;

/// <summary>
/// 自适应教学编排器：把"答错 → 诊断定位盲区 → 更新能力图谱 → 讲解 → 变式练习"串成完整闭环。
///
/// 铁律：
/// - 掌握度更新只由判定结果驱动（BKT），不臆造。
/// - 变式题由 VariantService 基于母题生成，不自由编题。
/// - 诊断判定复用 DiagnosticTeachingEngine（规则引擎），LLM 不参与。
/// </summary>
public sealed class AdaptiveTeachingOrchestrator
{
    private readonly DiagnosticTeachingEngine _diagnostic;
    private readonly StudentModelService _studentModel;
    private readonly VariantService _variants;
    private readonly TutorDbContext _db;

    public AdaptiveTeachingOrchestrator(
        DiagnosticTeachingEngine diagnostic,
        StudentModelService studentModel,
        VariantService variants,
        TutorDbContext db)
    {
        _diagnostic = diagnostic;
        _studentModel = studentModel;
        _variants = variants;
        _db = db;
    }

    // ==================== 1. 答错 → 启动诊断 ====================

    /// <summary>
    /// 学生答错某步时调用：记录答错轨迹，启动诊断会话，返回第一个检查点。
    /// </summary>
    public async Task<DiagnosticStartResult?> StartForWrongAnswerAsync(
        string studentId,
        string problemId,
        int stepIndex,
        string wrongAnswer,
        CancellationToken ct = default)
    {
        // 确保学生存在（掌握度/错题等表有外键约束）
        await _studentModel.GetOrCreateAsync(studentId, ct);

        // 记录答错轨迹
        await _studentModel.AddStepTraceAsync(
            studentId, problemId, stepIndex, wrongAnswer,
            StepVerdict.Incorrect, ErrorTag.ConceptConfusion, 0,
            "答错，启动诊断导学", ct);

        // 记录错题
        await _studentModel.RecordMistakeAsync(studentId, problemId, ErrorTag.ConceptConfusion, ct);

        await _db.SaveChangesAsync(ct);

        // 启动诊断
        return await _diagnostic.StartAsync(studentId, problemId, stepIndex, ct);
    }

    // ==================== 2. 回答诊断检查点 ====================

    /// <summary>
    /// 学生回答诊断检查点。若定位到盲区，则降低对应知识点掌握度并记录。
    /// </summary>
    public async Task<DiagnosticAnswerResult> SubmitDiagnosticAnswerAsync(
        string sessionId,
        string answer,
        CancellationToken ct = default)
    {
        var result = _diagnostic.SubmitAnswer(sessionId, answer);

        // 定位到盲区时：降低掌握度 + 记录
        if (result.Action == DiagnosticAction.Explain && result.GapCheckpointIndex.HasValue)
        {
            var session = _diagnostic.GetSession(sessionId);
            if (session is not null)
            {
                var gapCp = session.Structure.Checkpoints
                    .FirstOrDefault(c => c.Index == result.GapCheckpointIndex);
                if (gapCp is not null)
                {
                    // 降低盲区知识点的掌握度
                    await _studentModel.UpdateMasteryAsync(
                        session.StudentId, gapCp.KnowledgeId, correct: false, ct);
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        return result;
    }

    // ==================== 3. 重试通过 → 更新掌握度 + 变式练习 ====================

    /// <summary>
    /// 学生重试原步骤。若通过，则提升掌握度、安排复习、标记错题通过，并生成变式练习题。
    /// </summary>
    public async Task<RetryWithPracticeResult> RetryWithPracticeAsync(
        string sessionId,
        string answer,
        CancellationToken ct = default)
    {
        var result = _diagnostic.RetryStep(sessionId, answer);

        if (result.Action != DiagnosticAction.Completed)
        {
            await _db.SaveChangesAsync(ct);
            return new RetryWithPracticeResult(result, null, null);
        }

        // 通过：提升掌握度 + 安排复习 + 标记错题通过 + 生成变式
        var session = _diagnostic.GetSession(sessionId);
        if (session is not null)
        {
            // 找到盲区知识点（若有），提升其掌握度
            var targetKnowledgeId = session.GapCheckpointIndex.HasValue
                ? session.Structure.Checkpoints
                    .FirstOrDefault(c => c.Index == session.GapCheckpointIndex)?.KnowledgeId
                : session.Structure.Checkpoints.LastOrDefault()?.KnowledgeId;

            if (!string.IsNullOrEmpty(targetKnowledgeId))
            {
                await _studentModel.UpdateMasteryAsync(
                    session.StudentId, targetKnowledgeId, correct: true, ct);
                await _studentModel.ScheduleReviewAsync(
                    session.StudentId, targetKnowledgeId, good: true, ct);
            }

            // 标记错题通过
            await _studentModel.MarkMistakePassedAsync(session.StudentId, session.ProblemId, ct);

            // 生成变式练习题
            var mother = await _db.Problems
                .Include(p => p.Steps)
                .Include(p => p.KnowledgeLinks)
                .FirstOrDefaultAsync(p => p.Id == session.ProblemId, ct);

            Problem? variant = null;
            if (mother is not null)
            {
                variant = await _variants.GenerateAsync(mother, ct);
            }

            await _db.SaveChangesAsync(ct);
            return new RetryWithPracticeResult(result, variant, targetKnowledgeId);
        }

        await _db.SaveChangesAsync(ct);
        return new RetryWithPracticeResult(result, null, null);
    }
}

/// <summary>重试结果 + 变式练习题</summary>
public sealed record RetryWithPracticeResult(
    DiagnosticAnswerResult RetryResult,
    Problem? PracticeVariant,
    string? TargetKnowledgeId);
