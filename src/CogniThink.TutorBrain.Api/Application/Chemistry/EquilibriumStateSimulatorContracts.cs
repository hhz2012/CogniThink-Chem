namespace CogniThink.TutorBrain.Application.Chemistry;

// 平衡态数值模拟器契约（§7.2）。SpeciesInput.Concentration/Volume 已在 ChemicalSimulatorContracts 定义。

/// <summary>平衡态数值查询项（可组合，多项 AND）</summary>
public enum EquilibriumQuantity
{
    /// <summary>体系 pH 数值</summary>
    Ph,
    /// <summary>主要物种的平衡浓度数值（mol/L）</summary>
    IonConcentrations,
    /// <summary>Ksp 残留离子浓度</summary>
    KspResidual,
    /// <summary>沉淀转化平衡常数 K = Ksp1/Ksp2</summary>
    PrecipitationConversion,
    /// <summary>粒子浓度排序（委托 Core）</summary>
    ConcentrationOrdering,
    /// <summary>三大守恒式（委托 Core）</summary>
    Conservations,
    /// <summary>酸碱性定性（委托 Core）</summary>
    AcidityQualitative
}

/// <summary>平衡态数值查询：对给定体系求什么</summary>
public sealed class EquilibriumQuery
{
    public List<EquilibriumQuantity> Quantities { get; set; } = new();

    /// <summary>题给已知数（如 pH、指定离子浓度、题给 Ksp 覆盖值）；当 acid-constants.json 缺常数时优先用题给</summary>
    public Dictionary<string, double> Given { get; set; } = new(StringComparer.Ordinal);

    /// <summary>沉淀转化时指定原沉淀 / 加入离子 / 目标沉淀（映射到 AqueousRequest.Conversion）</summary>
    public ConversionSpec? Conversion { get; set; }

    /// <summary>要求排序的粒子清单（映射到 AqueousRequest.OrderSpecies）</summary>
    public List<string> OrderSpecies { get; set; } = new();
}

/// <summary>平衡数值端点请求体（HTTP / 测试入口）：体系 + 查询项</summary>
public sealed class EquilibriumRequest
{
    public ChemicalSystemInput Input { get; set; } = new();
    public EquilibriumQuery Query { get; set; } = new();
}

public sealed class ConversionSpec
{
    /// <summary>原沉淀化学式，如 "AgCl"</summary>
    public string Original { get; set; } = "";
    /// <summary>加入的离子，如 "I^-"</summary>
    public string AddedIon { get; set; } = "";
    /// <summary>目标沉淀，如 "AgI"</summary>
    public string Target { get; set; } = "";
}

public enum EquilibriumDetermination
{
    Determined,
    Underdetermined
}

/// <summary>平衡态数值结果</summary>
public sealed class EquilibriumNumericalResult
{
    public EquilibriumDetermination Status { get; set; }
    public double? Ph { get; set; }

    /// <summary>物种 / 离子 → 平衡浓度（mol/L）；只填能确定的项</summary>
    public Dictionary<string, double> IonConcentrations { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Ksp 残留浓度（mol/L）与对应离子</summary>
    public double? KspResidualConcentration { get; set; }
    public string? KspResidualIon { get; set; }

    /// <summary>沉淀转化平衡常数 K = Ksp1/Ksp2（精确有理文本，来自 ConstantSpec.Ratio）；无法确定时为空</summary>
    public string? ConversionConstantK { get; set; }
    public bool? ConversionProceeds { get; set; }

    /// <summary>定性结果（委托 Core AqueousEquilibriumEngine）</summary>
    public List<string> ConcentrationOrdering { get; set; } = new();
    public List<string> ConservationEquations { get; set; } = new();
    public string? AcidityVerdict { get; set; }

    /// <summary>推导步骤（人类可读），每步标注所用常数与来源</summary>
    public List<string> DerivationSteps { get; set; } = new();
    /// <summary>知识锚点：常数 Key（如 "CH3COOH"）/ 规则 Id（如 "SOL-006"），用于溯源</summary>
    public List<string> EvidenceKeys { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
