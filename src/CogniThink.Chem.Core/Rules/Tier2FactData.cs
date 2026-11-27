using System.Collections.Generic;

namespace CogniThink.Chem.Core.Rules;

// Tier 2 事实/规则库数据模型。化学事实全部来自 data/highschool/*.json，
// 本文件只定义结构，不含任何硬编码化学事实。
// 通过 RuleStore.Load().Query<T>(key) 加载（ChemDataLoader：camelCase / 大小写不敏感 / 允许尾逗号与注释）。

/// <summary>物质分类事实库：data/highschool/substance-classification.json</summary>
public sealed class ClassificationData
{
    public string Version { get; set; } = "";
    public string Source { get; set; } = "";
    public List<ClassifiedSubstance> Substances { get; set; } = new();
    public Dictionary<string, List<string>> AllotropeGroups { get; set; } = new(System.StringComparer.Ordinal);
    public Dictionary<string, List<string>> IsotopeGroups { get; set; } = new(System.StringComparer.Ordinal);
}

public sealed class ClassifiedSubstance
{
    public string Formula { get; set; } = "";
    public List<string> Names { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

/// <summary>物质转化链规则库：data/highschool/transformation-rules.json</summary>
public sealed class TransformationRuleData
{
    public string Version { get; set; } = "";
    public string Source { get; set; } = "";
    public List<TransformationStep> Steps { get; set; } = new();
}

public sealed class TransformationStep
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public List<string> Reagents { get; set; } = new();   // 必需试剂（归一化后），可空
    public List<string> Conditions { get; set; } = new(); // 必需条件（归一化后），可空
    public bool Feasible { get; set; } = true;
    public string Reason { get; set; } = "";
}

/// <summary>NA 粒子事实库：data/highschool/na-particle-facts.json</summary>
public sealed class NaParticleFactData
{
    public string Version { get; set; } = "";
    public string Source { get; set; } = "";
    public List<NaParticleFact> Formulas { get; set; } = new();
}

public sealed class NaParticleFact
{
    public string Formula { get; set; } = "";
    public List<string> Names { get; set; } = new();
    public double MolarMass { get; set; }
    public int Electrons { get; set; }
    public int Protons { get; set; }
    public int Neutrons { get; set; }
    public int Atoms { get; set; }
    public int SigmaBonds { get; set; }
    public int PiBonds { get; set; }
    public int CovalentBonds { get; set; }
    public bool Molecular { get; set; }    // 是否为分子晶体/分子（σ/π/共价键查询仅对分子有意义）
    public int Ions { get; set; }          // 每式单元完全电离产生的离子数（名义）
    public double HBonds { get; set; }     // 每式平均氢键数（如冰中 H2O = 2）
    public string StpState { get; set; } = "gas"; // gas / solid / liquid
    public List<string> HydrolyzableIons { get; set; } = new(); // 溶于水会水解的离子（使其实际浓度低于名义）
    public string Note { get; set; } = "";
}

/// <summary>
/// 计数事实库：data/highschool/counting-facts.json。
/// 收录无法用构造异构枚举器计算的计数（笼状、苯环、萘等固定结构的取代物数/等效氢数）。
/// 引擎枚举能算的（烷烃/烷基）不进此表，保持"计算是真值源"。
/// 每条经化学核对；匹配走精确子串 + 数目比对，歧义则不判（零假阳性）。
/// </summary>
public sealed class CountingFactData
{
    public string Version { get; set; } = "";
    public string Source { get; set; } = "";
    public List<CountingFact> Facts { get; set; } = new();
}

public sealed class CountingFact
{
    /// <summary>条目 id，如 "CUBANE-2CL"。</summary>
    public string Id { get; set; } = "";
    /// <summary>选项文本必须同时包含这里的全部子串才命中（如 ["立方烷","六氯"]）。</summary>
    public List<string> MatchAllOf { get; set; } = new();
    /// <summary>选项文本若含这里任一子串则不命中（如苯的事实排除"甲苯"，避免误中）。</summary>
    public List<string> Exclude { get; set; } = new();
    /// <summary>正确数目。</summary>
    public int Count { get; set; }
    /// <summary>核对依据（供讲解/留痕）。</summary>
    public string Note { get; set; } = "";
}

/// <summary>
/// 结构/周期律/化学键比较通用真值库：data/highschool/structure-property-facts.json。
/// 供选择项陈述真伪判定（CompositeStatementJudge 追加原语）使用。
/// 每条均为教材通用化学真值，可跨题复用，非单题策展。
/// </summary>
public sealed class StructurePropertyData
{
    public string Version { get; set; } = "";
    public string Source { get; set; } = "";
    /// <summary>常见化合物 → 晶体类型。</summary>
    public List<CrystalTypeEntry> CrystalTypes { get; set; } = new();
    /// <summary>含（或不含）配位键的常见物种。</summary>
    public List<CoordinateBondEntry> CoordinateBonds { get; set; } = new();
}

public sealed class CrystalTypeEntry
{
    /// <summary>归一化化学式（ASCII，无上下标），如 "GaN"、"SiO2"。</summary>
    public string Formula { get; set; } = "";
    /// <summary>中文别名，如 "氮化镓"、"二氧化硅"。</summary>
    public List<string> Names { get; set; } = new();
    /// <summary>真值晶体类型：原子晶体 / 分子晶体 / 离子晶体 / 金属晶体。</summary>
    public string Type { get; set; } = "";
}

public sealed class CoordinateBondEntry
{
    /// <summary>归一化物种展示键，如 "NH3BH3"。</summary>
    public string Species { get; set; } = "";
    /// <summary>在题面中检索的归一化子串（去括号/电荷），如 "Cu(NH3)4"。</summary>
    public string Match { get; set; } = "";
    /// <summary>中文别名。</summary>
    public List<string> Names { get; set; } = new();
    /// <summary>该物种是否含配位键。</summary>
    public bool HasCoordinateBond { get; set; }
    /// <summary>配位键给体原子（可选，供留痕）。</summary>
    public string Donor { get; set; } = "";
    /// <summary>配位键受体原子（可选，供留痕）。</summary>
    public string Acceptor { get; set; } = "";
}
