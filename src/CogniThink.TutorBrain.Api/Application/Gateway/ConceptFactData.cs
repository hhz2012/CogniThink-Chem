using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 概念事实库数据模型（阶段6-B / AC-24）：data/highschool/concept-facts.json。
/// 化学事实全部来自 JSON，本文件只定义结构，不含任何硬编码化学事实。
/// 通过 <c>RuleStore.Load().Query&lt;ConceptFactData&gt;("concept-facts")</c> 加载
/// （<see cref="CogniThink.Chem.Core.Rules.RuleStore"/> 内部缓存）。
/// </summary>
public sealed class ConceptFactData
{
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("source")] public string Source { get; set; } = "";
    [JsonPropertyName("entries")] public List<ConceptFactEntry> Entries { get; set; } = new();
}

/// <summary>单条概念事实：触发条件（归一化子串匹配）+ 答案 + 推导。</summary>
public sealed class ConceptFactEntry
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("kind")] public string Kind { get; set; } = "";

    /// <summary>归一化题干必须全部包含的子串（已小写、去 ^、上下标转数字）。</summary>
    [JsonPropertyName("required")] public List<string> RequiredAll { get; set; } = new();

    /// <summary>归一化题干至少包含其一的子串；为空表示无额外要求。</summary>
    [JsonPropertyName("requiredAny")] public List<string> RequiredAny { get; set; } = new();

    [JsonPropertyName("answer")] public string Answer { get; set; } = "";
    [JsonPropertyName("derivation")] public List<string> Derivation { get; set; } = new();
    [JsonPropertyName("constraint")] public string Constraint { get; set; } = "";
}
