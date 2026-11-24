namespace CogniThink.TutorBrain.Application.ScaffoldGenerator;

// ============================================================
// Task 5: 生成器管线的诊断结果（供批量命令审计，无静默丢弃）。
// Status: published（已发布，可能含 degradedSteps）/ unsuitable（不发布）/ conflict（F 冲突，不发布）。
// 红线：Scaffold == null 时绝不发布猜测 correct；Errors 必带原因。
// ============================================================

public sealed class ScaffoldBuildResult
{
    public GeneratedScaffold? Scaffold { get; init; }

    /// <summary>published | unsuitable | conflict</summary>
    public string Status { get; init; } = "unsuitable";

    public List<string> Errors { get; init; } = new();

    public static ScaffoldBuildResult Fail(string status, params string[] errors)
        => new() { Scaffold = null, Status = status, Errors = errors.ToList() };
}
