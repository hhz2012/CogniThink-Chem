namespace CogniThink.TutorBrain.Application.Gateway;

using System.Text.Json.Serialization;

// ============================================================
// 化学伴思 LLM 网关 —— 类型定义
// 化学引擎是唯一真值源，LLM 只做「输入标准化」+「引导讲解」
// ============================================================

/// <summary>任务类型</summary>
public enum TaskType
{
    Balance,          // 配平
    ReactionCheck,    // 反应校验
    Calculation,      // 定量计算
    StructureVerify,  // 结构式
    IonCheck,         // 离子共存
    Vsepr,            // 价层电子对
    Titration,        // 滴定
    Equilibrium,      // 平衡
    Concept,          // 纯概念讲解（不调引擎）
    Unknown           // 无法识别
}

/// <summary>引擎请求 JSON（LLM→引擎）</summary>
public sealed class EngineRequest
{
    [JsonPropertyName("task_type")]
    public string TaskType { get; set; } = "unknown";

    [JsonPropertyName("raw_text")]
    public string RawText { get; set; } = "";

    [JsonPropertyName("reaction")]
    public EngineReaction Reaction { get; set; } = new();

    [JsonPropertyName("smiles")]
    public List<string> Smiles { get; set; } = new();

    [JsonPropertyName("formula")]
    public string Formula { get; set; } = "";

    [JsonPropertyName("known")]
    public EngineKnown Known { get; set; } = new();

    [JsonPropertyName("student_answer")]
    public string StudentAnswer { get; set; } = "";

    [JsonPropertyName("options")]
    public EngineOptions Options { get; set; } = new();
}

public sealed class EngineReaction
{
    [JsonPropertyName("left")]
    public List<string> Left { get; set; } = new();

    [JsonPropertyName("right")]
    public List<string> Right { get; set; } = new();

    [JsonPropertyName("conditions")]
    public List<string> Conditions { get; set; } = new();
}

public sealed class EngineKnown
{
    [JsonPropertyName("n")]
    public double? N { get; set; }

    [JsonPropertyName("c")]
    public double? C { get; set; }

    [JsonPropertyName("V")]
    public double? V { get; set; }

    [JsonPropertyName("Ksp")]
    public double? Ksp { get; set; }

    [JsonPropertyName("Ka")]
    public double? Ka { get; set; }

    [JsonPropertyName("pH")]
    public double? PH { get; set; }
}

public sealed class EngineOptions
{
    [JsonPropertyName("strict_oxidation")]
    public bool StrictOxidation { get; set; } = true;

    [JsonPropertyName("check_atom_conservation")]
    public bool CheckAtomConservation { get; set; } = true;

    [JsonPropertyName("check_charge_conservation")]
    public bool CheckChargeConservation { get; set; } = true;

    [JsonPropertyName("check_reaction_feasibility")]
    public bool CheckReactionFeasibility { get; set; } = true;

    [JsonPropertyName("high_school_rule_set")]
    public bool HighSchoolRuleSet { get; set; } = true;
}

/// <summary>引擎返回 JSON（引擎→LLM）</summary>
public sealed class EngineResponse
{
    /// <summary>true=正确 false=错误 null=不在引擎建模范围</summary>
    [JsonPropertyName("ok")]
    public bool? Ok { get; set; }

    [JsonPropertyName("final_result")]
    public string FinalResult { get; set; } = "";

    [JsonPropertyName("is_student_answer_correct")]
    public bool IsStudentAnswerCorrect { get; set; }

    [JsonPropertyName("error_tags")]
    public List<string> ErrorTags { get; set; } = new();

    [JsonPropertyName("derivation_steps")]
    public List<string> DerivationSteps { get; set; } = new();

    [JsonPropertyName("constraint_note")]
    public string ConstraintNote { get; set; } = "";

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; } = 1.0;

    [JsonPropertyName("need_more_condition")]
    public bool NeedMoreCondition { get; set; }

    // —— 结构化图形资产引擎产出（graph/curve/molecule）；均为可选，默认 null，向后兼容 ——
    /// <summary>产出该结论的资产引擎类型：graph / curve / molecule（无资产分支为 null）</summary>
    [JsonPropertyName("asset_kind")]
    public string? AssetKind { get; set; }

    /// <summary>curve 主数值（ValueAtX→y；Extrema/Knee→x；Intersection→交点 x）；供结构化判分比对</summary>
    [JsonPropertyName("numeric_value")]
    public double? NumericValue { get; set; }

    /// <summary>curve 次数值（Extrema/Knee→y；Intersection→交点 y）</summary>
    [JsonPropertyName("numeric_value2")]
    public double? NumericValue2 { get; set; }

    // —— molecule 结构化特征（MoleculeAnalyzer 产出）；均可选，默认 null ——
    [JsonPropertyName("mol_formula")] public string? MoleculeFormula { get; set; }
    [JsonPropertyName("mol_unsaturation")] public int? MoleculeUnsaturation { get; set; }
    [JsonPropertyName("mol_is_polar")] public bool? MoleculeIsPolar { get; set; }
    [JsonPropertyName("mol_central_hybrid")] public string? MoleculeCentralHybridization { get; set; }
    [JsonPropertyName("mol_groups")] public string[]? MoleculeFunctionalGroups { get; set; }
}

/// <summary>网关处理结果</summary>
public sealed class GatewayResult
{
    [JsonPropertyName("task_type")]
    public string TaskType { get; set; } = "unknown";

    [JsonPropertyName("engine_request")]
    public EngineRequest EngineRequest { get; set; } = new();

    [JsonPropertyName("engine_response")]
    public EngineResponse EngineResponse { get; set; } = new();

    [JsonPropertyName("reply")]
    public string Reply { get; set; } = "";
}

/// <summary>错误码映射</summary>
public static class ErrorTags
{
    public static readonly Dictionary<string, string> Map = new()
    {
        ["E01"] = "原子不守恒",
        ["E02"] = "电荷不守恒",
        ["E03"] = "电子得失不等",
        ["E04"] = "反应不可发生",
        ["E05"] = "产物错误",
        ["E06"] = "介质矛盾",
        ["E07"] = "离子不共存",
        ["E08"] = "符号错",
        ["E09"] = "杂化错",
        ["E10"] = "VSEPR 错",
        ["E11"] = "计算错",
        ["E12"] = "过量少量忽略"
    };

    public static string GetText(string tag) =>
        Map.TryGetValue(tag, out var text) ? text : tag;
}
