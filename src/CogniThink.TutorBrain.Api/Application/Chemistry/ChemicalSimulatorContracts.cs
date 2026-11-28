namespace CogniThink.TutorBrain.Application.Chemistry;

// 化学模拟器统一契约（输入 + 输出），三个主体类共用。
// 与旧 ChemicalSimulationEngine.cs 中的 SimulationRequest/SimulationResult 等 DTO 解耦：
// 旧 DTO 保留在适配器文件中用于兼容，新代码使用本文件定义的类型。

// ==================== 输入契约（§2） ====================

/// <summary>模拟环境条件。未显式给出的条件按"不满足"处理，避免引擎凭空加热或点燃。</summary>
public sealed class ChemicalConditions
{
    /// <summary>水溶液体系；为 true 时自动补充足量水作溶剂</summary>
    public bool Aqueous { get; set; } = true;

    public bool Heating { get; set; }
    public bool Ignition { get; set; }
    public bool Catalyst { get; set; }
    public bool Electricity { get; set; }
    public bool Light { get; set; }
    public bool Concentrated { get; set; }
    public bool Dilute { get; set; }

    /// <summary>空气环境；为 true 时自动补充足量氧气</summary>
    public bool Air { get; set; } = true;

    public bool Acidic { get; set; }
    public bool Alkaline { get; set; }

    /// <summary>高温（区别于普通加热）：如煅烧/灼烧/825℃以上。需高温的反应开 Heating 不满足</summary>
    public bool HighTemperature { get; set; }

    /// <summary>醇溶液体系（如卤代烃消去的 NaOH 乙醇溶液）；区别于 Aqueous 水溶液</summary>
    public bool Alcoholic { get; set; }

    /// <summary>加压/高压环境（如合成氨）</summary>
    public bool Pressurized { get; set; }

    /// <summary>是否包含长期缓慢过程（如铁生锈、常温缓慢氧化）。默认 false：trace 只展示快速过程</summary>
    public bool IncludeSlow { get; set; }

    /// <summary>最大反应步数，防止病态数据导致死循环</summary>
    public int MaxSteps { get; set; } = 24;

    /// <summary>是否返回每步被考虑但未发生的候选反应（默认关闭，避免噪声）</summary>
    public bool ReturnConsidered { get; set; }
}

/// <summary>参与模拟的初始物种</summary>
public sealed class SpeciesInput
{
    /// <summary>物质 Id、名称、化学式或别名（经知识库解析）</summary>
    public string Substance { get; set; } = string.Empty;

    /// <summary>物质的量（mol），默认 1；水作溶剂时可不填。反应模拟（ChemicalSimulator）以 Moles 为准</summary>
    public double Moles { get; set; } = 1;

    /// <summary>可选：物质的量浓度（mol/L）。平衡数值模拟（EquilibriumStateSimulator）以 Concentration/Volume 为准</summary>
    public double? Concentration { get; set; }

    /// <summary>可选：溶液体积（L）。与 Concentration 配合；仅给 Concentration 时视为单物种浓度</summary>
    public double? Volume { get; set; }

    /// <summary>可选角色标签（如 "待测物"/"滴定剂"），不影响推演，仅用于讲解归因</summary>
    public string? Role { get; set; }
}

/// <summary>分步加料：同一容器内先加一批、反应完后再加下一批</summary>
public sealed class SystemStage
{
    public string Name { get; set; } = string.Empty;
    public List<SpeciesInput> Species { get; set; } = new();
}

/// <summary>
/// 化学体系统一输入：三个模拟器共用。描述"初始有什么物种、各多少量、在什么环境条件下"，
/// 可选分步加料（Stages）。`Moles` 供反应模拟；`Concentration`/`Volume` 供平衡数值模拟。
/// </summary>
public sealed class ChemicalSystemInput
{
    public List<SpeciesInput> Species { get; set; } = new();
    public List<SystemStage> Stages { get; set; } = new();
    public ChemicalConditions Conditions { get; set; } = new();
}

// ==================== 输出契约（§3） ====================

/// <summary>一步反应的化学依据（可溯源）</summary>
public sealed record ReactionBasis(
    string ReactionId,
    /// <summary>关联知识点 Id，取自 Reaction.KnowledgeIds；为空表示该反应未挂知识点</summary>
    IReadOnlyList<string> KnowledgeIds,
    /// <summary>本步为何选中此反应：类型优先级、限量试剂判定、条件满足情况</summary>
    string SelectionReason);

/// <summary>反应优先级（数值越大越优先）</summary>
public sealed record ReactionPriority(
    /// <summary>综合评分（类型优先级 + 反应物数 + 金属活动性 - 可逆/兜底惩罚）</summary>
    double Score,
    /// <summary>在本步候选中的排名（1 = 最优先，实际发生的反应 Rank=1）</summary>
    int Rank);

/// <summary>一次反应事件（可追溯到反应条目与计量依据）</summary>
public sealed record ReactionEvent(
    int Step,
    int StageIndex,
    string StageName,
    string ReactionId,
    string Equation,
    string Type,
    List<string> Conditions,
    List<string> Phenomena,
    /// <summary>反应物消耗量（物质 Id → mol）</summary>
    Dictionary<string, double> Consumed,
    /// <summary>产物生成量（物质 Id → mol）</summary>
    Dictionary<string, double> Produced,
    /// <summary>反应进度（mol）：限量试剂允许的最大反应量</summary>
    double Extent,
    ReactionBasis Basis,
    ReactionPriority Priority,
    string SolutionColorAfter,
    List<string> Precipitates,
    List<string> Gases);

/// <summary>本步被考虑但未发生的候选反应及其原因</summary>
public sealed record ConsideredCandidate(
    string ReactionId,
    string Equation,
    double Score,
    /// <summary>未发生原因：条件不满足 / 反应进度≈0 / 被更高优先级胜出</summary>
    string Reason);

/// <summary>被跳过的反应详情（带方程式与原因），供前端灰显展示"为何没反应"</summary>
public sealed record SkippedReactionDetail(
    string ReactionId,
    string Equation,
    string Reason);

/// <summary>某物质的状态快照</summary>
public sealed record SpeciesState(
    string Id,
    string Name,
    string Formula,
    double InitialMoles,
    double FinalMoles,
    string AmountText,
    string State,
    string? Color,
    string? SolutionColor,
    string Solubility,
    string Role);

/// <summary>体系状态汇总</summary>
public sealed class SystemState
{
    public List<SpeciesState> Species { get; set; } = new();
    public string SolutionColor { get; set; } = "无色";
    public List<string> Precipitates { get; set; } = new();
    public List<string> Gases { get; set; } = new();
    public List<string> ResidualGases { get; set; } = new();
    public List<string> Solids { get; set; } = new();
    public List<string> Phenomena { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

/// <summary>正向模拟结果</summary>
public sealed class SimulationTrace
{
    public bool Converged { get; set; }
    public int Steps { get; set; }
    public List<ReactionEvent> Events { get; set; } = new();
    /// <summary>每步被考虑但未发生的候选（按步分组）；ReturnConsidered=false 时为空</summary>
    public List<List<ConsideredCandidate>> Considered { get; set; } = new();
    public List<SpeciesState> InitialSpecies { get; set; } = new();
    public SystemState FinalState { get; set; } = new();
    public List<string> Evidence { get; set; } = new();
    public List<string> SkippedReactions { get; set; } = new();
    /// <summary>被跳过反应的原因（反应 Id → 原因）；供适配器还原旧 SimulationSkippedReaction，新消费者可直接读 SkippedReactions</summary>
    public Dictionary<string, string> SkippedReasons { get; set; } = new(StringComparer.Ordinal);
    /// <summary>被跳过反应的详情（带方程式与原因），供前端灰显展示；含条件不满足、缓慢过程等</summary>
    public List<SkippedReactionDetail> SkippedDetails { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
