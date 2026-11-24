namespace CogniThink.TutorBrain.Application.ScaffoldGenerator;

// ============================================================
// Task 2: 候选项生成器 —— 把引擎 correct 包装成 StepGroundTruth，混入干扰项。
// 红线：
//   1. options 必含 correct 全部项；
//   2. 干扰项与 correct 不相交（相交即丢弃，绝不把正确答案混进干扰项）；
//   3. 稳定打乱（同输入同输出，可复现、可单测）。
// ============================================================

public sealed class OptionsBuilder : IOptionsBuilder
{
    private readonly IDistractorProvider _distractors;

    public OptionsBuilder(IDistractorProvider? distractors = null)
        => _distractors = distractors ?? new StaticDistractorProvider();

    public StepGroundTruth Build(StepCorrect correct, AtomicTaskKind kind, EngineArtifacts artifacts)
    {
        var correctSet = ScaffoldGeneratorUtils.CorrectSet(correct);

        // 1) correct 项（key = value，与前端 validateStep 比对口径一致）
        var options = correctSet
            .Select(v => new StepOption { Key = v, Label = v })
            .ToList();

        // 2) 干扰项：取 → 去重 → 过滤不撞 correct → 限量
        var maxDistractors = Math.Max(2, Math.Min(correctSet.Count * 2, 6));
        var distractors = _distractors.GetDistractors(kind, correct, artifacts)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct(StringComparer.Ordinal)
            .Where(d => !correctSet.Contains(d))   // 红线：不撞 correct
            .Take(maxDistractors)
            .Select(v => new StepOption { Key = v, Label = v })
            .ToList();
        options.AddRange(distractors);

        // 3) 稳定打乱（种子 = kind + correct 集合，跨进程可复现）
        var seed = ScaffoldGeneratorUtils.StableSeed(kind, correctSet);
        ScaffoldGeneratorUtils.Shuffle(options, seed);

        return new StepGroundTruth
        {
            Type = ScaffoldGeneratorUtils.InteractionTypeName(correct.Interaction),
            Options = options,
            Correct = ScaffoldGeneratorUtils.CorrectValue(correct),
        };
    }
}
