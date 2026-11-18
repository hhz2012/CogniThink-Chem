namespace CogniThink.TutorBrain.Application;

using CogniThink.TutorBrain.Application.Agent;
using CogniThink.TutorBrain.Application.Reasoning;
using CogniThink.TutorBrain.Domain;

/// <summary>状态机输出：前端根据 Status / Action / HintLevel 渲染探险闯关 UI</summary>
public sealed record TutorDecision(
    TutorStatus Status,
    StepVerdict Verdict,
    ErrorTag ErrorTag,
    int HintLevel,
    RecommendedAction RecommendedAction,
    string Message,
    IReadOnlyList<string> RelatedKnowledgeIds,
    bool UnlockedNextStep,
    bool Completed,
    int NextStepIndex,
    string? HintText);

/// <summary>
/// 导学状态机（核心）。
/// 状态流转全部写死在代码中，不依赖 prompt：
///   待作答 → 步骤校验 → 分支（正确 / 局部小错 / 关键错误 / 无法判定）
///   连续卡壳 ≥ 3 次 → EscalatingHint（线索 → 子问题 → 关键条件 → 脚手架）
///   最后一步正确 → Completed（关卡通过）
/// 红线：是否给提示、给到第几级由本类决定；LLM 只负责话术。
/// </summary>
public sealed class TutorStateMachine
{
    private readonly IReadOnlyDictionary<Subject, IReasoningEngine> _engines;
    private readonly ITutorNarrativeAgent _narrative;

    public TutorStateMachine(IEnumerable<IReasoningEngine> engines, ITutorNarrativeAgent narrative)
    {
        _engines = engines.ToDictionary(e => e.Subject);
        _narrative = narrative;
    }

    public async Task<TutorDecision> HandleAsync(
        Problem problem,
        ProblemStep step,
        TutorState state,
        IReadOnlyList<string> knowledgeNames,
        string stepContent,
        CancellationToken cancellationToken = default)
    {
        // 0) 步骤顺序守门：必须按推理链推进
        if (step.StepIndex != state.CurrentStepIndex)
        {
            var msg = await ComposeAsync(
                NarrativeScene.FormatIssue, problem, step,
                StepEvaluation.Ungradable("步骤顺序不正确"),
                state, knowledgeNames, null, cancellationToken);

            return new TutorDecision(
                state.Status, StepVerdict.Ungradable, ErrorTag.None, state.HintLevel,
                RecommendedAction.RetryStep, msg,
                new[] { step.KnowledgeId }, false, false, state.CurrentStepIndex, null);
        }

        if (!_engines.TryGetValue(problem.Subject, out var engine))
        {
            var msg = await ComposeAsync(
                NarrativeScene.FormatIssue, problem, step,
                StepEvaluation.Ungradable("当前学科缺少推理引擎"),
                state, knowledgeNames, null, cancellationToken);

            return new TutorDecision(
                state.Status, StepVerdict.Ungradable, ErrorTag.None, state.HintLevel,
                RecommendedAction.RetryStep, msg,
                new[] { step.KnowledgeId }, false, false, state.CurrentStepIndex, null);
        }

        // 1) 调用化学规则引擎判定，LLM 不参与
        var evaluation = await engine.EvaluateAsync(problem, step, stepContent, cancellationToken);
        state.UpdatedAt = DateTime.UtcNow;

        // 2) 分支流转
        return evaluation.Verdict switch
        {
            StepVerdict.Correct => await OnCorrectAsync(problem, step, state, knowledgeNames, cancellationToken),
            StepVerdict.PartiallyCorrect => await OnMistakeAsync(problem, step, state, knowledgeNames, evaluation, isKeyStep: false, cancellationToken),
            StepVerdict.Incorrect => await OnMistakeAsync(problem, step, state, knowledgeNames, evaluation, isKeyStep: step.IsKeyStep, cancellationToken),
            _ => await OnUngradableAsync(problem, step, state, knowledgeNames, evaluation, cancellationToken)
        };
    }

    /// <summary>步骤正确：推进下一步；最后一步则通关</summary>
    private async Task<TutorDecision> OnCorrectAsync(
        Problem problem,
        ProblemStep step,
        TutorState state,
        IReadOnlyList<string> knowledgeNames,
        CancellationToken ct)
    {
        state.ConsecutiveStuck = 0;
        state.HintLevel = 0;

        var evaluation = new StepEvaluation(StepVerdict.Correct, ErrorTag.None, "正确", Array.Empty<string>());
        var isLastStep = step.StepIndex >= problem.Steps.Count;

        if (isLastStep)
        {
            state.Status = TutorStatus.Completed;
            var message = await ComposeAsync(
                NarrativeScene.LevelComplete, problem, step, evaluation, state, knowledgeNames, null, ct);

            return new TutorDecision(
                TutorStatus.Completed, StepVerdict.Correct, ErrorTag.None, 0,
                RecommendedAction.FinishLevel, message,
                new[] { step.KnowledgeId }, false, true, step.StepIndex, null);
        }

        state.Status = TutorStatus.StepAccepted;
        state.CurrentStepIndex = step.StepIndex + 1;

        var proceedMsg = await ComposeAsync(
            NarrativeScene.StepCorrect, problem, step, evaluation, state, knowledgeNames, null, ct);

        return new TutorDecision(
            TutorStatus.StepAccepted, StepVerdict.Correct, ErrorTag.None, 0,
            RecommendedAction.Proceed, proceedMsg,
            new[] { step.KnowledgeId }, true, false, state.CurrentStepIndex, null);
    }

    /// <summary>
    /// 出错分支：不直接指出错处。
    /// 第 1 次 → 自查方向；第 2 次 → 子问题；第 3 次 → 关键线索；第 4 次起 → 分步脚手架。
    /// 连续卡壳 ≥ 3 次进入 EscalatingHint。
    /// </summary>
    private async Task<TutorDecision> OnMistakeAsync(
        Problem problem,
        ProblemStep step,
        TutorState state,
        IReadOnlyList<string> knowledgeNames,
        StepEvaluation evaluation,
        bool isKeyStep,
        CancellationToken ct)
    {
        state.ConsecutiveStuck += 1;
        // 提示级别只升不降，上限 4
        state.HintLevel = Math.Min(4, Math.Max(state.HintLevel + 1, state.ConsecutiveStuck));

        var escalating = state.ConsecutiveStuck >= 3;
        state.Status = escalating
            ? TutorStatus.EscalatingHint
            : isKeyStep ? TutorStatus.KeyMistake : TutorStatus.LocalMistake;

        var hintText = PickHint(step, state.HintLevel);
        var action = state.HintLevel switch
        {
            <= 1 => RecommendedAction.SelfCheck,
            2 => RecommendedAction.AskSubQuestion,
            3 => RecommendedAction.GiveClue,
            _ => RecommendedAction.GiveScaffold
        };

        var scene = escalating
            ? NarrativeScene.EscalatingHint
            : isKeyStep ? NarrativeScene.KeyMistake : NarrativeScene.PartialMistake;

        var message = await ComposeAsync(scene, problem, step, evaluation, state, knowledgeNames, hintText, ct);

        return new TutorDecision(
            state.Status, evaluation.Verdict, evaluation.ErrorTag, state.HintLevel,
            action, message,
            new[] { step.KnowledgeId }, false, false, state.CurrentStepIndex, hintText);
    }

    /// <summary>无法判定：提示规范作答，不推进、不升级提示</summary>
    private async Task<TutorDecision> OnUngradableAsync(
        Problem problem,
        ProblemStep step,
        TutorState state,
        IReadOnlyList<string> knowledgeNames,
        StepEvaluation evaluation,
        CancellationToken ct)
    {
        var message = await ComposeAsync(
            NarrativeScene.FormatIssue, problem, step, evaluation, state, knowledgeNames, null, ct);

        return new TutorDecision(
            state.Status, StepVerdict.Ungradable, ErrorTag.None, state.HintLevel,
            RecommendedAction.RetryStep, message,
            new[] { step.KnowledgeId }, false, false, state.CurrentStepIndex, null);
    }

    /// <summary>提示梯度：自查 → 子问题 → 关键线索 → 脚手架</summary>
    private static string? PickHint(ProblemStep step, int level) => level switch
    {
        1 => step.HintSelfCheck,
        2 => step.HintSubQuestion,
        3 => step.HintClue,
        _ => step.Scaffold
    };

    private Task<string> ComposeAsync(
        NarrativeScene scene,
        Problem problem,
        ProblemStep step,
        StepEvaluation evaluation,
        TutorState state,
        IReadOnlyList<string> knowledgeNames,
        string? hintText,
        CancellationToken ct) =>
        _narrative.ComposeAsync(
            new NarrativeRequest(
                scene,
                problem.Subject,
                problem.Title,
                step.StepIndex,
                step.Prompt,
                evaluation,
                state.HintLevel,
                hintText,
                knowledgeNames,
                scene == NarrativeScene.LevelComplete ? "同知识点变式题已生成" : null),
            ct);
}
