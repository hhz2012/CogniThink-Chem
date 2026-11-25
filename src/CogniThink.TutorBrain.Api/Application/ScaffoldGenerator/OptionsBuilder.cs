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
        // C 步：选项来自固定维度目录（不混干扰项）；correct 为检测到的 key。
        if (kind == AtomicTaskKind.C)
            return BuildConditionsOptions(correct, artifacts);

        // D 步：选项为待排序物种（key/label 取自顺序表，不混干扰项）；correct 为有序 key 列表。
        if (kind == AtomicTaskKind.D)
            return BuildReactivityOrderOptions(correct, artifacts);

        // A/B/E/F：通用路径（correct + 干扰项 + 稳定打乱）。
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

    /// <summary>C 步：用维度固定目录构造选项（选项集稳定，不依赖干扰项），correct 保持检测值。</summary>
    private StepGroundTruth BuildConditionsOptions(StepCorrect correct, EngineArtifacts artifacts)
    {
        var cond = EngineArtifactsBuilder.GetConditions(artifacts)
            ?? throw new InvalidOperationException("C 步 BuildOptions 时条件提取结果缺失（correct 与 artifacts 不一致）");
        if (!ConditionOptionCatalog.ByDimension.TryGetValue(cond.PrimaryDimension, out var catalog))
            throw new InvalidOperationException($"C 步维度无候选目录：{cond.PrimaryDimension}");

        var options = catalog
            .Select(t => new StepOption { Key = t.Key, Label = t.Label })
            .ToList();

        var seed = ScaffoldGeneratorUtils.StableSeed(AtomicTaskKind.C, ScaffoldGeneratorUtils.CorrectSet(correct));
        ScaffoldGeneratorUtils.Shuffle(options, seed);

        return new StepGroundTruth
        {
            Type = ScaffoldGeneratorUtils.InteractionTypeName(correct.Interaction),
            Options = options,
            Correct = ScaffoldGeneratorUtils.CorrectValue(correct),
        };
    }

    /// <summary>D 步：选项为命中物种（保序表中 key/label），不混干扰项；correct 保留引擎原始顺序。</summary>
    private StepGroundTruth BuildReactivityOrderOptions(StepCorrect correct, EngineArtifacts artifacts)
    {
        var order = EngineArtifactsBuilder.GetReactivityOrder(artifacts)
            ?? throw new InvalidOperationException("D 步 BuildOptions 时排序提取结果缺失（correct 与 artifacts 不一致）");

        var options = order.OrderedSpecies
            .Select(s => new StepOption { Key = s.Key, Label = s.Label })
            .ToList();

        var seed = ScaffoldGeneratorUtils.StableSeed(AtomicTaskKind.D, ScaffoldGeneratorUtils.CorrectSet(correct));
        ScaffoldGeneratorUtils.Shuffle(options, seed);

        return new StepGroundTruth
        {
            Type = ScaffoldGeneratorUtils.InteractionTypeName(correct.Interaction),
            Options = options,
            Correct = ScaffoldGeneratorUtils.CorrectValue(correct),
        };
    }
}
