using CogniThink.TutorBrain.Application.ScaffoldGenerator;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// Task 2 验收：候选项生成器 —— correct 全包含、干扰项不撞 correct、库覆盖、稳定可复现。
/// </summary>
public class ScaffoldGeneratorTask2Tests
{
    // ---- TR-2.3：correct 非空时 options 必含 correct 全部项 ----
    // ---- TR-2.1：distractors ∩ correct == ∅（含撞 correct 的干扰源被过滤） ----

    [Fact]
    public void Build_DistractorsNeverOverlapCorrect_EvenWhenProviderLeaks()
    {
        var correct = new StepCorrect
        {
            Interaction = AtomicTaskInteraction.MultiCheck,
            Set = new() { "Fe", "Cl" },
            Provenance = "RedoxAnalysis.Changes",
        };
        // 伪造的干扰源故意返回一个撞 correct 的值 "Fe" —— 必须被过滤掉
        var provider = new FakeDistractorProvider(new[] { "Fe", "Cu", "Zn" });
        var builder = new OptionsBuilder(provider);
        var truth = builder.Build(correct, AtomicTaskKind.B, new EngineArtifacts());

        var correctSet = new HashSet<string> { "Fe", "Cl" };

        // correct 全部出现在 options
        foreach (var c in correctSet)
            Assert.Contains(truth.Options, o => o.Key == c);

        // 干扰项（非 correct）绝不与 correct 相交
        var distractors = truth.Options.Where(o => !correctSet.Contains(o.Key)).Select(o => o.Key).ToList();
        Assert.DoesNotContain(distractors, d => correctSet.Contains(d));
        // "Fe" 是 correct，不应作为干扰项出现第二次
        Assert.Equal(1, truth.Options.Count(o => o.Key == "Fe"));
    }

    [Fact]
    public void Build_RealArtifacts_ABE_OptionsContainCorrectAndNoOverlap()
    {
        var artifacts = EngineArtifactsBuilder.Build(new QuestionCase
        {
            Id = "t", Type = "choice",
            Question = "反应：Zn + CuSO4 = ZnSO4 + Cu。",
            Answer = "A",
        });
        var extractor = new StepTruthExtractor();
        var builder = new OptionsBuilder();

        foreach (var kind in new[] { AtomicTaskKind.A, AtomicTaskKind.B, AtomicTaskKind.E })
        {
            var correct = extractor.Extract(kind, artifacts);
            Assert.NotNull(correct);
            var truth = builder.Build(correct!, kind, artifacts);

            var correctSet = ScaffoldGeneratorUtils.CorrectSet(correct!);
            Assert.NotEmpty(correctSet);

            // TR-2.3：correct 全部在 options
            foreach (var c in correctSet)
                Assert.Contains(truth.Options, o => o.Key == c);

            // TR-2.1：干扰项不撞 correct
            var distractors = truth.Options.Where(o => !correctSet.Contains(o.Key));
            Assert.DoesNotContain(distractors, o => correctSet.Contains(o.Key));

            // type 与交互一致
            Assert.Equal(ScaffoldGeneratorUtils.InteractionTypeName(correct!.Interaction), truth.Type);
        }
    }

    // ---- 稳定打乱：同输入同输出（可复现，便于单测与回放） ----

    [Fact]
    public void Build_StableOrder_SameInputSameOutput()
    {
        var correct = new StepCorrect
        {
            Interaction = AtomicTaskInteraction.MultiCheck,
            Set = new() { "A", "B", "C", "D", "E" },
            Provenance = "x",
        };
        var builder = new OptionsBuilder(new StaticDistractorProvider());
        var artifacts = new EngineArtifacts();

        var t1 = builder.Build(correct, AtomicTaskKind.F, artifacts);
        var t2 = builder.Build(correct, AtomicTaskKind.F, artifacts);

        Assert.Equal(t1.Options.Select(o => o.Key), t2.Options.Select(o => o.Key));
    }

    // ---- TR-2.2：静态库每类覆盖 ≥10 项 ----

    [Fact]
    public void StaticLibrary_CoversAtLeast10PerKind()
    {
        var provider = new StaticDistractorProvider();
        // 空 artifacts → 无上下文相关项，只有静态库
        var artifacts = new EngineArtifacts();
        var correct = new StepCorrect
        {
            Interaction = AtomicTaskInteraction.MultiCheck,
            Set = new() { "Xx" }, // 不会与任何库项相撞
            Provenance = "x",
        };

        foreach (var kind in new[] { AtomicTaskKind.A, AtomicTaskKind.B, AtomicTaskKind.E })
        {
            var d = provider.GetDistractors(kind, correct, artifacts);
            Assert.True(d.Count >= 10, $"{kind} 静态干扰库仅 {d.Count} 项，要求 ≥10");
        }
    }

    // ---- correct 序列化值正确（multi-check → 集合，single-select → 字符串） ----

    [Fact]
    public void Build_MultiCheck_CorrectIsList_SingleSelect_CorrectIsString()
    {
        var artifacts = new EngineArtifacts();

        var multi = new OptionsBuilder().Build(
            new StepCorrect { Interaction = AtomicTaskInteraction.MultiCheck, Set = new() { "Fe", "Cu" }, Provenance = "x" },
            AtomicTaskKind.B, artifacts);
        Assert.IsType<List<string>>(multi.Correct);
        Assert.Equal(new[] { "Cu", "Fe" }, (List<string>)multi.Correct!);

        var single = new OptionsBuilder().Build(
            new StepCorrect { Interaction = AtomicTaskInteraction.SingleSelect, Value = "A", Provenance = "x" },
            AtomicTaskKind.F, artifacts);
        Assert.Equal("A", single.Correct);
    }

    // ---- 伪干扰源（用于验证过滤逻辑） ----
    private sealed class FakeDistractorProvider : IDistractorProvider
    {
        private readonly string[] _values;
        public FakeDistractorProvider(string[] values) => _values = values;
        public IReadOnlyList<string> GetDistractors(AtomicTaskKind kind, StepCorrect correct, EngineArtifacts artifacts)
            => _values;
    }
}
