namespace CogniThink.Chem.Core.Knowledge;

/// <summary>高中化学五大知识域。</summary>
public enum KnowledgeModule
{
    /// <summary>物质与无机（元素化合物、离子反应、工艺流程）。</summary>
    Inorganic = 0,

    /// <summary>有机化学（烃、衍生物、合成）。</summary>
    Organic = 1,

    /// <summary>化学反应原理（热化学、速率、平衡、电化学、溶液平衡）。</summary>
    ReactionPrinciples = 2,

    /// <summary>化学实验（操作、制备、检验、定量、探究）。</summary>
    Experiment = 3,

    /// <summary>物质结构与性质。</summary>
    Structure = 4
}

/// <summary>知识层级：主干可作判定依据，延伸只作解释。</summary>
public enum KnowledgeTier
{
    Main = 0,
    Extended = 1
}

/// <summary>覆盖清单中的一个知识点。</summary>
public sealed class KnowledgeItem
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public KnowledgeModule Module { get; set; }

    public KnowledgeTier Tier { get; set; }

    /// <summary>教材出处（章节/条目），用于可追溯。</summary>
    public string TextbookRef { get; set; } = "";

    public List<string> Prerequisites { get; set; } = new();

    public List<string> Rules { get; set; } = new();

    public List<string> Questions { get; set; } = new();

    /// <summary>一句话要点（供讲解/引导直接取用，不含结论）。</summary>
    public string Summary { get; set; } = "";

    /// <summary>常见误区（学生易错点，用于讲解"易错点"块与引导提醒）。</summary>
    public string Pitfall { get; set; } = "";
}
