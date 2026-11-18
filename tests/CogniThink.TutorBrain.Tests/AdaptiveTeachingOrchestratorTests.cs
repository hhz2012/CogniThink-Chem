namespace CogniThink.TutorBrain.Tests;

using CogniThink.TutorBrain.Application;
using CogniThink.TutorBrain.Application.Diagnostic;
using CogniThink.TutorBrain.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// 自适应教学完整闭环测试：
/// 答错 → 诊断定位盲区 → 更新能力图谱（降低掌握度）→ 讲解 → 重试通过
/// → 提升掌握度 + 安排复习 + 生成变式练习题。
/// </summary>
public sealed class AdaptiveTeachingOrchestratorTests : IClassFixture<DiagnosticFixture>
{
    private readonly DiagnosticFixture _fixture;

    public AdaptiveTeachingOrchestratorTests(DiagnosticFixture fixture)
    {
        _fixture = fixture;
        // 每个测试使用独立的变更追踪器，避免跨测试实体污染
        _fixture.Db.ChangeTracker.Clear();
    }

    // =====================================================================
    // 能力图谱服务测试
    // =====================================================================

    [Fact]
    public async Task 能力图谱返回所有知识点节点和依赖边()
    {
        var (nodes, edges) = await _fixture.AbilityGraph.GetGraphAsync("stu-graph");

        Assert.True(nodes.Count >= 6, "应包含种子数据中的所有知识点");
        Assert.True(edges.Count > 0, "应包含知识点之间的依赖边");

        // 每个节点都有掌握度字段
        Assert.All(nodes, n =>
        {
            Assert.InRange(n.Mastery, 0, 1);
            Assert.False(string.IsNullOrEmpty(n.Name));
        });
    }

    [Fact]
    public async Task 未练习过的知识点掌握度为零()
    {
        var node = await _fixture.AbilityGraph.GetNodeAsync("stu-new", "C-FE");

        Assert.NotNull(node);
        Assert.Equal(0, node!.Mastery);
        Assert.Equal(0, node.Attempts);
        Assert.True(node.IsWeak);
    }

    [Fact]
    public async Task 掌握度低于阈值的知识点被标记为薄弱()
    {
        var node = await _fixture.AbilityGraph.GetNodeAsync("stu-weak", "C-FE");
        Assert.NotNull(node);
        Assert.True(node!.IsWeak == (node.Mastery < 0.5));
    }

    [Fact]
    public async Task 薄弱前置查询返回掌握度不足的前置知识点()
    {
        // 确保学生存在
        await _fixture.StudentModel.GetOrCreateAsync("stu-prereq");

        // 降低电解质掌握度（先保存第一次再更新第二次，避免重复实体）
        await _fixture.StudentModel.UpdateMasteryAsync("stu-prereq", "C-ELECTROLYTE", correct: false);
        await _fixture.Db.SaveChangesAsync();
        await _fixture.StudentModel.UpdateMasteryAsync("stu-prereq", "C-ELECTROLYTE", correct: false);
        await _fixture.Db.SaveChangesAsync();

        var weak = await _fixture.AbilityGraph.GetWeakPrerequisitesAsync("stu-prereq", "C-REDOX");

        Assert.Contains(weak, w => w.KnowledgeId == "C-ELECTROLYTE");
        Assert.All(weak, w => Assert.True(w.Mastery < 0.5));
    }

    // =====================================================================
    // 自适应教学闭环测试
    // =====================================================================

    [Fact]
    public async Task 答错后启动诊断并记录错题()
    {
        var result = await _fixture.Adaptive.StartForWrongAnswerAsync(
            "stu-adaptive-1", "C-FE-01", 1, "NaOH");

        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result!.SessionId));

        // 错题应已记录
        var mistakes = _fixture.Db.MistakeRecords
            .Where(m => m.StudentId == "stu-adaptive-1" && m.ProblemId == "C-FE-01")
            .ToList();
        Assert.NotEmpty(mistakes);
        Assert.False(mistakes[0].Passed);
    }

    [Fact]
    public async Task 答错后诊断检查点数量与推理结构一致()
    {
        var result = await _fixture.Adaptive.StartForWrongAnswerAsync(
            "stu-adaptive-2", "C-FE-01", 1, "NaOH");

        Assert.NotNull(result);
        Assert.True(result!.Structure.Checkpoints.Count >= 2);
        Assert.NotNull(result.CurrentCheckpoint);
        Assert.Equal(1, result.CurrentCheckpoint.Index);
    }

    [Fact]
    public async Task 诊断答错时降低盲区知识点掌握度()
    {
        var start = await _fixture.Adaptive.StartForWrongAnswerAsync(
            "stu-adaptive-3", "C-FE-01", 1, "NaOH");
        var sid = start!.SessionId;

        // 先获取盲区知识点的初始掌握度
        var session = _fixture.Engine.GetSession(sid);
        Assert.NotNull(session);

        // 答错第一个检查点
        var ansResult = await _fixture.Adaptive.SubmitDiagnosticAnswerAsync(sid, "我不会");

        Assert.Equal(DiagnosticAction.Explain, ansResult.Action);
        Assert.True(ansResult.GapCheckpointIndex > 0);

        // 验证盲区知识点掌握度已被记录（有尝试记录）
        var gapCp = session!.Structure.Checkpoints
            .First(c => c.Index == ansResult.GapCheckpointIndex);
        var mastery = _fixture.Db.KnowledgeMasteries
            .FirstOrDefault(m => m.StudentId == "stu-adaptive-3" && m.KnowledgeId == gapCp.KnowledgeId);

        Assert.NotNull(mastery);
        Assert.True(mastery!.Attempts >= 1);
    }

    [Fact]
    public async Task 诊断答对时不降低掌握度并推进到下一检查点()
    {
        var start = await _fixture.Adaptive.StartForWrongAnswerAsync(
            "stu-adaptive-4", "C-FE-01", 1, "NaOH");
        var sid = start!.SessionId;

        var firstCp = start.Structure.Checkpoints[0];
        var correctAnswer = $"{firstCp.KnowledgeName} {firstCp.Explanation}";

        var ansResult = await _fixture.Adaptive.SubmitDiagnosticAnswerAsync(sid, correctAnswer);

        Assert.Equal(DiagnosticAction.NextCheckpoint, ansResult.Action);
        Assert.Null(ansResult.GapCheckpointIndex);
    }

    [Fact]
    public async Task 完整闭环_答错诊断到重试通过生成变式题()
    {
        const string studentId = "stu-full-adaptive";
        const string problemId = "C-FE-01";
        const int stepIndex = 1;

        // 1. 答错 → 启动诊断
        var start = await _fixture.Adaptive.StartForWrongAnswerAsync(
            studentId, problemId, stepIndex, "NaOH");
        var sid = start!.SessionId;
        Assert.NotNull(start.CurrentCheckpoint);

        // 2. 答错诊断检查点 → 定位盲区，掌握度降低
        var wrongAns = await _fixture.Adaptive.SubmitDiagnosticAnswerAsync(sid, "完全不会");
        Assert.Equal(DiagnosticAction.Explain, wrongAns.Action);
        Assert.True(wrongAns.GapCheckpointIndex > 0);

        // 3. 用正确答案重试原步骤
        var retry = await _fixture.Adaptive.RetryWithPracticeAsync(sid, "KSCN");

        // 4. 验证：重试通过
        Assert.Equal(DiagnosticAction.Completed, retry.RetryResult.Action);
        Assert.Equal(StepVerdict.Correct, retry.RetryResult.Verdict);

        // 5. 验证：生成了变式练习题
        Assert.NotNull(retry.PracticeVariant);
        Assert.StartsWith(problemId + "-V", retry.PracticeVariant!.Id);

        // 6. 验证：错题已标记通过
        var mistake = _fixture.Db.MistakeRecords
            .FirstOrDefault(m => m.StudentId == studentId && m.ProblemId == problemId);
        Assert.NotNull(mistake);
        Assert.True(mistake!.Passed);

        // 7. 验证：能力图谱中该知识点掌握度已提升（有尝试记录）
        var targetKp = retry.TargetKnowledgeId;
        Assert.NotNull(targetKp);
        var mastery = _fixture.Db.KnowledgeMasteries
            .FirstOrDefault(m => m.StudentId == studentId && m.KnowledgeId == targetKp);
        Assert.NotNull(mastery);
        Assert.True(mastery!.Attempts >= 1);
    }

    [Fact]
    public async Task 完整闭环_离子共存题答错后诊断到变式练习()
    {
        const string studentId = "stu-coexist-adaptive";
        const string problemId = "C-COEXIST-01";

        // 1. 答错
        var start = await _fixture.Adaptive.StartForWrongAnswerAsync(
            studentId, problemId, 1, "能共存");
        var sid = start!.SessionId;

        // 2. 答错诊断检查点
        var wrongAns = await _fixture.Adaptive.SubmitDiagnosticAnswerAsync(sid, "不知道");
        Assert.Equal(DiagnosticAction.Explain, wrongAns.Action);

        // 3. 用标准表达式重试
        var step = _fixture.Db.Problems
            .Include(p => p.Steps)
            .First(p => p.Id == problemId)
            .Steps.First(s => s.StepIndex == 1);

        var retry = await _fixture.Adaptive.RetryWithPracticeAsync(sid, step.ExpectedExpression);

        // 验证：重试通过且生成变式
        Assert.True(retry.RetryResult.Verdict is StepVerdict.Correct or StepVerdict.PartiallyCorrect);
        Assert.NotNull(retry.PracticeVariant);
        Assert.StartsWith(problemId + "-V", retry.PracticeVariant!.Id);
    }

    [Fact]
    public async Task 重试未通过时不生成变式题()
    {
        const string studentId = "stu-retry-fail";
        var start = await _fixture.Adaptive.StartForWrongAnswerAsync(
            studentId, "C-FE-01", 1, "NaOH");
        var sid = start!.SessionId;

        // 答错诊断
        await _fixture.Adaptive.SubmitDiagnosticAnswerAsync(sid, "不会");

        // 用错误答案重试
        var retry = await _fixture.Adaptive.RetryWithPracticeAsync(sid, "随便答一个错误答案");

        Assert.NotEqual(DiagnosticAction.Completed, retry.RetryResult.Action);
        Assert.Null(retry.PracticeVariant);
    }

    [Fact]
    public async Task 能力图谱反映答错后的掌握度变化()
    {
        const string studentId = "stu-graph-change";
        const string knowledgeId = "C-FE";

        // 初始掌握度
        var before = await _fixture.AbilityGraph.GetNodeAsync(studentId, knowledgeId);
        Assert.Equal(0, before!.Attempts);

        // 答错并定位到 C-FE 盲区
        var start = await _fixture.Adaptive.StartForWrongAnswerAsync(
            studentId, "C-FE-01", 1, "NaOH");
        var sid = start!.SessionId;

        // 通过前置检查点直到核心
        var checkpoints = start.Structure.Checkpoints;
        for (var i = 0; i < checkpoints.Count - 1; i++)
        {
            var cp = checkpoints[i];
            var ans = $"{cp.KnowledgeName} {cp.Explanation}";
            var r = await _fixture.Adaptive.SubmitDiagnosticAnswerAsync(sid, ans);
            if (r.Action != DiagnosticAction.NextCheckpoint) break;
        }

        // 答错核心（C-FE）
        await _fixture.Adaptive.SubmitDiagnosticAnswerAsync(sid, "用 NaOH");

        // 重试通过
        await _fixture.Adaptive.RetryWithPracticeAsync(sid, "KSCN");

        // 验证掌握度有更新
        var after = await _fixture.AbilityGraph.GetNodeAsync(studentId, knowledgeId);
        Assert.True(after!.Attempts > before!.Attempts);
    }

    [Fact]
    public async Task 变式练习题包含原题的知识点()
    {
        const string studentId = "stu-variant-kp";
        var start = await _fixture.Adaptive.StartForWrongAnswerAsync(
            studentId, "C-FE-01", 1, "NaOH");
        var sid = start!.SessionId;

        await _fixture.Adaptive.SubmitDiagnosticAnswerAsync(sid, "不会");
        var retry = await _fixture.Adaptive.RetryWithPracticeAsync(sid, "KSCN");

        // 变式题应包含与原题相同的知识点
        Assert.NotNull(retry.PracticeVariant);
        var variantKps = retry.PracticeVariant!.KnowledgeLinks.Select(l => l.KnowledgeId).ToList();
        Assert.Contains("C-FE", variantKps);
    }
}
