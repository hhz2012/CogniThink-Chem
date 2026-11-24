namespace CogniThink.TutorBrain.Application.ScaffoldGenerator;

// ============================================================
// 按题动态生成脚手架 —— 管线接口契约（Task 0）
// 七层管线：选链 → 提真值 → 候选项 → 文案 → 校验门 → 产物。
// 本文件只定接口签名 + 空壳实现；逻辑在 Task 1~5 填充。
// 红线：correct 只来自引擎（IStepTruthExtractor），LLM 只写文案（IReleasesGenerator），
//       发布前必过 IScaffoldValidator。
// ============================================================

/// <summary>
/// 一步的正确答案（correct）运行时包装。提取器返回它；null = 引擎无法可靠产出 → 该步降级。
/// <see cref="Interaction"/> 决定取 <see cref="Set"/>（multi-check / order）还是 <see cref="Value"/>（single-select）。
/// <see cref="Provenance"/> 记录来源引擎字段（如 "RedoxAnalysis.Changes"），用于审计与校验门重算。
/// </summary>
public sealed class StepCorrect
{
    public AtomicTaskInteraction Interaction { get; set; }
    public List<string>? Set { get; set; }
    public string? Value { get; set; }
    public string Provenance { get; set; } = "";
}

/// <summary>链选择器：题目 → ScaffoldModuleKind；无法映射返回 null（整题不发布）。</summary>
public interface IModuleSelector
{
    ScaffoldModuleKind? Select(QuestionCase @case, EngineArtifacts artifacts);
}

/// <summary>真值提取器：从引擎产物取某步 correct；失败返回 null（绝不返回猜测值）。</summary>
public interface IStepTruthExtractor
{
    StepCorrect? Extract(AtomicTaskKind kind, EngineArtifacts artifacts);
}

/// <summary>
/// 候选项生成器：把引擎 correct 包装成 options（含干扰项，校验不撞 correct）。
/// 接收 <paramref name="artifacts"/> 以便生成与题干上下文相关的干扰项（如 B 步取非变价元素、E 步取反应物）。
/// </summary>
public interface IOptionsBuilder
{
    StepGroundTruth Build(StepCorrect correct, AtomicTaskKind kind, EngineArtifacts artifacts);
}

/// <summary>文案生成器：LLM 写 releases 四档；LLM 不可用时回退模板。只写文案，不碰 correct。</summary>
public interface IReleasesGenerator
{
    Task<StepScaffoldReleases> GenerateAsync(
        AtomicTaskKind kind,
        StepCorrect? correct,
        EngineArtifacts artifacts,
        CancellationToken ct = default);
}

/// <summary>校验门：发布前反向核对 correct 与引擎一致、F 与 answer 一致、distractors 不撞 correct。</summary>
public interface IScaffoldValidator
{
    ScaffoldValidationSummary Validate(GeneratedScaffold scaffold, EngineArtifacts artifacts);
}

/// <summary>生成器入口：组装完整管线，返回产物或 null（整题不发布）。</summary>
public interface IScaffoldBuilder
{
    Task<GeneratedScaffold?> BuildAsync(QuestionCase @case, CancellationToken ct = default);
}

// ---------- 生成器入口实现在 ScaffoldBuilder.cs（Task 5 填充完整管线） ----------
