namespace CogniThink.TutorBrain.Application.Ability;

using CogniThink.TutorBrain.Domain;

/// <summary>
/// 个人能力图谱中的节点：一个知识点 + 学生在该知识点上的多维学情。
/// 图谱节点叠加了：知识掌握度 + 各能力维度得分 + 错因分布 + 练习历史。
/// </summary>
public sealed class PersonalAbilityNode
{
    public string KnowledgeId { get; set; } = string.Empty;
    public string KnowledgeName { get; set; } = string.Empty;
    public CognitiveLevel Level { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>知识掌握度 0~1（来自 BKT）</summary>
    public double Mastery { get; set; }

    /// <summary>该知识点上的练习次数</summary>
    public int Attempts { get; set; }

    /// <summary>该知识点上的正确次数</summary>
    public int Corrects { get; set; }

    /// <summary>是否为薄弱知识点（掌握度 < 0.5 且有练习记录）</summary>
    public bool IsWeak { get; set; }

    /// <summary>该知识点上的能力维度得分（只列出该知识点考察的能力）</summary>
    public List<AbilityScoreItem> AbilityScores { get; set; } = new();

    /// <summary>错因分布（标签 → 次数）</summary>
    public List<ErrorTagCount> ErrorPattern { get; set; } = new();

    /// <summary>最后练习时间</summary>
    public DateTime? LastPracticedAt { get; set; }

    /// <summary>薄弱原因说明（可解释性）</summary>
    public string? WeakReason { get; set; }
}

/// <summary>能力维度得分（图谱节点内的子结构）</summary>
public sealed class AbilityScoreItem
{
    public ChemistryAbility Ability { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Level => Score switch
    {
        >= 0.8 => "优秀",
        >= 0.6 => "良好",
        >= 0.4 => "一般",
        _ => "薄弱"
    };
    public bool IsWeak => Score < 0.4;
}

/// <summary>错因标签计数</summary>
public sealed class ErrorTagCount
{
    public ErrorTag Tag { get; set; }
    public string TagText { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>
/// 个人能力图谱的边：前置知识点 → 后继知识点。
/// 复用知识图谱的依赖关系，但标注哪些边的两端是薄弱节点。
/// </summary>
public sealed class PersonalAbilityEdge
{
    public string FromId { get; set; } = string.Empty;
    public string ToId { get; set; } = string.Empty;
    public bool FromWeak { get; set; }
    public bool ToWeak { get; set; }
    /// <summary>是否为"薄弱传导链"（前置薄弱导致后继薄弱）</summary>
    public bool IsWeakChain => FromWeak && ToWeak;
}

/// <summary>
/// 薄弱知识链：从某个薄弱知识点追溯到其薄弱前置节点的路径。
/// 用于诊断"后继学不好是因为前置没掌握"。
/// </summary>
public sealed class WeakKnowledgeChain
{
    public List<string> Path { get; set; } = new();
    public string RootCause { get; set; } = string.Empty;
}

/// <summary>
/// 个人能力图谱：把学生对各知识点的掌握度、各能力维度得分、错因模式
/// 叠加到知识依赖图上，形成"知识+能力+错因"的三维学情图谱。
/// 数据全部来自现有表（KnowledgeMastery / StepTrace / MistakeRecord），
/// 实时聚合，不新增持久化表。
/// </summary>
public sealed class PersonalAbilityGraph
{
    public string StudentId { get; set; } = string.Empty;

    /// <summary>图谱节点（知识点 + 学情）</summary>
    public List<PersonalAbilityNode> Nodes { get; set; } = new();

    /// <summary>图谱边（依赖关系 + 薄弱传导）</summary>
    public List<PersonalAbilityEdge> Edges { get; set; } = new();

    /// <summary>知识维度综合得分</summary>
    public double KnowledgeScore { get; set; }

    /// <summary>能力维度综合得分</summary>
    public double AbilityScore { get; set; }

    /// <summary>综合得分（知识 60% + 能力 40%）</summary>
    public double OverallScore { get; set; }

    /// <summary>薄弱知识点</summary>
    public List<PersonalAbilityNode> WeakNodes { get; set; } = new();

    /// <summary>薄弱知识链（前置薄弱传导到后继）</summary>
    public List<WeakKnowledgeChain> WeakChains { get; set; } = new();

    /// <summary>全局高频错因</summary>
    public List<ErrorTagCount> GlobalErrorPattern { get; set; } = new();

    /// <summary>最薄弱的能力维度</summary>
    public List<AbilityScoreItem> WeakAbilities { get; set; } = new();
}
