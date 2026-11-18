namespace CogniThink.TutorBrain.Application.Diagnostic;

/// <summary>
/// 思考策略库：为每个知识点配置"教方法、教思考"的引导策略。
/// 核心原则：
/// - 引导式问题不嵌入答案，让学生自己组织思路。
/// - 思考策略给出通用分析方法，而非本题结论。
/// - 分析框架是一步步的问题链，引导学生按方法走。
/// - 常见误区针对该知识点学生最容易犯的错。
/// 所有内容基于知识库中知识点的描述，不臆造。
/// </summary>
public static class ThinkingStrategyLibrary
{
    private static readonly Dictionary<string, ThinkingStrategy> _strategies = new()
    {
        ["C-ELECTROLYTE"] = new ThinkingStrategy
        {
            KnowledgeId = "C-ELECTROLYTE",
            KnowledgeName = "电解质与电离",
            // 引导式问题：问方法，不问结论
            GuidedQuestion = "判断一种物质是不是电解质，你会按什么步骤分析？",
            // 思考方法：通用分析路径
            Strategy = "判断电解质的核心是看「在水溶液中或熔融状态下能否电离出自由移动的离子」，而不是看它是否溶于水。能完全电离的是强电解质，部分电离的是弱电解质。",
            // 分析框架：一步步引导
            AnalysisFramework = new List<string>
            {
                "第一步：先看物质类别——是化合物，还是单质或混合物？（电解质必须是化合物）",
                "第二步：看它在水溶液中或熔融状态下，能否电离出自由移动的离子？",
                "第三步：如果能电离，是完全电离还是部分电离？完全电离=强电解质，部分电离=弱电解质。"
            },
            CommonMisconception = "易错点：① 溶于水的不一定是电解质（如酒精溶于水但不电离）；② 难溶的不一定不是电解质（如 BaSO₄ 难溶，但溶解的那部分完全电离，属于强电解质）；③ 单质和混合物既不是电解质也不是非电解质。",
            TransferHint = "以后遇到判断电解质的题目，都按「类别→能否电离→电离程度」这三步走，不要被溶解性误导。"
        },

        ["C-ION-REACTION"] = new ThinkingStrategy
        {
            KnowledgeId = "C-ION-REACTION",
            KnowledgeName = "离子反应",
            GuidedQuestion = "判断一个反应是不是离子反应，最关键看什么？",
            Strategy = "离子反应的本质是「某些离子浓度降低」。复分解反应发生的条件是有沉淀、气体或弱电解质生成，满足其一即可。写离子方程式时，强电解质拆成离子，弱电解质、沉淀、气体、水保留化学式。",
            AnalysisFramework = new List<string>
            {
                "第一步：把反应物中能拆的强电解质拆成离子形式。",
                "第二步：检查有没有离子结合生成沉淀、气体或弱电解质（如水、醋酸）。",
                "第三步：有上述产物生成，就是离子反应；删去两边不参与反应的离子，写出离子方程式。"
            },
            CommonMisconception = "易错点：① 不是所有化学反应都是离子反应；② 弱电解质（如醋酸、氨水）和水在离子方程式中不能拆；③ 沉淀和气体不能拆。",
            TransferHint = "记住「拆不拆」的判断标准：强酸强碱可溶性盐要拆，其余不拆。这个规则适用于所有离子方程式书写。"
        },

        ["C-REDOX"] = new ThinkingStrategy
        {
            KnowledgeId = "C-REDOX",
            KnowledgeName = "氧化还原反应",
            GuidedQuestion = "分析氧化还原反应，你第一步会做什么？",
            Strategy = "氧化还原反应的本质是电子转移，标志是化合价升降。口诀：升失氧（化合价升高→失电子→被氧化→作还原剂），降得还（化合价降低→得电子→被还原→作氧化剂）。",
            AnalysisFramework = new List<string>
            {
                "第一步：标出反应前后各元素的化合价。",
                "第二步：找化合价升高的元素——它失电子、被氧化，对应的反应物是还原剂。",
                "第三步：找化合价降低的元素——它得电子、被还原，对应的反应物是氧化剂。",
                "第四步：验证电子守恒——化合价升高总数等于降低总数。"
            },
            CommonMisconception = "易错点：① 氧化剂和还原剂指的是反应物，不是产物；② 同一种物质可能既是氧化剂又是还原剂（歧化反应）；③ 有单质参加或生成的反应不一定是氧化还原（如同素异形体转化）。",
            TransferHint = "任何氧化还原反应的分析都从「标化合价」开始，这是最可靠的起点。不要凭感觉判断氧化还原。"
        },

        ["C-COEXIST"] = new ThinkingStrategy
        {
            KnowledgeId = "C-COEXIST",
            KnowledgeName = "离子共存判断",
            GuidedQuestion = "判断离子能否共存，你会按什么顺序排查？",
            Strategy = "离子能否共存，本质是看离子之间会不会发生反应。按四步法依次排查：沉淀→气体和弱电解质→氧化还原→络合。先看题干条件（酸性/碱性/无色等）再排查。",
            AnalysisFramework = new List<string>
            {
                "第一步：先看题干限制条件（酸性→排除与 H⁺ 反应的离子；碱性→排除与 OH⁻ 反应的离子；无色→排除有色离子）。",
                "第二步：查沉淀——有没有离子结合生成沉淀？",
                "第三步：查气体和弱电解质——有没有生成气体或弱电解质（如水、醋酸、氨水）？",
                "第四步：查氧化还原——有没有氧化性离子（如 NO₃⁻+H⁺、Fe³⁺、MnO₄⁻）与还原性离子（如 Fe²⁺、S²⁻、I⁻）相遇？"
            },
            CommonMisconception = "易错点：① 忽略题干的酸碱条件就直接判断；② 只看沉淀不看氧化还原（如 Fe²⁺ 与 NO₃⁻ 在中性条件下可共存，但酸性条件下不共存）；③ 忘记有色离子（Cu²⁺、Fe³⁺、Fe²⁺、MnO₄⁻）在无色条件下不能存在。",
            TransferHint = "共存题永远先读条件再排查，四步走一遍基本不会漏。氧化还原是最容易被忽略的，养成最后查一遍的习惯。"
        },

        ["C-FE"] = new ThinkingStrategy
        {
            KnowledgeId = "C-FE",
            KnowledgeName = "铁及其化合物",
            GuidedQuestion = "检验一种离子时，你需要考虑哪些因素？",
            Strategy = "检验离子要抓「特征反应」——试剂、现象、结论三位一体。Fe³⁺ 用 KSCN 溶液（变红），Fe²⁺ 先加 KSCN 不变色再加氯水（变红）。转化看价态：Fe²⁺ 被氧化变 Fe³⁺，Fe³⁺ 被还原变 Fe²⁺。",
            AnalysisFramework = new List<string>
            {
                "第一步：确定待检离子的价态——是 Fe²⁺ 还是 Fe³⁺？",
                "第二步：选择对应的特征检验试剂（Fe³⁺→KSCN；Fe²⁺→KSCN 后加氯水）。",
                "第三步：描述实验现象（颜色变化）。",
                "第四步：由现象反推离子种类，得出结论。"
            },
            CommonMisconception = "易错点：① 混淆 Fe²⁺ 和 Fe³⁺ 的检验方法；② 检验 Fe²⁺ 时忘记先加 KSCN 确认不变色，直接加氯水会分不清原来到底有没有 Fe²⁺；③ 描述现象时只说结论不说颜色变化。",
            TransferHint = "检验任何离子都遵循「试剂→现象→结论」三步，Fe 的检验只是一个例子。记住这个通用格式，其他离子检验也适用。"
        },

        ["C-NA"] = new ThinkingStrategy
        {
            KnowledgeId = "C-NA",
            KnowledgeName = "钠及其化合物",
            GuidedQuestion = "鉴别两种物质时，你会从哪些性质差异入手？",
            Strategy = "鉴别物质的核心是找「性质差异」——反应速率、产物种类、热稳定性等。选操作最简便、现象最明显的方法。Na₂CO₃ 与盐酸反应分步进行产气慢，NaHCO₃ 与盐酸反应一步进行产气快。",
            AnalysisFramework = new List<string>
            {
                "第一步：列出两种物质的关键性质（与酸反应、热稳定性、溶解性等）。",
                "第二步：对比找出性质差异点。",
                "第三步：选择能体现差异的试剂和操作。",
                "第四步：描述现象，并把现象对应到具体物质。"
            },
            CommonMisconception = "易错点：① 用加热法鉴别溶液不合适，应比较与酸反应的产气速率；② Na₂CO₃ 与酸反应是分步进行（CO₃²⁻→HCO₃⁻→CO₂），所以产气较慢；③ 描述结论时要把现象和物质一一对应。",
            TransferHint = "鉴别题的通用思路是「找差异→选方法→写现象→得结论」，不只是钠盐，任何物质鉴别都按这个思路走。"
        }
    };

    /// <summary>
    /// 获取知识点的思考策略。找不到时返回通用策略（只含名称，无具体内容）。
    /// </summary>
    public static ThinkingStrategy Get(string knowledgeId)
    {
        return _strategies.TryGetValue(knowledgeId, out var s) ? s : new ThinkingStrategy
        {
            KnowledgeId = knowledgeId,
            KnowledgeName = knowledgeId,
            GuidedQuestion = $"关于「{knowledgeId}」，你打算怎么分析这个问题？",
            Strategy = "先分析题目条件，再回忆相关概念，最后逐步推理得出结论。",
            AnalysisFramework = new List<string>
            {
                "第一步：题目给了哪些条件？",
                "第二步：涉及哪个核心概念？",
                "第三步：根据概念逐步推理。"
            },
            CommonMisconception = "注意概念的适用条件，不要凭感觉判断。",
            TransferHint = "把分析方法迁移到类似题目上。"
        };
    }
}

/// <summary>
/// 单个知识点的思考策略配置。
/// </summary>
public sealed class ThinkingStrategy
{
    public string KnowledgeId { get; set; } = string.Empty;
    public string KnowledgeName { get; set; } = string.Empty;

    /// <summary>引导式问题：问方法，不嵌入答案</summary>
    public string GuidedQuestion { get; set; } = string.Empty;

    /// <summary>思考方法：通用分析路径</summary>
    public string Strategy { get; set; } = string.Empty;

    /// <summary>分析框架：一步步的问题链</summary>
    public List<string> AnalysisFramework { get; set; } = new();

    /// <summary>常见误区</summary>
    public string CommonMisconception { get; set; } = string.Empty;

    /// <summary>迁移提示</summary>
    public string TransferHint { get; set; } = string.Empty;
}
