namespace CogniThink.TutorBrain.Infrastructure;

using Microsoft.EntityFrameworkCore;
using CogniThink.TutorBrain.Domain;

/// <summary>示例种子数据：化学推断 3 题 + 6 个化学知识点及依赖关系</summary>
public static class SeedData
{
    public static async Task EnsureSeededAsync(TutorDbContext db, CancellationToken ct = default)
    {
        if (await db.KnowledgeNodes.AnyAsync(ct)) return;

        // ==================== 知识点 ====================
        var nodes = new List<KnowledgeNode>
        {
            new()
            {
                Id = "C-ELECTROLYTE", Name = "电解质与电离", Subject = Subject.Chemistry,
                Level = CognitiveLevel.Remember,
                Description = "强弱电解质、电离方程式与离子来源。",
                ErrorTags = new List<ErrorTag> { ErrorTag.ConceptConfusion }
            },
            new()
            {
                Id = "C-ION-REACTION", Name = "离子反应", Subject = Subject.Chemistry,
                Level = CognitiveLevel.Understand,
                Description = "复分解反应发生条件与离子方程式书写。",
                ErrorTags = new List<ErrorTag> { ErrorTag.ConceptConfusion, ErrorTag.ConditionMissed }
            },
            new()
            {
                Id = "C-REDOX", Name = "氧化还原反应", Subject = Subject.Chemistry,
                Level = CognitiveLevel.Apply,
                Description = "化合价升降、电子转移与常见氧化剂还原剂。",
                ErrorTags = new List<ErrorTag> { ErrorTag.ConceptConfusion, ErrorTag.Calculation }
            },
            new()
            {
                Id = "C-COEXIST", Name = "离子共存判断", Subject = Subject.Chemistry,
                Level = CognitiveLevel.Analyze,
                Description = "结合酸性/碱性条件、沉淀、氧化还原判断离子能否大量共存。",
                ErrorTags = new List<ErrorTag>
                {
                    ErrorTag.CoexistenceJudgement, ErrorTag.ConditionMissed, ErrorTag.ReasoningJump
                }
            },
            new()
            {
                Id = "C-FE", Name = "铁及其化合物", Subject = Subject.Chemistry,
                Level = CognitiveLevel.Understand,
                Description = "Fe²⁺、Fe³⁺ 的检验、转化与特征颜色。",
                ErrorTags = new List<ErrorTag> { ErrorTag.ConceptConfusion }
            },
            new()
            {
                Id = "C-NA", Name = "钠及其化合物", Subject = Subject.Chemistry,
                Level = CognitiveLevel.Understand,
                Description = "Na₂CO₃ 与 NaHCO₃ 的性质差异与鉴别。",
                ErrorTags = new List<ErrorTag> { ErrorTag.ConceptConfusion, ErrorTag.ConditionMissed }
            }
        };

        db.KnowledgeNodes.AddRange(nodes);
        await db.SaveChangesAsync(ct);

        // ==================== 依赖边（前置 → 后继） ====================
        var edges = new (string From, string To)[]
        {
            ("C-ELECTROLYTE", "C-ION-REACTION"),
            ("C-ELECTROLYTE", "C-REDOX"),
            ("C-ION-REACTION", "C-COEXIST"),
            ("C-REDOX", "C-COEXIST"),
            ("C-REDOX", "C-FE"),
            ("C-ION-REACTION", "C-NA")
        };

        foreach (var (from, to) in edges)
            db.KnowledgeEdges.Add(new KnowledgeEdge { FromId = from, ToId = to });

        await db.SaveChangesAsync(ct);

        // ==================== 题目 ====================
        db.Problems.AddRange(
            BuildChemistryFeTest(),
            BuildChemistryNaTest(),
            BuildChemistryCoexist());

        await db.SaveChangesAsync(ct);
    }

    // ---------- 化学 1：Fe³⁺ 检验 ----------
    private static Problem BuildChemistryFeTest()
    {
        var p = new Problem
        {
            Id = "C-FE-01",
            Subject = Subject.Chemistry,
            Category = "ChemistryInference",
            Title = "Fe³⁺ 的检验",
            Stem = "某溶液中可能含有 Fe³⁺。请设计实验检验，并写出现象与结论。",
            AnswerKey = "加 KSCN 溶液变红，说明含 Fe³⁺",
            Difficulty = 2
        };
        p.KnowledgeLinks.Add(new ProblemKnowledge { ProblemId = p.Id, KnowledgeId = "C-FE", IsPrimary = true });
        p.KnowledgeLinks.Add(new ProblemKnowledge { ProblemId = p.Id, KnowledgeId = "C-REDOX" });

        p.Steps.Add(new ProblemStep
        {
            ProblemId = p.Id, StepIndex = 1,
            Prompt = "第一步：选择什么试剂检验 Fe³⁺？",
            ExpectedExpression = "KSCN",
            ExpectedKeywords = "KSCN/硫氰化钾",
            DistractorKeywords = "NaOH/氢氧化钠",
            KnowledgeId = "C-FE",
            HintSelfCheck = "回忆 Fe³⁺ 的特征检验试剂，不要与 Fe²⁺ 的检验混淆。",
            HintSubQuestion = "Fe³⁺ 遇什么试剂会显红色？",
            HintClue = "KSCN（硫氰化钾）溶液遇 Fe³⁺ 变红。",
            Scaffold = "选 KSCN → 观察颜色变化 → 得结论。"
        });

        p.Steps.Add(new ProblemStep
        {
            ProblemId = p.Id, StepIndex = 2,
            Prompt = "第二步：加入试剂后应观察到什么现象？",
            ExpectedExpression = "溶液变红色",
            ExpectedKeywords = "变红/红色",
            VagueKeywords = "颜色变化",
            KnowledgeId = "C-FE",
            IsKeyStep = true,
            HintSelfCheck = "现象要写颜色变化，而不是写结论。",
            HintSubQuestion = "Fe³⁺ 与 SCN⁻ 形成什么颜色的物质？",
            HintClue = "溶液由无色变为红色。",
            Scaffold = "加 KSCN → 溶液变红。"
        });

        p.Steps.Add(new ProblemStep
        {
            ProblemId = p.Id, StepIndex = 3,
            Prompt = "第三步：由现象得出什么结论？",
            ExpectedExpression = "含Fe3+",
            ExpectedKeywords = "Fe3+/铁离子,含有",
            KnowledgeId = "C-FE",
            IsKeyStep = true,
            HintSelfCheck = "结论要回扣题干中的待检离子。",
            HintSubQuestion = "溶液变红说明哪种离子存在？",
            HintClue = "说明原溶液中含有 Fe³⁺。",
            Scaffold = "现象：变红 → 结论：含 Fe³⁺。"
        });

        return p;
    }

    // ---------- 化学 2：Na₂CO₃ / NaHCO₃ 鉴别 ----------
    private static Problem BuildChemistryNaTest()
    {
        var p = new Problem
        {
            Id = "C-NA-01",
            Subject = Subject.Chemistry,
            Category = "ChemistryInference",
            Title = "碳酸钠与碳酸氢钠的鉴别",
            Stem = "有两瓶无色溶液，分别是 Na₂CO₃ 和 NaHCO₃，请用最简便的方法鉴别。",
            AnswerKey = "加稀盐酸，产生气体更快的是 NaHCO₃",
            Difficulty = 2
        };
        p.KnowledgeLinks.Add(new ProblemKnowledge { ProblemId = p.Id, KnowledgeId = "C-NA", IsPrimary = true });
        p.KnowledgeLinks.Add(new ProblemKnowledge { ProblemId = p.Id, KnowledgeId = "C-ION-REACTION" });

        p.Steps.Add(new ProblemStep
        {
            ProblemId = p.Id, StepIndex = 1,
            Prompt = "第一步：选择什么试剂进行鉴别？",
            ExpectedExpression = "稀盐酸",
            ExpectedKeywords = "盐酸/稀盐酸",
            KnowledgeId = "C-NA",
            HintSelfCheck = "两种物质都属于碳酸盐，找它们共同的反应对象。",
            HintSubQuestion = "碳酸盐遇酸会产生什么？",
            HintClue = "可用稀盐酸，观察产生气体的快慢。",
            Scaffold = "选稀盐酸 → 比较产气快慢 → 得结论。"
        });

        p.Steps.Add(new ProblemStep
        {
            ProblemId = p.Id, StepIndex = 2,
            Prompt = "第二步：两者产生气体的快慢有什么不同？",
            ExpectedExpression = "NaHCO3产生气体更快",
            ExpectedKeywords = "NaHCO3,更快/剧烈",
            KnowledgeId = "C-NA",
            IsKeyStep = true,
            HintSelfCheck = "比较相同条件下两者与酸反应的剧烈程度。",
            HintSubQuestion = "哪个物质中碳酸氢根直接与 H⁺ 反应？",
            HintClue = "NaHCO₃ 与盐酸反应更剧烈，产气更快。",
            Scaffold = "NaHCO₃ 产气更快 → 据此鉴别。"
        });

        p.Steps.Add(new ProblemStep
        {
            ProblemId = p.Id, StepIndex = 3,
            Prompt = "第三步：写出鉴别的最终结论。",
            ExpectedExpression = "产生气体更快的是NaHCO3",
            ExpectedKeywords = "更快,NaHCO3",
            KnowledgeId = "C-NA",
            IsKeyStep = true,
            HintSelfCheck = "结论要能区分两瓶溶液。",
            HintSubQuestion = "观察到产气更快的那瓶是什么？",
            HintClue = "产气更快的是 NaHCO₃，另一瓶是 Na₂CO₃。",
            Scaffold = "现象对应物质 → 写出结论。"
        });

        return p;
    }

    // ---------- 化学 3：离子共存 ----------
    private static Problem BuildChemistryCoexist()
    {
        var p = new Problem
        {
            Id = "C-COEXIST-01",
            Subject = Subject.Chemistry,
            Category = "ChemistryInference",
            Title = "酸性溶液中的离子共存",
            Stem = "在酸性溶液中，Fe²⁺、NO₃⁻、Cl⁻、Na⁺ 能否大量共存？请说明理由。",
            AnswerKey = "不能大量共存：酸性条件下 NO₃⁻ 具强氧化性，会氧化 Fe²⁺",
            Difficulty = 3
        };
        p.KnowledgeLinks.Add(new ProblemKnowledge { ProblemId = p.Id, KnowledgeId = "C-COEXIST", IsPrimary = true });
        p.KnowledgeLinks.Add(new ProblemKnowledge { ProblemId = p.Id, KnowledgeId = "C-REDOX" });
        p.KnowledgeLinks.Add(new ProblemKnowledge { ProblemId = p.Id, KnowledgeId = "C-ION-REACTION" });

        p.Steps.Add(new ProblemStep
        {
            ProblemId = p.Id, StepIndex = 1,
            Prompt = "第一步：酸性条件下，哪两种离子组合会产生强氧化性？",
            ExpectedExpression = "H+与NO3-相当于硝酸",
            ExpectedKeywords = "H+,NO3-,硝酸",
            KnowledgeId = "C-REDOX",
            HintSelfCheck = "先看题干给的限制条件：酸性意味着存在什么离子？",
            HintSubQuestion = "H⁺ 与 NO₃⁻ 同时存在时，相当于哪种常见强氧化剂？",
            HintClue = "H⁺ 与 NO₃⁻ 组合相当于硝酸，具有强氧化性。",
            Scaffold = "酸性 → H⁺ 存在 → 与 NO₃⁻ 组合成硝酸。"
        });

        p.Steps.Add(new ProblemStep
        {
            ProblemId = p.Id, StepIndex = 2,
            Prompt = "第二步：这个强氧化性组合对 Fe²⁺ 会产生什么影响？",
            ExpectedExpression = "将Fe2+氧化为Fe3+",
            ExpectedKeywords = "氧化,Fe2+,Fe3+",
            KnowledgeId = "C-COEXIST",
            IsKeyStep = true,
            HintSelfCheck = "Fe²⁺ 具有还原性，遇到强氧化剂会怎样？",
            HintSubQuestion = "Fe²⁺ 被氧化后变成什么离子？",
            HintClue = "Fe²⁺ 被氧化为 Fe³⁺。",
            Scaffold = "强氧化性组合 → 氧化 Fe²⁺ → 生成 Fe³⁺。"
        });

        p.Steps.Add(new ProblemStep
        {
            ProblemId = p.Id, StepIndex = 3,
            Prompt = "第三步：给出共存结论。",
            ExpectedExpression = "不能大量共存",
            ExpectedKeywords = "不能,共存",
            KnowledgeId = "C-COEXIST",
            IsKeyStep = true,
            HintSelfCheck = "有离子发生反应，还能大量共存吗？",
            HintSubQuestion = "发生氧化还原反应后，原离子还能同时大量存在吗？",
            HintClue = "因发生氧化还原反应，不能大量共存。",
            Scaffold = "存在反应 → 不能大量共存。"
        });

        return p;
    }
}
