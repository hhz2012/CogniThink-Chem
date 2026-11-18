namespace CogniThink.TutorBrain.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CogniThink.TutorBrain.Api.Contracts;
using CogniThink.TutorBrain.Domain;
using CogniThink.TutorBrain.Infrastructure;

[ApiController]
[Route("api/problem")]
public sealed class ProblemController : ControllerBase
{
    private readonly TutorDbContext _db;

    public ProblemController(TutorDbContext db) => _db = db;

    /// <summary>题目列表（不返回标准答案）</summary>
    [HttpGet]
    public async Task<ActionResult<List<ProblemSummaryDto>>> List(
        [FromQuery] string? subject,
        CancellationToken ct)
    {
        var query = _db.Problems.Include(p => p.Steps).AsQueryable();

        if (!string.IsNullOrWhiteSpace(subject) &&
            Enum.TryParse<Subject>(subject, true, out var parsed))
        {
            query = query.Where(p => p.Subject == parsed);
        }

        var problems = await query.OrderBy(p => p.Difficulty).ToListAsync(ct);
        return Ok(problems.Select(DtoMapper.ToSummary).ToList());
    }

    /// <summary>加载题目（题干 + 步骤提示，不含标准表达式与答案）</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ProblemDto>> Get(string id, CancellationToken ct)
    {
        var problem = await _db.Problems
            .Include(p => p.Steps)
            .Include(p => p.KnowledgeLinks)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (problem is null) return NotFound(new { message = $"题目 {id} 不存在" });
        return Ok(DtoMapper.ToDto(problem));
    }
}
