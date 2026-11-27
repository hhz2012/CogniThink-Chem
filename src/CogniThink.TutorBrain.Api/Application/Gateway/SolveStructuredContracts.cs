namespace CogniThink.TutorBrain.Application.Gateway;

using System.Text.Json.Serialization;

// ============================================================
// solve 投影富化：学生面「结构化辅助」段（加性、nullable）。
// 仅由 HTTP /solve 路径的 SolveStructuredEnricher 填充，不进判分路径，
// 不改 EngineResponse 的判决字段（Ok/FinalResult/ConclusionState/TaskType）。
// 与 StudentSolveView 同文件族，JSON 命名风格一致（小写下划线）。
// ============================================================

/// <summary>学生面结构化辅助数据（数值 / 水相 / 平衡 / 反应轨迹）。任意子段可为 null。</summary>
public sealed class StudentSolveStructured
{
    [JsonPropertyName("numeric")]
    public NumericSnapshot? Numeric { get; set; }

    [JsonPropertyName("aqueous")]
    public AqueousSnapshot? Aqueous { get; set; }

    [JsonPropertyName("equilibrium")]
    public EquilibriumSnapshot? Equilibrium { get; set; }

    [JsonPropertyName("reaction_trace")]
    public ReactionTraceSnapshot? ReactionTrace { get; set; }

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();

    /// <summary>所有子段均为空时返回 true（投影据此决定不下发空壳）。</summary>
    public bool IsEmpty() =>
        Numeric is null &&
        Aqueous is null &&
        Equilibrium is null &&
        (ReactionTrace is null || ReactionTrace.IsEmpty()) &&
        Warnings.Count == 0;
}

/// <summary>定量计算快照（由题给 n/c/V 确定性算术补算，不蒙数）。</summary>
public sealed class NumericSnapshot
{
    [JsonPropertyName("value")]
    public double? Value { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("species")]
    public string? Species { get; set; }

    [JsonPropertyName("formula")]
    public string? Formula { get; set; }
}

/// <summary>水相离子平衡快照（EquilibriumStateSimulator，仅 status=Determined 时填充）。</summary>
public sealed class AqueousSnapshot
{
    [JsonPropertyName("ordering")]
    public List<string> Ordering { get; set; } = new();

    [JsonPropertyName("conservation_equations")]
    public List<string> ConservationEquations { get; set; } = new();

    [JsonPropertyName("ph")]
    public double? Ph { get; set; }

    [JsonPropertyName("acidity_verdict")]
    public string? AcidityVerdict { get; set; }

    [JsonPropertyName("ion_concentrations")]
    public Dictionary<string, double> IonConcentrations { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>沉淀溶解平衡快照（Ksp 残留 / 沉淀转化 K）。</summary>
public sealed class EquilibriumSnapshot
{
    [JsonPropertyName("ksp_residual_concentration")]
    public double? KspResidualConcentration { get; set; }

    [JsonPropertyName("ksp_residual_ion")]
    public string? KspResidualIon { get; set; }

    [JsonPropertyName("conversion_constant_k")]
    public string? ConversionConstantK { get; set; }

    [JsonPropertyName("conversion_proceeds")]
    public bool? ConversionProceeds { get; set; }
}

/// <summary>正向反应轨迹快照（ChemicalSimulator.SimulationTrace 投影）。</summary>
public sealed class ReactionTraceSnapshot
{
    [JsonPropertyName("events")]
    public List<TraceEventDto> Events { get; set; } = new();

    [JsonPropertyName("final_species")]
    public List<SpeciesAmountDto> FinalSpecies { get; set; } = new();

    [JsonPropertyName("converged")]
    public bool Converged { get; set; }

    public bool IsEmpty() => Events.Count == 0 && FinalSpecies.Count == 0;
}

public sealed class TraceEventDto
{
    [JsonPropertyName("equation")]
    public string? Equation { get; set; }

    [JsonPropertyName("stage_index")]
    public int StageIndex { get; set; }

    [JsonPropertyName("extent")]
    public double Extent { get; set; }

    [JsonPropertyName("basis_knowledge_ids")]
    public List<string> BasisKnowledgeIds { get; set; } = new();

    [JsonPropertyName("priority_rank")]
    public int? PriorityRank { get; set; }
}

public sealed class SpeciesAmountDto
{
    [JsonPropertyName("substance")]
    public string? Substance { get; set; }

    [JsonPropertyName("moles")]
    public double? Moles { get; set; }
}
