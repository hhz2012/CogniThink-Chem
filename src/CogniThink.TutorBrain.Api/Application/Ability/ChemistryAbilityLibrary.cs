namespace CogniThink.TutorBrain.Application.Ability;

using CogniThink.TutorBrain.Domain;

/// <summary>
/// 化学解题基本能力维度。
/// 归纳自：化学学科核心素养 + 现有错因标签体系 + 题型特征。
/// 每项能力可通过学生的作答表现（verdict / errorTag / hintLevel）量化。
/// </summary>
public enum ChemistryAbility
{
    /// <summary>审题分析能力：从题干提取关键条件、明确问题指向</summary>
    ProblemAnalysis = 1,

    /// <summary>概念辨析能力：准确理解和区分化学概念、公式</summary>
    ConceptDifferentiation = 2,

    /// <summary>逻辑推理能力：基于已知条件逐步推导、因果连贯</summary>
    LogicalReasoning = 3,

    /// <summary>化学计算能力：定量计算、化学量与单位处理</summary>
    ChemicalCalculation = 4,

    /// <summary>实验探究能力：试剂选择、现象预测、结论推导</summary>
    ExperimentalInquiry = 5,

    /// <summary>信息整合能力：多条件综合判断（如离子共存）</summary>
    InformationIntegration = 6
}

/// <summary>
/// 能力元数据：名称、描述、对应的错因标签、引导式提问模板。
/// 所有内容基于化学教学实际归纳，不臆造。
/// </summary>
public sealed class AbilityMetadata
{
    public ChemistryAbility Ability { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>该能力对应的错因标签（学生在这些错因上出错，说明该能力薄弱）</summary>
    public List<ErrorTag> RelatedErrorTags { get; set; } = new();

    /// <summary>能力薄弱时的引导式问题模板（教方法，不直接给答案）</summary>
    public string GuidedQuestionTemplate { get; set; } = string.Empty;

    /// <summary>能力对应的思考方法提示</summary>
    public string StrategyHint { get; set; } = string.Empty;
}

/// <summary>
/// 化学解题基本能力知识库：定义 6 项能力的元数据、错因映射、知识点映射。
/// </summary>
public static class ChemistryAbilityLibrary
{
    private static readonly Dictionary<ChemistryAbility, AbilityMetadata> _abilities = new()
    {
        [ChemistryAbility.ProblemAnalysis] = new AbilityMetadata
        {
            Ability = ChemistryAbility.ProblemAnalysis,
            Name = "审题分析能力",
            Description = "从题干中提取关键条件（量、状态、环境），明确题目要求解决的问题。",
            RelatedErrorTags = new List<ErrorTag> { ErrorTag.Misread, ErrorTag.ConditionMissed },
            GuidedQuestionTemplate = "先别急着算，题目给了哪些条件？要求回答什么？把条件一条条列出来。",
            StrategyHint = "审题三步法：① 圈出限制条件（酸/碱/过量/少量）；② 明确问的是「能不能」「是什么」「有多少」；③ 确认已知量和未知量。"
        },
        [ChemistryAbility.ConceptDifferentiation] = new AbilityMetadata
        {
            Ability = ChemistryAbility.ConceptDifferentiation,
            Name = "概念辨析能力",
            Description = "准确理解化学概念的内涵与外延，区分易混淆概念（如电解质/非电解质、强/弱电解质）。",
            RelatedErrorTags = new List<ErrorTag> { ErrorTag.ConceptConfusion, ErrorTag.FormulaMisremembered },
            GuidedQuestionTemplate = "这个概念的定义是什么？和它容易混淆的概念有什么区别？",
            StrategyHint = "概念辨析法：① 背定义抓关键词；② 列对比表找差异；③ 用反例验证（如「溶于水的不一定是电解质」）。"
        },
        [ChemistryAbility.LogicalReasoning] = new AbilityMetadata
        {
            Ability = ChemistryAbility.LogicalReasoning,
            Name = "逻辑推理能力",
            Description = "基于已知条件和化学规律，逐步推导结论，保持因果链条完整。",
            RelatedErrorTags = new List<ErrorTag> { ErrorTag.ReasoningJump },
            GuidedQuestionTemplate = "你的结论是怎么一步步推出来的？每一步的依据是什么？",
            StrategyHint = "推理链法：① 写出前提条件；② 每一步写出「因为…所以…」；③ 检查中间有没有跳步。"
        },
        [ChemistryAbility.ChemicalCalculation] = new AbilityMetadata
        {
            Ability = ChemistryAbility.ChemicalCalculation,
            Name = "化学计算能力",
            Description = "进行物质的量、浓度、质量等定量计算，正确处理单位和有效数字。",
            RelatedErrorTags = new List<ErrorTag> { ErrorTag.Calculation },
            GuidedQuestionTemplate = "这道题涉及哪些化学量？单位统一了吗？计算过程写出来看看。",
            StrategyHint = "计算三步法：① 写关系式（化学方程式或比例式）；② 统一单位；③ 代入计算并检查量纲。"
        },
        [ChemistryAbility.ExperimentalInquiry] = new AbilityMetadata
        {
            Ability = ChemistryAbility.ExperimentalInquiry,
            Name = "实验探究能力",
            Description = "根据实验目的选择试剂、预测现象、由现象推导结论。",
            RelatedErrorTags = new List<ErrorTag>(),
            GuidedQuestionTemplate = "实验目的是什么？选什么试剂？预期看到什么现象？现象说明什么？",
            StrategyHint = "实验三段式：① 试剂→② 现象→③ 结论，三者一一对应，缺一不可。"
        },
        [ChemistryAbility.InformationIntegration] = new AbilityMetadata
        {
            Ability = ChemistryAbility.InformationIntegration,
            Name = "信息整合能力",
            Description = "综合多个条件（如酸性环境、离子性质、氧化还原）进行判断，不遗漏反应类型。",
            RelatedErrorTags = new List<ErrorTag> { ErrorTag.CoexistenceJudgement },
            GuidedQuestionTemplate = "题目给了哪几个条件？每个条件排除了哪些可能？有没有漏掉的反应类型？",
            StrategyHint = "排查法：① 先看环境条件（酸/碱/无色）；② 逐类排查（沉淀→气体→弱电解质→氧化还原）；③ 最后检查有没有遗漏。"
        }
    };

    /// <summary>知识点 → 主要考察的能力 映射</summary>
    private static readonly Dictionary<string, List<ChemistryAbility>> _knowledgeToAbilities = new()
    {
        ["C-ELECTROLYTE"] = new List<ChemistryAbility>
        {
            ChemistryAbility.ConceptDifferentiation,
            ChemistryAbility.LogicalReasoning
        },
        ["C-ION-REACTION"] = new List<ChemistryAbility>
        {
            ChemistryAbility.ConceptDifferentiation,
            ChemistryAbility.LogicalReasoning,
            ChemistryAbility.InformationIntegration
        },
        ["C-REDOX"] = new List<ChemistryAbility>
        {
            ChemistryAbility.ConceptDifferentiation,
            ChemistryAbility.LogicalReasoning
        },
        ["C-COEXIST"] = new List<ChemistryAbility>
        {
            ChemistryAbility.ProblemAnalysis,
            ChemistryAbility.InformationIntegration,
            ChemistryAbility.LogicalReasoning
        },
        ["C-FE"] = new List<ChemistryAbility>
        {
            ChemistryAbility.ExperimentalInquiry,
            ChemistryAbility.ConceptDifferentiation
        },
        ["C-NA"] = new List<ChemistryAbility>
        {
            ChemistryAbility.ExperimentalInquiry,
            ChemistryAbility.InformationIntegration
        }
    };

    /// <summary>错因标签 → 能力 映射（一个错因可能对应一个能力）</summary>
    private static readonly Dictionary<ErrorTag, ChemistryAbility> _errorTagToAbility = new()
    {
        [ErrorTag.Misread] = ChemistryAbility.ProblemAnalysis,
        [ErrorTag.ConditionMissed] = ChemistryAbility.ProblemAnalysis,
        [ErrorTag.ConceptConfusion] = ChemistryAbility.ConceptDifferentiation,
        [ErrorTag.FormulaMisremembered] = ChemistryAbility.ConceptDifferentiation,
        [ErrorTag.ReasoningJump] = ChemistryAbility.LogicalReasoning,
        [ErrorTag.Calculation] = ChemistryAbility.ChemicalCalculation,
        [ErrorTag.CoexistenceJudgement] = ChemistryAbility.InformationIntegration
    };

    public static AbilityMetadata Get(ChemistryAbility ability) =>
        _abilities.TryGetValue(ability, out var m) ? m : new AbilityMetadata { Ability = ability, Name = ability.ToString() };

    public static IReadOnlyList<AbilityMetadata> All => _abilities.Values.ToList();

    /// <summary>获取知识点考察的能力列表</summary>
    public static IReadOnlyList<ChemistryAbility> GetAbilitiesForKnowledge(string knowledgeId) =>
        _knowledgeToAbilities.TryGetValue(knowledgeId, out var list) ? list : new List<ChemistryAbility>();

    /// <summary>由错因标签定位薄弱能力</summary>
    public static ChemistryAbility? GetAbilityForErrorTag(ErrorTag tag) =>
        _errorTagToAbility.TryGetValue(tag, out var a) ? a : null;

    /// <summary>获取所有能力枚举</summary>
    public static IReadOnlyList<ChemistryAbility> AllAbilities =>
        Enum.GetValues<ChemistryAbility>().ToList();
}
