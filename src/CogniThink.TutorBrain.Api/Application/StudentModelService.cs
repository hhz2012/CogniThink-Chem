namespace CogniThink.TutorBrain.Application;

using Microsoft.EntityFrameworkCore;
using CogniThink.TutorBrain.Domain;
using CogniThink.TutorBrain.Infrastructure;

public sealed record ReviewSuggestion(
    string KnowledgeId,
    string KnowledgeName,
    string Reason,
    DateTime DueAt,
    double Mastery);

/// <summary>学生学情模型服务：掌握度、卡壳轨迹、错题、间隔复习、闯关进度</summary>
public sealed class StudentModelService
{
    private const double Alpha = 0.15;  // 答对增益
    private const double Beta = 0.25;   // 答错衰减
    private const double MinMastery = 0.02;
    private const double MaxMastery = 0.99;
    private const double DefaultMastery = 0.20;

    private readonly TutorDbContext _db;

    public StudentModelService(TutorDbContext db) => _db = db;

    public async Task<Student> GetOrCreateAsync(string studentId, CancellationToken ct = default)
    {
        var student = await _db.Students
            .Include(s => s.Masteries)
            .Include(s => s.Mistakes)
            .Include(s => s.Progress)
            .Include(s => s.Reviews)
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        if (student is not null) return student;

        student = new Student { Id = studentId, Name = studentId };
        _db.Students.Add(student);
        await _db.SaveChangesAsync(ct);
        return student;
    }

    /// <summary>简化 BKT：答对 M'=M+α(1-M)，答错 M'=M-βM</summary>
    public async Task<KnowledgeMastery> UpdateMasteryAsync(
        string studentId,
        string knowledgeId,
        bool correct,
        CancellationToken ct = default)
    {
        var mastery = await _db.KnowledgeMasteries
            .FirstOrDefaultAsync(m => m.StudentId == studentId && m.KnowledgeId == knowledgeId, ct);

        if (mastery is null)
        {
            mastery = new KnowledgeMastery
            {
                StudentId = studentId,
                KnowledgeId = knowledgeId,
                Value = DefaultMastery
            };
            _db.KnowledgeMasteries.Add(mastery);
        }

        mastery.Attempts += 1;
        if (correct)
        {
            mastery.Corrects += 1;
            mastery.Value += Alpha * (1 - mastery.Value);
        }
        else
        {
            mastery.Value -= Beta * mastery.Value;
        }

        mastery.Value = Math.Clamp(mastery.Value, MinMastery, MaxMastery);
        mastery.UpdatedAt = DateTime.UtcNow;
        return mastery;
    }

    public async Task AddStepTraceAsync(
        string studentId,
        string problemId,
        int stepIndex,
        string content,
        StepVerdict verdict,
        ErrorTag errorTag,
        int hintLevel,
        string feedback,
        CancellationToken ct = default)
    {
        _db.StepTraces.Add(new StepTrace
        {
            StudentId = studentId,
            ProblemId = problemId,
            StepIndex = stepIndex,
            StepContent = content,
            Verdict = verdict,
            ErrorTag = errorTag,
            HintLevel = hintLevel,
            Feedback = feedback,
            CreatedAt = DateTime.UtcNow
        });
        await Task.CompletedTask;
    }

    /// <summary>记录错题（同一题同一错因只累加最近记录）</summary>
    public async Task RecordMistakeAsync(
        string studentId,
        string problemId,
        ErrorTag errorTag,
        CancellationToken ct = default)
    {
        var record = await _db.MistakeRecords
            .FirstOrDefaultAsync(m => m.StudentId == studentId && m.ProblemId == problemId, ct);

        if (record is null)
        {
            _db.MistakeRecords.Add(new MistakeRecord
            {
                StudentId = studentId,
                ProblemId = problemId,
                ErrorTag = errorTag,
                FirstWrongAt = DateTime.UtcNow,
                Passed = false
            });
        }
        else
        {
            record.ErrorTag = errorTag;
            record.Passed = false;
        }
    }

    /// <summary>标记错题复测通过</summary>
    public async Task MarkMistakePassedAsync(string studentId, string problemId, CancellationToken ct = default)
    {
        var record = await _db.MistakeRecords
            .FirstOrDefaultAsync(m => m.StudentId == studentId && m.ProblemId == problemId, ct);
        if (record is null) return;
        record.Passed = true;
        record.LastReviewedAt = DateTime.UtcNow;
    }

    /// <summary>轻量 FSRS：正确拉长间隔，错误重置为 1 天</summary>
    public async Task ScheduleReviewAsync(
        string studentId,
        string knowledgeId,
        bool good,
        CancellationToken ct = default)
    {
        var review = await _db.ReviewSchedules
            .FirstOrDefaultAsync(r => r.StudentId == studentId && r.KnowledgeId == knowledgeId, ct);

        if (review is null)
        {
            review = new ReviewSchedule
            {
                StudentId = studentId,
                KnowledgeId = knowledgeId,
                IntervalDays = 1,
                EaseFactor = 2.5,
                Repetitions = 0
            };
            _db.ReviewSchedules.Add(review);
        }

        if (good)
        {
            review.Repetitions += 1;
            review.IntervalDays = review.Repetitions switch
            {
                1 => 1,
                2 => 3,
                _ => Math.Min(60, (int)Math.Round(review.IntervalDays * review.EaseFactor))
            };
            review.EaseFactor = Math.Min(2.8, review.EaseFactor + 0.05);
        }
        else
        {
            review.Repetitions = 0;
            review.IntervalDays = 1;
            review.EaseFactor = Math.Max(1.3, review.EaseFactor - 0.2);
        }

        review.DueAt = DateTime.UtcNow.AddDays(review.IntervalDays);
    }

    public async Task UpdateProgressAsync(
        string studentId,
        string problemId,
        int currentStep,
        bool completed,
        bool retried,
        CancellationToken ct = default)
    {
        var progress = await _db.ChallengeProgresses
            .FirstOrDefaultAsync(p => p.StudentId == studentId && p.ProblemId == problemId, ct);

        if (progress is null)
        {
            progress = new ChallengeProgress
            {
                StudentId = studentId,
                ProblemId = problemId
            };
            _db.ChallengeProgresses.Add(progress);
        }

        progress.CurrentStep = Math.Max(progress.CurrentStep, currentStep);
        if (retried) progress.RetryCount += 1;
        progress.Status = completed ? ChallengeStatus.Completed : ChallengeStatus.InProgress;
        progress.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>复测建议：到期复习 + 薄弱知识点</summary>
    public async Task<List<ReviewSuggestion>> GetReviewSuggestionsAsync(
        string studentId,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var due = await _db.ReviewSchedules
            .Where(r => r.StudentId == studentId && r.DueAt <= now)
            .ToListAsync(ct);

        var nodes = await _db.KnowledgeNodes.ToDictionaryAsync(n => n.Id, n => n.Name, ct);
        var result = due.Select(r => new ReviewSuggestion(
                r.KnowledgeId,
                nodes.TryGetValue(r.KnowledgeId, out var name) ? name : r.KnowledgeId,
                "间隔复习到期",
                r.DueAt,
                0))
            .ToList();

        // 薄弱但未安排复习的知识点补充进来
        var scheduled = new HashSet<string>(due.Select(r => r.KnowledgeId));
        var weak = await _db.KnowledgeMasteries
            .Where(m => m.StudentId == studentId && m.Attempts > 0 && m.Value < 0.5)
            .OrderBy(m => m.Value)
            .Take(5)
            .ToListAsync(ct);

        foreach (var m in weak.Where(m => !scheduled.Contains(m.KnowledgeId)))
        {
            result.Add(new ReviewSuggestion(
                m.KnowledgeId,
                nodes.TryGetValue(m.KnowledgeId, out var n) ? n : m.KnowledgeId,
                "掌握度偏低，建议巩固",
                now,
                m.Value));
        }

        return result;
    }
}
