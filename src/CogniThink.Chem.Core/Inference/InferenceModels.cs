using CogniThink.Chem.Core.Math;
using CogniThink.Chem.Core.Reactions;

namespace CogniThink.Chem.Core.Inference;

/// <summary>
/// 推断结论状态。知识驱动确定性推断的三态出口：
/// 要么落到可溯源知识条目（Solved），要么信息不足（Underdetermined），
/// 要么条件与知识矛盾（NoSolution）——绝不猜测。
/// </summary>
public enum InferenceStatus
{
    /// <summary>推断成功，结论可溯源到知识库条目。</summary>
    Solved,

    /// <summary>信息不足：现有知识无法确定唯一结论（并列候选保留，交由上层决策）。</summary>
    Underdetermined,

    /// <summary>无解：给定条件与知识库矛盾（如反应物组合在课内知识中不存在反应）。</summary>
    NoSolution,
}

/// <summary>
/// 单条推理步骤：记录"依据什么、推出什么"，是讲解（能讲）与引导（能指导思考）的原料。
/// </summary>
public sealed class InferenceStep
{
    /// <summary>本步推理的叙述（中文）。</summary>
    public string Description { get; set; } = "";

    /// <summary>证据内容：知识条目 id、方程式或现象描述。</summary>
    public string Evidence { get; set; } = "";

    /// <summary>归属知识点 id（可为空）。</summary>
    public string KnowledgeId { get; set; } = "";

    /// <summary>教材出处（可为空）。</summary>
    public string Source { get; set; } = "";
}

// ---------------------------------------------------------------------------
// 产物推断
// ---------------------------------------------------------------------------

/// <summary>产物推断请求：给定反应物与条件，求产物。</summary>
public sealed class ProductInferenceRequest
{
    /// <summary>反应物（L2 写法）。</summary>
    public List<string> Reactants { get; set; } = new();

    /// <summary>反应条件令牌，如 ["点燃"]、["△","浓"]。</summary>
    public List<string> Conditions { get; set; } = new();
}

/// <summary>
/// 产物推断结果。知识库可能命中多条（条件变体/并列可能），全部保留于
/// <see cref="Matches"/>，由上层结合条件特异性选择或并列呈现，引擎不猜。
/// </summary>
public sealed class ProductInferenceResult
{
    public InferenceStatus Status { get; set; }

    /// <summary>知识库命中的反应条目（已按条件过滤）。</summary>
    public List<ReactionEntry> Matches { get; set; } = new();

    public List<InferenceStep> Steps { get; set; } = new();

    /// <summary>状态说明（NoSolution/Underdetermined 时给出原因）。</summary>
    public string Message { get; set; } = "";
}

// ---------------------------------------------------------------------------
// 离子共存
// ---------------------------------------------------------------------------

/// <summary>共存判定结论。</summary>
public enum CoexistenceVerdict
{
    /// <summary>能大量共存。</summary>
    Coexist,

    /// <summary>不能大量共存（存在反应冲突）。</summary>
    NotCoexist,

    /// <summary>信息不足（如出现知识库未收录的离子）。</summary>
    Underdetermined,
}

/// <summary>离子共存请求。</summary>
public sealed class CoexistenceRequest
{
    /// <summary>题给离子（L2 写法）。</summary>
    public List<string> Species { get; set; } = new();

    /// <summary>介质："acidic"（酸性）、"basic"（碱性）或空（不限）。</summary>
    public string Medium { get; set; } = "";
}

/// <summary>一对（组）不能共存的离子及其反应依据。</summary>
public sealed class CoexistenceConflict
{
    /// <summary>参与冲突的离子（含介质引入的 H^+/OH^-）。</summary>
    public List<string> InvolvedIons { get; set; } = new();

    /// <summary>知识库中对应的反应条目。</summary>
    public ReactionEntry? Reaction { get; set; }

    /// <summary>冲突类型说明（生成沉淀/气体/弱电解质/发生氧化还原等）。</summary>
    public string Reason { get; set; } = "";
}

/// <summary>离子共存判定结果。</summary>
public sealed class CoexistenceResult
{
    public CoexistenceVerdict Verdict { get; set; }

    public List<CoexistenceConflict> Conflicts { get; set; } = new();

    public List<InferenceStep> Steps { get; set; } = new();

    public string Message { get; set; } = "";
}

// ---------------------------------------------------------------------------
// 成分推断（物质鉴别/组成判断）
// ---------------------------------------------------------------------------

/// <summary>组成判定：某候选物种"一定存在/一定不存在/可能存在"。</summary>
public enum CompositionVerdict
{
    /// <summary>一定存在。</summary>
    Must,

    /// <summary>一定不存在。</summary>
    MustNot,

    /// <summary>可能存在（现有证据既不能证实也不能排除）。</summary>
    Maybe,

    /// <summary>未知（证据未涉及）。</summary>
    Unknown,
}

/// <summary>单个实验步骤（与基准题 given.steps 结构对应）。</summary>
public sealed class ExperimentStep
{
    public int Step { get; set; }

    /// <summary>处理对象描述，如 "溶液"、"白色沉淀"。</summary>
    public string From { get; set; } = "";

    /// <summary>处理后结果描述，如 "气体"、"白色沉淀"。</summary>
    public string To { get; set; } = "";

    /// <summary>加入的试剂（L2 写法）。</summary>
    public List<string> Reagents { get; set; } = new();

    /// <summary>操作条件令牌，如 ["△"]。</summary>
    public List<string> Conditions { get; set; } = new();

    /// <summary>现象描述文本。</summary>
    public string Phenomenon { get; set; } = "";
}

/// <summary>成分推断请求。</summary>
public sealed class IdentificationRequest
{
    /// <summary>候选物种（L2 写法）。</summary>
    public List<string> CandidateSpecies { get; set; } = new();

    /// <summary>实验步骤序列。</summary>
    public List<ExperimentStep> Steps { get; set; } = new();

    /// <summary>介质："acidic"/"basic"/空。</summary>
    public string Medium { get; set; } = "";

    /// <summary>
    /// 单一实体模式：待测对象是候选中的"某一种"（如"气体 A 可能是……"），
    /// 能解释全部证据的唯一候选判 Must，其余判 MustNot。
    /// 混合成分模式（可能含多种）为 false。
    /// </summary>
    public bool SingleEntity { get; set; }
}

/// <summary>成分推断结果。</summary>
public sealed class IdentificationResult
{
    public InferenceStatus Status { get; set; }

    /// <summary>每个候选物种的组成判定。</summary>
    public Dictionary<string, CompositionVerdict> Composition { get; set; } = new();

    /// <summary>支撑结论的反应条目（按推理顺序）。</summary>
    public List<ReactionEntry> Equations { get; set; } = new();

    public List<InferenceStep> Steps { get; set; } = new();

    public string Message { get; set; } = "";
}

// ---------------------------------------------------------------------------
// 过程推断
// ---------------------------------------------------------------------------

/// <summary>过程推断请求：为实验流程的每一步找到对应反应。</summary>
public sealed class ProcessInferenceRequest
{
    /// <summary>候选物种（L2 写法）：字母标签 A/B/C… 的取值域。</summary>
    public List<string> CandidateSpecies { get; set; } = new();

    public List<ExperimentStep> Steps { get; set; } = new();

    /// <summary>介质："acidic"/"basic"/空。</summary>
    public string Medium { get; set; } = "";
}

/// <summary>单个步骤的推断结果。</summary>
public sealed class ProcessStepResult
{
    public int Step { get; set; }

    /// <summary>命中的反应条目（未命中为 null）。</summary>
    public ReactionEntry? Reaction { get; set; }

    /// <summary>输出的方程式（优先离子方程式，其次分子方程式）。</summary>
    public string Equation { get; set; } = "";

    /// <summary>本步说明。</summary>
    public string Explanation { get; set; } = "";
}

/// <summary>过程推断结果。</summary>
public sealed class ProcessInferenceResult
{
    public InferenceStatus Status { get; set; }

    /// <summary>字母标签 → 物种 的赋值（Solved 时唯一；Underdetermined 时为首个可行解）。</summary>
    public Dictionary<string, string> Assignment { get; set; } = new();

    public List<ProcessStepResult> StepResults { get; set; } = new();

    public List<InferenceStep> Steps { get; set; } = new();

    public string Message { get; set; } = "";
}

// ---------------------------------------------------------------------------
// 化学计量计算
// ---------------------------------------------------------------------------

/// <summary>化学计量请求：基于配平方程式，由已知量求目标量（物质的量）。</summary>
public sealed class StoichiometryRequest
{
    /// <summary>化学方程式（未配平亦可，引擎先配平）。</summary>
    public string Equation { get; set; } = "";

    /// <summary>已知量的物种。</summary>
    public string KnownSpecies { get; set; } = "";

    /// <summary>已知物种的物质的量（mol）。</summary>
    public Rational KnownMoles { get; set; }

    /// <summary>要求的目标物种。</summary>
    public string TargetSpecies { get; set; } = "";
}

/// <summary>化学计量结果。</summary>
public sealed class StoichiometryResult
{
    public InferenceStatus Status { get; set; }

    /// <summary>目标物种的物质的量（mol）。</summary>
    public Rational TargetMoles { get; set; }

    /// <summary>配平后的方程式。</summary>
    public string BalancedEquation { get; set; } = "";

    public List<InferenceStep> Steps { get; set; } = new();

    public string Message { get; set; } = "";
}
