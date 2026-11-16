using CogniThink.Chem.Core.Reactions;

namespace CogniThink.Chem.Core.Inference;

/// <summary>
/// 过程推断引擎（转化链）：把字母标签 A/B/C… 回溯赋值到候选物种，
/// 使每一步"反应物（起点物种 + 试剂）→ 产物含终点物种"都能在知识库中找到条目支撑。
/// 解计数：唯一解 → Solved；多解 → Underdetermined；无解 → NoSolution。
/// 铁律：每一步方程式都来自知识库条目，绝不编造。
/// 约定：
/// <list type="bullet">
/// <item>试剂可以是字母标签（如 PROC-008 的 B），与 from/to 标签一同参与赋值；
/// 单字母试剂若不是已知白名单物种则视为标签。</item>
/// <item>无试剂且带 △/点燃/高温 条件的步骤，若单反应物无分解条目，
/// 允许补入空气中的 O2 再检索（金属在空气中加热/燃烧的教材惯例）。</item>
/// </list>
/// </summary>
public static class ProcessInferenceEngine
{
    public static ProcessInferenceResult Infer(ProcessInferenceRequest request, ReactionLibrary? library = null)
    {
        library ??= ReactionLibrary.Default.Value;
        var candidates = request.CandidateSpecies.Distinct(StringComparer.Ordinal).ToList();
        return ChainAssignmentCore.Infer(ProcessOptions, candidates, request.Steps, library);
    }

    /// <summary>过程引擎选项：文案与行为逐字保留原实现（全库匹配、证据取方程式、仅"变为"对称约束）。</summary>
    private static ChainOptions ProcessOptions { get; } = new()
    {
        SourceName = "ProcessInferenceEngine",
        EntryIdPrefix = null,
        EvidenceUsesEquation = true,
        StepDescriptionIncludesEquation = true,
        FullPhenomenonConstraint = false,
        SelfTransformText = "自身分解",
        NoSteps = "未提供任何实验步骤，无法推断。",
        NoLabels = "步骤中未出现字母标签，无法建立转化链赋值。",
        Intro = (labels, candidateCount) => $"转化链含 {labels.Count} 个字母标签（{string.Join("、", labels)}），候选物种 {candidateCount} 种，开始约束回溯赋值。",
        ExhaustedStep = "回溯穷举所有标签赋值，均至少有一步找不到知识库反应支撑。",
        NoSolution = "知识库支撑不了该转化链：没有任何候选赋值能使每一步都落到已知反应。",
        LostSupport = k => $"第 {k} 步在最终赋值下失去知识库支撑。",
        Underdetermined = n => $"存在至少 {n} 组赋值都能解释全部步骤，条件不足，无法唯一确定各字母代表的物质。",
        Solved = "唯一赋值满足全部步骤，每一步均有知识库反应支撑。",
    };
}
