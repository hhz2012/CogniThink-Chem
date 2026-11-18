namespace CogniThink.TutorBrain.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CogniThink.TutorBrain.Application.Chemistry;
using CogniThink.TutorBrain.Domain.Chemistry;
using CogniThink.TutorBrain.Infrastructure;

public sealed record CoexistRequest(
    List<string> Ions,
    bool Acidic = false,
    bool Alkaline = false);

public sealed record CompileRequest(string Stem);

/// <summary>
/// 化学知识库接口：物质、颜色、溶解度、反应、离子共存、题目编译。
/// 所有结论来自内部知识库与规则，不经大模型。
/// </summary>
[ApiController]
[Route("api/chemistry")]
public sealed class ChemistryController : ControllerBase
{
    private readonly ChemicalKnowledgeBase _kb;
    private readonly ChemicalInferenceService _chem;
    private readonly ProblemQueryCompiler _compiler;
    private readonly ChemicalSimulationEngine _simulator;
    private readonly ChemicalReverseInferenceEngine _reverse;
    private readonly ReactionEquationParser _equationParser;
    private readonly TutorDbContext _db;

    public ChemistryController(
        ChemicalKnowledgeBase kb,
        ChemicalInferenceService chem,
        ProblemQueryCompiler compiler,
        ChemicalSimulationEngine simulator,
        ChemicalReverseInferenceEngine reverse,
        ReactionEquationParser equationParser,
        TutorDbContext db)
    {
        _kb = kb;
        _chem = chem;
        _compiler = compiler;
        _simulator = simulator;
        _reverse = reverse;
        _equationParser = equationParser;
        _db = db;
    }

    /// <summary>物质检索（名称、化学式、别名）</summary>
    [HttpGet("substances")]
    public ActionResult<IEnumerable<Substance>> ListSubstances([FromQuery] string? q)
    {
        var list = _kb.Substances.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            list = list.Where(s =>
                s.Name.Contains(q) ||
                s.Formula.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                s.Aliases.Any(a => a.Contains(q)));
        }
        return Ok(list);
    }

    /// <summary>单个物质详情（颜色、溶解性、特性）</summary>
    [HttpGet("substances/{key}")]
    public ActionResult<Substance> GetSubstance(string key)
    {
        var substance = _kb.FindSubstance(key);
        return substance is null
            ? NotFound(new { message = $"未找到物质 {key}" })
            : Ok(substance);
    }

    /// <summary>离子列表</summary>
    [HttpGet("ions")]
    public ActionResult<IEnumerable<ChemicalIon>> ListIons() => Ok(_kb.Ions);

    /// <summary>查询溶解性</summary>
    [HttpGet("solubility")]
    public ActionResult<object> GetSolubility([FromQuery] string cation, [FromQuery] string anion)
    {
        var level = _chem.GetSolubility(cation, anion);
        return Ok(new
        {
            cation,
            anion,
            level = level.ToString(),
            text = ChemicalInferenceService.SolubilityText(level)
        });
    }

    /// <summary>按物质查询相关反应</summary>
    [HttpGet("reactions")]
    public ActionResult<IEnumerable<Reaction>> ListReactions([FromQuery] string? substance)
    {
        if (string.IsNullOrWhiteSpace(substance)) return Ok(_kb.Reactions);

        var item = _kb.FindSubstance(substance);
        return Ok(item is null ? Array.Empty<Reaction>() : _kb.GetReactionsFor(item.Id));
    }

    /// <summary>离子共存判断（返回冲突证据）</summary>
    [HttpPost("coexist")]
    public ActionResult<object> CheckCoexistence([FromBody] CoexistRequest request)
    {
        if (request.Ions.Count == 0)
            return BadRequest(new { message = "ions 不能为空" });

        var result = _chem.CheckCoexistence(request.Ions, request.Acidic, request.Alkaline);
        return Ok(new
        {
            canCoexist = result.CanCoexist,
            conflicts = result.Conflicts,
            evidence = result.Evidence
        });
    }

    /// <summary>题目编译：题干 → 内部查询 → 知识库求值</summary>
    [HttpPost("compile")]
    public ActionResult<object> Compile([FromBody] CompileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Stem))
            return BadRequest(new { message = "stem 不能为空" });

        var query = _compiler.Compile(request.Stem);
        var result = _chem.Evaluate(query);
        return Ok(result);
    }

    /// <summary>知识点 → 反应 → 反应物/产物 → 颜色、溶解性与特性的完整链路</summary>
    [HttpGet("knowledge/{id}/graph")]
    public async Task<ActionResult<object>> GetKnowledgeGraph(string id, CancellationToken ct)
    {
        var knowledge = await _db.KnowledgeNodes.AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == id, ct);
        if (knowledge is null)
            return NotFound(new { message = $"未找到知识点 {id}" });

        var reactions = _kb.GetReactionsForKnowledge(id);
        var substances = _kb.GetSubstancesForKnowledge(id);
        return Ok(new
        {
            knowledgeId = id,
            knowledgeName = knowledge.Name,
            reactionCount = reactions.Count,
            substanceCount = substances.Count,
            reactions = reactions.Select(r => new
            {
                r.Id,
                r.Equation,
                Type = r.Type.ToString(),
                r.Conditions,
                r.Phenomena,
                r.KnowledgeIds,
                Reactants = r.Reactants.Select(_kb.FindSubstance).Where(s => s is not null).ToList(),
                Products = r.Products.Select(_kb.FindSubstance).Where(s => s is not null).ToList()
            }),
            substances
        });
    }

    /// <summary>
    /// 正向状态推演：初始物质 + 条件 → 依次发生的反应 → 剩余物质、最终颜色/状态与现象。
    /// 可用 stages 表示分阶段加料（先溶解、后沉淀）。
    /// 例：{"species":[{"substance":"Fe","moles":1},{"substance":"CuSO4","moles":1}],"aqueous":true}
    /// </summary>
    [HttpPost("simulate")]
    public ActionResult<SimulationResult> Simulate([FromBody] SimulationRequest request)
    {
        if (request.Species.Count == 0)
            return BadRequest(new { message = "species 不能为空" });

        return Ok(_simulator.Simulate(request));
    }

    /// <summary>
    /// 逆向推理：已知物质 + 观察到的颜色/沉淀/气体/目标产物 → 可能发生的反应、缺少的物质与制备路径，
    /// 并用正向推演回放验证。
    /// 例：{"known":["KSCN"],"solutionColor":"血红色","targetProducts":["Fe(SCN)3"]}
    /// </summary>
    [HttpPost("reverse")]
    public ActionResult<ReverseInferenceResult> Reverse([FromBody] ReverseInferenceRequest request)
    {
        return Ok(_reverse.Infer(request));
    }

    /// <summary>知识库健康检查（数量与自检错误）</summary>
    [HttpGet("health")]
    public ActionResult<object> Health() => Ok(new
    {
        substances = _kb.Substances.Count,
        ions = _kb.Ions.Count,
        reactions = _kb.Reactions.Count,
        interactions = _kb.Interactions.Count,
        stoichiometryWarnings = _equationParser.Warnings,
        errors = _kb.LoadErrors
    });
}
