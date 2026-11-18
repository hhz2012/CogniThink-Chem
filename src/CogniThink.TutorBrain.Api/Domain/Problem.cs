namespace CogniThink.TutorBrain.Domain;

/// <summary>
/// 题目实体：题干 + 绑定知识点 + 标准推理步骤树
/// </summary>
public class Problem
{
    public string Id { get; set; } = string.Empty;

    public Subject Subject { get; set; }

    /// <summary>题型分类：ChemistryInference</summary>
    public string Category { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>题干（纯文本，前端负责渲染画布）</summary>
    public string Stem { get; set; } = string.Empty;

    /// <summary>最终结论（不直接下发给学生，仅状态机通关判定使用）</summary>
    public string? AnswerKey { get; set; }

    /// <summary>若为变式题，记录母题 Id</summary>
    public string? VariantOfId { get; set; }

    /// <summary>难度：1~5</summary>
    public int Difficulty { get; set; } = 1;

    public List<ProblemKnowledge> KnowledgeLinks { get; set; } = new();

    public List<ProblemStep> Steps { get; set; } = new();
}

/// <summary>题目与知识点多对多绑定</summary>
public class ProblemKnowledge
{
    public string ProblemId { get; set; } = string.Empty;
    public Problem? Problem { get; set; }

    public string KnowledgeId { get; set; } = string.Empty;
    public KnowledgeNode? Knowledge { get; set; }

    /// <summary>是否为主考点（变式生成时优先保持）</summary>
    public bool IsPrimary { get; set; }
}

/// <summary>
/// 标准推理步骤（推理步骤树的一个节点）
/// 判定一律由领域规则引擎依据 ExpectedExpression 完成，LLM 不参与判对错。
/// </summary>
public class ProblemStep
{
    public int Id { get; set; }

    public string ProblemId { get; set; } = string.Empty;
    public Problem? Problem { get; set; }

    /// <summary>步骤序号（从 1 开始）</summary>
    public int StepIndex { get; set; }

    /// <summary>向学生提出的本步问题</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>标准推理表达式（规则引擎判定依据）</summary>
    public string ExpectedExpression { get; set; } = string.Empty;

    /// <summary>必备关键词（逗号分隔），用于区分"局部小错"与"关键错误"</summary>
    public string? ExpectedKeywords { get; set; }

    /// <summary>
    /// 干扰关键词（逗号分隔，同义词用/分隔）：
    /// 学生答案中出现这些词说明含有错误额外信息。
    /// 当必备关键词全部命中时，若同时命中干扰词，则降级为 PartiallyCorrect。
    /// </summary>
    public string? DistractorKeywords { get; set; }

    /// <summary>
    /// 模糊关键词（逗号分隔，同义词用/分隔）：
    /// 学生答案中出现这些词说明方向正确但表述过于笼统。
    /// 当必备关键词未命中时，若命中模糊词，则升级为 PartiallyCorrect（而非 Incorrect）。
    /// </summary>
    public string? VagueKeywords { get; set; }

    /// <summary>该步骤对应的知识点</summary>
    public string KnowledgeId { get; set; } = string.Empty;

    /// <summary>是否为关键步骤（关键步骤出错直接进入 KeyMistake）</summary>
    public bool IsKeyStep { get; set; }

    // ===== 四级降级提示：由状态机决定何时放出，不由前端自由展示 =====

    /// <summary>一级：自查方向（不指出具体错误）</summary>
    public string? HintSelfCheck { get; set; }

    /// <summary>二级：子问题引导</summary>
    public string? HintSubQuestion { get; set; }

    /// <summary>三级：关键线索</summary>
    public string? HintClue { get; set; }

    /// <summary>四级：分步脚手架（最低梯度，仅在多次卡壳后给）</summary>
    public string? Scaffold { get; set; }
}
