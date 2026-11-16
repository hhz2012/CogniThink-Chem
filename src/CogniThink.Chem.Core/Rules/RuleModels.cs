namespace CogniThink.Chem.Core.Rules;

/// <summary>
/// 规则层级（对应规划 3.1）。数值越大表示越"具体"：
/// 例外(4) &gt; 条件性(3) &gt; 结构性(2) &gt; 原理性(1)；守恒(0) 是硬约束，不参与优先级比较。
/// </summary>
public enum RuleLayer
{
    /// <summary>L0 守恒与定义，作为硬约束，违反即淘汰。</summary>
    Conservation = 0,

    /// <summary>L1 原理与定律（勒夏特列、Ksp 判据、盖斯定律等）。</summary>
    Principle = 1,

    /// <summary>L2 结构性规律（周期律、活动性顺序、越弱越水解等）。</summary>
    Structural = 2,

    /// <summary>L3 条件性经验（浓稀差异、钝化、介质影响）。</summary>
    Conditional = 3,

    /// <summary>L4 特例与例外（覆盖一般规则，必须有明确触发条件）。</summary>
    Exception = 4
}

/// <summary>证据类型，数值见 <see cref="EvidenceTypes.Strength"/>。</summary>
public enum EvidenceType
{
    Conservation = 0,
    Quantitative = 1,
    Qualitative = 2,
    Memory = 3
}

/// <summary>证据强度换算与展示。</summary>
public static class EvidenceTypes
{
    /// <summary>强度分：守恒(4) &gt; 定量(3) &gt; 定性(2) &gt; 记忆(1)。</summary>
    public static int Strength(EvidenceType type) => type switch
    {
        EvidenceType.Conservation => 4,
        EvidenceType.Quantitative => 3,
        EvidenceType.Qualitative => 2,
        _ => 1
    };

    public static string Display(EvidenceType type) => type switch
    {
        EvidenceType.Conservation => "守恒",
        EvidenceType.Quantitative => "定量",
        EvidenceType.Qualitative => "定性",
        _ => "记忆"
    };
}

/// <summary>学段范围：必修 &gt; 选择性必修 &gt; 延伸（延伸只作解释）。</summary>
public enum GradeScope
{
    Required = 0,
    Elective = 1,
    Extended = 2
}

/// <summary>结构化命题。谓词与主客体都用受控词表，避免自然语言带来的歧义。</summary>
public sealed class Proposition
{
    public string Predicate { get; set; } = "";

    public string Subject { get; set; } = "";

    public string? Object { get; set; }

    /// <summary>人类可读表述，仅用于展示，不参与判定。</summary>
    public string? Display { get; set; }

    public override string ToString() =>
        string.IsNullOrEmpty(Object) ? $"{Predicate}({Subject})" : $"{Predicate}({Subject}, {Object})";
}

/// <summary>规则前提：对事实上下文的一组约束。</summary>
public sealed class RulePrecondition
{
    /// <summary>事实键，如 <c>acid_family</c>、<c>concentration</c>。</summary>
    public string Key { get; set; } = "";

    /// <summary>可接受取值（任一命中即满足）。</summary>
    public List<string> Values { get; set; } = new();

    /// <summary>为真时要求"不存在"这些取值。</summary>
    public bool Negate { get; set; }

    /// <summary>非空表示该前提是默认假设（如"常温""稀溶液"）；满足但应降权并在解释中声明。</summary>
    public string? DefaultAssumption { get; set; }
}

/// <summary>一条化学规则（声明式数据，可在 JSON 中维护）。</summary>
public sealed class Rule
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public RuleLayer Layer { get; set; }

    /// <summary>归属知识点，必须存在于覆盖清单。</summary>
    public string KnowledgeId { get; set; } = "";

    public List<RulePrecondition> Preconditions { get; set; } = new();

    public Proposition Conclusion { get; set; } = new();

    /// <summary>本规则覆盖（取代）的一般规则 id；用于例外覆盖。</summary>
    public List<string> Overrides { get; set; } = new();

    /// <summary>本规则已知的例外规则 id；用于一致性检查。</summary>
    public List<string> Exceptions { get; set; } = new();

    public GradeScope GradeScope { get; set; } = GradeScope.Required;

    public EvidenceType Evidence { get; set; } = EvidenceType.Qualitative;

    /// <summary>教材/课标出处，可追溯。</summary>
    public string Source { get; set; } = "";

    /// <summary>互斥结论分组，用于冲突扫描。</summary>
    public string ConflictGroup { get; set; } = "";

    /// <summary>与教材表述冲突，待评审；不参与裁决。</summary>
    public bool PendingReview { get; set; }

    /// <summary>显式回归用例 id；缺失时由回归器生成激活冒烟用例。</summary>
    public List<string> Tests { get; set; } = new();

    public override string ToString() => $"{Id} {Name}";
}
