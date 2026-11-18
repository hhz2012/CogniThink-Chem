namespace CogniThink.TutorBrain.Tests;

using CogniThink.TutorBrain.Application;
using CogniThink.TutorBrain.Application.Ability;
using CogniThink.TutorBrain.Domain;
using Xunit;

/// <summary>
/// 化学解题基本能力评价系统测试：
/// 验证能力框架、能力-表现映射、综合评价、能力导学。
/// </summary>
public sealed class AbilityEvaluationTests : IClassFixture<DiagnosticFixture>
{
    private readonly DiagnosticFixture _fixture;

    public AbilityEvaluationTests(DiagnosticFixture fixture) => _fixture = fixture;

    // ==================== 能力框架 ====================

    [Fact]
    public void 能力框架包含六项基本能力()
    {
        var all = ChemistryAbilityLibrary.AllAbilities;
        Assert.Equal(6, all.Count);
        Assert.Contains(ChemistryAbility.ProblemAnalysis, all);
        Assert.Contains(ChemistryAbility.ConceptDifferentiation, all);
        Assert.Contains(ChemistryAbility.LogicalReasoning, all);
        Assert.Contains(ChemistryAbility.ChemicalCalculation, all);
        Assert.Contains(ChemistryAbility.ExperimentalInquiry, all);
        Assert.Contains(ChemistryAbility.InformationIntegration, all);
    }

    [Fact]
    public void 每项能力都有名称描述和引导问题()
    {
        foreach (var ability in ChemistryAbilityLibrary.AllAbilities)
        {
            var meta = ChemistryAbilityLibrary.Get(ability);
            Assert.False(string.IsNullOrEmpty(meta.Name), $"{ability} 缺少名称");
            Assert.False(string.IsNullOrEmpty(meta.Description), $"{ability} 缺少描述");
            Assert.False(string.IsNullOrEmpty(meta.GuidedQuestionTemplate), $"{ability} 缺少引导问题");
            Assert.False(string.IsNullOrEmpty(meta.StrategyHint), $"{ability} 缺少策略提示");
        }
    }

    [Fact]
    public void 错因标签能映射到对应能力()
    {
        Assert.Equal(ChemistryAbility.ProblemAnalysis,
            ChemistryAbilityLibrary.GetAbilityForErrorTag(ErrorTag.ConditionMissed));
        Assert.Equal(ChemistryAbility.ConceptDifferentiation,
            ChemistryAbilityLibrary.GetAbilityForErrorTag(ErrorTag.ConceptConfusion));
        Assert.Equal(ChemistryAbility.LogicalReasoning,
            ChemistryAbilityLibrary.GetAbilityForErrorTag(ErrorTag.ReasoningJump));
        Assert.Equal(ChemistryAbility.ChemicalCalculation,
            ChemistryAbilityLibrary.GetAbilityForErrorTag(ErrorTag.Calculation));
        Assert.Equal(ChemistryAbility.InformationIntegration,
            ChemistryAbilityLibrary.GetAbilityForErrorTag(ErrorTag.CoexistenceJudgement));
    }

    [Fact]
    public void 知识点映射到考察能力()
    {
        var coexistAbilities = ChemistryAbilityLibrary.GetAbilitiesForKnowledge("C-COEXIST");
        Assert.Contains(ChemistryAbility.InformationIntegration, coexistAbilities);
        Assert.Contains(ChemistryAbility.ProblemAnalysis, coexistAbilities);

        var feAbilities = ChemistryAbilityLibrary.GetAbilitiesForKnowledge("C-FE");
        Assert.Contains(ChemistryAbility.ExperimentalInquiry, feAbilities);
    }

    // ==================== 能力-表现映射 ====================

    [Fact]
    public void 答对时主考察能力提升幅度大于次考察能力()
    {
        var impacts = AbilityPerformanceMapper.Map(
            "C-COEXIST", StepVerdict.Correct, ErrorTag.None, hintLevel: 0);

        Assert.NotEmpty(impacts);
        var primary = impacts.First(i => i.IsPrimary);
        var secondary = impacts.First(i => !i.IsPrimary);
        Assert.True(primary.ScoreDelta > secondary.ScoreDelta);
        Assert.True(primary.ScoreDelta > 0);
    }

    [Fact]
    public void 答错时薄弱能力受到惩罚()
    {
        var impacts = AbilityPerformanceMapper.Map(
            "C-COEXIST", StepVerdict.Incorrect, ErrorTag.CoexistenceJudgement, hintLevel: 0);

        var weak = impacts.FirstOrDefault(i => i.Ability == ChemistryAbility.InformationIntegration);
        Assert.NotNull(weak);
        Assert.True(weak!.ScoreDelta < 0);
    }

    [Fact]
    public void 提示级别越高答对增益越小()
    {
        var noHint = AbilityPerformanceMapper.Map(
            "C-FE", StepVerdict.Correct, ErrorTag.None, hintLevel: 0);
        var highHint = AbilityPerformanceMapper.Map(
            "C-FE", StepVerdict.Correct, ErrorTag.None, hintLevel: 4);

        var primaryNoHint = noHint.First(i => i.IsPrimary).ScoreDelta;
        var primaryHighHint = highHint.First(i => i.IsPrimary).ScoreDelta;
        Assert.True(primaryNoHint > primaryHighHint);
    }

    // ==================== 能力评价服务 ====================

    [Fact]
    public async Task 无作答记录时返回默认能力得分()
    {
        var service = new AbilityEvaluationService(_fixture.Db);
        var scores = await service.EvaluateAsync("stu-no-trace");

        Assert.Equal(6, scores.Count);
        Assert.All(scores, s => Assert.Equal(0.5, s.Score));
        Assert.All(scores, s => Assert.Equal(0, s.Attempts));
    }

    [Fact]
    public async Task 连续答错后对应能力得分下降()
    {
        var studentId = "stu-ability-down";
        await _fixture.StudentModel.GetOrCreateAsync(studentId);

        // 模拟 5 次在 C-COEXIST 上答错，错因为共存判断错误
        for (var i = 0; i < 5; i++)
        {
            await _fixture.StudentModel.AddStepTraceAsync(
                studentId, "C-COEXIST-01", 2, "错误答案",
                StepVerdict.Incorrect, ErrorTag.CoexistenceJudgement, 0, "反馈", default);
        }
        await _fixture.Db.SaveChangesAsync();

        var service = new AbilityEvaluationService(_fixture.Db);
        var scores = await service.EvaluateAsync(studentId);

        var infoIntegration = scores.First(s => s.Ability == ChemistryAbility.InformationIntegration);
        Assert.True(infoIntegration.Attempts > 0);
        Assert.True(infoIntegration.Score < 0.5,
            $"连续答错后信息整合能力应低于默认值，实际为 {infoIntegration.Score}");
    }

    [Fact]
    public async Task 答对后能力得分上升()
    {
        var studentId = "stu-ability-up";
        await _fixture.StudentModel.GetOrCreateAsync(studentId);

        // 先答错一次降低
        await _fixture.StudentModel.AddStepTraceAsync(
            studentId, "C-FE-01", 1, "错误",
            StepVerdict.Incorrect, ErrorTag.ConceptConfusion, 0, "反馈", default);
        // 再答对三次提升
        for (var i = 0; i < 3; i++)
        {
            await _fixture.StudentModel.AddStepTraceAsync(
                studentId, "C-FE-01", 1, "KSCN",
                StepVerdict.Correct, ErrorTag.None, 0, "反馈", default);
        }
        await _fixture.Db.SaveChangesAsync();

        var service = new AbilityEvaluationService(_fixture.Db);
        var scores = await service.EvaluateAsync(studentId);

        var experimental = scores.First(s => s.Ability == ChemistryAbility.ExperimentalInquiry);
        Assert.True(experimental.Corrects > 0);
        Assert.True(experimental.Score > 0, "答对后能力得分应大于 0");
    }

    // ==================== 综合评价 ====================

    [Fact]
    public async Task 综合评价包含知识和能力两个维度()
    {
        var studentId = "stu-comprehensive";
        await _fixture.StudentModel.GetOrCreateAsync(studentId);

        // 制造一些作答记录
        await _fixture.StudentModel.AddStepTraceAsync(
            studentId, "C-FE-01", 1, "KSCN",
            StepVerdict.Correct, ErrorTag.None, 0, "反馈", default);
        await _fixture.Db.SaveChangesAsync();

        var abilityEval = new AbilityEvaluationService(_fixture.Db);
        var graph = new AbilityGraphService(_fixture.Db);
        var comp = new ComprehensiveEvaluationService(abilityEval, graph);

        var eval = await comp.EvaluateAsync(studentId);

        Assert.Equal(studentId, eval.StudentId);
        Assert.True(eval.OverallScore is > 0 and <= 1);
        Assert.True(eval.KnowledgeScore is > 0 and <= 1);
        Assert.True(eval.AbilityScore is > 0 and <= 1);
        Assert.NotEmpty(eval.KnowledgeMasteries);
        Assert.Equal(6, eval.AbilityScores.Count);
    }

    [Fact]
    public async Task 综合评价生成教学建议()
    {
        var studentId = "stu-suggestion";
        await _fixture.StudentModel.GetOrCreateAsync(studentId);

        // 连续答错制造薄弱项
        for (var i = 0; i < 4; i++)
        {
            await _fixture.StudentModel.AddStepTraceAsync(
                studentId, "C-COEXIST-01", 2, "错",
                StepVerdict.Incorrect, ErrorTag.CoexistenceJudgement, 0, "反馈", default);
        }
        await _fixture.Db.SaveChangesAsync();

        var abilityEval = new AbilityEvaluationService(_fixture.Db);
        var graph = new AbilityGraphService(_fixture.Db);
        var comp = new ComprehensiveEvaluationService(abilityEval, graph);

        var eval = await comp.EvaluateAsync(studentId);

        Assert.NotEmpty(eval.TeachingSuggestions);
        Assert.Contains(eval.TopErrorTags, t => t == "共存判断错误");
    }

    // ==================== 能力导学 ====================

    [Fact]
    public void 每项能力都能生成引导式交互()
    {
        var service = new AbilityGuidedTeachingService(new AbilityEvaluationService(_fixture.Db));

        foreach (var ability in ChemistryAbilityLibrary.AllAbilities)
        {
            var interaction = service.GetGuidanceForAbility(ability);
            Assert.NotNull(interaction);
            Assert.False(string.IsNullOrEmpty(interaction!.Question));
            Assert.False(string.IsNullOrEmpty(interaction.StrategyHint));
            Assert.NotEmpty(interaction.ProgressiveHints);
        }
    }

    [Fact]
    public void 引导式交互包含三级渐进提示()
    {
        var service = new AbilityGuidedTeachingService(new AbilityEvaluationService(_fixture.Db));
        var interaction = service.GetGuidanceForAbility(ChemistryAbility.InformationIntegration);

        Assert.Equal(3, interaction.ProgressiveHints.Count);
        // 渐进提示应从抽象到具体：第一级不含具体例子
        Assert.DoesNotContain("例如", interaction.ProgressiveHints[0]);
    }

    [Fact]
    public async Task 针对薄弱能力生成引导()
    {
        var studentId = "stu-guidance";
        await _fixture.StudentModel.GetOrCreateAsync(studentId);

        for (var i = 0; i < 4; i++)
        {
            await _fixture.StudentModel.AddStepTraceAsync(
                studentId, "C-COEXIST-01", 2, "错",
                StepVerdict.Incorrect, ErrorTag.CoexistenceJudgement, 0, "反馈", default);
        }
        await _fixture.Db.SaveChangesAsync();

        var service = new AbilityGuidedTeachingService(new AbilityEvaluationService(_fixture.Db));
        var interaction = await service.GetGuidanceForWeakestAbilityAsync(studentId);

        Assert.NotNull(interaction);
        Assert.Equal(ChemistryAbility.InformationIntegration, interaction!.Ability);
    }

    [Fact]
    public async Task 针对知识点生成能力导向提示()
    {
        var studentId = "stu-kp-guidance";
        await _fixture.StudentModel.GetOrCreateAsync(studentId);

        var service = new AbilityGuidedTeachingService(new AbilityEvaluationService(_fixture.Db));
        var interaction = await service.GetGuidanceForStepAsync(studentId, "C-FE");

        Assert.NotNull(interaction);
        Assert.Contains(interaction!.RelatedKnowledgeIds, k => k == "C-FE");
    }
}
