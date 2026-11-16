using CogniThink.Chem.Core.Reactions;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.Chem.Core.Inference;

/// <summary>
/// 有机推断引擎：把字母标签 A/B/C… 回溯赋值到候选有机物，
/// 使每一步"反应物（起点物种 + 试剂）→ 产物含终点物种"都能在有机反应知识库
/// （RX-OR-* 条目）中找到支撑；现象约束要求起点或终点物种能解释题给现象。
/// 解计数：唯一解 → Solved；多解 → Underdetermined；无解 → NoSolution。
/// 铁律：每一步方程式都来自知识库条目，绝不编造。
/// 约定：
/// <list type="bullet">
/// <item>试剂可以是字母标签（如 ORG-H01 第 4 步的 B），与 from/to 标签一同参与赋值；
/// 单字母试剂若不是已知白名单物种则视为标签。</item>
/// <item>无试剂且带 △/点燃/高温 条件的步骤，若单反应物无匹配条目，
/// 允许补入空气中的 O2 再检索（有机物在空气中燃烧/加热的教材惯例）。</item>
/// <item>推理步骤的证据只写知识条目 id（RX-OR-*），绝不写方程式原文——
/// 引导生成器会把 RX- 前缀证据直接呈现给学生，方程式即答案泄露。</item>
/// </list>
/// </summary>
public static class OrganicInferenceEngine
{
    public static OrganicInferenceResult Infer(OrganicInferenceRequest request, ReactionLibrary? library = null)
    {
        library ??= ReactionLibrary.Default.Value;
        var result = new OrganicInferenceResult();
        var candidates = request.CandidateSpecies.Distinct(StringComparer.Ordinal).ToList();
        var steps = request.Steps;

        if (steps.Count == 0)
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = "未提供任何转化步骤，无法推断有机转化链。";
            return result;
        }

        // 候选预检：全部候选必须是可解析且课内白名单已知的物种（知识驱动铁律）
        foreach (var candidate in candidates)
        {
            bool known;
            try
            {
                known = WhitelistGuard.Check(Chemistry.Species.Parse(candidate)).Known;
            }
            catch (FormatException)
            {
                known = false;
            }

            if (!known)
            {
                result.Status = InferenceStatus.NoSolution;
                result.Message = $"候选物种 \"{candidate}\" 不在课内白名单或无法解析，超出知识库范围，引擎拒绝推断。";
                return result;
            }
        }

        // 公共核心完成标签收集/回溯/解计数输出（有机选项），结果字段同构转填
        var chain = ChainAssignmentCore.Infer(OrganicOptions, candidates, steps, library);
        result.Status = chain.Status;
        result.Assignment = chain.Assignment;
        result.StepResults = chain.StepResults;
        result.Steps = chain.Steps;
        result.Message = chain.Message;
        return result;
    }

    /// <summary>有机引擎选项：文案与行为逐字保留原实现（RX-OR-* 过滤、证据只放条目 id、通用现象约束）。</summary>
    private static ChainOptions OrganicOptions { get; } = new()
    {
        SourceName = "OrganicInferenceEngine",
        EntryIdPrefix = "RX-OR-",
        EvidenceUsesEquation = false,
        StepDescriptionIncludesEquation = false,
        FullPhenomenonConstraint = true,
        SelfTransformText = "自身转化",
        NoSteps = "未提供任何转化步骤，无法推断有机转化链。",
        NoLabels = "步骤中未出现字母标签，无法建立有机转化链赋值。",
        Intro = (labels, candidateCount) => $"有机转化链含 {labels.Count} 个字母标签（{string.Join("、", labels)}），候选有机物 {candidateCount} 种，在有机反应知识库中回溯赋值。",
        ExhaustedStep = "回溯穷举所有标签赋值，均至少有一步找不到有机知识库反应支撑。",
        NoSolution = "有机反应知识库支撑不了该转化链：没有任何候选赋值能使每一步都落到已知有机反应。",
        LostSupport = k => $"第 {k} 步在最终赋值下失去有机知识库支撑。",
        Underdetermined = n => $"存在至少 {n} 组赋值都能解释全部步骤与现象，条件不足，无法唯一确定各字母代表的有机物。",
        Solved = "唯一赋值满足全部步骤，每一步均有有机知识库反应支撑。",
    };
}

// ---------------------------------------------------------------------------
// 有机推断
// ---------------------------------------------------------------------------

/// <summary>有机推断请求：为有机转化链的每一步找到有机知识库反应支撑。</summary>
public sealed class OrganicInferenceRequest
{
    /// <summary>候选有机物（L2 写法）：字母标签 A/B/C… 的取值域。</summary>
    public List<string> CandidateSpecies { get; set; } = new();

    /// <summary>转化步骤序列。</summary>
    public List<ExperimentStep> Steps { get; set; } = new();

    /// <summary>介质："acidic"/"basic"/空（有机推断一般不限定）。</summary>
    public string Medium { get; set; } = "";
}

/// <summary>有机推断结果。</summary>
public sealed class OrganicInferenceResult
{
    public InferenceStatus Status { get; set; }

    /// <summary>字母标签 → 有机物 的赋值（Solved 时唯一；Underdetermined 时为首个可行解）。</summary>
    public Dictionary<string, string> Assignment { get; set; } = new();

    /// <summary>各步命中的反应与方程式。</summary>
    public List<ProcessStepResult> StepResults { get; set; } = new();

    public List<InferenceStep> Steps { get; set; } = new();

    public string Message { get; set; } = "";
}
