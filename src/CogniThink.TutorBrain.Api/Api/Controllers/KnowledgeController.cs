namespace CogniThink.TutorBrain.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CogniThink.TutorBrain.Api.Contracts;
using CogniThink.TutorBrain.Application;
using CogniThink.TutorBrain.Domain;
using CogniThink.TutorBrain.Infrastructure;

[ApiController]
[Route("api/knowledge")]
public sealed class KnowledgeController : ControllerBase
{
    private readonly TutorDbContext _db;
    private readonly KnowledgeGraphService _graph;

    public KnowledgeController(TutorDbContext db, KnowledgeGraphService graph)
    {
        _db = db;
        _graph = graph;
    }

    /// <summary>知识点列表</summary>
    [HttpGet]
    public async Task<ActionResult<List<KnowledgeDto>>> List(
        [FromQuery] string? subject,
        CancellationToken ct)
    {
        Subject? parsed = null;
        if (!string.IsNullOrWhiteSpace(subject) && Enum.TryParse<Subject>(subject, true, out var s))
            parsed = s;

        var nodes = await _graph.GetNodesAsync(parsed, ct);
        var edges = await _db.KnowledgeEdges.ToListAsync(ct);

        var result = nodes.Select(n => new KnowledgeDto(
            n.Id,
            n.Name,
            n.Subject.ToString(),
            n.Level.ToString(),
            n.Description,
            edges.Where(e => e.ToId == n.Id).Select(e => e.FromId).ToList(),
            n.ErrorTags.Select(t => t.ToText()).ToList())).ToList();

        return Ok(result);
    }

    /// <summary>单个知识点</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<KnowledgeDto>> Get(string id, CancellationToken ct)
    {
        var node = await _graph.GetNodeAsync(id, ct);
        if (node is null) return NotFound(new { message = $"知识点 {id} 不存在" });

        var prereqs = await _db.KnowledgeEdges
            .Where(e => e.ToId == id).Select(e => e.FromId).ToListAsync(ct);

        return Ok(new KnowledgeDto(
            node.Id, node.Name, node.Subject.ToString(), node.Level.ToString(),
            node.Description, prereqs, node.ErrorTags.Select(t => t.ToText()).ToList()));
    }

    /// <summary>查询知识点依赖链（全部前置，递归）</summary>
    [HttpGet("{id}/dependencies")]
    public async Task<ActionResult<List<string>>> GetDependencies(string id, CancellationToken ct)
    {
        var node = await _graph.GetNodeAsync(id, ct);
        if (node is null) return NotFound(new { message = $"知识点 {id} 不存在" });
        return Ok(await _graph.GetPrerequisiteChainAsync(id, ct));
    }

    /// <summary>查询薄弱前置知识点（导学原则：先补前置再学后继）</summary>
    [HttpGet("{id}/weak-prerequisites")]
    public async Task<ActionResult<List<WeakKnowledgeDto>>> GetWeakPrerequisites(
        string id,
        [FromQuery] string studentId,
        [FromQuery] double threshold = 0.5,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(studentId))
            return BadRequest(new { message = "studentId 必填" });

        var items = await _graph.GetWeakPrerequisitesAsync(studentId, id, threshold, ct);
        return Ok(items.Select(DtoMapper.ToDto).ToList());
    }
}
