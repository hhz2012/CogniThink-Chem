namespace CogniThink.TutorBrain.Application.Ability;

using CogniThink.TutorBrain.Application.Diagnostic;
using CogniThink.TutorBrain.Domain;

/// <summary>闭环阶段</summary>
public enum LoopPhase
{
    /// <summary>错误交互：诊断定位盲区</summary>
    ErrorInteraction = 0,
    /// <summary>知识点讲解：讲解盲区知识点</summary>
    KnowledgeTeaching = 1,
    /// <summary>同类练习：变式练习</summary>
    VariantPractice = 2,
    /// <summary>闭环完成</summary>
    Completed = 3
}

/// <summary>闭环单次交互的返回</summary>
public sealed class LoopStepResult
{
    public LoopPhase Phase { get; set; }
    public string Message { get; set; } = string.Empty;

    /// <summary>当前阶段的交互内容（问题/讲解/练习题）</summary>
    public object? Payload { get; set; }

    /// <summary>闭环会话 Id</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>更新后的个人能力图谱摘要</summary>
    public PersonalAbilityGraph? Graph { get; set; }

    /// <summary>下一步建议</summary>
    public string NextSuggestion { get; set; } = string.Empty;

    /// <summary>闭环是否完成</summary>
    public bool IsCompleted => Phase == LoopPhase.Completed;
}

/// <summary>
/// 错误练习闭环编排服务：
/// 错误交互 → 知识点讲解 → 同类练习 → 反馈更新图谱
///
/// 闭环核心：
/// 1. 学生答错 → 启动诊断，逐步检查定位盲区（错误交互）
/// 2. 定位盲区 → 给出知识点讲解（含思考策略、分析步骤、常见误区）
/// 3. 学生重试通过 → 生成同类变式练习题
/// 4. 练习完成 → 更新个人能力图谱（掌握度 + 能力得分 + 错因模式）
///
/// 复用现有组件：DiagnosticTeachingEngine / VariantService / PersonalAbilityGraphService
/// </summary>
public sealed class ErrorPracticeLoopService
{
    private readonly DiagnosticTeachingEngine _diagnostic;
    private readonly AdaptiveTeachingOrchestrator _adaptive;
    private readonly PersonalAbilityGraphService _graphService;
    private readonly AbilityGuidedTeachingService _abilityGuided;

    public ErrorPracticeLoopService(
        DiagnosticTeachingEngine diagnostic,
        AdaptiveTeachingOrchestrator adaptive,
        PersonalAbilityGraphService graphService,
        AbilityGuidedTeachingService abilityGuided)
    {
        _diagnostic = diagnostic;
        _adaptive = adaptive;
        _graphService = graphService;
        _abilityGuided = abilityGuided;
    }

    /// <summary>
    /// 启动闭环：学生答错某步时调用。
    /// 返回诊断的第一个检查点（错误交互阶段）。
    /// </summary>
    public async Task<LoopStepResult> StartLoopAsync(
        string studentId,
        string problemId,
        int stepIndex,
        string wrongAnswer,
        CancellationToken ct = default)
    {
        var start = await _adaptive.StartForWrongAnswerAsync(
            studentId, problemId, stepIndex, wrongAnswer, ct);

        if (start is null)
        {
            return new LoopStepResult
            {
                Phase = LoopPhase.ErrorInteraction,
                Message = "题目或步骤不存在，无法启动闭环。"
            };
        }

        var graph = await _graphService.BuildAsync(studentId, ct);

        return new LoopStepResult
        {
            Phase = LoopPhase.ErrorInteraction,
            SessionId = start.SessionId,
            Message = $"你在这一步遇到了困难。我们先做几个小问题，找到卡壳的原因。",
            Payload = new
            {
                checkpoint = new
                {
                    index = start.CurrentCheckpoint.Index,
                    type = start.CurrentCheckpoint.Type.ToString(),
                    knowledgeName = start.CurrentCheckpoint.KnowledgeName,
                    question = start.CurrentCheckpoint.Question,
                    thinkingStrategy = start.CurrentCheckpoint.ThinkingStrategy,
                    analysisFramework = start.CurrentCheckpoint.AnalysisFramework,
                    commonMisconception = start.CurrentCheckpoint.CommonMisconception
                },
                totalCheckpoints = start.Structure.Checkpoints.Count
            },
            Graph = graph,
            NextSuggestion = "请回答上面的诊断问题，帮助定位你的知识盲区。"
        };
    }

    /// <summary>
    /// 提交诊断检查点的回答。
    /// - 答对 → 推进到下一个检查点
    /// - 答错 → 定位盲区，进入知识点讲解阶段
    /// </summary>
    public async Task<LoopStepResult> SubmitDiagnosticAnswerAsync(
        string sessionId, string answer, CancellationToken ct = default)
    {
        var result = await _adaptive.SubmitDiagnosticAnswerAsync(sessionId, answer, ct);
        var session = _diagnostic.GetSession(sessionId);
        var studentId = session?.StudentId ?? string.Empty;

        var graph = !string.IsNullOrEmpty(studentId)
            ? await _graphService.BuildAsync(studentId, ct)
            : null;

        if (result.Action == DiagnosticAction.Explain)
        {
            // 定位到盲区 → 进入知识点讲解阶段
            var cp = session?.Structure.Checkpoints
                .FirstOrDefault(c => c.Index == result.GapCheckpointIndex);

            return new LoopStepResult
            {
                Phase = LoopPhase.KnowledgeTeaching,
                SessionId = sessionId,
                Message = $"我们找到了卡壳的原因：「{cp?.KnowledgeName}」。下面讲解这个知识点。",
                Payload = new
                {
                    gapKnowledge = cp?.KnowledgeName,
                    explanation = result.Explanation is null ? null : new
                    {
                        knowledgeName = result.Explanation.KnowledgeName,
                        description = result.Explanation.Description,
                        thinkingStrategy = result.Explanation.ThinkingStrategy,
                        analysisSteps = result.Explanation.AnalysisSteps,
                        commonMisconception = result.Explanation.CommonMisconception,
                        transferHint = result.Explanation.TransferHint,
                        evidence = result.Explanation.Evidence
                    }
                },
                Graph = graph,
                NextSuggestion = "仔细阅读讲解，特别是思考方法和分析步骤，然后重试原步骤。"
            };
        }

        if (result.Action == DiagnosticAction.NextCheckpoint)
        {
            return new LoopStepResult
            {
                Phase = LoopPhase.ErrorInteraction,
                SessionId = sessionId,
                Message = "回答正确，继续下一个诊断问题。",
                Payload = result.NextCheckpoint is null ? null : new
                {
                    checkpoint = new
                    {
                        index = result.NextCheckpoint.Index,
                        type = result.NextCheckpoint.Type.ToString(),
                        knowledgeName = result.NextCheckpoint.KnowledgeName,
                        question = result.NextCheckpoint.Question,
                        thinkingStrategy = result.NextCheckpoint.ThinkingStrategy,
                        analysisFramework = result.NextCheckpoint.AnalysisFramework
                    }
                },
                Graph = graph,
                NextSuggestion = "请回答下一个诊断问题。"
            };
        }

        // ReadyToRetry → 所有检查点通过，可重试原步骤
        return new LoopStepResult
        {
            Phase = LoopPhase.KnowledgeTeaching,
            SessionId = sessionId,
            Message = "所有诊断问题都通过了，现在用讲解的方法重试原步骤。",
            Payload = new { retryStep = true },
            Graph = graph,
            NextSuggestion = "请重新作答原步骤，运用刚才学到的思考方法。"
        };
    }

    /// <summary>
    /// 学生重试原步骤。
    /// - 通过 → 生成同类变式练习，进入练习阶段
    /// - 未通过 → 继续讲解
    /// </summary>
    public async Task<LoopStepResult> RetryStepAsync(
        string sessionId, string answer, CancellationToken ct = default)
    {
        var result = await _adaptive.RetryWithPracticeAsync(sessionId, answer, ct);
        var session = _diagnostic.GetSession(sessionId);
        var studentId = session?.StudentId ?? string.Empty;

        var graph = !string.IsNullOrEmpty(studentId)
            ? await _graphService.BuildAsync(studentId, ct)
            : null;

        if (result.RetryResult.Action == DiagnosticAction.Completed)
        {
            // 重试通过 → 生成同类变式练习
            var variant = result.PracticeVariant;
            var abilityGuidance = !string.IsNullOrEmpty(studentId) && result.TargetKnowledgeId is not null
                ? await _abilityGuided.GetGuidanceForStepAsync(studentId, result.TargetKnowledgeId, ct)
                : null;

            return new LoopStepResult
            {
                Phase = LoopPhase.VariantPractice,
                SessionId = sessionId,
                Message = "很好！你掌握了这一步的推理方法。现在做一道同类变式题巩固一下。",
                Payload = new
                {
                    variant = variant is null ? null : new
                    {
                        problemId = variant.Id,
                        title = variant.Title,
                        stem = variant.Stem,
                        difficulty = variant.Difficulty,
                        steps = variant.Steps.Select(s => new
                        {
                            stepIndex = s.StepIndex,
                            prompt = s.Prompt
                        })
                    },
                    abilityGuidance = abilityGuidance is null ? null : new
                    {
                        ability = abilityGuidance.Ability.ToString(),
                        abilityName = abilityGuidance.AbilityName,
                        question = abilityGuidance.Question,
                        strategyHint = abilityGuidance.StrategyHint,
                        progressiveHints = abilityGuidance.ProgressiveHints
                    }
                },
                Graph = graph,
                NextSuggestion = "完成变式练习，运用学到的思考方法独立完成。"
            };
        }

        // 未通过 → 继续讲解
        return new LoopStepResult
        {
            Phase = LoopPhase.KnowledgeTeaching,
            SessionId = sessionId,
            Message = "还差一点，我们再回顾一下关键知识点和思考方法。",
            Payload = result.RetryResult.Explanation is null ? null : new
            {
                explanation = new
                {
                    knowledgeName = result.RetryResult.Explanation.KnowledgeName,
                    thinkingStrategy = result.RetryResult.Explanation.ThinkingStrategy,
                    analysisSteps = result.RetryResult.Explanation.AnalysisSteps,
                    commonMisconception = result.RetryResult.Explanation.CommonMisconception,
                    transferHint = result.RetryResult.Explanation.TransferHint
                }
            },
            Graph = graph,
            NextSuggestion = "再仔细看看分析步骤，然后重新作答。"
        };
    }

    /// <summary>
    /// 完成变式练习后调用，标记闭环完成并返回更新后的图谱。
    /// </summary>
    public async Task<LoopStepResult> CompleteLoopAsync(
        string studentId, CancellationToken ct = default)
    {
        var graph = await _graphService.BuildAsync(studentId, ct);

        var suggestions = new List<string>();
        if (graph.WeakNodes.Count > 0)
        {
            var names = string.Join("、", graph.WeakNodes.Take(2).Select(n => n.KnowledgeName));
            suggestions.Add($"建议继续巩固「{names}」等薄弱知识点。");
        }
        if (graph.WeakAbilities.Count > 0)
        {
            var weakest = graph.WeakAbilities[0];
            suggestions.Add($"{weakest.Name}仍需加强，建议多做相关练习。");
        }
        if (suggestions.Count == 0)
        {
            suggestions.Add("表现不错，建议挑战更高难度的题目。");
        }

        return new LoopStepResult
        {
            Phase = LoopPhase.Completed,
            Message = "闭环完成！你的能力图谱已更新。",
            Graph = graph,
            NextSuggestion = suggestions[0]
        };
    }
}
