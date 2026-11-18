namespace CogniThink.TutorBrain.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using CogniThink.TutorBrain.Application;
using CogniThink.TutorBrain.Application.Ability;
using CogniThink.TutorBrain.Domain;

/// <summary>
/// 能力评价与导学 API：
/// - 评价学生各化学解题能力得分
/// - 知识 + 能力综合评价
/// - 针对薄弱能力的引导式问答
/// </summary>
[ApiController]
[Route("api/ability")]
public sealed class AbilityController : ControllerBase
{
    private readonly AbilityEvaluationService _abilityEval;
    private readonly ComprehensiveEvaluationService _comprehensive;
    private readonly AbilityGuidedTeachingService _guided;
    private readonly PersonalAbilityGraphService _graph;
    private readonly ErrorPracticeLoopService _loop;
    private readonly StudentModelService _students;

    public AbilityController(
        AbilityEvaluationService abilityEval,
        ComprehensiveEvaluationService comprehensive,
        AbilityGuidedTeachingService guided,
        PersonalAbilityGraphService graph,
        ErrorPracticeLoopService loop,
        StudentModelService students)
    {
        _abilityEval = abilityEval;
        _comprehensive = comprehensive;
        _guided = guided;
        _graph = graph;
        _loop = loop;
        _students = students;
    }

    /// <summary>获取学生各能力得分</summary>
    [HttpGet("{studentId}/scores")]
    public async Task<IActionResult> GetScores(string studentId, CancellationToken ct)
    {
        await _students.GetOrCreateAsync(studentId, ct);
        var scores = await _abilityEval.EvaluateAsync(studentId, ct);
        return Ok(scores.Select(MapAbilityScore));
    }

    /// <summary>获取学生最薄弱的能力</summary>
    [HttpGet("{studentId}/weak")]
    public async Task<IActionResult> GetWeakAbilities(
        string studentId,
        [FromQuery] int topN = 3,
        CancellationToken ct = default)
    {
        await _students.GetOrCreateAsync(studentId, ct);
        var weak = await _abilityEval.GetWeakAbilitiesAsync(studentId, topN, ct);
        return Ok(weak.Select(MapAbilityScore));
    }

    /// <summary>获取知识 + 能力综合评价</summary>
    [HttpGet("{studentId}/comprehensive")]
    public async Task<IActionResult> GetComprehensive(string studentId, CancellationToken ct)
    {
        await _students.GetOrCreateAsync(studentId, ct);
        var eval = await _comprehensive.EvaluateAsync(studentId, ct);
        return Ok(new
        {
            studentId = eval.StudentId,
            overallScore = eval.OverallScore,
            overallLevel = eval.OverallLevel,
            knowledgeScore = eval.KnowledgeScore,
            abilityScore = eval.AbilityScore,
            knowledgeMasteries = eval.KnowledgeMasteries.Select(k => new
            {
                knowledgeId = k.KnowledgeId,
                knowledgeName = k.KnowledgeName,
                mastery = k.Mastery,
                attempts = k.Attempts,
                isWeak = k.IsWeak
            }),
            abilityScores = eval.AbilityScores.Select(MapAbilityScore),
            weakKnowledge = eval.WeakKnowledge.Select(k => new
            {
                knowledgeId = k.KnowledgeId,
                knowledgeName = k.KnowledgeName,
                mastery = k.Mastery
            }),
            weakAbilities = eval.WeakAbilities.Select(MapAbilityScore),
            topErrorTags = eval.TopErrorTags,
            teachingSuggestions = eval.TeachingSuggestions
        });
    }

    /// <summary>针对最薄弱能力生成引导式交互</summary>
    [HttpGet("{studentId}/guidance")]
    public async Task<IActionResult> GetGuidance(string studentId, CancellationToken ct)
    {
        await _students.GetOrCreateAsync(studentId, ct);
        var interaction = await _guided.GetGuidanceForWeakestAbilityAsync(studentId, ct);
        if (interaction is null)
            return NotFound(new { message = "暂无作答记录，无法定位薄弱能力。" });
        return Ok(MapInteraction(interaction));
    }

    /// <summary>针对指定能力生成引导式交互</summary>
    [HttpGet("guidance/{ability}")]
    public IActionResult GetGuidanceForAbility(ChemistryAbility ability)
    {
        var interaction = _guided.GetGuidanceForAbility(ability);
        return Ok(MapInteraction(interaction));
    }

    /// <summary>针对某题某步（知识点）生成能力导向提示</summary>
    [HttpGet("{studentId}/guidance/knowledge/{knowledgeId}")]
    public async Task<IActionResult> GetGuidanceForKnowledge(
        string studentId, string knowledgeId, CancellationToken ct)
    {
        await _students.GetOrCreateAsync(studentId, ct);
        var interaction = await _guided.GetGuidanceForStepAsync(studentId, knowledgeId, ct);
        if (interaction is null)
            return NotFound(new { message = "该知识点未配置能力映射。" });
        return Ok(MapInteraction(interaction));
    }

    /// <summary>获取所有化学解题能力的元数据（能力框架）</summary>
    [HttpGet("framework")]
    public IActionResult GetFramework()
    {
        return Ok(ChemistryAbilityLibrary.All.Select(m => new
        {
            ability = m.Ability.ToString(),
            name = m.Name,
            description = m.Description,
            relatedErrorTags = m.RelatedErrorTags.Select(t => t.ToText()),
            strategyHint = m.StrategyHint
        }));
    }

    // ==================== 个人能力图谱 ====================

    /// <summary>获取学生个人能力图谱（知识掌握度 + 能力得分 + 错因模式）</summary>
    [HttpGet("{studentId}/graph")]
    public async Task<IActionResult> GetGraph(string studentId, CancellationToken ct)
    {
        await _students.GetOrCreateAsync(studentId, ct);
        var graph = await _graph.BuildAsync(studentId, ct);
        return Ok(MapGraph(graph));
    }

    // ==================== 错误练习闭环 ====================

    /// <summary>启动闭环：学生答错某步时调用，进入错误交互（诊断）阶段</summary>
    [HttpPost("loop/start")]
    public async Task<IActionResult> StartLoop(
        [FromBody] StartLoopRequest req, CancellationToken ct)
    {
        await _students.GetOrCreateAsync(req.StudentId, ct);
        var result = await _loop.StartLoopAsync(req.StudentId, req.ProblemId, req.StepIndex, req.WrongAnswer, ct);
        return Ok(MapLoopResult(result));
    }

    /// <summary>提交诊断检查点的回答</summary>
    [HttpPost("loop/answer")]
    public async Task<IActionResult> AnswerLoop(
        [FromBody] AnswerLoopRequest req, CancellationToken ct)
    {
        var result = await _loop.SubmitDiagnosticAnswerAsync(req.SessionId, req.Answer, ct);
        return Ok(MapLoopResult(result));
    }

    /// <summary>讲解后重试原步骤</summary>
    [HttpPost("loop/retry")]
    public async Task<IActionResult> RetryLoop(
        [FromBody] AnswerLoopRequest req, CancellationToken ct)
    {
        var result = await _loop.RetryStepAsync(req.SessionId, req.Answer, ct);
        return Ok(MapLoopResult(result));
    }

    /// <summary>完成变式练习，标记闭环完成</summary>
    [HttpPost("loop/complete/{studentId}")]
    public async Task<IActionResult> CompleteLoop(string studentId, CancellationToken ct)
    {
        var result = await _loop.CompleteLoopAsync(studentId, ct);
        return Ok(MapLoopResult(result));
    }

    private static object MapAbilityScore(AbilityScore s) => new
    {
        ability = s.Ability.ToString(),
        name = s.Name,
        description = s.Description,
        score = s.Score,
        level = s.Level,
        isWeak = s.IsWeak,
        attempts = s.Attempts,
        corrects = s.Corrects,
        avgHintLevel = s.AvgHintLevel,
        topErrorTags = s.TopErrorTags
    };

    private static object MapInteraction(AbilityGuidedInteraction i) => new
    {
        ability = i.Ability.ToString(),
        abilityName = i.AbilityName,
        question = i.Question,
        strategyHint = i.StrategyHint,
        progressiveHints = i.ProgressiveHints,
        relatedKnowledgeIds = i.RelatedKnowledgeIds
    };

    private static object MapGraph(PersonalAbilityGraph g) => new
    {
        studentId = g.StudentId,
        overallScore = g.OverallScore,
        knowledgeScore = g.KnowledgeScore,
        abilityScore = g.AbilityScore,
        nodes = g.Nodes.Select(n => new
        {
            knowledgeId = n.KnowledgeId,
            knowledgeName = n.KnowledgeName,
            level = n.Level.ToString(),
            description = n.Description,
            mastery = n.Mastery,
            attempts = n.Attempts,
            corrects = n.Corrects,
            isWeak = n.IsWeak,
            abilityScores = n.AbilityScores.Select(a => new
            {
                ability = a.Ability.ToString(),
                name = a.Name,
                score = a.Score,
                level = a.Level,
                isWeak = a.IsWeak
            }),
            errorPattern = n.ErrorPattern.Select(e => new { tag = e.TagText, count = e.Count }),
            lastPracticedAt = n.LastPracticedAt,
            weakReason = n.WeakReason
        }),
        edges = g.Edges.Select(e => new
        {
            fromId = e.FromId,
            toId = e.ToId,
            fromWeak = e.FromWeak,
            toWeak = e.ToWeak,
            isWeakChain = e.IsWeakChain
        }),
        weakNodes = g.WeakNodes.Select(n => new
        {
            knowledgeId = n.KnowledgeId,
            knowledgeName = n.KnowledgeName,
            mastery = n.Mastery,
            weakReason = n.WeakReason
        }),
        weakChains = g.WeakChains.Select(c => new { path = c.Path, rootCause = c.RootCause }),
        globalErrorPattern = g.GlobalErrorPattern.Select(e => new { tag = e.TagText, count = e.Count }),
        weakAbilities = g.WeakAbilities.Select(a => new
        {
            ability = a.Ability.ToString(),
            name = a.Name,
            score = a.Score,
            level = a.Level
        })
    };

    private static object MapLoopResult(LoopStepResult r) => new
    {
        phase = r.Phase.ToString(),
        message = r.Message,
        sessionId = r.SessionId,
        payload = r.Payload,
        graph = r.Graph is null ? null : MapGraph(r.Graph),
        nextSuggestion = r.NextSuggestion,
        isCompleted = r.IsCompleted
    };
}

public sealed record StartLoopRequest(string StudentId, string ProblemId, int StepIndex, string WrongAnswer);
public sealed record AnswerLoopRequest(string SessionId, string Answer);
