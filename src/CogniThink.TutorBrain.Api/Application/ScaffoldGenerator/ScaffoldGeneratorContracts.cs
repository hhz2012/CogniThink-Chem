namespace CogniThink.TutorBrain.Application.ScaffoldGenerator;

using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using CogniThink.TutorBrain.Application.Gateway;

// ============================================================
// 按题动态生成脚手架 —— 类型契约（Task 0）
// 镜像前端 miniapp/src/types/scaffold.ts，保证生成产物 JSON 可被前端直接反序列化。
// 化学引擎是唯一真值源；LLM 只写文案（releases），不产 correct。
// 本文件只定义类型，不实现逻辑（实现见 Task 1~5）。
// ============================================================

// ---------- 枚举（值与前端字面量对齐，JsonPropertyName 输出连字符小写） ----------

// 注意：JsonStringEnumConverter 在 net8.0 下实测不识别 [EnumMember] / [JsonPropertyName]，
// 故用自定义 EnumMemberJsonConverter<T> 读取成员标注输出前端字面量；每个成员同时标注两者——
// [EnumMember] 驱动序列化输出，[JsonPropertyName] 留作文档与意图声明。
[JsonConverter(typeof(EnumMemberJsonConverter<ScaffoldModuleKind>))]
public enum ScaffoldModuleKind
{
    [EnumMember(Value = "redox-reasoning")] [JsonPropertyName("redox-reasoning")] RedoxReasoning,
    [EnumMember(Value = "ionic-system")] [JsonPropertyName("ionic-system")] IonicSystem,
    [EnumMember(Value = "aqueous-equilibrium")] [JsonPropertyName("aqueous-equilibrium")] AqueousEquilibrium,
    [EnumMember(Value = "sequential-reaction")] [JsonPropertyName("sequential-reaction")] SequentialReaction,
    [EnumMember(Value = "electrochem-reasoning")] [JsonPropertyName("electrochem-reasoning")] ElectrochemReasoning,
}

[JsonConverter(typeof(EnumMemberJsonConverter<AtomicTaskKind>))]
public enum AtomicTaskKind
{
    [EnumMember(Value = "A")] [JsonPropertyName("A")] A,
    [EnumMember(Value = "B")] [JsonPropertyName("B")] B,
    [EnumMember(Value = "C")] [JsonPropertyName("C")] C,
    [EnumMember(Value = "D")] [JsonPropertyName("D")] D,
    [EnumMember(Value = "E")] [JsonPropertyName("E")] E,
    [EnumMember(Value = "F")] [JsonPropertyName("F")] F,
}

[JsonConverter(typeof(EnumMemberJsonConverter<AtomicTaskInteraction>))]
public enum AtomicTaskInteraction
{
    [EnumMember(Value = "multi-check")] [JsonPropertyName("multi-check")] MultiCheck,
    [EnumMember(Value = "single-select")] [JsonPropertyName("single-select")] SingleSelect,
    [EnumMember(Value = "order")] [JsonPropertyName("order")] Order,
}

// ---------- 题干输入（对齐 eval case 形状） ----------

/// <summary>
/// 生成器的题干输入。字段对齐 eval checkpoint 的 <c>case.{id,type,question,options,answer,...}</c>，
/// 超集无害（生成器按需读取）。
/// </summary>
public sealed class QuestionCase
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";        // choice / fill / calculation / judge ...
    [JsonPropertyName("question")] public string Question { get; set; } = "";
    [JsonPropertyName("options")] public List<string> Options { get; set; } = new();
    [JsonPropertyName("answer")] public string Answer { get; set; } = "";
    [JsonPropertyName("answer_keywords")] public List<string> AnswerKeywords { get; set; } = new();
    [JsonPropertyName("category")] public string? Category { get; set; }    // ChemCategory，可空
    [JsonPropertyName("note")] public string? Note { get; set; }
}

// ---------- 引擎产物容器（生成器消费的全部真值来源） ----------

/// <summary>
/// 生成器消费的引擎产物集合。
/// <see cref="Response"/> = gateway 的 EngineResponse（FinalResult / ReasoningNodes / ResultProvenance / ConstraintNote 等）；
/// <see cref="EngineOutputs"/> = 底层引擎的结构化中间结果（键约定：equation / redox / oxidation / aqueous / electrochem / coexistence，
/// 值为对应引擎的返回对象；Task 1 填入）。提取器按键取值并 cast。
/// 把底层结果与 gateway 响应解耦：Task 0 不依赖 Chem.Core 具体类型，编译安全；Task 1 决定直调底层引擎还是扩展 gateway 加性字段。
/// </summary>
public sealed class EngineArtifacts
{
    public QuestionCase Case { get; set; } = new();
    public EngineResponse? Response { get; set; }
    /// <summary>底层引擎结构化中间结果；键约定见类注释。未命中的键不存在。</summary>
    public Dictionary<string, object?> EngineOutputs { get; set; } = new();
}

// ---------- 镜像前端：候选项 / 真值 / 释放文案 ----------

public sealed class StepOption
{
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("label")] public string Label { get; set; } = "";
}

/// <summary>
/// 某步真值。<see cref="Correct"/> 运行时类型由 <see cref="Type"/> 决定：
/// multi-check / order → <c>List&lt;string&gt;</c>；single-select → <c>string</c>。
/// 与前端判别联合 <c>StepGroundTruth</c> 的 <c>correct</c> 字段兼容。
/// </summary>
public sealed class StepGroundTruth
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("options")] public List<StepOption> Options { get; set; } = new();
    [JsonPropertyName("correct")] public object? Correct { get; set; }
}

public sealed class DiscriminationQuestion
{
    [JsonPropertyName("stem")] public string Stem { get; set; } = "";
    [JsonPropertyName("options")] public List<string> Options { get; set; } = new();
}

public sealed class ContrastCard
{
    [JsonPropertyName("correct")] public ContrastSide Correct { get; set; } = new();
    [JsonPropertyName("wrong")] public ContrastSide Wrong { get; set; } = new();
}
public sealed class ContrastSide
{
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("detail")] public string Detail { get; set; } = "";
}

public sealed class RuleDrawer
{
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("body")] public string Body { get; set; } = "";
}

/// <summary>一步的阶梯释放内容（错 1/2/3 次递进释放）。四档必填，永不崩。</summary>
public sealed class StepScaffoldReleases
{
    [JsonPropertyName("hint")] public string Hint { get; set; } = "";
    [JsonPropertyName("probe")] public DiscriminationQuestion Probe { get; set; } = new();
    [JsonPropertyName("scaffold")] public ContrastCard Scaffold { get; set; } = new();
    [JsonPropertyName("reveal")] public RuleDrawer Reveal { get; set; } = new();
}

/// <summary>一步的练习配置：链上的原子任务 kind + 真值 + 释放文案。</summary>
public sealed class StepPractice
{
    [JsonPropertyName("kind")] public AtomicTaskKind Kind { get; set; }
    /// <summary>该步真值；降级步（只读揭示）为 null，前端不渲染作答控件。</summary>
    [JsonPropertyName("truth")] public StepGroundTruth? Truth { get; set; }
    [JsonPropertyName("releases")] public StepScaffoldReleases Releases { get; set; } = new();
}

/// <summary>题面条件（分支判定依据；可空）。</summary>
public sealed class BranchConditions
{
    // 未设置字段省略：前端 strictNullChecks 下 `medium?: string` 等不接受 null 字面量，
    // 只序列化有值的字段，输出稀疏对象对齐前端 undefined 语义（与 GeneratedScaffold.Conditions 同口径）。
    [JsonPropertyName("medium")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Medium { get; set; }
    [JsonPropertyName("quantity")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Quantity { get; set; }
    [JsonPropertyName("concentration")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Concentration { get; set; }
    [JsonPropertyName("temperature")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Temperature { get; set; }
    [JsonPropertyName("electrolyzed")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? Electrolyzed { get; set; }
    [JsonPropertyName("membrane")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Membrane { get; set; }
}

// ---------- 生成产物：GeneratedScaffold（ScaffoldPractice 超集） ----------

/// <summary>降级步记录：哪一步、为何降级为只读揭示。</summary>
public sealed class DegradedStep
{
    [JsonPropertyName("kind")] public AtomicTaskKind Kind { get; set; }
    [JsonPropertyName("reason")] public string Reason { get; set; } = "";
}

/// <summary>校验门结果摘要。</summary>
public sealed class ScaffoldValidationSummary
{
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("errors")] public List<string> Errors { get; set; } = new();

    /// <summary>
    /// 是否存在 F 步 correct 与存储 answer 的真实冲突（驱动 status=conflict 分类，替代错误文案子串匹配）。
    /// 不序列化：避免改变落盘产物字节、破坏前端契约与 Task7 字节幂等护栏。
    /// </summary>
    [JsonIgnore] public bool Conflict { get; set; }
}

/// <summary>
/// 生成产物：<c>ScaffoldPractice</c> 超集，带审计字段。
/// 与前端 <c>ScaffoldPractice</c> 同构（字段一一对应），额外增加可追溯元数据。
/// 前端反序列化时忽略多余字段即可；或前端定义 <c>GeneratedScaffold</c> 承接全部字段。
/// </summary>
public sealed class GeneratedScaffold
{
    // —— ScaffoldPractice 字段 ——
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("module")] public ScaffoldModuleKind Module { get; set; }
    // 可选字段为 null 时省略：前端 strictNullChecks 下 `conditions?: T` 不接受 null 字面量。
    [JsonPropertyName("conditions")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public BranchConditions? Conditions { get; set; }
    [JsonPropertyName("branchEntryId")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? BranchEntryId { get; set; }
    [JsonPropertyName("steps")] public List<StepPractice> Steps { get; set; } = new();

    // —— 超集审计字段 ——
    /// <summary>来源题目 id（eval case id）。</summary>
    [JsonPropertyName("sourceQuestionId")] public string SourceQuestionId { get; set; } = "";
    /// <summary>每步 correct 的来源引擎字段（如 "RedoxAnalysis.Changes"）；降级步不出现。</summary>
    [JsonPropertyName("provenance")] public Dictionary<string, string> Provenance { get; set; } = new();
    /// <summary>降级为只读揭示的步及原因。</summary>
    [JsonPropertyName("degradedSteps")] public List<DegradedStep> DegradedSteps { get; set; } = new();
    /// <summary>发布前校验门结果。</summary>
    [JsonPropertyName("validation")] public ScaffoldValidationSummary Validation { get; set; } = new();
}
