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
    public int Ions { get; set; }          // 每式单元完全电离产生的离子数（名义）
    public double HBonds { get; set; }     // 每式平均氢键数（如冰中 H2O = 2）
    public string StpState { get; set; } = "gas"; // gas / solid / liquid
    public List<string> HydrolyzableIons { get; set; } = new(); // 溶于水会水解的离子（使其实际浓度低于名义）
    public string Note { get; set; } = "";
}
