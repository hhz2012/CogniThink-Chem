namespace CogniThink.TutorBrain.Application.Evaluation;

using CogniThink.TutorBrain.Application.Reasoning;
using CogniThink.TutorBrain.Domain;

/// <summary>单条测试样本</summary>
public sealed class JudgeTestCase
{
    public string ProblemId { get; set; } = string.Empty;
    public string ProblemTitle { get; set; } = string.Empty;
    public int StepIndex { get; set; }
    public string StepPrompt { get; set; } = string.Empty;
    public string StudentAnswer { get; set; } = string.Empty;

    /// <summary>标准答案（ground truth）</summary>
    public StepVerdict ExpectedVerdict { get; set; }

    /// <summary>该样本的备注（正确答案变体/常见错误等）</summary>
    public string Note { get; set; } = string.Empty;
}

/// <summary>单条样本的对比判定结果</summary>
public sealed class JudgeCaseResult
{
    public JudgeTestCase Case { get; set; } = null!;
    public StepVerdict EngineVerdict { get; set; }
    public StepVerdict LlmVerdict { get; set; }
    public string? LlmReason { get; set; }
    public bool EngineCorrect => EngineVerdict == Case.ExpectedVerdict;
    public bool LlmCorrect => LlmVerdict == Case.ExpectedVerdict;
}

/// <summary>对比评估汇总</summary>
public sealed class EngineVsLlmSummary
{
    public int TotalCases { get; set; }
    public int EngineCorrect { get; set; }
    public int LlmCorrect { get; set; }
    public int LlmUngradable { get; set; }
    public double EngineAccuracy => TotalCases > 0 ? (double)EngineCorrect / TotalCases : 0;
    public double LlmAccuracy => TotalCases > 0 ? (double)LlmCorrect / TotalCases : 0;

    /// <summary>引擎判对但大模型判错的样本</summary>
    public List<JudgeCaseResult> EngineOnlyCorrect { get; set; } = new();

    /// <summary>大模型判对但引擎判错的样本</summary>
    public List<JudgeCaseResult> LlmOnlyCorrect { get; set; } = new();

    /// <summary>两者都判错的样本</summary>
    public List<JudgeCaseResult> BothWrong { get; set; } = new();

    /// <summary>两者都判对的样本</summary>
    public List<JudgeCaseResult> BothCorrect { get; set; } = new();

    public bool LlmAvailable { get; set; }
}

/// <summary>
/// 化学规则引擎 vs 大模型 判定准确率对比评估服务。
///
/// 测试集构造原则：
/// - 从种子题目的标准答案出发，生成正确变体、部分正确变体、错误变体
/// - 以标准答案为 ground truth
/// - 分别用引擎和大模型判定，统计准确率
/// </summary>
public sealed class EngineVsLlmEvaluationService
{
    private readonly LlmJudgeService _llmJudge;

    public EngineVsLlmEvaluationService(LlmJudgeService llmJudge)
    {
        _llmJudge = llmJudge;
    }

    public bool LlmAvailable => _llmJudge.IsAvailable;

    /// <summary>
    /// 构造化学判定测试集。
    /// 基于种子数据中的题目和标准答案，生成正确/部分正确/错误答案变体。
    /// </summary>
    public List<JudgeTestCase> BuildTestCases()
    {
        var cases = new List<JudgeTestCase>();

        // ========== C-FE-01：Fe³⁺ 检验 ==========
        // 步骤1：选择试剂
        cases.Add(new JudgeTestCase
        {
            ProblemId = "C-FE-01", ProblemTitle = "Fe³⁺ 离子的检验",
            StepIndex = 1, StepPrompt = "选择什么试剂检验 Fe³⁺？",
            StudentAnswer = "KSCN", ExpectedVerdict = StepVerdict.Correct,
            Note = "标准答案"
        });
        cases.Add(new JudgeTestCase
        {
            ProblemId = "C-FE-01", ProblemTitle = "Fe³⁺ 离子的检验",
            StepIndex = 1, StepPrompt = "选择什么试剂检验 Fe³⁺？",
            StudentAnswer = "硫氰化钾溶液", ExpectedVerdict = StepVerdict.Correct,
            Note = "标准答案的同义表达"
        });
        cases.Add(new JudgeTestCase
        {
            ProblemId = "C-FE-01", ProblemTitle = "Fe³⁺ 离子的检验",
            StepIndex = 1, StepPrompt = "选择什么试剂检验 Fe³⁺？",
            StudentAnswer = "酚酞", ExpectedVerdict = StepVerdict.Incorrect,
            Note = "完全错误的试剂"
        });
        cases.Add(new JudgeTestCase
        {
            ProblemId = "C-FE-01", ProblemTitle = "Fe³⁺ 离子的检验",
            StepIndex = 1, StepPrompt = "选择什么试剂检验 Fe³⁺？",
            StudentAnswer = "KSCN溶液或NaOH", ExpectedVerdict = StepVerdict.PartiallyCorrect,
            Note = "主要试剂正确但多了无关试剂"
        });

        // 步骤2：现象
        cases.Add(new JudgeTestCase
        {
            ProblemId = "C-FE-01", ProblemTitle = "Fe³⁺ 离子的检验",
            StepIndex = 2, StepPrompt = "加入试剂后应观察到什么现象？",
            StudentAnswer = "溶液变红色", ExpectedVerdict = StepVerdict.Correct,
            Note = "标准答案"
        });
        cases.Add(new JudgeTestCase
        {
            ProblemId = "C-FE-01", ProblemTitle = "Fe³⁺ 离子的检验",
            StepIndex = 2, StepPrompt = "加入试剂后应观察到什么现象？",
            StudentAnswer = "变红", ExpectedVerdict = StepVerdict.Correct,
            Note = "标准答案简写"
        });
        cases.Add(new JudgeTestCase
        {
            ProblemId = "C-FE-01", ProblemTitle = "Fe³⁺ 离子的检验",
            StepIndex = 2, StepPrompt = "加入试剂后应观察到什么现象？",
            StudentAnswer = "无明显现象", ExpectedVerdict = StepVerdict.Incorrect,
            Note = "与事实相反的现象"
        });
        cases.Add(new JudgeTestCase
        {
            ProblemId = "C-FE-01", ProblemTitle = "Fe³⁺ 离子的检验",
            StepIndex = 2, StepPrompt = "加入试剂后应观察到什么现象？",
            StudentAnswer = "有颜色变化", ExpectedVerdict = StepVerdict.PartiallyCorrect,
            Note = "方向对但不具体"
        });

        // 步骤3：结论
        cases.Add(new JudgeTestCase
        {
            ProblemId = "C-FE-01", ProblemTitle = "Fe³⁺ 离子的检验",
            StepIndex = 3, StepPrompt = "由现象得出什么结论？",
            StudentAnswer = "含有Fe3+", ExpectedVerdict = StepVerdict.Correct,
            Note = "标准答案"
        });
        cases.Add(new JudgeTestCase
        {
            ProblemId = "C-FE-01", ProblemTitle = "Fe³⁺ 离子的检验",
            StepIndex = 3, StepPrompt = "由现象得出什么结论？",
            StudentAnswer = "不含Fe3+", ExpectedVerdict = StepVerdict.Incorrect,
            Note = "结论相反"
        });

        // ========== C-COEXIST-01：离子共存 ==========
        cases.Add(new JudgeTestCase
        {
            ProblemId = "C-COEXIST-01", ProblemTitle = "酸性条件下离子共存判断",
            StepIndex = 1, StepPrompt = "酸性条件下，哪两种离子组合会产生强氧化性？",
            StudentAnswer = "H+与NO3-相当于硝酸", ExpectedVerdict = StepVerdict.Correct,
            Note = "标准答案"
        });
        cases.Add(new JudgeTestCase
        {
            ProblemId = "C-COEXIST-01", ProblemTitle = "酸性条件下离子共存判断",
            StepIndex = 1, StepPrompt = "酸性条件下，哪两种离子组合会产生强氧化性？",
            StudentAnswer = "H+和NO3-形成硝酸有强氧化性", ExpectedVerdict = StepVerdict.Correct,
            Note = "标准答案的扩展表达"
        });
        cases.Add(new JudgeTestCase
        {
            ProblemId = "C-COEXIST-01", ProblemTitle = "酸性条件下离子共存判断",
            StepIndex = 1, StepPrompt = "酸性条件下，哪两种离子组合会产生强氧化性？",
            StudentAnswer = "Na+和Cl-", ExpectedVerdict = StepVerdict.Incorrect,
            Note = "完全错误的离子组合"
        });

        cases.Add(new JudgeTestCase
        {
            ProblemId = "C-COEXIST-01", ProblemTitle = "酸性条件下离子共存判断",
            StepIndex = 2, StepPrompt = "这个强氧化性组合对 Fe²⁺ 会产生什么影响？",
            StudentAnswer = "将Fe2+氧化为Fe3+", ExpectedVerdict = StepVerdict.Correct,
            Note = "标准答案"
        });
        cases.Add(new JudgeTestCase
        {
            ProblemId = "C-COEXIST-01", ProblemTitle = "酸性条件下离子共存判断",
            StepIndex = 2, StepPrompt = "这个强氧化性组合对 Fe²⁺ 会产生什么影响？",
            StudentAnswer = "不反应", ExpectedVerdict = StepVerdict.Incorrect,
            Note = "与氧化还原事实矛盾"
        });
        cases.Add(new JudgeTestCase
        {
            ProblemId = "C-COEXIST-01", ProblemTitle = "酸性条件下离子共存判断",
            StepIndex = 2, StepPrompt = "这个强氧化性组合对 Fe²⁺ 会产生什么影响？",
            StudentAnswer = "氧化Fe2+", ExpectedVerdict = StepVerdict.PartiallyCorrect,
            Note = "方向对但缺产物"
        });

        cases.Add(new JudgeTestCase
        {
            ProblemId = "C-COEXIST-01", ProblemTitle = "酸性条件下离子共存判断",
            StepIndex = 3, StepPrompt = "给出共存结论。",
            StudentAnswer = "不能大量共存", ExpectedVerdict = StepVerdict.Correct,
            Note = "标准答案"
        });
        cases.Add(new JudgeTestCase
        {
            ProblemId = "C-COEXIST-01", ProblemTitle = "酸性条件下离子共存判断",
            StepIndex = 3, StepPrompt = "给出共存结论。",
            StudentAnswer = "可以大量共存", ExpectedVerdict = StepVerdict.Incorrect,
            Note = "结论相反"
        });

        // ========== C-NA-01：Na2CO3与NaHCO3鉴别 ==========
        cases.Add(new JudgeTestCase
        {
            ProblemId = "C-NA-01", ProblemTitle = "Na₂CO₃ 与 NaHCO₃ 的鉴别",
            StepIndex = 1, StepPrompt = "选择什么试剂进行鉴别？",
            StudentAnswer = "稀盐酸", ExpectedVerdict = StepVerdict.Correct,
            Note = "标准答案"
        });
        cases.Add(new JudgeTestCase
        {
            ProblemId = "C-NA-01", ProblemTitle = "Na₂CO₃ 与 NaHCO₃ 的鉴别",
            StepIndex = 1, StepPrompt = "选择什么试剂进行鉴别？",
            StudentAnswer = "盐酸", ExpectedVerdict = StepVerdict.Correct,
            Note = "标准答案简写"
        });
        cases.Add(new JudgeTestCase
        {
            ProblemId = "C-NA-01", ProblemTitle = "Na₂CO₃ 与 NaHCO₃ 的鉴别",
            StepIndex = 1, StepPrompt = "选择什么试剂进行鉴别？",
            StudentAnswer = "水", ExpectedVerdict = StepVerdict.Incorrect,
            Note = "错误试剂"
        });

        cases.Add(new JudgeTestCase
        {
            ProblemId = "C-NA-01", ProblemTitle = "Na₂CO₃ 与 NaHCO₃ 的鉴别",
            StepIndex = 2, StepPrompt = "两者产生气体的快慢有什么不同？",
            StudentAnswer = "NaHCO3产生气体更快", ExpectedVerdict = StepVerdict.Correct,
            Note = "标准答案"
        });
        cases.Add(new JudgeTestCase
        {
            ProblemId = "C-NA-01", ProblemTitle = "Na₂CO₃ 与 NaHCO₃ 的鉴别",
            StepIndex = 2, StepPrompt = "两者产生气体的快慢有什么不同？",
            StudentAnswer = "NaHCO3更剧烈", ExpectedVerdict = StepVerdict.Correct,
            Note = "同义词替换，正确"
        });

        return cases;
    }

    /// <summary>
    /// 运行对比评估：引擎判定 + 大模型判定，统计准确率。
    /// </summary>
    public async Task<EngineVsLlmSummary> EvaluateAsync(CancellationToken ct = default)
    {
        var cases = BuildTestCases();
        var results = new List<JudgeCaseResult>();

        foreach (var c in cases)
        {
            // 引擎判定
            var step = new ProblemStep
            {
                ExpectedExpression = GetExpectedExpression(c.ProblemId, c.StepIndex),
                ExpectedKeywords = GetExpectedKeywords(c.ProblemId, c.StepIndex),
                DistractorKeywords = GetDistractorKeywords(c.ProblemId, c.StepIndex),
                VagueKeywords = GetVagueKeywords(c.ProblemId, c.StepIndex)
            };
            var engineEval = ChemistryReasoningEngine.Evaluate(step, c.StudentAnswer);

            // 大模型判定
            var llmResult = await _llmJudge.JudgeAsync(
                c.ProblemTitle, c.StepPrompt, c.StudentAnswer, ct);

            results.Add(new JudgeCaseResult
            {
                Case = c,
                EngineVerdict = engineEval.Verdict,
                LlmVerdict = llmResult.Verdict,
                LlmReason = llmResult.Reason
            });
        }

        return Summarize(results);
    }

    private static EngineVsLlmSummary Summarize(List<JudgeCaseResult> results)
    {
        var summary = new EngineVsLlmSummary
        {
            TotalCases = results.Count,
            LlmAvailable = results.Any(r => r.LlmVerdict != StepVerdict.Ungradable)
        };

        foreach (var r in results)
        {
            if (r.EngineCorrect) summary.EngineCorrect++;
            if (r.LlmVerdict == StepVerdict.Ungradable) summary.LlmUngradable++;
            else if (r.LlmCorrect) summary.LlmCorrect++;

            if (r.EngineCorrect && r.LlmCorrect) summary.BothCorrect.Add(r);
            else if (r.EngineCorrect && !r.LlmCorrect) summary.EngineOnlyCorrect.Add(r);
            else if (!r.EngineCorrect && r.LlmCorrect) summary.LlmOnlyCorrect.Add(r);
            else summary.BothWrong.Add(r);
        }

        return summary;
    }

    // 测试集对应的标准答案（与 SeedData 一致）
    private static string GetExpectedExpression(string problemId, int stepIndex)
    {
        return (problemId, stepIndex) switch
        {
            ("C-FE-01", 1) => "KSCN",
            ("C-FE-01", 2) => "溶液变红色",
            ("C-FE-01", 3) => "含Fe3+",
            ("C-COEXIST-01", 1) => "H+与NO3-相当于硝酸",
            ("C-COEXIST-01", 2) => "将Fe2+氧化为Fe3+",
            ("C-COEXIST-01", 3) => "不能大量共存",
            ("C-NA-01", 1) => "稀盐酸",
            ("C-NA-01", 2) => "NaHCO3产生气体更快",
            ("C-NA-01", 3) => "产生气体更快的是NaHCO3",
            _ => ""
        };
    }

    private static string GetExpectedKeywords(string problemId, int stepIndex)
    {
        return (problemId, stepIndex) switch
        {
            ("C-FE-01", 1) => "KSCN/硫氰化钾",
            ("C-FE-01", 2) => "变红/红色",
            ("C-FE-01", 3) => "Fe3+/铁离子,含有",
            ("C-COEXIST-01", 1) => "H+,NO3-,硝酸",
            ("C-COEXIST-01", 2) => "氧化,Fe2+,Fe3+",
            ("C-COEXIST-01", 3) => "不能,共存",
            ("C-NA-01", 1) => "盐酸/稀盐酸",
            ("C-NA-01", 2) => "NaHCO3,更快/剧烈",
            ("C-NA-01", 3) => "更快,NaHCO3",
            _ => ""
        };
    }

    private static string? GetDistractorKeywords(string problemId, int stepIndex)
    {
        return (problemId, stepIndex) switch
        {
            // Fe³⁺检验的标准试剂是KSCN，NaOH是干扰项（生成沉淀，非特征检验）
            ("C-FE-01", 1) => "NaOH/氢氧化钠",
            _ => null
        };
    }

    private static string? GetVagueKeywords(string problemId, int stepIndex)
    {
        return (problemId, stepIndex) switch
        {
            // "颜色变化"方向对但太笼统，未指明具体颜色
            ("C-FE-01", 2) => "颜色变化",
            _ => null
        };
    }
}
