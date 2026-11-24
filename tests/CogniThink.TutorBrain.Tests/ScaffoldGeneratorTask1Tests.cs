using CogniThink.Chem.Core.Balancing;
using CogniThink.Chem.Core.Parsing;
using CogniThink.TutorBrain.Application.ScaffoldGenerator;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// Task 1 验收：引擎真值提取器 + 链选择器。
/// 真值源为 Chem.Core 公开引擎；此处验证「提取器忠实搬运引擎结果、不猜测、不遗漏、失败降级」。
/// </summary>
public class ScaffoldGeneratorTask1Tests
{
    // ---- TR-1.1：A/B/E/F 各步 correct 与对应引擎字段重算一致 ----

    [Theory]
    [InlineData("2Fe + 3Cl2 = 2FeCl3")]
    [InlineData("Zn + CuSO4 = ZnSO4 + Cu")]
    [InlineData("2Na + 2H2O = 2NaOH + H2")]
    public void Extract_ABEF_MatchesEngineFields(string equation)
    {
        var artifacts = EngineArtifactsBuilder.Build(new QuestionCase
        {
            Id = "t",
            Type = "choice",
            Question = $"反应方程式：{equation}。下列说法正确的是？",
            Answer = "A",
        });

        var parsed = EngineArtifactsBuilder.GetParsed(artifacts);
        var redox = EngineArtifactsBuilder.GetRedox(artifacts);
        Assert.NotNull(parsed);
        Assert.NotNull(redox);
        Assert.True(redox!.Success);
        Assert.True(redox.IsRedox);

        var extractor = new StepTruthExtractor();

        // A 步：全部物种，与引擎 AllSpecies 一致
        var a = extractor.Extract(AtomicTaskKind.A, artifacts);
        Assert.NotNull(a);
        Assert.Equal(AtomicTaskInteraction.MultiCheck, a!.Interaction);
        Assert.Equal(parsed!.AllSpecies.Select(s => s.Formula).Distinct(), a.Set);
        Assert.Equal("ParsedEquation.AllSpecies", a.Provenance);

        // B 步：变价元素，与引擎 Changes 一致
        var b = extractor.Extract(AtomicTaskKind.B, artifacts);
        Assert.NotNull(b);
        Assert.Equal(redox.Changes.Select(c => c.Element).Distinct(), b!.Set);
        Assert.Equal("RedoxAnalysis.Changes", b.Provenance);

        // E 步：产物物种，与引擎 Right 一致
        var e = extractor.Extract(AtomicTaskKind.E, artifacts);
        Assert.NotNull(e);
        Assert.Equal(parsed.Right.Select(t => t.Species.Formula).Distinct(), e!.Set);
        Assert.Equal("ParsedEquation.Right", e.Provenance);

        // F 步：存储 answer
        var f = extractor.Extract(AtomicTaskKind.F, artifacts);
        Assert.NotNull(f);
        Assert.Equal(AtomicTaskInteraction.SingleSelect, f!.Interaction);
        Assert.Equal("A", f.Value);
        Assert.Equal("case.answer", f.Provenance);
    }

    [Fact]
    public void Extract_F_MultiChoiceAnswer_GivesMultiCheck()
    {
        var artifacts = EngineArtifactsBuilder.Build(new QuestionCase
        {
            Id = "t", Type = "choice",
            Question = "反应：2Fe + 3Cl2 = 2FeCl3。",
            Answer = "AC",
        });
        var f = new StepTruthExtractor().Extract(AtomicTaskKind.F, artifacts);
        Assert.NotNull(f);
        Assert.Equal(AtomicTaskInteraction.MultiCheck, f!.Interaction);
        Assert.Equal(new[] { "A", "C" }, f.Set);
    }

    // ---- 回归 #1：choice 逗号/分隔符多选归一化；非 choice 返回 null（不存整串误报 conflict） ----

    [Theory]
    [InlineData("A,C", "A,C")]
    [InlineData("A,B,C,D", "A,B,C,D")]
    [InlineData("A、C", "A,C")]
    [InlineData("A;C", "A,C")]
    public void Extract_F_ChoiceSeparatorMulti_GivesMultiCheck(string answer, string expectedJoined)
    {
        var expected = expectedJoined.Split(',');
        var artifacts = EngineArtifactsBuilder.Build(new QuestionCase
        {
            Id = "t", Type = "choice",
            Question = "反应：2Fe + 3Cl2 = 2FeCl3。",
            Answer = answer,
        });
        var f = new StepTruthExtractor().Extract(AtomicTaskKind.F, artifacts);
        Assert.NotNull(f);
        Assert.Equal(AtomicTaskInteraction.MultiCheck, f!.Interaction);
        Assert.Equal(expected, f.Set);
    }

    [Fact]
    public void Extract_F_NonChoice_ReturnsNull()
    {
        var artifacts = EngineArtifactsBuilder.Build(new QuestionCase
        {
            Id = "t", Type = "fill",
            Question = "反应：2Fe + 3Cl2 = 2FeCl3。",
            Answer = "H2O",
        });
        Assert.Null(new StepTruthExtractor().Extract(AtomicTaskKind.F, artifacts));
    }

    // ---- TR-1.2：引擎结果缺失 → 返回 null，不抛错、不返回空集合冒充 ----

    [Fact]
    public void Extract_NoEquationInQuestion_ReturnsNullForABEF()
    {
        // 题干无方程式 → 抽不到 → parsed/redox 为空
        var artifacts = EngineArtifactsBuilder.Build(new QuestionCase
        {
            Id = "t", Type = "choice",
            Question = "下列关于化学平衡的说法正确的是？",
            Answer = "B",
        });

        Assert.Null(EngineArtifactsBuilder.GetParsed(artifacts));
        Assert.Null(EngineArtifactsBuilder.GetRedox(artifacts));

        var extractor = new StepTruthExtractor();
        Assert.Null(extractor.Extract(AtomicTaskKind.A, artifacts));
        Assert.Null(extractor.Extract(AtomicTaskKind.B, artifacts));
        Assert.Null(extractor.Extract(AtomicTaskKind.E, artifacts));
        // F 步不依赖引擎，仍可从存储 answer 取
        var f = extractor.Extract(AtomicTaskKind.F, artifacts);
        Assert.NotNull(f);
        Assert.Equal("B", f!.Value);
    }

    [Fact]
    public void Extract_NonRedoxEquation_BStepReturnsNull()
    {
        // 复分解反应：无变价 → redox.IsRedox=false → B 步降级（不编造变价元素）
        var artifacts = EngineArtifactsBuilder.Build(new QuestionCase
        {
            Id = "t", Type = "choice",
            Question = "反应：AgNO3 + NaCl = AgCl + NaNO3。",
            Answer = "A",
        });
        var redox = EngineArtifactsBuilder.GetRedox(artifacts);
        Assert.NotNull(redox);
        Assert.False(redox!.IsRedox); // 非氧化还原

        var b = new StepTruthExtractor().Extract(AtomicTaskKind.B, artifacts);
        Assert.Null(b); // 不猜测变价物种
    }

    [Fact]
    public void Extract_CDSteps_AlwaysNull_Degraded()
    {
        var artifacts = EngineArtifactsBuilder.Build(new QuestionCase
        {
            Id = "t", Type = "choice",
            Question = "反应：Zn + CuSO4 = ZnSO4 + Cu。",
            Answer = "A",
        });
        var extractor = new StepTruthExtractor();
        // C/D 本轮不实现，恒降级
        Assert.Null(extractor.Extract(AtomicTaskKind.C, artifacts));
        Assert.Null(extractor.Extract(AtomicTaskKind.D, artifacts));
    }

    [Fact]
    public void Build_DoesNotThrow_OnMalformedEquation()
    {
        // 畸形方程式不抛错 → parsed 为空，下游降级
        var artifacts = EngineArtifactsBuilder.Build(new QuestionCase
        {
            Id = "t", Type = "choice",
            Question = "pH=7 时溶液呈中性。",
            Answer = "A",
        });
        // pH=7 不应被误判为方程式（两侧无元素字母）
        Assert.Null(EngineArtifactsBuilder.GetParsed(artifacts));
    }

    // ---- TR-1.3：selectModule 映射覆盖 5 模块中的 redox；未知 → null ----

    [Fact]
    public void SelectModule_RedoxByEngine_ReturnsRedoxReasoning()
    {
        var artifacts = EngineArtifactsBuilder.Build(new QuestionCase
        {
            Id = "t", Type = "choice",
            Question = "反应：2Fe + 3Cl2 = 2FeCl3。",
            Answer = "A",
        });
        var module = new ModuleSelector().Select(new QuestionCase { Question = "x" }, artifacts);
        Assert.Equal(ScaffoldModuleKind.RedoxReasoning, module);
    }

    [Fact]
    public void SelectModule_RedoxByKeyword_WhenEngineMisses()
    {
        // 引擎未判定氧化还原但题干含关键词 → 仍映射 redox（给一次机会）
        var artifacts = new EngineArtifacts { Case = new QuestionCase { Question = "探究氧化还原反应规律" } };
        var module = new ModuleSelector().Select(artifacts.Case, artifacts);
        Assert.Equal(ScaffoldModuleKind.RedoxReasoning, module);
    }

    [Fact]
    public void SelectModule_Unknown_ReturnsNull()
    {
        var artifacts = new EngineArtifacts { Case = new QuestionCase { Question = "下列说法正确的是" } };
        Assert.Null(new ModuleSelector().Select(artifacts.Case, artifacts));
    }
}
