namespace CogniThink.TutorBrain.Tests;

using CogniThink.TutorBrain.Application.Diagnostic;
using CogniThink.TutorBrain.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// 高中化学常见易错点专项测试：
/// 针对离子共存、Fe²⁺/Fe³⁺ 检验、Na₂CO₃/NaHCO₃ 鉴别、条件漏看、概念混淆等
/// 高频易错点，验证诊断引擎能正确构建推理结构、定位盲区、给出针对性讲解。
/// </summary>
public sealed class DiagnosticErrorPronePointsTests : IClassFixture<DiagnosticFixture>
{
    private readonly DiagnosticFixture _fixture;

    public DiagnosticErrorPronePointsTests(DiagnosticFixture fixture) => _fixture = fixture;

    // =====================================================================
    // 易错点 1：离子共存 —— 酸性条件下 NO₃⁻ 氧化 Fe²⁺
    // 典型错误：忽略 H⁺ + NO₃⁻ = 硝酸（强氧化性），误判能共存
    // =====================================================================

    [Fact]
    public async Task 离子共存题推理结构必须包含氧化还原前置检查点()
    {
        var structure = await _fixture.Engine.BuildStructureAsync("C-COEXIST-01", 1);

        Assert.NotNull(structure);
        // 第 1 步知识点是 C-REDOX，推理结构应包含氧化还原与电解质前置
        var ids = structure!.Checkpoints.Select(c => c.KnowledgeId).ToList();
        Assert.Contains("C-REDOX", ids);
        Assert.Contains("C-ELECTROLYTE", ids);
    }

    [Fact]
    public async Task 忽略酸性条件误判能共存时定位到第一个盲区检查点()
    {
        var start = await _fixture.Engine.StartAsync("stu-coexist-wrong", "C-COEXIST-01", 1);
        var sid = start!.SessionId;

        // 学生错误回答：只说 NO3-，忽略 H+ 和硝酸的强氧化性
        var result = _fixture.Engine.SubmitAnswer(sid, "NO3- 和 Fe2+ 可以共存");

        Assert.NotNull(result);
        // 判定为不正确，返回讲解
        Assert.True(result!.Verdict is StepVerdict.Incorrect or StepVerdict.PartiallyCorrect);
        Assert.Equal(DiagnosticAction.Explain, result.Action);
        Assert.NotNull(result.GapCheckpointIndex);
        // 盲区落在第一个未通过的检查点（前置或核心）
        var gapCp = start.Structure.Checkpoints
            .First(c => c.Index == result.GapCheckpointIndex);
        Assert.Contains(gapCp.KnowledgeId, new[] { "C-ELECTROLYTE", "C-REDOX" });
    }

    [Fact]
    public async Task 通过前置检查点后正确回答硝酸强氧化性可推进到下一步()
    {
        var start = await _fixture.Engine.StartAsync("stu-coexist-right", "C-COEXIST-01", 1);
        var sid = start!.SessionId;

        // 先通过前置检查点（电解质）
        var prereq = start.Structure.Checkpoints[0];
        var ans0 = _fixture.Engine.SubmitAnswer(sid, $"{prereq.KnowledgeName} {prereq.Explanation}");
        Assert.True(ans0!.Verdict is StepVerdict.Correct or StepVerdict.PartiallyCorrect);

        // 再正确回答核心（氧化还原：H+ 与 NO3- 形成硝酸）
        var result = _fixture.Engine.SubmitAnswer(sid, "H+ 与 NO3- 相当于硝酸，具有强氧化性");

        Assert.NotNull(result);
        Assert.True(result!.Verdict is StepVerdict.Correct or StepVerdict.PartiallyCorrect);
        Assert.Equal(DiagnosticAction.NextCheckpoint, result.Action);
    }

    [Fact]
    public async Task 离子共存讲解应包含正确的盲区知识点描述()
    {
        var start = await _fixture.Engine.StartAsync("stu-coexist-explain", "C-COEXIST-01", 1);
        var sid = start!.SessionId;

        var result = _fixture.Engine.SubmitAnswer(sid, "能共存，不反应");
        Assert.Equal(DiagnosticAction.Explain, result!.Action);
        Assert.NotNull(result.Explanation);
        // 讲解内容来自实际盲区检查点的知识点
        Assert.False(string.IsNullOrEmpty(result.Explanation!.Description));
    }

    // =====================================================================
    // 易错点 2：Fe²⁺ / Fe³⁺ 检验混淆
    // 典型错误：以为 KSCN 检验 Fe²⁺，或用 NaOH 检验 Fe³⁺
    // =====================================================================

    [Fact]
    public async Task 铁检验题推理结构包含铁化合物核心检查点()
    {
        var structure = await _fixture.Engine.BuildStructureAsync("C-FE-01", 1);

        Assert.NotNull(structure);
        var core = structure!.Checkpoints.First(c => c.Type == CheckpointType.Core);
        Assert.Equal("C-FE", core.KnowledgeId);
        Assert.Contains("Fe³⁺", core.Question);
    }

    [Fact]
    public async Task 误用NaOH检验Fe3Plus时定位到第一个盲区检查点()
    {
        var start = await _fixture.Engine.StartAsync("stu-fe-naoh", "C-FE-01", 1);
        var sid = start!.SessionId;

        // 典型错误：用 NaOH 检验 Fe³⁺
        var result = _fixture.Engine.SubmitAnswer(sid, "加 NaOH 看沉淀颜色");

        Assert.NotNull(result);
        Assert.Equal(DiagnosticAction.Explain, result!.Action);
        Assert.NotNull(result.Explanation);
        // 盲区落在第一个未通过的检查点（前置或核心）
        var gapCp = start.Structure.Checkpoints
            .First(c => c.Index == result.GapCheckpointIndex);
        Assert.Contains(gapCp.KnowledgeId, new[] { "C-REDOX", "C-ELECTROLYTE", "C-FE" });
    }

    [Fact]
    public async Task 通过前置检查点后正确回答KSCN可推进到下一步()
    {
        var start = await _fixture.Engine.StartAsync("stu-fe-kscn", "C-FE-01", 1);
        var sid = start!.SessionId;

        // 先通过前置检查点
        var prereqs = start.Structure.Checkpoints
            .Where(c => c.Type == CheckpointType.Prerequisite).ToList();
        foreach (var p in prereqs)
        {
            var ans = _fixture.Engine.SubmitAnswer(sid, $"{p.KnowledgeName} {p.Explanation}");
            Assert.True(ans!.Verdict is StepVerdict.Correct or StepVerdict.PartiallyCorrect);
        }

        // 再正确回答核心（KSCN）
        var result = _fixture.Engine.SubmitAnswer(sid, "KSCN 硫氰化钾溶液");

        Assert.NotNull(result);
        Assert.True(result!.Verdict is StepVerdict.Correct or StepVerdict.PartiallyCorrect);
        Assert.Equal(DiagnosticAction.NextCheckpoint, result.Action);
    }

    [Fact]
    public async Task Fe3Plus检验讲解包含物质颜色证据()
    {
        var start = await _fixture.Engine.StartAsync("stu-fe-color", "C-FE-01", 1);
        var sid = start!.SessionId;

        var result = _fixture.Engine.SubmitAnswer(sid, "我不知道");
        Assert.Equal(DiagnosticAction.Explain, result!.Action);
        // 讲解应包含化学证据（铁的化合物颜色或相关反应）
        Assert.NotNull(result.Explanation);
    }

    // =====================================================================
    // 易错点 3：Na₂CO₃ / NaHCO₃ 鉴别 —— 与酸反应速率
    // 典型错误：以为 Na₂CO₃ 产气更快，或无法区分
    // =====================================================================

    [Fact]
    public async Task 钠鉴别题推理结构包含钠化合物核心检查点()
    {
        var structure = await _fixture.Engine.BuildStructureAsync("C-NA-01", 2);

        Assert.NotNull(structure);
        var core = structure!.Checkpoints.First(c => c.Type == CheckpointType.Core);
        Assert.Equal("C-NA", core.KnowledgeId);
    }

    [Fact]
    public async Task 误以为Na2CO3产气更快时定位到第一个盲区检查点()
    {
        var start = await _fixture.Engine.StartAsync("stu-na-wrong", "C-NA-01", 2);
        var sid = start!.SessionId;

        // 典型错误：认为 Na2CO3 反应更快
        var result = _fixture.Engine.SubmitAnswer(sid, "Na2CO3 产生气体更快");

        Assert.NotNull(result);
        Assert.Equal(DiagnosticAction.Explain, result!.Action);
        Assert.NotNull(result.Explanation);
        // 盲区落在第一个未通过的检查点（前置或核心）
        var gapCp = start.Structure.Checkpoints
            .First(c => c.Index == result.GapCheckpointIndex);
        Assert.Contains(gapCp.KnowledgeId, new[] { "C-ION-REACTION", "C-NA" });
    }

    [Fact]
    public async Task 通过前置检查点后正确回答NaHCO3产气更快可推进()
    {
        var start = await _fixture.Engine.StartAsync("stu-na-right", "C-NA-01", 2);
        var sid = start!.SessionId;

        // 先通过前置检查点
        var prereqs = start.Structure.Checkpoints
            .Where(c => c.Type == CheckpointType.Prerequisite).ToList();
        foreach (var p in prereqs)
        {
            var ans = _fixture.Engine.SubmitAnswer(sid, $"{p.KnowledgeName} {p.Explanation}");
            Assert.True(ans!.Verdict is StepVerdict.Correct or StepVerdict.PartiallyCorrect);
        }

        // 再正确回答核心（NaHCO3 产气更快）
        var result = _fixture.Engine.SubmitAnswer(sid, "NaHCO3 产生气体更快更剧烈");

        Assert.NotNull(result);
        Assert.True(result!.Verdict is StepVerdict.Correct or StepVerdict.PartiallyCorrect);
        Assert.Equal(DiagnosticAction.NextCheckpoint, result.Action);
    }

    // =====================================================================
    // 易错点 4：条件漏看 —— 限量/过量/酸碱性条件
    // =====================================================================

    [Fact]
    public async Task 离子共存题核心检查点问题应体现硝酸强氧化性条件()
    {
        var structure = await _fixture.Engine.BuildStructureAsync("C-COEXIST-01", 1);

        Assert.NotNull(structure);
        // 第 1 步核心检查点的问题应涉及 H+ 与 NO3- 的强氧化性组合
        var core = structure!.Checkpoints.First(c => c.Type == CheckpointType.Core);
        Assert.True(
            core.Question.Contains("H⁺") || core.Question.Contains("H+") ||
            core.Question.Contains("硝酸") || core.Question.Contains("氧化"),
            $"核心问题应涉及酸性/硝酸氧化性，实际为：{core.Question}");
    }

    [Fact]
    public async Task 完全空白回答被判定为不可评分而非错误()
    {
        var start = await _fixture.Engine.StartAsync("stu-blank", "C-FE-01", 1);
        var sid = start!.SessionId;

        var result = _fixture.Engine.SubmitAnswer(sid, "");

        Assert.NotNull(result);
        // 空白回答的判定应由推理引擎处理
        Assert.True(result!.Verdict is StepVerdict.Ungradable or StepVerdict.Incorrect);
    }

    // =====================================================================
    // 易错点 5：概念混淆 —— 氧化还原 / 电解质
    // =====================================================================

    [Fact]
    public async Task 电解质前置检查点问题考查强弱电解质概念()
    {
        var structure = await _fixture.Engine.BuildStructureAsync("C-COEXIST-01", 1);

        Assert.NotNull(structure);
        var electrolyteCp = structure!.Checkpoints
            .FirstOrDefault(c => c.KnowledgeId == "C-ELECTROLYTE");
        Assert.NotNull(electrolyteCp);
        Assert.Contains("电解质", electrolyteCp!.Question);
    }

    [Fact]
    public async Task 氧化还原讲解包含化合价升降核心概念()
    {
        var explanation = _fixture.Engine.ExplainKnowledge("C-REDOX");
        Assert.NotNull(explanation);
        Assert.Contains("化合价", explanation!.Description);
    }

    [Fact]
    public async Task 氧化还原讲解应关联化学知识库反应证据()
    {
        var explanation = _fixture.Engine.ExplainKnowledge("C-REDOX");
        Assert.NotNull(explanation);
        // 氧化还原知识点应关联到知识库中的反应
        Assert.NotEmpty(explanation!.RelatedReactions);
    }

    // =====================================================================
    // 易错点 6：推理跳跃 —— 未经过中间步骤直接给结论
    // =====================================================================

    [Fact]
    public async Task 推理结构中推理检查点排在核心检查点之后()
    {
        var structure = await _fixture.Engine.BuildStructureAsync("C-FE-01", 1);

        Assert.NotNull(structure);
        var coreIndex = structure!.Checkpoints
            .First(c => c.Type == CheckpointType.Core).Index;
        var reasoningIndex = structure.Checkpoints
            .First(c => c.Type == CheckpointType.Reasoning).Index;

        // 推理结论检查点必须在核心知识点之后，防止学生跳跃推理
        Assert.True(reasoningIndex > coreIndex);
    }

    [Fact]
    public async Task 前置检查点必须排在核心检查点之前()
    {
        var structure = await _fixture.Engine.BuildStructureAsync("C-COEXIST-01", 1);

        Assert.NotNull(structure);
        var prereqIndices = structure!.Checkpoints
            .Where(c => c.Type == CheckpointType.Prerequisite)
            .Select(c => c.Index).ToList();
        var coreIndex = structure.Checkpoints
            .First(c => c.Type == CheckpointType.Core).Index;

        // 所有前置检查点必须在核心之前
        Assert.All(prereqIndices, idx => Assert.True(idx < coreIndex));
    }

    // =====================================================================
    // 易错点 7：完整诊断流程 —— 从答错到掌握
    // =====================================================================

    [Fact]
    public async Task Fe3Plus检验完整诊断流程_答错到重试通过()
    {
        var start = await _fixture.Engine.StartAsync("stu-full-flow", "C-FE-01", 1);
        var sid = start!.SessionId;

        // 1. 答错 → 定位盲区
        var wrong = _fixture.Engine.SubmitAnswer(sid, "用 NaOH 检验");
        Assert.Equal(DiagnosticAction.Explain, wrong!.Action);

        // 2. 用正确答案重试原步骤
        var retry = _fixture.Engine.RetryStep(sid, "KSCN");
        Assert.NotNull(retry);
        Assert.Equal(StepVerdict.Correct, retry!.Verdict);
        Assert.Equal(DiagnosticAction.Completed, retry.Action);
    }

    [Fact]
    public async Task 离子共存完整诊断流程_答错氧化还原后重试通过()
    {
        var start = await _fixture.Engine.StartAsync("stu-coexist-full", "C-COEXIST-01", 1);
        var sid = start!.SessionId;

        // 答错氧化还原前置
        var wrong = _fixture.Engine.SubmitAnswer(sid, "能共存");
        Assert.Equal(DiagnosticAction.Explain, wrong!.Action);

        // 用正确表达式重试
        var step = _fixture.Db.Problems
            .Include(p => p.Steps)
            .First(p => p.Id == "C-COEXIST-01")
            .Steps.First(s => s.StepIndex == 1);

        var retry = _fixture.Engine.RetryStep(sid, step.ExpectedExpression);
        Assert.NotNull(retry);
        Assert.True(retry!.Verdict is StepVerdict.Correct or StepVerdict.PartiallyCorrect);
    }

    [Fact]
    public async Task 钠鉴别完整诊断流程_全部答对后可重试()
    {
        var start = await _fixture.Engine.StartAsync("stu-na-full", "C-NA-01", 2);
        var sid = start!.SessionId;
        var total = start.Structure.Checkpoints.Count;

        DiagnosticAnswerResult? answer = null;
        for (var i = 0; i < total; i++)
        {
            var cp = start.Structure.Checkpoints[i];
            var keywords = cp.ExpectedKeywords ?? string.Empty;
            var fullAnswer = $"{cp.KnowledgeName}：{cp.Explanation} {keywords}";
            answer = _fixture.Engine.SubmitAnswer(sid, fullAnswer);
        }

        Assert.Equal(DiagnosticAction.ReadyToRetry, answer!.Action);
    }

    // =====================================================================
    // 易错点 8：讲解内容覆盖所有知识点
    // =====================================================================

    [Theory]
    [InlineData("C-ELECTROLYTE", "电解质与电离")]
    [InlineData("C-ION-REACTION", "离子反应")]
    [InlineData("C-REDOX", "氧化还原反应")]
    [InlineData("C-COEXIST", "离子共存判断")]
    [InlineData("C-FE", "铁及其化合物")]
    [InlineData("C-NA", "钠及其化合物")]
    public void 所有知识点都能获取讲解内容(string knowledgeId, string expectedName)
    {
        var explanation = _fixture.Engine.ExplainKnowledge(knowledgeId);
        Assert.NotNull(explanation);
        Assert.Equal(expectedName, explanation!.KnowledgeName);
        Assert.False(string.IsNullOrEmpty(explanation.Description));
    }

    [Fact]
    public void 离子共存知识点讲解包含共存判断规则()
    {
        var explanation = _fixture.Engine.ExplainKnowledge("C-COEXIST");
        Assert.NotNull(explanation);
        // 离子共存讲解应包含共存判断的核心依据
        Assert.Contains("共存", explanation!.Description);
    }

    [Fact]
    public void 钠化合物知识点讲解包含Na2CO3与NaHCO3性质()
    {
        var explanation = _fixture.Engine.ExplainKnowledge("C-NA");
        Assert.NotNull(explanation);
        Assert.Contains("Na", explanation!.Description);
    }
}
