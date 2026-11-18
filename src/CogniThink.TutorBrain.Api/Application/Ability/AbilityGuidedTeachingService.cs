namespace CogniThink.TutorBrain.Application.Ability;

using CogniThink.TutorBrain.Domain;

/// <summary>能力导学的一次交互</summary>
public sealed class AbilityGuidedInteraction
{
    public ChemistryAbility Ability { get; set; }
    public string AbilityName { get; set; } = string.Empty;

    /// <summary>本次引导的问题（教方法，不直接给答案）</summary>
    public string Question { get; set; } = string.Empty;

    /// <summary>思考方法提示</summary>
    public string StrategyHint { get; set; } = string.Empty;

    /// <summary>渐进式提示（从抽象到具体，学生可逐级请求）</summary>
    public List<string> ProgressiveHints { get; set; } = new();

    /// <summary>该能力相关的知识点</summary>
    public List<string> RelatedKnowledgeIds { get; set; } = new();
}

/// <summary>
/// 能力导向交互问答服务：
/// 根据学生的薄弱能力，生成引导式问题和渐进提示，训练该能力。
/// 核心原则：教方法、教思考，不直接给答案。
/// </summary>
public sealed class AbilityGuidedTeachingService
{
    private readonly AbilityEvaluationService _abilityEval;

    public AbilityGuidedTeachingService(AbilityEvaluationService abilityEval)
    {
        _abilityEval = abilityEval;
    }

    /// <summary>
    /// 为学生生成针对最薄弱能力的引导式交互。
    /// </summary>
    public async Task<AbilityGuidedInteraction?> GetGuidanceForWeakestAbilityAsync(
        string studentId, CancellationToken ct = default)
    {
        var weak = await _abilityEval.GetWeakAbilitiesAsync(studentId, 1, ct);
        if (weak.Count == 0) return null;

        return BuildInteraction(weak[0].Ability);
    }

    /// <summary>
    /// 为指定能力生成引导式交互。
    /// </summary>
    public AbilityGuidedInteraction GetGuidanceForAbility(ChemistryAbility ability)
    {
        return BuildInteraction(ability);
    }

    /// <summary>
    /// 为某题某步生成能力导向的提示。
    /// 结合该步骤考察的能力和学生在这些能力上的表现，给出针对性提示。
    /// </summary>
    public async Task<AbilityGuidedInteraction?> GetGuidanceForStepAsync(
        string studentId, string knowledgeId, CancellationToken ct = default)
    {
        var abilities = ChemistryAbilityLibrary.GetAbilitiesForKnowledge(knowledgeId);
        if (abilities.Count == 0) return null;

        var scores = await _abilityEval.EvaluateForKnowledgeAsync(studentId, knowledgeId, ct);

        // 找该步骤考察能力中学生最薄弱的
        var weakest = scores
            .Where(s => s.Attempts > 0)
            .OrderBy(s => s.Score)
            .FirstOrDefault();

        var targetAbility = weakest?.Ability ?? abilities[0];
        return BuildInteraction(targetAbility);
    }

    private static AbilityGuidedInteraction BuildInteraction(ChemistryAbility ability)
    {
        var meta = ChemistryAbilityLibrary.Get(ability);

        return new AbilityGuidedInteraction
        {
            Ability = ability,
            AbilityName = meta.Name,
            Question = meta.GuidedQuestionTemplate,
            StrategyHint = meta.StrategyHint,
            ProgressiveHints = BuildProgressiveHints(ability, meta),
            RelatedKnowledgeIds = GetRelatedKnowledge(ability)
        };
    }

    /// <summary>
    /// 为每项能力生成 3 级渐进提示：
    /// 1 级：方向提醒（最抽象）
    /// 2 级：方法步骤
    /// 3 级：具体示例（最具体，但仍不直接给答案）
    /// </summary>
    private static List<string> BuildProgressiveHints(ChemistryAbility ability, AbilityMetadata meta)
    {
        return ability switch
        {
            ChemistryAbility.ProblemAnalysis => new List<string>
            {
                "先读一遍题目，把所有条件圈出来。",
                "按「限制条件—已知量—未知量」三类整理。",
                "例如酸性条件意味着有大量 H⁺，会影响哪些离子的存在？"
            },
            ChemistryAbility.ConceptDifferentiation => new List<string>
            {
                "回忆这个概念的定义。",
                "写出定义中的关键词，再想想它的反例。",
                "例如电解质的关键词是「化合物」「水溶液或熔融」「导电」，反例是酒精（溶于水但不导电）。"
            },
            ChemistryAbility.LogicalReasoning => new List<string>
            {
                "你的结论是什么？前提是什么？",
                "把推理过程写成「因为…所以…」的链条。",
                "例如：因为 H⁺ 和 NO₃⁻ 共存相当于硝酸，所以有强氧化性，所以能氧化 Fe²⁺。"
            },
            ChemistryAbility.ChemicalCalculation => new List<string>
            {
                "先写出涉及的化学方程式或关系式。",
                "把所有量的单位统一，再列比例式。",
                "例如：n = m/M，先把质量换成物质的量再计算。"
            },
            ChemistryAbility.ExperimentalInquiry => new List<string>
            {
                "实验目的是什么？要检验什么？",
                "按「试剂—现象—结论」三段式组织。",
                "例如：检验 Fe³⁺ 选 KSCN，现象是变红，结论是含 Fe³⁺。"
            },
            ChemistryAbility.InformationIntegration => new List<string>
            {
                "题目有几个条件？先列出来。",
                "按「沉淀→气体→弱电解质→氧化还原」顺序排查。",
                "例如：酸性条件下，除了沉淀，还要查 NO₃⁻ 会不会氧化还原性离子。"
            },
            _ => new List<string> { meta.StrategyHint }
        };
    }

    private static List<string> GetRelatedKnowledge(ChemistryAbility ability)
    {
        // 反向查哪些知识点考察该能力
        var allKps = new[]
        {
            "C-ELECTROLYTE", "C-ION-REACTION", "C-REDOX",
            "C-COEXIST", "C-FE", "C-NA"
        };
        return allKps
            .Where(kp => ChemistryAbilityLibrary.GetAbilitiesForKnowledge(kp).Contains(ability))
            .ToList();
    }
}
