namespace CogniThink.TutorBrain.Tests;

using CogniThink.TutorBrain.Application.Diagnostic;
using CogniThink.TutorBrain.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// 诊断导学引擎测试：
/// 验证推理结构构建、逐项诊断定位盲区、讲解、重试原步骤的完整流程。
/// </summary>
public sealed class DiagnosticTeachingEngineTests : IClassFixture<DiagnosticFixture>
{
    private readonly DiagnosticFixture _fixture;

    public DiagnosticTeachingEngineTests(DiagnosticFixture fixture) => _fixture = fixture;

    // ==================== 推理结构构建 ====================

    [Fact]
    public async Task 构建推理结构包含前置核心和推理三类检查点()
    {
        // C-FE-01 第 1 步：KSCN 检验 Fe3+
        var structure = await _fixture.Engine.BuildStructureAsync("C-FE-01", 1);

        Assert.NotNull(structure);
        Assert.Equal(1, structure!.StepIndex);
        Assert.True(structure.Checkpoints.Count >= 2);

        var types = structure.Checkpoints.Select(c => c.Type).ToList();
        Assert.Contains(CheckpointType.Core, types);
        Assert.Contains(CheckpointType.Reasoning, types);
    }

    [Fact]
    public async Task 不存在的题目返回空结构()
    {
        var structure = await _fixture.Engine.BuildStructureAsync("NOT-EXIST", 1);
        Assert.Null(structure);
    }

    [Fact]
    public async Task 离子共存题的推理结构包含前置氧化还原知识点()
    {
        // C-COEXIST-01 第 1 步的知识点是 C-REDOX，前置应含 C-ELECTROLYTE
        var structure = await _fixture.Engine.BuildStructureAsync("C-COEXIST-01", 1);

        Assert.NotNull(structure);
        var prereq = structure!.Checkpoints
            .Where(c => c.Type == CheckpointType.Prerequisite)
            .Select(c => c.KnowledgeId)
            .ToList();

        Assert.Contains("C-ELECTROLYTE", prereq);
    }

    // ==================== 诊断会话流程 ====================

    [Fact]
    public async Task 开始诊断返回第一个检查点()
    {
        var result = await _fixture.Engine.StartAsync("stu-test", "C-FE-01", 1);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result!.SessionId));
        Assert.NotNull(result.CurrentCheckpoint);
        Assert.Equal(1, result.CurrentCheckpoint.Index);
        Assert.Equal(DiagnosticStatus.InProgress, _fixture.Engine.GetSession(result.SessionId)!.Status);
    }

    [Fact]
    public async Task 连续答对所有检查点后可重试原步骤()
    {
        var start = await _fixture.Engine.StartAsync("stu-all-correct", "C-FE-01", 1);
        Assert.NotNull(start);

        var sessionId = start!.SessionId;
        var total = start.Structure.Checkpoints.Count;

        DiagnosticAnswerResult? answer = null;
        for (var i = 0; i < total; i++)
        {
            // 构造包含知识点名称、描述与关键词的完整回答（应判为正确或部分正确）
            var cp = start.Structure.Checkpoints[i];
            var keywords = cp.ExpectedKeywords ?? string.Empty;
            var fullAnswer = $"{cp.KnowledgeName}：{cp.Explanation} {keywords}";
            answer = _fixture.Engine.SubmitAnswer(sessionId, fullAnswer);
            Assert.NotNull(answer);
        }

        // 全部通过后应返回 ReadyToRetry
        Assert.Equal(DiagnosticAction.ReadyToRetry, answer!.Action);
    }

    [Fact]
    public async Task 答错检查点时定位盲区并返回讲解()
    {
        var start = await _fixture.Engine.StartAsync("stu-wrong", "C-FE-01", 1);
        Assert.NotNull(start);

        var sessionId = start!.SessionId;

        // 提交明显错误的答案
        var result = _fixture.Engine.SubmitAnswer(sessionId, "我不知道，完全不会");

        Assert.NotNull(result);
        Assert.True(result!.Verdict is StepVerdict.Incorrect or StepVerdict.PartiallyCorrect);
        Assert.Equal(DiagnosticAction.Explain, result.Action);
        Assert.NotNull(result.Explanation);
        Assert.NotNull(result.GapCheckpointIndex);
        Assert.False(string.IsNullOrEmpty(result.Explanation!.KnowledgeName));
    }

    [Fact]
    public async Task 讲解后用正确答案重试原步骤可通过()
    {
        var start = await _fixture.Engine.StartAsync("stu-retry-pass", "C-FE-01", 1);
        var sessionId = start!.SessionId;

        // 答错定位盲区
        var wrong = _fixture.Engine.SubmitAnswer(sessionId, "乱答一气");
        Assert.Equal(DiagnosticAction.Explain, wrong!.Action);

        // 用该步骤的标准表达式重试
        var step = _fixture.Db.Problems
            .Include(p => p.Steps)
            .First(p => p.Id == "C-FE-01")
            .Steps.First(s => s.StepIndex == 1);

        var retry = _fixture.Engine.RetryStep(sessionId, step.ExpectedExpression);
        Assert.NotNull(retry);
        Assert.Equal(StepVerdict.Correct, retry!.Verdict);
        Assert.Equal(DiagnosticAction.Completed, retry.Action);
    }

    // ==================== 讲解内容 ====================

    [Fact]
    public void 按知识点Id获取讲解返回描述和证据()
    {
        var explanation = _fixture.Engine.ExplainKnowledge("C-FE");
        Assert.NotNull(explanation);
        Assert.Equal("铁及其化合物", explanation!.KnowledgeName);
        Assert.False(string.IsNullOrEmpty(explanation.Description));
    }

    [Fact]
    public void 氧化还原知识点讲解包含相关反应证据()
    {
        var checkpoint = new ReasoningCheckpoint
        {
            KnowledgeId = "C-REDOX",
            KnowledgeName = "氧化还原反应",
            Explanation = "化合价升降与电子转移",
            EvidenceType = EvidenceType.Reaction
        };

        var explanation = _fixture.Engine.BuildExplanation(checkpoint);
        Assert.NotEmpty(explanation.RelatedReactions);
        Assert.Contains(explanation.Evidence, e => e.StartsWith("相关反应："));
    }

    [Fact]
    public void 铁知识点讲解包含物质颜色证据()
    {
        var checkpoint = new ReasoningCheckpoint
        {
            KnowledgeId = "C-FE",
            KnowledgeName = "铁及其化合物",
            Explanation = "Fe2+、Fe3+ 的检验与转化",
            EvidenceType = EvidenceType.SubstanceColor
        };

        var explanation = _fixture.Engine.BuildExplanation(checkpoint);
        Assert.NotEmpty(explanation.Evidence);
    }

    // ==================== 会话查询 ====================

    [Fact]
    public async Task 查询不存在的会话返回空()
    {
        var session = _fixture.Engine.GetSession("non-existent-session");
        Assert.Null(session);
    }

    [Fact]
    public async Task 提交答案后会话状态更新()
    {
        var start = await _fixture.Engine.StartAsync("stu-state", "C-NA-01", 1);
        var sessionId = start!.SessionId;

        _fixture.Engine.SubmitAnswer(sessionId, "错误答案");

        var session = _fixture.Engine.GetSession(sessionId);
        Assert.NotNull(session);
        Assert.Equal(DiagnosticStatus.GapFound, session!.Status);
        Assert.NotNull(session.GapCheckpointIndex);
    }

    // ==================== 教方法、教思考：检查点不直接给答案 ====================

    [Fact]
    public async Task 前置检查点问题不嵌入知识点描述()
    {
        var structure = await _fixture.Engine.BuildStructureAsync("C-FE-01", 1);
        Assert.NotNull(structure);

        var prereq = structure!.Checkpoints.First(c => c.Type == CheckpointType.Prerequisite);
        var kp = _fixture.Db.KnowledgeNodes.First(n => n.Id == prereq.KnowledgeId);

        // 问题中不应包含知识点描述原文（答案）
        Assert.DoesNotContain(kp.Description, prereq.Question);
        // 问题应该是引导式问句
        Assert.Contains("？", prereq.Question);
    }

    [Fact]
    public async Task 所有检查点都包含思考策略和分析框架()
    {
        var structure = await _fixture.Engine.BuildStructureAsync("C-COEXIST-01", 2);
        Assert.NotNull(structure);

        foreach (var cp in structure!.Checkpoints)
        {
            Assert.False(string.IsNullOrEmpty(cp.ThinkingStrategy),
                $"检查点 {cp.Index} ({cp.KnowledgeName}) 缺少思考策略");
            Assert.NotEmpty(cp.AnalysisFramework);
            Assert.False(string.IsNullOrEmpty(cp.CommonMisconception),
                $"检查点 {cp.Index} ({cp.KnowledgeName}) 缺少常见误区");
        }
    }

    [Fact]
    public async Task 讲解内容包含思考策略分析步骤和迁移提示()
    {
        var start = await _fixture.Engine.StartAsync("stu-thinking", "C-FE-01", 1);
        var sessionId = start!.SessionId;

        var result = _fixture.Engine.SubmitAnswer(sessionId, "完全不会");
        Assert.NotNull(result);
        Assert.NotNull(result!.Explanation);

        var exp = result.Explanation!;
        Assert.False(string.IsNullOrEmpty(exp.ThinkingStrategy), "讲解缺少思考策略");
        Assert.NotEmpty(exp.AnalysisSteps);
        Assert.False(string.IsNullOrEmpty(exp.CommonMisconception), "讲解缺少常见误区");
        Assert.False(string.IsNullOrEmpty(exp.TransferHint), "讲解缺少迁移提示");
    }

    [Fact]
    public void 按知识点Id获取讲解包含思考引导字段()
    {
        var exp = _fixture.Engine.ExplainKnowledge("C-COEXIST");
        Assert.NotNull(exp);
        Assert.False(string.IsNullOrEmpty(exp!.ThinkingStrategy));
        Assert.NotEmpty(exp.AnalysisSteps);
        Assert.False(string.IsNullOrEmpty(exp.CommonMisconception));
        Assert.False(string.IsNullOrEmpty(exp.TransferHint));
    }

    [Fact]
    public async Task 推理检查点引导学生用思考方法而非直接给原题()
    {
        var structure = await _fixture.Engine.BuildStructureAsync("C-FE-01", 2);
        Assert.NotNull(structure);

        var reasoning = structure!.Checkpoints.First(c => c.Type == CheckpointType.Reasoning);
        // 推理检查点应以引导语开头，让学生结合思路作答
        Assert.StartsWith("结合刚才的分析思路", reasoning.Question);
    }
}
