namespace CogniThink.TutorBrain.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CogniThink.TutorBrain.Api.Contracts;
using CogniThink.TutorBrain.Application;
using CogniThink.TutorBrain.Domain;
using CogniThink.TutorBrain.Infrastructure;

[ApiController]
[Route("api/student")]
public sealed class StudentController : ControllerBase
{
    private readonly StudentModelService _students;
    private readonly KnowledgeGraphService _graph;
    private readonly TutorDbContext _db;

    public StudentController(
        StudentModelService students,
        KnowledgeGraphService graph,
        TutorDbContext db)
    {
        _students = students;
        _graph = graph;
        _db = db;
    }

    /// <summary>创建 / 获取学生学情</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<StudentDto>> Get(string id, CancellationToken ct)
    {
        var student = await _students.GetOrCreateAsync(id, ct);
        var names = await _graph.GetNameMapAsync(student.Masteries.Select(m => m.KnowledgeId), ct);
        return Ok(DtoMapper.ToDto(student, names));
    }

    /// <summary>获取薄弱知识点（掌握度低于阈值）</summary>
    [HttpGet("{id}/weak-knowledge")]
    public async Task<ActionResult<List<WeakKnowledgeDto>>> GetWeakKnowledge(
        string id,
        [FromQuery] double threshold = 0.5,
        CancellationToken ct = default)
    {
        await _students.GetOrCreateAsync(id, ct);
        var items = await _graph.GetWeakKnowledgeAsync(id, threshold, ct);
        return Ok(items.Select(DtoMapper.ToDto).ToList());
    }

    /// <summary>触发复测建议（间隔复习到期 + 薄弱知识点）</summary>
    [HttpGet("{id}/review-suggest")]
    public async Task<ActionResult<List<ReviewSuggestionDto>>> GetReviewSuggestions(
        string id,
        CancellationToken ct)
    {
        await _students.GetOrCreateAsync(id, ct);
        var items = await _students.GetReviewSuggestionsAsync(id, ct);
        return Ok(items.Select(s => new ReviewSuggestionDto(
            s.KnowledgeId, s.KnowledgeName, s.Reason, s.DueAt, Math.Round(s.Mastery, 3))).ToList());
    }

    /// <summary>获取闯关进度</summary>
    [HttpGet("{id}/progress")]
    public async Task<ActionResult<List<ProgressDto>>> GetProgress(string id, CancellationToken ct)
    {
        await _students.GetOrCreateAsync(id, ct);
        var rows = await _db.ChallengeProgresses
            .Where(p => p.StudentId == id)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync(ct);

        return Ok(rows.Select(p => new ProgressDto(
            p.ProblemId, p.Status.ToString(), p.CurrentStep, p.RetryCount)).ToList());
    }

    /// <summary>保存闯关进度（前端主动上报）</summary>
    [HttpPost("{id}/progress")]
    public async Task<IActionResult> SaveProgress(
        string id,
        [FromBody] SaveProgressRequest request,
        CancellationToken ct)
    {
        await _students.GetOrCreateAsync(id, ct);

        var progress = await _db.ChallengeProgresses
            .FirstOrDefaultAsync(p => p.StudentId == id && p.ProblemId == request.ProblemId, ct);

        if (progress is null)
        {
            progress = new ChallengeProgress { StudentId = id, ProblemId = request.ProblemId };
            _db.ChallengeProgresses.Add(progress);
        }

        progress.CurrentStep = request.CurrentStep;
        progress.RetryCount = request.RetryCount;
        progress.Status = request.Completed ? ChallengeStatus.Completed : ChallengeStatus.InProgress;
        progress.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
