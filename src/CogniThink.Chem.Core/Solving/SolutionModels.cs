using CogniThink.Chem.Core.Benchmark;
using CogniThink.Chem.Core.Inference;
using CogniThink.Chem.Core.Math;

namespace CogniThink.Chem.Core.Solving;

/// <summary>
/// 统一解答：引擎对一道题的端到端求解结果（"能解"的产物，"能讲/能引导"的原料）。
/// 答案按类别承载：
/// <list type="bullet">
/// <item>产物推断：<see cref="Equations"/>（知识库命中的方程式）。</item>
/// <item>成分推断：<see cref="Composition"/>（Must/MustNot/Maybe）+ <see cref="Equations"/>（证据方程式）。</item>
/// <item>过程推断：<see cref="SubstanceMap"/>（标签→物质）+ <see cref="Equations"/>（各步方程式，按步骤序）。</item>
/// <item>离子共存：<see cref="Coexistence"/> + <see cref="Equations"/>（不共存理由方程式）。</item>
/// <item>定量计算：<see cref="NumericValue"/>/<see cref="NumericUnit"/>。</item>
/// <item>电化学：<see cref="Electrodes"/>（电极反应式）+ <see cref="Equations"/>（总反应）。</item>
/// <item>化学平衡：<see cref="Equilibrium"/>（三段式表 + 转化率/Kp/移动方向）。</item>
/// <item>水溶液离子平衡：<see cref="Aqueous"/>（浓度顺序 + 守恒等式）。</item>
/// <item>工业流程：<see cref="SubstanceMap"/> + <see cref="Purposes"/>（步骤号→操作目的）。</item>
/// </list>
/// 铁律延续：<see cref="Status"/> 非 Solved 时答案字段不得被当作结论使用。
/// </summary>
public sealed class Solution
{
    public string ProblemId { get; set; } = "";

    public ProblemCategory Category { get; set; }

    /// <summary>求解状态（沿用推断三态语义：Solved/Underdetermined/NoSolution）。</summary>
    public InferenceStatus Status { get; set; }

    public bool IsSolved => Status == InferenceStatus.Solved;

    /// <summary>状态说明（欠定/无解时给出原因）。</summary>
    public string Message { get; set; } = "";

    /// <summary>方程式答案（含义随类别变化，见类注释）。</summary>
    public List<string> Equations { get; set; } = new();

    /// <summary>成分推断结论：候选物种 → Must/MustNot/Maybe。</summary>
    public Dictionary<string, CompositionVerdict> Composition { get; set; } = new();

    /// <summary>过程推断结论：字母标签 → 物质化学式。</summary>
    public Dictionary<string, string> SubstanceMap { get; set; } = new();

    /// <summary>离子共存结论（仅离子共存题）。</summary>
    public CoexistenceVerdict? Coexistence { get; set; }

    /// <summary>计算题数值答案（精确有理数）。</summary>
    public Rational? NumericValue { get; set; }

    public string NumericUnit { get; set; } = "";

    public string NumericSpecies { get; set; } = "";

    /// <summary>电化学结论：电极反应式（原电池填 Negative/Positive，电解池填 Anode/Cathode）。</summary>
    public ElectrodeEquationsSpec? Electrodes { get; set; }

    /// <summary>化学平衡结论：三段式表 + 转化率/分数/Kp/移动方向（数值为精确有理数字符串）。</summary>
    public EquilibriumAnswerSpec? Equilibrium { get; set; }

    /// <summary>水溶液离子平衡结论：粒子浓度顺序 + 守恒等式。</summary>
    public AqueousAnswerSpec? Aqueous { get; set; }

    /// <summary>工业流程结论：步骤号 → 该步操作目的（推理链同步给出原理与方程式）。</summary>
    public Dictionary<string, string> Purposes { get; set; } = new();

    /// <summary>求解步骤（每步可溯源到知识条目），讲解与引导由此渲染。</summary>
    public List<SolutionStep> Steps { get; set; } = new();
}

/// <summary>解答中的一步（由引擎 <see cref="InferenceStep"/> 规范化而来）。</summary>
public sealed class SolutionStep
{
    public int Order { get; set; }

    /// <summary>本步叙述（中文）。</summary>
    public string Description { get; set; } = "";

    /// <summary>证据：知识条目 id、方程式或检索手段。</summary>
    public string Evidence { get; set; } = "";

    /// <summary>归属知识点 id（可为空）。</summary>
    public string KnowledgeId { get; set; } = "";

    /// <summary>教材出处（可为空）。</summary>
    public string Source { get; set; } = "";
}
