namespace CogniThink.TutorBrain.Domain.Chemistry;

/// <summary>物质大类</summary>
public enum SubstanceCategory
{
    SimpleSubstance,
    Oxide,
    Acid,
    Base,
    Salt,
    Organic,
    Indicator
}

public enum PhysicalState
{
    Solid,
    Liquid,
    Gas,
    Aqueous
}

/// <summary>水溶性等级</summary>
public enum SolubilityLevel
{
    Soluble,
    SlightlySoluble,
    Insoluble,
    Decomposes,
    Reacts
}

/// <summary>反应类型</summary>
public enum ReactionType
{
    Combination,
    Decomposition,
    Displacement,
    Metathesis,
    Redox,
    Neutralization,
    Precipitation,
    GasEvolution,
    Complexation,
    Hydrolysis,
    Esterification,
    Addition,
    Substitution,
    Oxidation
}

/// <summary>离子相互作用类型（用于共存判断）</summary>
public enum InteractionType
{
    Precipitation,
    GasEvolution,
    WeakElectrolyte,
    Redox,
    Complexation,
    NoReaction
}

/// <summary>题目编译后的内部查询类型</summary>
public enum ChemicalQueryType
{
    Coexistence,
    Identification,
    Inference,
    Equation,
    Color,
    Solubility,
    Unknown
}

/// <summary>物质（知识库核心实体）</summary>
public sealed class Substance
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Formula { get; set; } = string.Empty;
    public SubstanceCategory Category { get; set; }
    public PhysicalState State { get; set; }

    /// <summary>固体/纯物质颜色</summary>
    public string? Color { get; set; }

    /// <summary>水溶液颜色</summary>
    public string? SolutionColor { get; set; }

    public SolubilityLevel Solubility { get; set; } = SolubilityLevel.Soluble;

    /// <summary>化学特性标签：氧化性、还原性、酸性、碱性、不稳定性等</summary>
    public List<string> Properties { get; set; } = new();

    /// <summary>别名（用于题目文本匹配）</summary>
    public List<string> Aliases { get; set; } = new();

    /// <summary>备注（俗名、用途、特殊现象）</summary>
    public string? Note { get; set; }

    /// <summary>直接关联的知识点 Id；反应涉及的物质还会通过 Reaction.KnowledgeIds 间接关联</summary>
    public List<string> KnowledgeIds { get; set; } = new();
}

/// <summary>知识点—物质多对多绑定</summary>
public sealed class KnowledgeSubstanceLink
{
    public string KnowledgeId { get; set; } = string.Empty;
    public List<string> SubstanceIds { get; set; } = new();
}

/// <summary>离子</summary>
public sealed class ChemicalIon
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Formula { get; set; } = string.Empty;
    public int Charge { get; set; }
    public string? SolutionColor { get; set; }
    public List<string> Properties { get; set; } = new();
    public List<string> Aliases { get; set; } = new();
}

/// <summary>化学反应</summary>
public sealed class Reaction
{
    public string Id { get; set; } = string.Empty;

    /// <summary>反应物（物质 Id）</summary>
    public List<string> Reactants { get; set; } = new();

    /// <summary>产物（物质 Id）</summary>
    public List<string> Products { get; set; } = new();

    public string Equation { get; set; } = string.Empty;
    public ReactionType Type { get; set; }

    /// <summary>反应条件：溶液、加热、点燃、催化剂、浓、稀、酸性、碱性等</summary>
    public List<string> Conditions { get; set; } = new();

    /// <summary>实验现象</summary>
    public List<string> Phenomena { get; set; } = new();

    public bool Reversible { get; set; }

    /// <summary>关联知识点 Id（对接知识点图谱）</summary>
    public List<string> KnowledgeIds { get; set; } = new();
}

/// <summary>离子对相互作用（共存判断规则）</summary>
public sealed class IonInteraction
{
    public string Id { get; set; } = string.Empty;
    public List<string> Ions { get; set; } = new();
    public InteractionType Type { get; set; }

    /// <summary>结果描述，如"生成白色沉淀 BaSO4"</summary>
    public string Result { get; set; } = string.Empty;

    /// <summary>限定条件：酸性、碱性、加热等；为空表示任意条件</summary>
    public string? Condition { get; set; }

    public List<string> KnowledgeIds { get; set; } = new();
}

/// <summary>溶解性表：阴离子规则 + 阳离子例外</summary>
public sealed class SolubilityTable
{
    public List<SolubilityRule> Rules { get; set; } = new();
    public List<SolubilityException> Exceptions { get; set; } = new();
}

public sealed class SolubilityRule
{
    /// <summary>阴离子，如 Cl-、SO4^2-</summary>
    public string Anion { get; set; } = string.Empty;
    public SolubilityLevel Level { get; set; }
    /// <summary>该阴离子下的例外阳离子</summary>
    public List<string> Cations { get; set; } = new();
}

public sealed class SolubilityException
{
    public string Cation { get; set; } = string.Empty;
    public string Anion { get; set; } = string.Empty;
    public SolubilityLevel Level { get; set; }
}

/// <summary>题目编译结果：题干 → 内部查询请求</summary>
public sealed class ChemicalQuery
{
    public ChemicalQueryType Type { get; set; }
    public List<string> Substances { get; set; } = new();
    public List<string> Ions { get; set; } = new();
    public bool Acidic { get; set; }
    public bool Alkaline { get; set; }
    public bool Heated { get; set; }
    public bool Excess { get; set; }
    public string RawStem { get; set; } = string.Empty;
}

/// <summary>共存冲突证据</summary>
public sealed class CoexistenceConflict
{
    public List<string> Ions { get; set; } = new();
    public InteractionType Type { get; set; }
    public string Result { get; set; } = string.Empty;
    public List<string> KnowledgeIds { get; set; } = new();
}
