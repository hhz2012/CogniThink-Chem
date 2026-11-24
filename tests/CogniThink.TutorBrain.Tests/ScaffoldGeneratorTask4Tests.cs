using CogniThink.TutorBrain.Application.ScaffoldGenerator;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// Task 4 验收：校验门护栏。构造违例脚手架必被拦截（Ok=false），
/// 合法组装（含 C/D 降级）必放行（Ok=true）。0 违例流入产物。
/// </summary>
public class ScaffoldGeneratorTask4Tests
{
    // ---- 可控真值提取器：返回指定 correct，模拟引擎重算结果 ----
    private sealed class FakeExtractor : IStepTruthExtractor
    {
        private readonly Dictionary<AtomicTaskKind, StepCorrect?> _map;
        public FakeExtractor(Dictionary<AtomicTaskKind, StepCorrect?> map) => _map = map;
        public StepCorrect? Extract(AtomicTaskKind kind, EngineArtifacts a)
            => _map.TryGetValue(kind, out var v) ? v : null;
    }

    private static StepCorrect SC(params string[] set) => new()
    {
        Interaction = AtomicTaskInteraction.MultiCheck,
        Set = set.ToList(),
        Provenance = "engine",
    };

    private static StepCorrect SC(string value) => new()
    {
        Interaction = AtomicTaskInteraction.SingleSelect,
        Value = value,
        Provenance = "engine",
    };

    // 引擎视角的标准氧化还原真值（A/B/E/F 有，C/D 无）
    private static Dictionary<AtomicTaskKind, StepCorrect?> RedoxTruth() => new()
    {
        [AtomicTaskKind.A] = SC("Fe", "Cl2", "FeCl3"),
        [AtomicTaskKind.B] = SC("Fe", "Cl"),
        [AtomicTaskKind.C] = null,
        [AtomicTaskKind.D] = null,
        [AtomicTaskKind.E] = SC("FeCl3"),
        [AtomicTaskKind.F] = SC("A"),
    };

    private static EngineArtifacts Artifacts(string answer = "A") => new()
    {
        Case = new QuestionCase { Id = "t", Question = "反应：2Fe + 3Cl2 = 2FeCl3。", Answer = answer },
    };

    private static StepPractice MakeStep(AtomicTaskKind kind, string[]? correct, bool singleSelect = false)
    {
        StepGroundTruth? truth = null;
        StepCorrect? sc = null;
        if (correct is not null)
        {
            sc = new StepCorrect
            {
                Interaction = singleSelect ? AtomicTaskInteraction.SingleSelect : AtomicTaskInteraction.MultiCheck,
                Set = singleSelect ? null : correct.ToList(),
                Value = singleSelect ? correct[0] : null,
                Provenance = "engine",
            };
            truth = new StepGroundTruth
            {
                Type = ScaffoldGeneratorUtils.InteractionTypeName(sc.Interaction),
                Options = correct.Select(c => new StepOption { Key = c, Label = c }).ToList(),
                Correct = ScaffoldGeneratorUtils.CorrectValue(sc),
            };
        }
        return new StepPractice
        {
            Kind = kind,
            Truth = truth,
            Releases = ScaffoldReleaseTemplates.BuildFallback(kind, sc, new EngineArtifacts()),
        };
    }

    private static GeneratedScaffold MakeScaffold(params StepPractice[] steps)
    {
        var sc = new GeneratedScaffold
        {
            Id = "s",
            Module = ScaffoldModuleKind.RedoxReasoning,
            Steps = steps.ToList(),
        };
        foreach (var s in steps)
            if (s.Truth is not null)
                sc.Provenance[s.Kind.ToString()] = "engine";
        return sc;
    }

    private static GeneratedScaffold ValidRedoxScaffold() => MakeScaffold(
        MakeStep(AtomicTaskKind.A, new[] { "Fe", "Cl2", "FeCl3" }),
        MakeStep(AtomicTaskKind.B, new[] { "Fe", "Cl" }),
        MakeStep(AtomicTaskKind.C, null),   // 降级步
        MakeStep(AtomicTaskKind.D, null),   // 降级步
        MakeStep(AtomicTaskKind.E, new[] { "FeCl3" }),
        MakeStep(AtomicTaskKind.F, new[] { "A" }, singleSelect: true));

    // ---- TR-4.2a：C/D 降级步放行；A/B/E/F 正确 → Ok=true ----

    [Fact]
    public void Validate_ValidScaffoldWithCDDegraded_OkTrue()
    {
        var validator = new ScaffoldValidator(new FakeExtractor(RedoxTruth()));
        var summary = validator.Validate(ValidRedoxScaffold(), Artifacts());

        Assert.True(summary.Ok, string.Join(" | ", summary.Errors));
        Assert.Empty(summary.Errors);
    }

    // ---- TR-4.1 护栏：correct 与引擎不一致 → 拦截 ----

    [Fact]
    public void Validate_CorrectMismatchesEngine_OkFalse()
    {
        var scaffold = ValidRedoxScaffold();
        // 篡改 B 步 correct：声称 ["X", "Y"]，但引擎重算为 ["Fe", "Cl"]
        var bStep = scaffold.Steps.First(s => s.Kind == AtomicTaskKind.B);
        bStep.Truth!.Options = new() { new() { Key = "X" }, new() { Key = "Y" } };
        bStep.Truth.Correct = new List<string> { "X", "Y" };

        var validator = new ScaffoldValidator(new FakeExtractor(RedoxTruth()));
        var summary = validator.Validate(scaffold, Artifacts());

        Assert.False(summary.Ok);
        Assert.Contains(summary.Errors, e => e.Contains("B") && e.Contains("不一致"));
    }

    // ---- TR-4.2b：关键步 A 缺失真值 → 整题不发布 ----

    [Fact]
    public void Validate_KeyStepAMissingTruth_OkFalse()
    {
        var scaffold = ValidRedoxScaffold();
        scaffold.Steps.First(s => s.Kind == AtomicTaskKind.A).Truth = null;

        var validator = new ScaffoldValidator(new FakeExtractor(RedoxTruth()));
        var summary = validator.Validate(scaffold, Artifacts());

        Assert.False(summary.Ok);
        Assert.Contains(summary.Errors, e => e.Contains("A") && e.Contains("缺失真值"));
    }

    // ---- TR-4.3：F 步 correct 与存储 answer 冲突 → 拦截 ----

    [Fact]
    public void Validate_FCorrectConflictsWithAnswer_OkFalse()
    {
        var scaffold = ValidRedoxScaffold();
        // 篡改 F 步 correct 为 "B"，但存储 answer 是 "A"
        var fStep = scaffold.Steps.First(s => s.Kind == AtomicTaskKind.F);
        fStep.Truth!.Options = new() { new() { Key = "B" } };
        fStep.Truth.Correct = "B";

        var validator = new ScaffoldValidator(new FakeExtractor(RedoxTruth()));
        var summary = validator.Validate(scaffold, Artifacts(answer: "A"));

        Assert.False(summary.Ok);
        Assert.Contains(summary.Errors, e => e.Contains("F") && e.Contains("冲突"));
    }

    // ---- 护栏：干扰项与 correct 相交 / correct 重复 → 拦截 ----

    [Fact]
    public void Validate_DistractorOverlapsCorrect_OkFalse()
    {
        var scaffold = ValidRedoxScaffold();
        var bStep = scaffold.Steps.First(s => s.Kind == AtomicTaskKind.B);
        // 给 B 步 options 额外塞一个 "Fe"（与 correct 重复，模拟干扰项撞 correct）
        bStep.Truth!.Options.Add(new StepOption { Key = "Fe", Label = "Fe" });

        var validator = new ScaffoldValidator(new FakeExtractor(RedoxTruth()));
        var summary = validator.Validate(scaffold, Artifacts());

        Assert.False(summary.Ok);
        Assert.Contains(summary.Errors, e => e.Contains("B"));
    }

    // ---- 护栏：provenance 缺失 → 拦截（审计不可缺） ----

    [Fact]
    public void Validate_MissingProvenance_OkFalse()
    {
        var scaffold = ValidRedoxScaffold();
        scaffold.Provenance.Clear();

        var validator = new ScaffoldValidator(new FakeExtractor(RedoxTruth()));
        var summary = validator.Validate(scaffold, Artifacts());

        Assert.False(summary.Ok);
        Assert.Contains(summary.Errors, e => e.Contains("provenance"));
    }

    // ---- 端到端 sanity：用真实引擎产物组装的脚手架应通过校验门 ----

    [Fact]
    public async Task Validate_RealEngineAssembledScaffold_OkTrue()
    {
        var artifacts = EngineArtifactsBuilder.Build(new QuestionCase
        {
            Id = "t",
            Type = "choice",
            Question = "反应：Zn + CuSO4 = ZnSO4 + Cu。",
            Answer = "A",
        });

        var extractor = new StepTruthExtractor();
        var options = new OptionsBuilder();
        var releases = new ReleasesGenerator();   // 无 LLM → 模板回退

        var scaffold = new GeneratedScaffold
        {
            Id = "gen",
            Module = ScaffoldModuleKind.RedoxReasoning,
            Steps = new List<StepPractice>(),
        };
        foreach (var kind in new[] { AtomicTaskKind.A, AtomicTaskKind.B, AtomicTaskKind.C, AtomicTaskKind.D, AtomicTaskKind.E, AtomicTaskKind.F })
        {
            var correct = extractor.Extract(kind, artifacts);
            var truth = correct is null ? null : options.Build(correct, kind, artifacts);
            var rel = await releases.GenerateAsync(kind, correct, artifacts);
            scaffold.Steps.Add(new StepPractice { Kind = kind, Truth = truth, Releases = rel });
            if (correct is not null)
                scaffold.Provenance[kind.ToString()] = correct.Provenance;
        }

        var summary = new ScaffoldValidator().Validate(scaffold, artifacts);

        Assert.True(summary.Ok, string.Join(" | ", summary.Errors));
    }
}
