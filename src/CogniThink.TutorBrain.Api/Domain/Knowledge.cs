namespace CogniThink.TutorBrain.Domain;

/// <summary>
/// 知识点实体（知识图谱节点）
/// 依赖关系通过 <see cref="KnowledgeEdge"/> 表达：前置知识点 → 后继知识点
/// </summary>
public class KnowledgeNode
{
    public string Id { get; set; } = string.Empty;

    /// <summary>知识点名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>所属学科：化学</summary>
    public Subject Subject { get; set; }

    /// <summary>课标认知层级</summary>
    public CognitiveLevel Level { get; set; }

    /// <summary>知识点描述（用于生成导学话术）</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>该知识点的高频易错标签</summary>
    public List<ErrorTag> ErrorTags { get; set; } = new();

    /// <summary>本题知识点依赖的前置节点</summary>
    public ICollection<KnowledgeEdge> Prerequisites { get; set; } = new List<KnowledgeEdge>();

    /// <summary>依赖本知识点的后继节点</summary>
    public ICollection<KnowledgeEdge> Dependents { get; set; } = new List<KnowledgeEdge>();
}

/// <summary>
/// 知识点依赖边：From（前置）→ To（后继）
/// 导学原则：前置未掌握时，不直接跳到后继知识点。
/// </summary>
public class KnowledgeEdge
{
    public string FromId { get; set; } = string.Empty;
    public KnowledgeNode? From { get; set; }

    public string ToId { get; set; } = string.Empty;
    public KnowledgeNode? To { get; set; }
}
