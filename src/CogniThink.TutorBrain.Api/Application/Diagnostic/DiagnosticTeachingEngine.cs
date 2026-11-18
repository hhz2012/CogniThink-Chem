namespace CogniThink.TutorBrain.Application.Diagnostic;

using Microsoft.EntityFrameworkCore;
using CogniThink.TutorBrain.Application.Chemistry;
using CogniThink.TutorBrain.Application.Reasoning;
using CogniThink.TutorBrain.Domain;
using CogniThink.TutorBrain.Infrastructure;

/// <summary>
/// 诊断导学引擎：
/// 当学生答错或跳过某一步时，把该步的推理链拆成可逐项考核的知识点检查点，
/// 逐一提问定位第一个盲区，给出针对性讲解（含化学证据），再让学生重试原步骤。
///
/// 铁律：
/// - 检查点与讲解全部来自知识库（知识点描述 + 化学知识库 + 题目步骤数据），不臆造。
/// - 对错判定复用 ChemistryReasoningEngine，LLM 不参与判定。
/// </summary>
public sealed class DiagnosticTeachingEngine
{
    private readonly TutorDbContext _db;
    private readonly ChemicalInferenceService _chemical;
    private readonly ChemicalKnowledgeBase _kb;
    private readonly KnowledgeGraphService _graph;
    private readonly DiagnosticSessionStore _store;

    public DiagnosticTeachingEngine(
        TutorDbContext db,
        ChemicalInferenceService chemical,
        ChemicalKnowledgeBase kb,
        KnowledgeGraphService graph,
        DiagnosticSessionStore store)
    {
        _db = db;
        _chemical = chemical;
        _kb = kb;
        _graph = graph;
        _store = store;
    }

    // ==================== 1. 构建推理结构 ====================

    /// <summary>
    /// 为题目某一步构建完整推理结构：前置知识点 → 核心知识点 → 本步推理结论。
    /// </summary>
    public async Task<ReasoningStructure?> BuildStructureAsync(
        string problemId, int stepIndex, CancellationToken ct = default)
    {
        var problem = await _db.Problems
            .Include(p => p.Steps)
            .Include(p => p.KnowledgeLinks)
            .FirstOrDefaultAsync(p => p.Id == problemId, ct);
        if (problem is null) return null;

        var step = problem.Steps.FirstOrDefault(s => s.StepIndex == stepIndex);
        if (step is null) return null;

        var structure = new ReasoningStructure
        {
            ProblemId = problemId,
            StepIndex = stepIndex,
            StepPrompt = step.Prompt
        };

        var idx = 1;

        // 1) 前置知识点检查点（从知识图谱递归获取，按依赖链由近到远）
        var prereqChain = await _graph.GetPrerequisiteChainAsync(step.KnowledgeId, ct);
        var allIds = prereqChain.Append(step.KnowledgeId).Distinct().ToList();
        var nodeMap = await _graph.GetNameMapAsync(allIds, ct);

        var prereqNodes = await _db.KnowledgeNodes
            .Where(n => prereqChain.Contains(n.Id))
            .ToListAsync(ct);

        // 前置按距离排序：直接前置在前
        var orderedPrereqs = prereqChain
            .Select(id => prereqNodes.FirstOrDefault(n => n.Id == id))
            .Where(n => n is not null)
            .Cast<KnowledgeNode>()
            .ToList();

        foreach (var kp in orderedPrereqs)
        {
            structure.Checkpoints.Add(CreatePrerequisiteCheckpoint(idx++, kp, step));
        }

        // 2) 核心知识点检查点（本步所属知识点）
        var coreNode = await _db.KnowledgeNodes.FirstOrDefaultAsync(n => n.Id == step.KnowledgeId, ct);
        if (coreNode is not null)
        {
            structure.Checkpoints.Add(CreateCoreCheckpoint(idx++, coreNode, step));
        }

        // 3) 本步推理结论检查点
        structure.Checkpoints.Add(CreateReasoningCheckpoint(idx++, step, coreNode));

        return structure;
    }

    private ReasoningCheckpoint CreatePrerequisiteCheckpoint(int index, KnowledgeNode kp, ProblemStep step)
    {
        var strategy = ThinkingStrategyLibrary.Get(kp.Id);
        var (evidenceType, evidenceKey) = InferEvidence(kp, step);
        return new ReasoningCheckpoint
        {
            Index = index,
            KnowledgeId = kp.Id,
            KnowledgeName = kp.Name,
            Type = CheckpointType.Prerequisite,
            // 引导式问题：问方法，不把知识点描述（答案）放进问题里
            Question = $"为了完成这一步，我们需要先确认你掌握了「{kp.Name}」。{strategy.GuidedQuestion}",
            ExpectedKeywords = DeriveKeywordsFromKnowledge(kp),
            Explanation = kp.Description,
            ThinkingStrategy = strategy.Strategy,
            AnalysisFramework = strategy.AnalysisFramework,
            CommonMisconception = strategy.CommonMisconception,
            EvidenceType = evidenceType,
            EvidenceKey = evidenceKey
        };
    }

    private ReasoningCheckpoint CreateCoreCheckpoint(int index, KnowledgeNode kp, ProblemStep step)
    {
        var strategy = ThinkingStrategyLibrary.Get(kp.Id);
        var (evidenceType, evidenceKey) = InferEvidence(kp, step);
        // 优先用题目自带的子问题，否则用策略库的引导式问题（不嵌入答案）
        var question = string.IsNullOrWhiteSpace(step.HintSubQuestion)
            ? strategy.GuidedQuestion
            : step.HintSubQuestion;
        return new ReasoningCheckpoint
        {
            Index = index,
            KnowledgeId = kp.Id,
            KnowledgeName = kp.Name,
            Type = CheckpointType.Core,
            Question = question,
            ExpectedKeywords = step.ExpectedKeywords,
            Explanation = step.HintClue ?? kp.Description,
            ThinkingStrategy = strategy.Strategy,
            AnalysisFramework = strategy.AnalysisFramework,
            CommonMisconception = strategy.CommonMisconception,
            EvidenceType = evidenceType,
            EvidenceKey = evidenceKey
        };
    }

    private ReasoningCheckpoint CreateReasoningCheckpoint(int index, ProblemStep step, KnowledgeNode? kp)
    {
        var strategy = ThinkingStrategyLibrary.Get(step.KnowledgeId);
        return new ReasoningCheckpoint
        {
            Index = index,
            KnowledgeId = step.KnowledgeId,
            KnowledgeName = kp?.Name ?? step.KnowledgeId,
            Type = CheckpointType.Reasoning,
            // 推理检查点：引导学生用刚才的思考方法自己推出结论
            Question = $"结合刚才的分析思路，{step.Prompt}",
            ExpectedKeywords = step.ExpectedKeywords,
            Explanation = step.Scaffold ?? step.HintClue ?? kp?.Description ?? string.Empty,
            ThinkingStrategy = strategy.Strategy,
            AnalysisFramework = strategy.AnalysisFramework,
            CommonMisconception = strategy.CommonMisconception,
            EvidenceType = EvidenceType.None,
            EvidenceKey = null
        };
    }

    /// <summary>根据知识点与步骤推断可用的化学证据类型</summary>
    private static (EvidenceType, string?) InferEvidence(KnowledgeNode kp, ProblemStep step)
    {
        var expr = (step.ExpectedExpression + " " + (step.ExpectedKeywords ?? string.Empty)).ToLowerInvariant();

        // 含离子共存相关词
        if (expr.Contains("共存") || kp.Id == "C-COEXIST")
            return (EvidenceType.Coexistence, null);

        // 含反应箭头或典型反应词
        if (expr.Contains("→") || expr.Contains("生成") || expr.Contains("反应"))
            return (EvidenceType.Reaction, step.ExpectedKeywords);

        // 含颜色词
        if (expr.Contains("色") || expr.Contains("变红") || expr.Contains("变蓝") || expr.Contains("变绿"))
            return (EvidenceType.SubstanceColor, step.ExpectedKeywords);

        return (EvidenceType.None, null);
    }

    /// <summary>从知识点名称派生判定关键词（学生描述概念时通常会提到名称）</summary>
    private static string? DeriveKeywordsFromKnowledge(KnowledgeNode kp)
    {
        // 用知识点名称作为关键词锚点：能描述该概念的回答通常会包含名称
        return kp.Name;
    }

    // ==================== 2. 诊断会话管理 ====================

    /// <summary>开始诊断：构建结构并返回第一个检查点</summary>
    public async Task<DiagnosticStartResult?> StartAsync(
        string studentId, string problemId, int stepIndex, CancellationToken ct = default)
    {
        var structure = await BuildStructureAsync(problemId, stepIndex, ct);
        if (structure is null || structure.Checkpoints.Count == 0) return null;

        var session = new DiagnosticSession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            StudentId = studentId,
            ProblemId = problemId,
            StepIndex = stepIndex,
            Structure = structure,
            CurrentCheckpointIndex = 1
        };
        _store.Store(session);

        var first = structure.Checkpoints[0];
        return new DiagnosticStartResult(
            session.SessionId,
            structure,
            first,
            $"这道题第 {stepIndex} 步的推理涉及 {structure.Checkpoints.Count} 个知识节点。我们从最基础的开始确认，请回答下面的问题。");
    }

    /// <summary>提交对当前检查点的回答</summary>
    public DiagnosticAnswerResult? SubmitAnswer(string sessionId, string answer)
    {
        if (!_store.TryGetValue(sessionId, out var session)) return null;
        session.UpdatedAt = DateTime.UtcNow;

        if (session.Status is not DiagnosticStatus.InProgress)
        {
            return new DiagnosticAnswerResult(
                StepVerdict.Ungradable, "当前诊断会话已不在进行中。",
                DiagnosticAction.Completed, null, null, null, session.Status);
        }

        var checkpoint = session.Structure.Checkpoints
            .FirstOrDefault(c => c.Index == session.CurrentCheckpointIndex);
        if (checkpoint is null)
        {
            session.Status = DiagnosticStatus.Completed;
            return new DiagnosticAnswerResult(
                StepVerdict.Correct, "所有检查点已完成。",
                DiagnosticAction.ReadyToRetry, null, null, null, session.Status);
        }

        // 用化学推理引擎判定（复用规则，LLM 不参与）
        var eval = ChemistryReasoningEngine.Evaluate(
            new ProblemStep
            {
                ExpectedExpression = checkpoint.ExpectedKeywords ?? checkpoint.Question,
                ExpectedKeywords = checkpoint.ExpectedKeywords
            },
            answer);

        session.Answers.Add(new CheckpointAnswer
        {
            CheckpointIndex = checkpoint.Index,
            Answer = answer,
            Verdict = eval.Verdict,
            Reason = eval.Reason
        });

        // 正确或基本正确 → 推进
        if (eval.Verdict is StepVerdict.Correct or StepVerdict.PartiallyCorrect)
        {
            var nextIdx = checkpoint.Index + 1;
            var next = session.Structure.Checkpoints.FirstOrDefault(c => c.Index == nextIdx);
            if (next is not null)
            {
                session.CurrentCheckpointIndex = nextIdx;
                return new DiagnosticAnswerResult(
                    eval.Verdict, "回答正确，继续下一个检查点。",
                    DiagnosticAction.NextCheckpoint, next, null, null, session.Status);
            }

            // 全部通过
            session.Status = DiagnosticStatus.Explained;
            return new DiagnosticAnswerResult(
                StepVerdict.Correct, "所有知识节点都已掌握，可以重新尝试原步骤了。",
                DiagnosticAction.ReadyToRetry, null, null, null, session.Status);
        }

        // 错误 → 定位盲区，给出讲解
        session.GapCheckpointIndex = checkpoint.Index;
        session.Status = DiagnosticStatus.GapFound;

        var explanation = BuildExplanation(checkpoint);

        return new DiagnosticAnswerResult(
            eval.Verdict,
            $"在「{checkpoint.KnowledgeName}」这个节点遇到了困难，这可能就是你卡壳的原因。",
            DiagnosticAction.Explain,
            null,
            explanation,
            checkpoint.Index,
            session.Status);
    }

    // ==================== 3. 讲解 ====================

    /// <summary>为检查点构建讲解内容（含化学证据与思考策略）</summary>
    public CheckpointExplanation BuildExplanation(ReasoningCheckpoint checkpoint)
    {
        var kp = _db.KnowledgeNodes.FirstOrDefault(n => n.Id == checkpoint.KnowledgeId);
        var strategy = ThinkingStrategyLibrary.Get(checkpoint.KnowledgeId);
        var explanation = new CheckpointExplanation
        {
            KnowledgeId = checkpoint.KnowledgeId,
            KnowledgeName = checkpoint.KnowledgeName,
            Level = kp?.Level ?? CognitiveLevel.Understand,
            Description = kp?.Description ?? string.Empty,
            Detail = checkpoint.Explanation,
            // 教方法、教思考
            ThinkingStrategy = checkpoint.ThinkingStrategy,
            AnalysisSteps = checkpoint.AnalysisFramework.Count > 0
                ? checkpoint.AnalysisFramework
                : strategy.AnalysisFramework,
            CommonMisconception = checkpoint.CommonMisconception,
            TransferHint = strategy.TransferHint
        };

        // 注入化学证据
        switch (checkpoint.EvidenceType)
        {
            case EvidenceType.SubstanceColor:
            {
                // 用知识点关联的物质颜色作为证据
                var substances = _kb.GetSubstancesForKnowledge(checkpoint.KnowledgeId);
                foreach (var s in substances.Take(6))
                {
                    var color = _chemical.GetColor(s.Id);
                    if (color is not null)
                        explanation.Evidence.Add($"{s.Name}（{s.Formula}）：{color}");
                }
                break;
            }
            case EvidenceType.Reaction:
            {
                // 用知识点关联的反应作为证据
                var reactions = _kb.GetReactionsForKnowledge(checkpoint.KnowledgeId);
                explanation.RelatedReactions = reactions.Take(3).Select(r => r.Equation).ToList();
                foreach (var r in reactions.Take(3))
                    explanation.Evidence.Add($"相关反应：{r.Equation}");
                break;
            }
            case EvidenceType.Coexistence:
            {
                explanation.Evidence.Add("离子共存判断依据：沉淀、气体、弱电解质、氧化还原、络合反应。");
                break;
            }
        }

        return explanation;
    }

    /// <summary>按知识点 Id 直接获取讲解</summary>
    public CheckpointExplanation? ExplainKnowledge(string knowledgeId)
    {
        var kp = _db.KnowledgeNodes.FirstOrDefault(n => n.Id == knowledgeId);
        if (kp is null) return null;

        var strategy = ThinkingStrategyLibrary.Get(knowledgeId);
        var checkpoint = new ReasoningCheckpoint
        {
            KnowledgeId = kp.Id,
            KnowledgeName = kp.Name,
            Explanation = kp.Description,
            ThinkingStrategy = strategy.Strategy,
            AnalysisFramework = strategy.AnalysisFramework,
            CommonMisconception = strategy.CommonMisconception,
            EvidenceType = InferEvidenceTypeForKnowledge(kp)
        };
        return BuildExplanation(checkpoint);
    }

    /// <summary>根据知识点推断可用的化学证据类型</summary>
    private static EvidenceType InferEvidenceTypeForKnowledge(KnowledgeNode kp)
    {
        var text = (kp.Id + " " + kp.Name + " " + kp.Description).ToLowerInvariant();
        if (kp.Id == "C-COEXIST" || text.Contains("共存"))
            return EvidenceType.Coexistence;
        if (text.Contains("氧化还原") || text.Contains("反应") || kp.Id.StartsWith("C-ION") || text.Contains("离子反应"))
            return EvidenceType.Reaction;
        if (text.Contains("铁") || text.Contains("铜") || text.Contains("颜色") || text.Contains("检验"))
            return EvidenceType.SubstanceColor;
        return EvidenceType.Reaction; // 默认尝试关联反应
    }

    // ==================== 4. 重试原步骤 ====================

    /// <summary>讲解后让学生重试原步骤，判定是否通过</summary>
    public DiagnosticAnswerResult? RetryStep(string sessionId, string answer)
    {
        if (!_store.TryGetValue(sessionId, out var session)) return null;
        session.UpdatedAt = DateTime.UtcNow;

        if (session.Status is not (DiagnosticStatus.GapFound or DiagnosticStatus.Explained))
        {
            return new DiagnosticAnswerResult(
                StepVerdict.Ungradable, "请先完成诊断流程再重试。",
                DiagnosticAction.Completed, null, null, null, session.Status);
        }

        // 用原步骤的标准表达式判定
        var problem = _db.Problems
            .Include(p => p.Steps)
            .FirstOrDefault(p => p.Id == session.ProblemId);
        var step = problem?.Steps.FirstOrDefault(s => s.StepIndex == session.StepIndex);
        if (step is null) return null;

        var eval = ChemistryReasoningEngine.Evaluate(step, answer);
        session.Answers.Add(new CheckpointAnswer
        {
            CheckpointIndex = 0,
            Answer = answer,
            Verdict = eval.Verdict,
            Reason = eval.Reason
        });

        if (eval.Verdict is StepVerdict.Correct or StepVerdict.PartiallyCorrect)
        {
            session.Status = DiagnosticStatus.Completed;
            return new DiagnosticAnswerResult(
                eval.Verdict, "很好！你已经掌握了这一步的推理，可以继续了。",
                DiagnosticAction.Completed, null, null, null, session.Status);
        }

        // 仍不对 → 再给讲解，保持 Explained 状态
        session.Status = DiagnosticStatus.Explained;
        var gapCp = session.GapCheckpointIndex.HasValue
            ? session.Structure.Checkpoints.FirstOrDefault(c => c.Index == session.GapCheckpointIndex)
            : session.Structure.Checkpoints.LastOrDefault();
        var explanation = gapCp is not null ? BuildExplanation(gapCp) : null;

        return new DiagnosticAnswerResult(
            eval.Verdict,
            "还差一点，我们再回顾一下关键知识点。",
            DiagnosticAction.Explain,
            null,
            explanation,
            session.GapCheckpointIndex,
            session.Status);
    }

    // ==================== 5. 会话查询 ====================

    public DiagnosticSession? GetSession(string sessionId) =>
        _store.Get(sessionId);
}
