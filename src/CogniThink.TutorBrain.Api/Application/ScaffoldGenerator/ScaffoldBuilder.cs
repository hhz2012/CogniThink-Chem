namespace CogniThink.TutorBrain.Application.ScaffoldGenerator;

// ============================================================
// Task 5: 生成器入口 —— 组装 Task 1~4 完整管线：
//   求解（引擎真值）→ 选链 → 提真值（A–F，C/D 无来源即降级）→ 候选项 → 文案 → 校验门 → 产物或 null。
// 红线（全程）：
//   - correct 只来自引擎（StepTruthExtractor），LLM 只写文案（结构隔离）；
//   - 校验门 Ok=false → 整题 null，绝不发布猜测 correct；
//   - 只读消费者：不改判分链路、不改题干/选项/answer/事实库；
//   - 幂等：不依赖进程内随机（OptionsBuilder 用稳定 seed），同输入同产物。
// ============================================================

public sealed class ScaffoldBuilder : IScaffoldBuilder
{
    private static readonly AtomicTaskKind[] ChainKinds =
    {
        AtomicTaskKind.A, AtomicTaskKind.B, AtomicTaskKind.C,
        AtomicTaskKind.D, AtomicTaskKind.E, AtomicTaskKind.F,
    };

    private static readonly Dictionary<ScaffoldModuleKind, string> ModuleTitles = new()
    {
        [ScaffoldModuleKind.RedoxReasoning] = "氧化还原推理",
        [ScaffoldModuleKind.IonicSystem] = "离子体系与反应",
        [ScaffoldModuleKind.AqueousEquilibrium] = "水溶液平衡",
        [ScaffoldModuleKind.SequentialReaction] = "先后反应与竞争",
        [ScaffoldModuleKind.ElectrochemReasoning] = "电化学推理",
    };

    private readonly IModuleSelector _moduleSelector;
    private readonly IStepTruthExtractor _extractor;
    private readonly IOptionsBuilder _optionsBuilder;
    private readonly IReleasesGenerator _releases;
    private readonly IScaffoldValidator _validator;

    public ScaffoldBuilder(
        IModuleSelector? moduleSelector = null,
        IStepTruthExtractor? extractor = null,
        IOptionsBuilder? optionsBuilder = null,
        IReleasesGenerator? releases = null,
        IScaffoldValidator? validator = null)
    {
        _moduleSelector = moduleSelector ?? new ModuleSelector();
        _extractor = extractor ?? new StepTruthExtractor();
        _optionsBuilder = optionsBuilder ?? new OptionsBuilder();
        _releases = releases ?? new ReleasesGenerator();
        // 校验门与本管线共享同一个真值提取器，确保「重算」口径与「组装」口径一致
        _validator = validator ?? new ScaffoldValidator(_extractor);
    }

    /// <summary>接口契约：成功 → 产物；任何不发布 → null。</summary>
    public async Task<GeneratedScaffold?> BuildAsync(QuestionCase @case, CancellationToken ct = default)
    {
        var result = await BuildDetailedAsync(@case, artifacts: null, ct).ConfigureAwait(false);
        return result.Scaffold;
    }

    /// <summary>
    /// 完整管线 + 诊断结果（供批量命令审计，无静默丢弃）。
    /// 可注入预构建的 <paramref name="artifacts"/>（含从 checkpoint 回填的 EngineResponse），
    /// 为 null 时现场用引擎解析构建。
    /// </summary>
    public async Task<ScaffoldBuildResult> BuildDetailedAsync(
        QuestionCase @case,
        EngineArtifacts? artifacts,
        CancellationToken ct = default)
    {
        if (@case is null || string.IsNullOrWhiteSpace(@case.Question))
            return ScaffoldBuildResult.Fail("unsuitable", "题干为空");

        artifacts ??= EngineArtifactsBuilder.Build(@case);

        var module = _moduleSelector.Select(@case, artifacts);
        if (module is null)
            return ScaffoldBuildResult.Fail("unsuitable", "module_mismatch: 无法映射到脚手架模块");

        var scaffold = new GeneratedScaffold
        {
            Id = $"gen_{@case.Id}",
            Title = $"{ModuleTitles.GetValueOrDefault(module.Value, module.Value.ToString())}练习 {@case.Id}",
            Module = module.Value,
            SourceQuestionId = @case.Id,
            Steps = new List<StepPractice>(),
        };

        // C 步提取到的题面条件写入 scaffold（对齐手工题 conditions 口径，供分支判定与前端展示）
        scaffold.Conditions = EngineArtifactsBuilder.GetConditions(artifacts)?.Conditions;

        foreach (var kind in ChainKinds)
        {
            ct.ThrowIfCancellationRequested();
            var correct = _extractor.Extract(kind, artifacts);

            StepGroundTruth? truth = null;
            if (correct is not null)
            {
                truth = _optionsBuilder.Build(correct, kind, artifacts);
                scaffold.Provenance[kind.ToString()] = correct.Provenance;
            }
            else
            {
                // C/D 步降级：无真值但仍产出文案（只读揭示），不判致命
                scaffold.DegradedSteps.Add(new DegradedStep
                {
                    Kind = kind,
                    Reason = "引擎无可靠真值（步级降级为只读揭示）",
                });
            }

            var releases = await _releases.GenerateAsync(kind, correct, artifacts, ct).ConfigureAwait(false);
            scaffold.Steps.Add(new StepPractice { Kind = kind, Truth = truth, Releases = releases });
        }

        // 校验门：任一违例 → 整题 null
        var validation = _validator.Validate(scaffold, artifacts);
        scaffold.Validation = validation;
        if (!validation.Ok)
        {
            var status = validation.Conflict ? "conflict" : "unsuitable";
            return new ScaffoldBuildResult { Scaffold = null, Status = status, Errors = validation.Errors };
        }

        return new ScaffoldBuildResult { Scaffold = scaffold, Status = "published", Errors = new List<string>() };
    }
}
