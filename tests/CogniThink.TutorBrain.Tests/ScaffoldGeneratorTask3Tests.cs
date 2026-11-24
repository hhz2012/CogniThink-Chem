using CogniThink.TutorBrain.Application.ScaffoldGenerator;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// Task 3 验收：文案生成器只写 releases 不碰 correct；LLM 不可用回退模板不崩；
/// LLM 干扰项补充不撞 correct。
/// </summary>
public class ScaffoldGeneratorTask3Tests
{
    // ---- 可控 LLM：可返回固定文本、可捕获提示词 ----
    private sealed class FakeLlm : IScaffoldLlmClient
    {
        public bool IsAvailable { get; set; } = true;
        public string? Response { get; set; }
        public string? LastSystem { get; private set; }
        public string? LastUser { get; private set; }

        public Task<string?> ChatAsync(string system, string user, float t, int m, CancellationToken ct = default)
        {
            LastSystem = system;
            LastUser = user;
            return Task.FromResult(Response);
        }
    }

    private static StepCorrect CorrectB() => new()
    {
        Interaction = AtomicTaskInteraction.MultiCheck,
        Set = new() { "Fe", "Cl" },
        Provenance = "RedoxAnalysis.Changes",
    };

    private static EngineArtifacts Artifacts() => new()
    {
        Case = new QuestionCase { Id = "t", Question = "反应：2Fe + 3Cl2 = 2FeCl3。", Answer = "A" },
    };

    private const string ValidReleasesJson = """
{
  "hint": "先标出变价元素，再判断谁升价谁降价。",
  "probe": { "stem": "该反应中谁是还原剂？", "options": ["Fe", "Cl2", "FeCl3"] },
  "scaffold": {
    "correct": { "title": "升价失电子", "detail": "Fe 由 0 价升到 +3 价，被氧化" },
    "wrong": { "title": "误判降价", "detail": "把 Cl 当成还原剂" }
  },
  "reveal": { "title": "变价判断规则", "body": "化合价升高、失去电子的物质是还原剂。" }
}
""";

    // ---- TR-3.1：LLM 输出只进 releases，不进 correct ----

    [Fact]
    public async Task Generate_ValidLlmOutput_PopulatesReleases_CorrectUntouched()
    {
        var llm = new FakeLlm { Response = ValidReleasesJson };
        var gen = new ReleasesGenerator(llm);
        var correct = CorrectB();

        var releases = await gen.GenerateAsync(AtomicTaskKind.B, correct, Artifacts());

        // LLM 文案进入 releases 四档
        Assert.Equal("先标出变价元素，再判断谁升价谁降价。", releases.Hint);
        Assert.Equal("该反应中谁是还原剂？", releases.Probe.Stem);
        Assert.Equal(new[] { "Fe", "Cl2", "FeCl3" }, releases.Probe.Options);
        Assert.Equal("升价失电子", releases.Scaffold.Correct.Title);
        Assert.Equal("Fe 由 0 价升到 +3 价，被氧化", releases.Scaffold.Correct.Detail);
        Assert.Equal("误判降价", releases.Scaffold.Wrong.Title);
        Assert.Equal("变价判断规则", releases.Reveal.Title);
        Assert.Equal("化合价升高、失去电子的物质是还原剂。", releases.Reveal.Body);

        // 结构保证：StepScaffoldReleases 不含 correct 字段 → LLM 无法触碰 correct。
        // 同一份 correct 交给 OptionsBuilder，仍产出与引擎一致的 correct（未被文案污染）。
        var truth = new OptionsBuilder().Build(correct, AtomicTaskKind.B, Artifacts());
        var correctSet = ScaffoldGeneratorUtils.CorrectSet(correct);
        foreach (var c in correctSet)
            Assert.Contains(truth.Options, o => o.Key == c);

        // 提示词把 correct 作为「已知事实」注入（强上下文约束），但仅作参考
        Assert.Contains("Fe", llm.LastUser);
        Assert.Contains("Cl", llm.LastUser);
        Assert.Contains("禁止写入文案或否定", llm.LastUser);
    }

    // ---- TR-3.2：LLM 不可用 / 返回 null → 回退模板，四档非空，不抛错 ----

    [Theory]
    [InlineData(false)]   // IsAvailable = false
    [InlineData(true)]    // 可用但返回 null
    public async Task Generate_LlmUnavailableOrNull_FallsBackToTemplate(bool availableButNull)
    {
        var llm = new FakeLlm { IsAvailable = availableButNull, Response = null };
        var gen = new ReleasesGenerator(llm);

        var releases = await gen.GenerateAsync(AtomicTaskKind.B, CorrectB(), Artifacts());

        Assert.False(string.IsNullOrWhiteSpace(releases.Hint));
        Assert.False(string.IsNullOrWhiteSpace(releases.Probe.Stem));
        Assert.NotEmpty(releases.Probe.Options);
        Assert.False(string.IsNullOrWhiteSpace(releases.Scaffold.Correct.Title));
        Assert.False(string.IsNullOrWhiteSpace(releases.Scaffold.Correct.Detail));
        Assert.False(string.IsNullOrWhiteSpace(releases.Scaffold.Wrong.Title));
        Assert.False(string.IsNullOrWhiteSpace(releases.Scaffold.Wrong.Detail));
        Assert.False(string.IsNullOrWhiteSpace(releases.Reveal.Title));
        Assert.False(string.IsNullOrWhiteSpace(releases.Reveal.Body));
    }

    [Fact]
    public async Task Generate_MalformedLlmOutput_FallsBackNotThrow()
    {
        var llm = new FakeLlm { Response = "这不是 JSON，也没有四个完整字段。" };
        var gen = new ReleasesGenerator(llm);

        var releases = await gen.GenerateAsync(AtomicTaskKind.B, CorrectB(), Artifacts());

        Assert.False(string.IsNullOrWhiteSpace(releases.Hint));
        Assert.NotEmpty(releases.Probe.Options);
    }

    // 构造器不传 LLM → 离线恒走模板（永不崩）
    [Fact]
    public async Task Generate_NoLlmInjected_UsesTemplate()
    {
        var gen = new ReleasesGenerator();
        var releases = await gen.GenerateAsync(AtomicTaskKind.E, CorrectB(), Artifacts());
        Assert.False(string.IsNullOrWhiteSpace(releases.Reveal.Body));
    }

    // correct 为 null（C/D 降级步）仍能生成 releases（只读揭示），不抛错
    [Fact]
    public async Task Generate_NullCorrect_StillProducesReleases()
    {
        var gen = new ReleasesGenerator(new FakeLlm { IsAvailable = false });
        var releases = await gen.GenerateAsync(AtomicTaskKind.C, null, Artifacts());
        Assert.False(string.IsNullOrWhiteSpace(releases.Hint));
        Assert.False(string.IsNullOrWhiteSpace(releases.Reveal.Body));
    }

    // ---- LlmDistractorProvider：LLM 干扰项不撞 correct，且合并基础库 ----

    [Fact]
    public void LlmDistractorProvider_FiltersCorrectAndMergesBase()
    {
        // 选一个不在 B 步静态库 / 方程式元素中的 correct 值（"Br"）：
        // 输出里若出现 "Br" 只能来自 LLM → 必须被防御过滤（正确碰撞单点过滤仍在 OptionsBuilder）。
        var correct = new StepCorrect
        {
            Interaction = AtomicTaskInteraction.MultiCheck,
            Set = new() { "Br" },
            Provenance = "RedoxAnalysis.Changes",
        };
        var baseProvider = new StaticDistractorProvider();
        // LLM 故意返回一个撞 correct 的 "Br" + 一个合法补充 "I"
        var llm = new FakeLlm { Response = "[\"Br\", \"I\"]" };
        var provider = new LlmDistractorProvider(baseProvider, llm);

        var distractors = provider.GetDistractors(AtomicTaskKind.B, correct, Artifacts());

        Assert.DoesNotContain("Br", distractors);   // LLM 吐出的 correct 被防御过滤
        Assert.Contains("I", distractors);          // LLM 合法补充保留
    }

    [Fact]
    public void LlmDistractorProvider_LlmUnavailable_ReturnsBaseOnly()
    {
        var correct = CorrectB();
        var baseProvider = new StaticDistractorProvider();
        var llm = new FakeLlm { IsAvailable = false };
        var provider = new LlmDistractorProvider(baseProvider, llm);

        var withLlm = provider.GetDistractors(AtomicTaskKind.B, correct, Artifacts());
        var baseline = baseProvider.GetDistractors(AtomicTaskKind.B, correct, Artifacts());

        Assert.Equal(new HashSet<string>(baseline), new HashSet<string>(withLlm));
    }
}
