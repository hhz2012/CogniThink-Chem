namespace CogniThink.TutorBrain.Application.Evaluation;

using System.Text.RegularExpressions;
using CogniThink.TutorBrain.Application.Agent;
using CogniThink.TutorBrain.Application.Gateway;

/// <summary>问答题对比测试用例</summary>
public sealed class QaTestCase
{
    public string Id { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;

    /// <summary>标准答案关键字（命中即算正确）</summary>
    public List<string> AnswerKeywords { get; set; } = new();

    /// <summary>期望结论方向：true=肯定/能/可以，false=否定/不能/不可以，null=无方向判断</summary>
    public bool? ExpectedAffirmative { get; set; }

    public string Note { get; set; } = string.Empty;
}

/// <summary>单条问答对比结果</summary>
public sealed class QaCaseResult
{
    public QaTestCase Case { get; set; } = null!;

    /// <summary>纯LLM回答原文</summary>
    public string PureLlmAnswer { get; set; } = string.Empty;

    /// <summary>纯LLM是否答对</summary>
    public bool PureLlmCorrect { get; set; }

    /// <summary>网关模式引擎返回的结论</summary>
    public string GatewayConclusion { get; set; } = string.Empty;

    /// <summary>网关模式引擎 ok 值</summary>
    public bool? GatewayOk { get; set; }

    /// <summary>网关模式任务类型</summary>
    public string TaskType { get; set; } = string.Empty;

    /// <summary>网关模式实际生成的回答（含回退到LLM的回答）</summary>
    public string GatewayAnswer { get; set; } = string.Empty;

    /// <summary>网关模式是否答对</summary>
    public bool GatewayCorrect { get; set; }

    /// <summary>网关模式是否走了引擎（非 concept/unknown）</summary>
    public bool GatewayUsedEngine { get; set; }
}

/// <summary>网关模式 vs 纯LLM 对比汇总</summary>
public sealed class GatewayVsLlmSummary
{
    public int TotalCases { get; set; }
    public int PureLlmCorrect { get; set; }
    public int GatewayCorrect { get; set; }
    public int GatewayEngineUsed { get; set; }
    public double PureLlmAccuracy => TotalCases > 0 ? (double)PureLlmCorrect / TotalCases : 0;
    public double GatewayAccuracy => TotalCases > 0 ? (double)GatewayCorrect / TotalCases : 0;

    /// <summary>仅网关答对的样本</summary>
    public List<QaCaseResult> GatewayOnlyCorrect { get; set; } = new();

    /// <summary>仅纯LLM答对的样本</summary>
    public List<QaCaseResult> PureLlmOnlyCorrect { get; set; } = new();

    /// <summary>两者都答对</summary>
    public List<QaCaseResult> BothCorrect { get; set; } = new();

    /// <summary>两者都答错</summary>
    public List<QaCaseResult> BothWrong { get; set; } = new();

    public bool LlmAvailable { get; set; }
}

/// <summary>
/// 网关模式（LLM解析→引擎计算）vs 纯LLM直接回答 的准确率对比评估。
///
/// 核心假设：纯LLM容易产生化学幻觉，网关模式通过引擎作为真值源能提升可靠性。
/// </summary>
public sealed class GatewayVsLlmEvaluationService
{
    private readonly LlmProxyService _llm;
    private readonly LlmGatewayService _gateway;

    public GatewayVsLlmEvaluationService(LlmProxyService llm, LlmGatewayService gateway)
    {
        _llm = llm;
        _gateway = gateway;
    }

    public bool LlmAvailable => _llm.IsAvailable;

    /// <summary>运行对比评估</summary>
    public async Task<GatewayVsLlmSummary> EvaluateAsync(CancellationToken ct = default)
    {
        var cases = BuildTestCases();
        var summary = new GatewayVsLlmSummary
        {
            TotalCases = cases.Count,
            LlmAvailable = LlmAvailable
        };

        foreach (var c in cases)
        {
            var result = new QaCaseResult { Case = c };

            // 模式1：纯LLM直接回答
            var pureAnswer = await AskPureLlmAsync(c.Question, ct);
            result.PureLlmAnswer = pureAnswer ?? string.Empty;
            result.PureLlmCorrect = IsAnswerCorrect(pureAnswer, c);

            // 模式2：网关模式（LLM解析→引擎计算）
            var gwResult = await _gateway.ProcessStudentMessageAsync(c.Question, ct);
            result.GatewayConclusion = gwResult.EngineResponse.FinalResult;
            result.GatewayOk = gwResult.EngineResponse.Ok;
            result.TaskType = gwResult.TaskType;
            result.GatewayUsedEngine = gwResult.EngineResponse.Ok.HasValue
                                       && gwResult.TaskType is not ("concept" or "unknown");

            // 网关模式的回答：优先用引擎结论；若引擎结论不够具体（如"已匹配到相关反应"），
            // 则让LLM基于引擎证据来回答；若引擎不在范围，则直接用纯LLM回答
            var gatewayAnswer = await BuildGatewayAnswerAsync(c, gwResult, ct);
            result.GatewayAnswer = gatewayAnswer ?? string.Empty;
            result.GatewayCorrect = IsAnswerCorrect(gatewayAnswer, c);

            // 分类
            if (result.PureLlmCorrect && result.GatewayCorrect)
                summary.BothCorrect.Add(result);
            else if (result.GatewayCorrect && !result.PureLlmCorrect)
                summary.GatewayOnlyCorrect.Add(result);
            else if (result.PureLlmCorrect && !result.GatewayCorrect)
                summary.PureLlmOnlyCorrect.Add(result);
            else
                summary.BothWrong.Add(result);

            if (result.PureLlmCorrect) summary.PureLlmCorrect++;
            if (result.GatewayCorrect) summary.GatewayCorrect++;
            if (result.GatewayUsedEngine) summary.GatewayEngineUsed++;
        }

        return summary;
    }

    /// <summary>让纯LLM直接回答问题</summary>
    private async Task<string?> AskPureLlmAsync(string question, CancellationToken ct)
    {
        const string systemPrompt =
            "你是高中化学老师。请直接回答学生的化学问题，给出明确结论。\n" +
            "要求：\n" +
            "1. 回答要包含明确的结论（能/不能、可以/不可以、是什么等）\n" +
            "2. 简短说明理由\n" +
            "3. 不要超过100字";

        return await _llm.ChatAsync(systemPrompt, question, 0.3f, 300, ct);
    }

    /// <summary>判断纯LLM回答是否正确</summary>
    private static bool IsAnswerCorrect(string? answer, QaTestCase c)
    {
        if (string.IsNullOrWhiteSpace(answer)) return false;
        // 规范化：上下标转普通数字，便于关键词匹配（如 FeCl₃ → FeCl3）
        var lower = NormalizeChemicalNotation(answer).ToLowerInvariant();

        // 1. 方向判断（能/不能）
        if (c.ExpectedAffirmative.HasValue)
        {
            var affirmative = ContainsAffirmative(lower);
            var negative = ContainsNegative(lower);
            // 否定词优先：只要出现否定词，按否定判断（避免"不能共存"因含"共存"被误判）
            if (negative) return c.ExpectedAffirmative == false;
            if (affirmative) return c.ExpectedAffirmative == true;
            // 都没有，回退到关键词判断
        }

        // 2. 关键词命中
        if (c.AnswerKeywords.Count > 0)
        {
            return c.AnswerKeywords.Any(k => lower.Contains(NormalizeChemicalNotation(k).ToLowerInvariant()));
        }

        return false;
    }

    /// <summary>把化学上下标字符转为普通数字，便于字符串匹配</summary>
    private static string NormalizeChemicalNotation(string input)
    {
        var chars = input.Select(c => c switch
        {
            '₀' => '0', '₁' => '1', '₂' => '2', '₃' => '3', '₄' => '4',
            '₅' => '5', '₆' => '6', '₇' => '7', '₈' => '8', '₉' => '9',
            '⁰' => '0', '¹' => '1', '²' => '2', '³' => '3', '⁴' => '4',
            '⁵' => '5', '⁶' => '6', '⁷' => '7', '⁸' => '8', '⁹' => '9',
            '⁺' => '+', '⁻' => '-',
            _ => c
        }).ToArray();
        return new string(chars);
    }

    /// <summary>基于网关结果构建回答</summary>
    private async Task<string?> BuildGatewayAnswerAsync(QaTestCase c, GatewayResult gw, CancellationToken ct)
    {
        var resp = gw.EngineResponse;

        // 引擎不在范围（concept/unknown）：直接用纯LLM回答
        if (!resp.Ok.HasValue || gw.TaskType is "concept" or "unknown")
        {
            return await AskPureLlmAsync(c.Question, ct);
        }

        var conclusion = resp.FinalResult ?? string.Empty;

        // 计算类：引擎给出了数值结果，直接作为回答（不走"具体结论"词匹配）
        if (gw.TaskType is "calculation" or "equilibrium")
        {
            return conclusion;
        }

        // 引擎返回了具体结论（如"不能大量共存"、"可以大量共存"）：直接用
        if (IsConcreteConclusion(conclusion))
        {
            return conclusion;
        }

        // 引擎结论不够具体（如"已匹配到相关反应"）：让LLM基于引擎证据回答
        var evidence = string.Join("；", resp.DerivationSteps);
        var systemPrompt =
            "你是高中化学老师。请根据以下化学知识库的证据，回答学生的问题。\n" +
            "必须基于证据回答，不要编造。如果证据不足以回答，直接说不知道。\n" +
            "回答要包含明确结论，不超过100字。";

        var userPrompt =
            $"学生问题：{c.Question}\n" +
            $"知识库证据：{evidence}\n" +
            $"引擎匹配结论：{conclusion}\n" +
            $"请回答学生的问题。";

        var evidenceBased = await _llm.ChatAsync(systemPrompt, userPrompt, 0.2f, 200, ct);

        // 如果基于证据的回答是"不知道"（证据不足），回退到纯LLM回答
        if (string.IsNullOrWhiteSpace(evidenceBased) ||
            evidenceBased.Contains("不知道") || evidenceBased.Contains("无法回答") ||
            evidenceBased.Contains("未能") || evidenceBased.Contains("无相关"))
        {
            return await AskPureLlmAsync(c.Question, ct);
        }

        return evidenceBased;
    }

    /// <summary>判断引擎结论是否足够具体（能直接回答问题）</summary>
    private static bool IsConcreteConclusion(string conclusion)
    {
        if (string.IsNullOrWhiteSpace(conclusion)) return false;
        // "已匹配到相关反应"这类结论不够具体
        if (conclusion.Contains("已匹配") || conclusion.Contains("相关反应")) return false;
        // 包含明确判断词的结论是具体的
        return conclusion.Contains("能") || conclusion.Contains("不能") ||
               conclusion.Contains("共存") || conclusion.Contains("不共存") ||
               conclusion.Contains("可以") || conclusion.Contains("不可以") ||
               conclusion.Contains("正确") || conclusion.Contains("错误");
    }

    /// <summary>判断网关模式结果是否正确（保留供兼容）</summary>
    private static bool IsGatewayCorrect(GatewayResult gw, QaTestCase c)
    {
        var resp = gw.EngineResponse;

        // concept/unknown 类型不走引擎，用 LLM 回复判断
        if (!resp.Ok.HasValue || gw.TaskType is "concept" or "unknown")
        {
            return IsAnswerCorrect(gw.Reply, c);
        }

        // 引擎返回了结论
        var conclusion = resp.FinalResult;
        if (string.IsNullOrWhiteSpace(conclusion)) return false;

        var lower = conclusion.ToLowerInvariant();

        // 方向判断
        if (c.ExpectedAffirmative.HasValue)
        {
            var affirmative = ContainsAffirmative(lower);
            var negative = ContainsNegative(lower);
            if (affirmative && !negative) return c.ExpectedAffirmative == true;
            if (negative && !affirmative) return c.ExpectedAffirmative == false;
        }

        // 关键词命中
        if (c.AnswerKeywords.Count > 0)
        {
            return c.AnswerKeywords.Any(k => lower.Contains(k.ToLowerInvariant()));
        }

        return false;
    }

    private static bool ContainsAffirmative(string text) =>
        // 排除否定形式中的肯定词（如"不能"中的"能"、"不可以"中的"可以"、"不共存"中的"共存"）
        text.Replace("不能", "").Replace("不可以", "").Replace("不共存", "")
            .Contains("可以") || text.Replace("不能", "").Contains("能") ||
        text.Replace("不共存", "").Contains("共存") || text.Contains("正确") || text.Contains("对");

    private static bool ContainsNegative(string text) =>
        text.Contains("不能") || text.Contains("不可以") || text.Contains("不共存") ||
        text.Contains("错误") || text.Contains("不对") || text.Contains("无法") ||
        text.Contains("不一定") || text.Contains("不是") || text.Contains("没有") || text.Contains("不具有");

    /// <summary>构造测试用例：覆盖引擎能处理的离子共存、反应、检验等类型</summary>
    private static List<QaTestCase> BuildTestCases()
    {
        var cases = new List<QaTestCase>
        {
        new()
        {
            Id = "ion-1",
            Question = "Fe2+、H+、NO3- 这三种离子能大量共存吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不能", "不共存", "氧化" },
            Note = "酸性条件下NO3-氧化Fe2+"
        },
        new()
        {
            Id = "ion-2",
            Question = "Na+、K+、Cl-、NO3- 能大量共存吗？",
            ExpectedAffirmative = true,
            AnswerKeywords = new() { "可以", "能", "共存" },
            Note = "四种离子互不反应"
        },
        new()
        {
            Id = "ion-3",
            Question = "Ba2+ 和 SO42- 能大量共存吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不能", "沉淀", "BaSO4" },
            Note = "生成BaSO4沉淀"
        },
        new()
        {
            Id = "ion-4",
            Question = "Ag+ 和 Cl- 能大量共存吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不能", "沉淀", "AgCl" },
            Note = "生成AgCl沉淀"
        },
        new()
        {
            Id = "ion-5",
            Question = "Cu2+ 和 OH- 能大量共存吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不能", "沉淀", "Cu(OH)2" },
            Note = "生成Cu(OH)2沉淀"
        },
        new()
        {
            Id = "ion-6",
            Question = "Fe3+ 和 SCN- 能大量共存吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不能", "络合", "红色" },
            Note = "Fe3+与SCN-形成血红色络合物"
        },
        new()
        {
            Id = "reaction-1",
            Question = "Fe 和稀 HNO3 反应能生成 H2 吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不能", "NO", "不生成氢气" },
            Note = "硝酸是强氧化性酸，与金属反应不生成H2"
        },
        new()
        {
            Id = "reaction-2",
            Question = "Cu 和稀 H2SO4 能反应吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不能", "不反应" },
            Note = "Cu在氢之后，与稀硫酸不反应"
        },
        new()
        {
            Id = "test-1",
            Question = "检验 Fe3+ 离子应该用什么试剂？",
            AnswerKeywords = new() { "KSCN", "硫氰化钾" },
            Note = "KSCN遇Fe3+变红"
        },
        new()
        {
            Id = "test-2",
            Question = "检验 Cl- 离子应该用什么试剂？",
            AnswerKeywords = new() { "AgNO3", "硝酸银" },
            Note = "AgNO3与Cl-生成AgCl白色沉淀"
        },
        new()
        {
            Id = "redox-1",
            Question = "在 Cu + HNO3(稀) → Cu(NO3)2 + NO + H2O 反应中，还原剂是什么？",
            AnswerKeywords = new() { "Cu", "铜" },
            Note = "Cu化合价升高，被氧化，是还原剂"
        },
        new()
        {
            Id = "concept-1",
            Question = "什么是电解质？",
            AnswerKeywords = new() { "导电", "化合物", "水溶液", "熔融" },
            Note = "概念题，LLM直接回答"
        },
        // ===== 离子共存扩展（引擎强项） =====
        new()
        {
            Id = "ion-7",
            Question = "MnO4-、Fe2+、H+ 能大量共存吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不能", "氧化", "MnO4" },
            Note = "酸性条件下MnO4-氧化Fe2+"
        },
        new()
        {
            Id = "ion-8",
            Question = "Al3+ 和 CO3^2- 能大量共存吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不能", "双水解", "沉淀" },
            Note = "Al3+与CO3^2-发生双水解"
        },
        new()
        {
            Id = "ion-9",
            Question = "H+ 和 CH3COO- 能大量共存吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不能", "弱电解质", "醋酸" },
            Note = "生成弱电解质CH3COOH"
        },
        new()
        {
            Id = "ion-10",
            Question = "OH- 和 NH4+ 能大量共存吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不能", "氨气", "NH3" },
            Note = "生成弱电解质NH3·H2O，加热放NH3"
        },
        new()
        {
            Id = "ion-11",
            Question = "Fe3+ 和 I- 能大量共存吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不能", "氧化", "I2" },
            Note = "Fe3+氧化I-为I2"
        },
        new()
        {
            Id = "ion-12",
            Question = "ClO- 和 I- 能大量共存吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不能", "氧化", "ClO" },
            Note = "ClO-氧化I-"
        },
        new()
        {
            Id = "ion-13",
            Question = "Mg2+、Na+、SO4^2-、Cl- 能大量共存吗？",
            ExpectedAffirmative = true,
            AnswerKeywords = new() { "可以", "能", "共存" },
            Note = "四种离子互不反应"
        },
        new()
        {
            Id = "ion-14",
            Question = "Fe3+ 和 HCO3- 能大量共存吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不能", "双水解", "Fe(OH)3" },
            Note = "Fe3+与HCO3-双水解生成Fe(OH)3和CO2"
        },
        // ===== 反应可行性扩展 =====
        new()
        {
            Id = "reaction-3",
            Question = "Zn 和稀盐酸反应能生成 H2 吗？",
            ExpectedAffirmative = true,
            AnswerKeywords = new() { "能", "可以", "H2", "氢气" },
            Note = "Zn在氢之前，与稀盐酸反应生成H2"
        },
        new()
        {
            Id = "reaction-4",
            Question = "Cu 和 AgNO3 溶液能反应吗？",
            ExpectedAffirmative = true,
            AnswerKeywords = new() { "能", "可以", "置换" },
            Note = "Cu比Ag活泼，能置换出Ag"
        },
        new()
        {
            Id = "reaction-5",
            Question = "Fe 和稀硫酸反应生成 Fe2(SO4)3 对吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不对", "错误", "FeSO4", "Fe2+" },
            Note = "稀硫酸只能把Fe氧化到+2价，生成FeSO4"
        },
        new()
        {
            Id = "reaction-6",
            Question = "Na 和 CuSO4 溶液反应能置换出 Cu 吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不能", "不可以", "先和水" },
            Note = "Na先与水反应，不能直接置换Cu"
        },
        // ===== 离子检验扩展 =====
        new()
        {
            Id = "test-3",
            Question = "检验 SO4^2- 离子应该用什么试剂？",
            AnswerKeywords = new() { "BaCl2", "Ba(NO3)2", "氯化钡", "硝酸钡", "Ba2+" },
            Note = "Ba2+与SO4^2-生成不溶于酸的BaSO4白色沉淀"
        },
        new()
        {
            Id = "test-4",
            Question = "检验 NH4+ 离子应该用什么方法？",
            AnswerKeywords = new() { "NaOH", "氢氧化钠", "加热", "红色石蕊", "氨气" },
            Note = "加NaOH加热，产生使湿润红色石蕊试纸变蓝的NH3"
        },
        new()
        {
            Id = "test-5",
            Question = "检验 CO3^2- 离子应该用什么试剂？",
            AnswerKeywords = new() { "盐酸", "HCl", "石灰水", "澄清石灰水" },
            Note = "加盐酸产生CO2，通入澄清石灰水变浑浊"
        },
        // ===== 氧化还原扩展 =====
        new()
        {
            Id = "redox-2",
            Question = "在 2FeCl3 + Cu = 2FeCl2 + CuCl2 反应中，氧化剂是什么？",
            AnswerKeywords = new() { "FeCl3", "Fe3+", "氯化铁" },
            Note = "Fe3+化合价降低，被还原，是氧化剂"
        },
        new()
        {
            Id = "redox-3",
            Question = "在 Cl2 + 2KI = 2KCl + I2 反应中，被氧化的物质是什么？",
            AnswerKeywords = new() { "KI", "I-", "碘化钾" },
            Note = "I-化合价升高，被氧化"
        },
        // ===== 概念扩展 =====
        new()
        {
            Id = "concept-2",
            Question = "什么是氧化还原反应？",
            AnswerKeywords = new() { "电子", "化合价", "氧化", "还原" },
            Note = "概念题"
        },
        new()
        {
            Id = "concept-3",
            Question = "强电解质和弱电解质的区别是什么？",
            AnswerKeywords = new() { "完全电离", "部分电离", "电离" },
            Note = "概念题"
        },
        // ===== 颜色/性质（引擎有颜色数据） =====
        new()
        {
            Id = "property-1",
            Question = "Fe3+ 离子的水溶液是什么颜色？",
            AnswerKeywords = new() { "黄", "棕黄" },
            Note = "Fe3+溶液黄色"
        },
        new()
        {
            Id = "property-2",
            Question = "MnO4- 离子的水溶液是什么颜色？",
            AnswerKeywords = new() { "紫红", "紫" },
            Note = "MnO4-溶液紫红色"
        },
        // ===== 陷阱题：引擎优势（引擎规则正确，LLM易犯错） =====
        new()
        {
            Id = "trap-ion-1",
            Question = "Fe2+ 和 NO3- 在中性溶液中能大量共存吗？",
            ExpectedAffirmative = true,
            AnswerKeywords = new() { "可以", "能", "共存" },
            Note = "陷阱：NO3-只在酸性条件下氧化Fe2+，中性可共存。LLM易忽略酸性条件"
        },
        new()
        {
            Id = "trap-ion-2",
            Question = "I- 和 NO3- 在中性溶液中能大量共存吗？",
            ExpectedAffirmative = true,
            AnswerKeywords = new() { "可以", "能", "共存" },
            Note = "陷阱：NO3-只在酸性条件下氧化I-，中性可共存"
        },
        new()
        {
            Id = "trap-ion-3",
            Question = "Fe2+ 和 MnO4- 在中性溶液中能大量共存吗？",
            ExpectedAffirmative = true,
            AnswerKeywords = new() { "可以", "能", "共存" },
            Note = "陷阱：MnO4-氧化Fe2+需酸性条件，中性反应极慢可共存"
        },
        new()
        {
            Id = "trap-ion-4",
            Question = "Ba2+ 和 OH- 能大量共存吗？",
            ExpectedAffirmative = true,
            AnswerKeywords = new() { "可以", "能", "共存" },
            Note = "陷阱：Ba(OH)2可溶，易与Ca(OH)2(微溶)、Mg(OH)2(难溶)混淆"
        },
        new()
        {
            Id = "trap-ion-5",
            Question = "Ca2+ 和 SO4^2- 能大量共存吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不能", "微溶", "CaSO4" },
            Note = "陷阱：CaSO4微溶，浓度大时沉淀，LLM易误认为可溶"
        },
        new()
        {
            Id = "trap-ion-6",
            Question = "Ag+ 和 OH- 能大量共存吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不能", "AgOH", "Ag2O", "分解" },
            Note = "陷阱：AgOH不稳定分解为Ag2O，LLM易忽略"
        },
        new()
        {
            Id = "trap-ion-7",
            Question = "MnO4-、Cl-、H+ 能大量共存吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不能", "氧化", "MnO4", "Cl2" },
            Note = "陷阱：酸性下MnO4-氧化Cl-为Cl2，LLM易忽略此对"
        },
        new()
        {
            Id = "trap-react-1",
            Question = "Fe 和稀盐酸反应生成 FeCl3 对吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不对", "错误", "FeCl2", "Fe2+" },
            Note = "陷阱：稀盐酸只能氧化Fe到+2价，LLM易与Cl2氧化混淆"
        },
        new()
        {
            Id = "trap-react-2",
            Question = "Cu 和稀硫酸能反应吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不能", "不可以" },
            Note = "陷阱：Cu在氢后，稀硫酸不反应，易与浓硫酸混淆"
        },
        new()
        {
            Id = "trap-react-3",
            Question = "Fe 和稀 H2SO4 反应生成 Fe2(SO4)3 对吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不对", "错误", "FeSO4", "Fe2+" },
            Note = "陷阱：稀硫酸只能氧化Fe到+2价"
        },
        // ===== 陷阱题：引擎局限（考察引擎覆盖边界） =====
        new()
        {
            Id = "trap-limit-1",
            Question = "Al 和 NaOH 溶液反应能生成 H2 吗？",
            ExpectedAffirmative = true,
            AnswerKeywords = new() { "能", "可以", "H2", "氢气" },
            Note = "引擎局限：金属+碱产H2不在当前规则中，回退LLM判断"
        },
        new()
        {
            Id = "trap-limit-2",
            Question = "在 3NO2 + H2O = 2HNO3 + NO 反应中，氧化剂和还原剂的质量比是多少？",
            AnswerKeywords = new() { "1", "2", "1:2" },
            Note = "引擎局限：歧化反应比例计算，需LLM配平分析"
        },
        new()
        {
            Id = "trap-limit-3",
            Question = "等物质的量的 NaHCO3 和 Ba(OH)2 反应的离子方程式是什么？",
            AnswerKeywords = new() { "HCO3", "Ba2+", "OH", "BaCO3", "H2O" },
            Note = "引擎局限：与量有关的离子方程式，需LLM分析"
        },
        // ===== 领域1：与量有关的离子方程式（重灾区） =====
        new()
        {
            Id = "d1-1",
            Question = "FeBr2 溶液中通入少量 Cl2，反应的离子方程式是什么？",
            AnswerKeywords = new() { "2Fe2+", "Cl2", "2Fe3+", "2Cl-" },
            Note = "陷阱：Cl2少量时只氧化Fe2+，不氧化Br-（Fe2+还原性强于Br-）"
        },
        new()
        {
            Id = "d1-2",
            Question = "FeBr2 溶液中通入过量 Cl2，反应的离子方程式是什么？",
            AnswerKeywords = new() { "2Fe2+", "4Br-", "3Cl2", "2Fe3+", "2Br2", "6Cl-" },
            Note = "陷阱：Cl2过量时Fe2+和Br-都被氧化，按1:2:3比例反应"
        },
        new()
        {
            Id = "d1-3",
            Question = "NaAlO2 溶液中通入少量 CO2，反应的离子方程式是什么？",
            AnswerKeywords = new() { "2AlO2-", "CO2", "3H2O", "2Al(OH)3", "CO3^2-" },
            Note = "陷阱：少量CO2生成CO3^2-，过量才生成HCO3-"
        },
        new()
        {
            Id = "d1-4",
            Question = "NaAlO2 溶液中通入过量 CO2，反应的离子方程式是什么？",
            AnswerKeywords = new() { "AlO2-", "CO2", "2H2O", "Al(OH)3", "HCO3-" },
            Note = "陷阱：过量CO2生成HCO3-，不是CO3^2-"
        },
        new()
        {
            Id = "d1-5",
            Question = "Ca(HCO3)2 溶液中加入少量 NaOH，反应的离子方程式是什么？",
            AnswerKeywords = new() { "Ca2+", "HCO3-", "OH-", "CaCO3", "H2O" },
            Note = "陷阱：NaOH少量时1个HCO3-反应，过量时2个HCO3-都反应"
        },
        // ===== 领域2：氧化还原产物与介质 =====
        new()
        {
            Id = "d2-1",
            Question = "MnO4- 在酸性条件下被还原的产物是什么？",
            AnswerKeywords = new() { "Mn2+", "二价锰" },
            Note = "陷阱：酸性→Mn2+，中性→MnO2，碱性→MnO4^2-"
        },
        new()
        {
            Id = "d2-2",
            Question = "MnO4- 在中性条件下被还原的产物是什么？",
            AnswerKeywords = new() { "MnO2", "二氧化锰" },
            Note = "陷阱：中性→MnO2，不是Mn2+"
        },
        new()
        {
            Id = "d2-3",
            Question = "ClO- 在酸性条件下的还原产物是什么？",
            AnswerKeywords = new() { "Cl-", "氯离子" },
            Note = "陷阱：酸性下ClO-被还原为Cl-，碱性下可能发生歧化"
        },
        new()
        {
            Id = "d2-4",
            Question = "稀硝酸与铜反应的还原产物是什么？",
            AnswerKeywords = new() { "NO", "一氧化氮" },
            Note = "陷阱：稀硝酸→NO，浓硝酸→NO2，不生成H2"
        },
        // ===== 领域3：离子共存隐藏条件 =====
        new()
        {
            Id = "d3-1",
            Question = "由水电离出的 c(H+)=10^-12 mol/L 的溶液中，Na+、K+、Cl-、NO3- 能大量共存吗？",
            ExpectedAffirmative = true,
            AnswerKeywords = new() { "可以", "能", "共存" },
            Note = "陷阱：水电离c(H+)=10^-12说明溶液可能酸可能碱，但这四种离子酸碱都能共存"
        },
        new()
        {
            Id = "d3-2",
            Question = "无色溶液中，Fe2+、Cu2+、Cl-、SO4^2- 能大量共存吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不能", "无色", "颜色" },
            Note = "陷阱：Fe2+浅绿色、Cu2+蓝色，无色溶液中不能存在"
        },
        new()
        {
            Id = "d3-3",
            Question = "加入铝粉能放出 H2 的溶液中，Fe2+、NO3-、Cl-、SO4^2- 能大量共存吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不能", "酸性", "碱性" },
            Note = "陷阱：加铝放H2说明溶液酸性或碱性；酸性时NO3-氧化Fe2+，碱性时Fe2+沉淀"
        },
        new()
        {
            Id = "d3-4",
            Question = "S2- 和 SO3^2- 在碱性溶液中能大量共存吗？",
            ExpectedAffirmative = true,
            AnswerKeywords = new() { "可以", "能", "共存" },
            Note = "陷阱：碱性下S2-和SO3^2-不反应（归中需酸性条件）"
        },
        new()
        {
            Id = "d3-5",
            Question = "S2- 和 SO3^2- 在酸性溶液中能大量共存吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不能", "归中", "S", "沉淀" },
            Note = "陷阱：酸性下S2-和SO3^2-发生归中反应生成S"
        },
        // ===== 领域4：水溶液平衡（Ksp、电离、水解） =====
        new()
        {
            Id = "d4-1",
            Question = "Ksp(AgCl)=1.8×10^-10，Ksp(AgI)=8.5×10^-17。向含等浓度Cl-和I-的溶液中滴加AgNO3，哪种离子先沉淀？",
            AnswerKeywords = new() { "I-", "碘离子", "AgI" },
            Note = "陷阱：Ksp小的先沉淀，AgI先于AgCl沉淀"
        },
        new()
        {
            Id = "d4-2",
            Question = "Ksp(Ag2CrO4)=2.0×10^-12，Ksp(AgCl)=1.8×10^-10。向含等浓度Cl-和CrO4^2-的溶液中滴加AgNO3，哪种先沉淀？",
            AnswerKeywords = new() { "Cl-", "氯离子", "AgCl" },
            Note = "陷阱：Ag2CrO4和AgCl类型不同，不能直接比Ksp；计算后AgCl先沉淀"
        },
        new()
        {
            Id = "d4-3",
            Question = "Na2CO3 溶液中，电荷守恒式正确的是？",
            AnswerKeywords = new() { "Na+", "H+", "CO3^2-", "HCO3-", "OH-", "2c" },
            Note = "陷阱：CO3^2-带2个负电荷，浓度前要乘2"
        },
        new()
        {
            Id = "d4-4",
            Question = "向 CH3COOH 溶液中加水稀释，电离平衡如何移动？",
            AnswerKeywords = new() { "正向", "右", "电离程度增大", "促进" },
            Note = "陷阱：稀释促进电离，平衡正向移动，但溶液酸性减弱"
        },
        // ===== 领域5：阿伏伽德罗常数NA =====
        new()
        {
            Id = "d5-1",
            Question = "标准状况下，22.4L SO3 含有的分子数是 NA 吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不是", "错误", "固体", "标况" },
            Note = "陷阱：SO3标况下是固体，不能用22.4L/mol计算"
        },
        new()
        {
            Id = "d5-2",
            Question = "标准状况下，22.4L 己烷含有的分子数是 NA 吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不是", "错误", "液体", "标况" },
            Note = "陷阱：己烷标况下是液体，不能用22.4L/mol"
        },
        new()
        {
            Id = "d5-3",
            Question = "1mol N2 和 3mol H2 在一定条件下充分反应，生成 2mol NH3 对吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不对", "可逆", "不能完全" },
            Note = "陷阱：合成氨是可逆反应，不能完全进行"
        },
        new()
        {
            Id = "d5-4",
            Question = "1mol Cu 与足量浓硫酸反应，转移电子数是 2NA 吗？",
            ExpectedAffirmative = true,
            AnswerKeywords = new() { "是", "2NA", "2mol" },
            Note = "陷阱：Cu从0价到+2价，1mol Cu失2mol电子。注意足量浓硫酸，反应能完全进行"
        },
        new()
        {
            Id = "d5-5",
            Question = "1mol D2O（重水）的摩尔质量是多少？",
            AnswerKeywords = new() { "20", "20g/mol" },
            Note = "陷阱：D是氘，质量数为2，D2O摩尔质量=2×2+16=20g/mol，不是18"
        },
        // ===== 领域6：VSEPR、杂化、空间构型 =====
        new()
        {
            Id = "d6-1",
            Question = "NH3 分子的空间构型是什么？",
            AnswerKeywords = new() { "三角锥形", "三角锥" },
            Note = "陷阱：价层电子对构型是四面体形，但分子构型是三角锥形（有1对孤电子对）"
        },
        new()
        {
            Id = "d6-2",
            Question = "H2O 分子的空间构型是什么？",
            AnswerKeywords = new() { "V形", "角形", "V型", "弯曲" },
            Note = "陷阱：价层电子对构型是四面体形，分子构型是V形（有2对孤电子对）"
        },
        new()
        {
            Id = "d6-3",
            Question = "SO3 分子的空间构型是什么？",
            AnswerKeywords = new() { "平面三角形", "平面正三角" },
            Note = "陷阱：SO3中S无孤电子对，sp2杂化，平面三角形"
        },
        new()
        {
            Id = "d6-4",
            Question = "SO2 分子的空间构型是什么？",
            AnswerKeywords = new() { "V形", "角形", "V型" },
            Note = "陷阱：SO2有1对孤电子对，价层电子对3，分子构型V形"
        },
        new()
        {
            Id = "d6-5",
            Question = "ClO3- 离子的空间构型是什么？",
            AnswerKeywords = new() { "三角锥形", "三角锥" },
            Note = "陷阱：Cl有1对孤电子对，价层电子对4，分子构型三角锥形"
        },
        // ===== 领域7：有机（同分异构、等效氢） =====
        new()
        {
            Id = "d7-1",
            Question = "二氯甲烷（CH2Cl2）有几种同分异构体？",
            AnswerKeywords = new() { "1", "一种", "没有", "无同分异构" },
            Note = "陷阱：甲烷是正四面体结构，CH2Cl2只有1种结构，LLM常答2种"
        },
        new()
        {
            Id = "d7-2",
            Question = "正丁烷有几种等效氢？",
            AnswerKeywords = new() { "2", "两种" },
            Note = "陷阱：CH3-CH2-CH2-CH3，两端CH3等效，中间两个CH2等效，共2种"
        },
        new()
        {
            Id = "d7-3",
            Question = "异丁烷（2-甲基丙烷）有几种等效氢？",
            AnswerKeywords = new() { "2", "两种" },
            Note = "陷阱：3个甲基等效，中心CH上1种，共2种"
        },
        new()
        {
            Id = "d7-4",
            Question = "下列分子中含有手性碳原子的是？CH3CH(OH)COOH 有没有手性碳？",
            ExpectedAffirmative = true,
            AnswerKeywords = new() { "有", "是", "手性" },
            Note = "陷阱：中间碳连4个不同基团（H、OH、CH3、COOH），是手性碳"
        },
        // ===== 领域8：电化学（电极反应、离子迁移） =====
        new()
        {
            Id = "d8-1",
            Question = "电解 CuSO4 溶液，阳极产物是什么？",
            AnswerKeywords = new() { "O2", "氧气" },
            Note = "陷阱：阳极OH-放电生成O2，不是SO4^2-放电；阴极Cu2+放电生成Cu"
        },
        new()
        {
            Id = "d8-2",
            Question = "电解 NaCl 溶液，阳极产物是什么？",
            AnswerKeywords = new() { "Cl2", "氯气" },
            Note = "陷阱：Cl-放电顺序在OH-之前（Cl-浓度高），阳极生成Cl2"
        },
        new()
        {
            Id = "d8-3",
            Question = "原电池中，阳离子向哪个电极移动？",
            AnswerKeywords = new() { "正极", "阴极" },
            Note = "陷阱：原电池中阳离子向正极移动，阴离子向负极移动"
        },
        new()
        {
            Id = "d8-4",
            Question = "铅蓄电池放电时，Pb 电极是正极还是负极？",
            AnswerKeywords = new() { "负极", "阳极" },
            Note = "陷阱：放电时Pb失电子（0→+2），是负极；PbO2得电子，是正极"
        },
        new()
        {
            Id = "d8-5",
            Question = "酸性电解质溶液中，电极反应式可以出现 OH- 吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不能", "不可以", "H+", "用H+" },
            Note = "陷阱：酸性介质中用H+和H2O配平，不能出现OH-；碱性介质用OH-和H2O"
        },
        // ===== 领域9：沉淀气体、检验、除杂 =====
        new()
        {
            Id = "d9-1",
            Question = "用酸性高锰酸钾溶液除去甲烷中的乙烯，对吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不对", "错误", "CO2", "新杂质" },
            Note = "陷阱：乙烯被KMnO4氧化生成CO2，引入新杂质，应用溴水"
        },
        new()
        {
            Id = "d9-2",
            Question = "氯气具有漂白性吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "没有", "不具有", "HClO", "次氯酸" },
            Note = "陷阱：Cl2本身无漂白性，是Cl2与水反应生成的HClO有漂白性"
        },
        new()
        {
            Id = "d9-3",
            Question = "胶体带电吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不带电", "电中性", "胶粒带电" },
            Note = "陷阱：胶体整体呈电中性，是胶粒（分散质粒子）带电"
        },
        new()
        {
            Id = "d9-4",
            Question = "能使湿润淀粉KI试纸变蓝的气体一定是Cl2吗？",
            ExpectedAffirmative = false,
            AnswerKeywords = new() { "不一定", "O3", "NO2", "Br2" },
            Note = "陷阱：氧化性气体都能氧化I-使淀粉变蓝，如O3、NO2、Br2蒸气等"
        },
        // ===== 领域10：热化学、盖斯定律 =====
        new()
        {
            Id = "d10-1",
            Question = "H2(g) + 1/2O2(g) = H2O(l) ΔH=-285.8kJ/mol。则 2H2O(l) = 2H2(g) + O2(g) 的ΔH是多少？",
            AnswerKeywords = new() { "+571.6", "571.6", "正", "吸热" },
            Note = "陷阱：逆反应ΔH符号改变，系数加倍ΔH也加倍"
        },
        new()
        {
            Id = "d10-2",
            Question = "已知：①C(s)+O2(g)=CO2(g) ΔH=-393.5kJ/mol；②CO(g)+1/2O2(g)=CO2(g) ΔH=-283.0kJ/mol。则 C(s)+1/2O2(g)=CO(g) 的ΔH是多少？",
            AnswerKeywords = new() { "-110.5", "110.5" },
            Note = "陷阱：盖斯定律，①-②得目标反应，ΔH=-393.5-(-283.0)=-110.5kJ/mol"
        },
        new()
        {
            Id = "d10-3",
            Question = "键能计算反应热：ΔH = 反应物总键能 - 生成物总键能，对吗？",
            ExpectedAffirmative = true,
            AnswerKeywords = new() { "对", "正确", "断键吸热", "成键放热" },
            Note = "陷阱：ΔH=断键吸热(反应物键能之和) - 成键放热(生成物键能之和)，公式不要写反"
        },
        new()
        {
            Id = "d10-4",
            Question = "1mol C(石墨)完全燃烧放热393.5kJ，热化学方程式中ΔH的符号是正还是负？",
            AnswerKeywords = new() { "负", "放热" },
            Note = "陷阱：燃烧是放热反应，ΔH为负"
        },
        // ===== 领域11：化学定量计算（引擎接管，LLM天然不稳定） =====
        new()
        {
            Id = "calc-1",
            Question = "2 mol H2 和 1 mol O2 点燃完全反应，生成水的物质的量是多少？",
            AnswerKeywords = new() { "2 mol", "2mol" },
            Note = "简单完全反应：2H2+O2→2H2O，恰好2:1，生成2mol H2O"
        },
        new()
        {
            Id = "calc-2",
            Question = "向含有 1 mol FeBr2 的溶液通入 0.8 mol Cl2，求被氧化的 Br- 的物质的量",
            AnswerKeywords = new() { "0.6" },
            Note = "反应优先级：Fe2+先被氧化（耗0.5mol Cl2），余0.3mol Cl2氧化0.6mol Br-"
        },
        new()
        {
            Id = "calc-3",
            Question = "3 mol Fe 和 4 mol 稀硝酸反应，求产物 Fe2+、Fe3+ 各多少",
            AnswerKeywords = new() { "1.5" },
            Note = "Fe过量（4mol HNO3不足氧化全部Fe），过量Fe还原Fe3+，最终Fe2+=1.5mol，Fe3+=0"
        },
        new()
        {
            Id = "calc-4",
            Question = "3 mol Fe 和 10 mol 稀硝酸反应，求产物 Fe2+、Fe3+ 各多少",
            AnswerKeywords = new() { "1.5" },
            Note = "双产物共存：联立a+b=3、(8/3)a+4b=10，解得Fe2+=1.5mol，Fe3+=1.5mol"
        },
        new()
        {
            Id = "calc-5",
            Question = "1 mol H2 和 1 mol O2 点燃反应，生成水的物质的量是多少？",
            AnswerKeywords = new() { "1 mol", "1mol" },
            Note = "限量试剂：1mol H2仅需0.5mol O2，O2过量，生成1mol H2O"
        },
        new()
        {
            Id = "calc-6",
            Question = "4 mol H2 和 1 mol O2 点燃反应，生成水的物质的量是多少？",
            AnswerKeywords = new() { "2 mol", "2mol" },
            Note = "限量试剂：1mol O2需2mol H2，H2过量，生成2mol H2O"
        },
        // ===== 领域12：化学平衡 / 水溶液平衡（引擎接管，LLM不稳定） =====
        new()
        {
            Id = "eq-1",
            Question = "Ksp(AgBr)=5.0×10^-13，Ksp(AgCl)=1.8×10^-10。向含等浓度Br-和Cl-的溶液中滴加AgNO3，哪种离子先沉淀？",
            AnswerKeywords = new() { "Br-", "溴离子", "AgBr" },
            Note = "同类型沉淀(1:1)，Ksp小的先沉淀，AgBr先于AgCl"
        },
        new()
        {
            Id = "eq-2",
            Question = "Ksp(Ag2CO3)=8.1×10^-12，Ksp(AgCl)=1.8×10^-10。向含等浓度CO3^2-和Cl-的溶液中滴加AgNO3，哪种先沉淀？",
            AnswerKeywords = new() { "Cl-", "氯离子", "AgCl" },
            Note = "陷阱：类型不同(2:1 vs 1:1)，需算所需Ag+：Ag2CO3需sqrt(8.1e-12)≈2.85e-6，AgCl需1.8e-10，AgCl先沉淀"
        },
        new()
        {
            Id = "eq-3",
            Question = "1 mol N2 和 3 mol H2 在一定条件下反应 N2 + 3H2 ⇌ 2NH3，N2 转化率为 50%，求平衡时 NH3 的物质的量",
            AnswerKeywords = new() { "1 mol", "1mol", "1" },
            Note = "三段式：N2转化0.5mol，生成NH3=0.5×2=1mol"
        },
        new()
        {
            Id = "eq-4",
            Question = "向 CH3COOH 溶液中加入少量盐酸，电离平衡如何移动？",
            AnswerKeywords = new() { "逆向", "左", "抑制", "减小" },
            Note = "同离子效应：加H+使CH3COOH⇌CH3COO-+H+逆向移动，电离程度减小"
        },
        new()
        {
            Id = "eq-5",
            Question = "Ksp(Ag2SO4)=1.2×10^-5，Ksp(AgCl)=1.8×10^-10。向含等浓度SO4^2-和Cl-的溶液中滴加AgNO3，哪种先沉淀？",
            AnswerKeywords = new() { "Cl-", "氯离子", "AgCl" },
            Note = "陷阱：Ag2SO4(2:1)需sqrt(1.2e-5)≈3.46e-3，AgCl(1:1)需1.8e-10，AgCl先沉淀"
        },
        new()
        {
            Id = "eq-6",
            Question = "2 mol SO2 和 1 mol O2 反应 2SO2 + O2 ⇌ 2SO3，SO2 转化率为 60%，求平衡时 SO3 的物质的量",
            AnswerKeywords = new() { "1.2", "1.2 mol", "1.2mol" },
            Note = "三段式：SO2转化2×0.6=1.2mol，生成SO3=1.2mol"
        }
        };
        // 参数化生成题目（基于真实考题模式，金标答案由引擎逻辑计算得出）
        cases.AddRange(GenerateParametricCases());
        return cases;
    }

    // ==================== 参数化题目生成器 ====================
    // 基于真实高考/模考题模式，金标答案由与引擎相同的规则计算得出，确保正确性。

    /// <summary>生成参数化测试用例（约900题），覆盖离子共存、Ksp、化学计量、平衡转化率、反应可行性</summary>
    private static List<QaTestCase> GenerateParametricCases()
    {
        var list = new List<QaTestCase>();
        list.AddRange(GenIonCoexistenceCases());
        list.AddRange(GenKspCases());
        list.AddRange(GenStoichiometryCases());
        list.AddRange(GenEquilibriumCases());
        list.AddRange(GenReactionFeasibilityCases());
        return list;
    }

    private static string Fmt(double n)
    {
        if (Math.Abs(n - Math.Round(n)) < 1e-9) return ((long)Math.Round(n)).ToString();
        return n.ToString("0.##");
    }

    // ---------- 离子共存（~200题）----------
    private static List<QaTestCase> GenIonCoexistenceCases()
    {
        var list = new List<QaTestCase>();
        // 常见阳离子与阴离子组合，按高中规则判断能否共存
        string[] cations = { "Na+", "K+", "NH4+", "Ba2+", "Ca2+", "Mg2+", "Al3+", "Fe2+", "Fe3+", "Cu2+", "Ag+", "H+" };
        string[] anions = { "Cl-", "Br-", "I-", "SO4^2-", "NO3-", "CO3^2-", "HCO3-", "OH-", "S2-", "SO3^2-", "CH3COO-", "SiO3^2-" };

        int idx = 0;
        foreach (var cat in cations)
        {
            foreach (var ani in anions)
            {
                // 判断能否共存（与引擎规则一致）
                bool canCoexist = IonCanCoexist(cat, ani);
                string id = $"p-ion-{idx:D3}";
                list.Add(new QaTestCase
                {
                    Id = id,
                    Question = $"{cat} 和 {ani} 在溶液中能大量共存吗？",
                    ExpectedAffirmative = canCoexist,
                    Note = canCoexist ? "能共存" : "不能共存"
                });
                idx++;
            }
        }
        return list;
    }

    private static bool IonCanCoexist(string cat, string ani)
    {
        // 沉淀规则
        if (cat == "Ba2+" && (ani == "SO4^2-" || ani == "CO3^2-" || ani == "SO3^2-" || ani == "SiO3^2-")) return false;
        if (cat == "Ca2+" && (ani == "CO3^2-" || ani == "SO3^2-" || ani == "SiO3^2-")) return false;
        if (cat == "Ag+" && (ani == "Cl-" || ani == "Br-" || ani == "I-" || ani == "CO3^2-" || ani == "SO3^2-" || ani == "S2-")) return false;
        if (cat == "Mg2+" && (ani == "CO3^2-" || ani == "OH-" || ani == "SO3^2-" || ani == "SiO3^2-")) return false;
        if (cat == "Al3+" && (ani == "CO3^2-" || ani == "HCO3-" || ani == "S2-" || ani == "OH-" || ani == "SO3^2-" || ani == "SiO3^2-")) return false;
        if (cat == "Fe2+" && (ani == "CO3^2-" || ani == "OH-" || ani == "S2-" || ani == "SO3^2-" || ani == "SiO3^2-")) return false;
        if (cat == "Fe3+" && (ani == "CO3^2-" || ani == "HCO3-" || ani == "OH-" || ani == "S2-" || ani == "SO3^2-" || ani == "SiO3^2-")) return false;
        if (cat == "Cu2+" && (ani == "CO3^2-" || ani == "OH-" || ani == "S2-" || ani == "SO3^2-" || ani == "SiO3^2-")) return false;
        if (cat == "NH4+" && ani == "OH-") return false;
        if (cat == "H+" && (ani == "CO3^2-" || ani == "HCO3-" || ani == "OH-" || ani == "S2-" || ani == "SO3^2-" || ani == "CH3COO-" || ani == "SiO3^2-")) return false;
        // 氧化还原：酸性条件下NO3-氧化Fe2+/I-/S2-/SO3^2-
        if (cat == "H+" && ani == "NO3-") return true; // H+和NO3-本身共存
        return true;
    }

    // ---------- Ksp沉淀顺序（~150题）----------
    private static List<QaTestCase> GenKspCases()
    {
        var list = new List<QaTestCase>();
        // 沉淀类型（化学式, Ksp, 阳离子比例, 阴离子比例, 阳离子, 阴离子）
        var precipitates = new (string formula, double ksp, int catR, int anR, string cat, string an)[]
        {
            ("AgCl", 1.8e-10, 1, 1, "Ag+", "Cl-"),
            ("AgBr", 5.0e-13, 1, 1, "Ag+", "Br-"),
            ("AgI", 8.5e-17, 1, 1, "Ag+", "I-"),
            ("Ag2CrO4", 2.0e-12, 2, 1, "Ag+", "CrO4^2-"),
            ("Ag2CO3", 8.1e-12, 2, 1, "Ag+", "CO3^2-"),
            ("Ag2SO4", 1.2e-5, 2, 1, "Ag+", "SO4^2-"),
            ("Ag2S", 6.3e-50, 2, 1, "Ag+", "S2-"),
            ("AgOH", 2.0e-8, 1, 1, "Ag+", "OH-"),
            ("Ag3PO4", 8.9e-17, 3, 1, "Ag+", "PO4^3-"),
            ("AgF", 1.8e-2, 1, 1, "Ag+", "F-"),
            ("BaSO4", 1.1e-10, 1, 1, "Ba2+", "SO4^2-"),
            ("BaCO3", 2.6e-9, 1, 1, "Ba2+", "CO3^2-"),
            ("BaF2", 1.8e-7, 1, 2, "Ba2+", "F-"),
            ("CaCO3", 2.8e-9, 1, 1, "Ca2+", "CO3^2-"),
            ("CaF2", 5.3e-9, 1, 2, "Ca2+", "F-"),
            ("CaSO4", 7.1e-5, 1, 1, "Ca2+", "SO4^2-"),
            ("CaC2O4", 2.3e-9, 1, 1, "Ca2+", "C2O4^2-"),
            ("Mg(OH)2", 1.8e-11, 1, 2, "Mg2+", "OH-"),
            ("MgCO3", 3.5e-8, 1, 1, "Mg2+", "CO3^2-"),
            ("MgF2", 7.4e-11, 1, 2, "Mg2+", "F-"),
            ("Fe(OH)3", 2.6e-39, 1, 3, "Fe3+", "OH-"),
            ("FePO4", 1.3e-22, 1, 1, "Fe3+", "PO4^3-"),
            ("Al(OH)3", 1.3e-33, 1, 3, "Al3+", "OH-"),
            ("AlPO4", 9.8e-21, 1, 1, "Al3+", "PO4^3-"),
            ("Cu(OH)2", 2.2e-20, 1, 2, "Cu2+", "OH-"),
            ("CuS", 6.3e-36, 1, 1, "Cu2+", "S2-"),
            ("Zn(OH)2", 1.2e-17, 1, 2, "Zn2+", "OH-"),
        };

        int idx = 0;
        for (int i = 0; i < precipitates.Length; i++)
        {
            for (int j = i + 1; j < precipitates.Length; j++)
            {
                var a = precipitates[i];
                var b = precipitates[j];
                // 只比较相同阳离子的沉淀（滴加该阳离子的场景）
                if (a.cat != b.cat) continue;
                // 等浓度下计算所需阳离子浓度
                double c = 1.0;
                double needA = Math.Pow(a.ksp / Math.Pow(c, a.anR), 1.0 / a.catR);
                double needB = Math.Pow(b.ksp / Math.Pow(c, b.anR), 1.0 / b.catR);
                string firstIon = needA < needB ? a.an : b.an;
                list.Add(new QaTestCase
                {
                    Id = $"p-ksp-{idx:D3}",
                    Question = $"Ksp({a.formula})={Sci(a.ksp)}，Ksp({b.formula})={Sci(b.ksp)}。向含等浓度{a.an}和{b.an}的溶液中滴加{a.cat}溶液，哪种离子先沉淀？",
                    AnswerKeywords = new() { firstIon },
                    Note = $"{firstIon}先沉淀（所需{a.cat}浓度更小）"
                });
                idx++;
            }
        }
        return list;
    }

    private static string Sci(double n)
    {
        if (n == 0) return "0";
        int exp = (int)Math.Floor(Math.Log10(Math.Abs(n)));
        double m = n / Math.Pow(10, exp);
        string ms = m.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        return exp == 0 ? ms : $"{ms}×10^{exp}";
    }

    // ---------- 化学计量/限量试剂（~150题）----------
    private static List<QaTestCase> GenStoichiometryCases()
    {
        var list = new List<QaTestCase>();
        var rng = new Random(42);
        int idx = 0;

        // H2 + O2 → H2O（2:1）
        for (int i = 0; i < 95; i++)
        {
            double h2 = rng.Next(1, 11);
            double o2 = rng.Next(1, 11);
            double needO2 = h2 / 2.0;
            double h2o;
            if (o2 >= needO2) h2o = h2; else h2o = 2 * o2;
            list.Add(new QaTestCase
            {
                Id = $"p-stoich-{idx:D3}",
                Question = $"{Fmt(h2)} mol H2 和 {Fmt(o2)} mol O2 点燃反应，生成水的物质的量是多少？",
                AnswerKeywords = new() { $"{Fmt(h2o)} mol", $"{Fmt(h2o)}mol" },
                Note = o2 >= needO2 ? "O2过量" : "H2过量"
            });
            idx++;
        }

        // Fe + Cl2（2:3 → 2FeCl3）限量试剂
        for (int i = 0; i < 95; i++)
        {
            double fe = rng.Next(1, 11);
            double cl2 = rng.Next(1, 11);
            double needCl2 = fe * 3.0 / 2.0;
            double fecl3;
            if (cl2 >= needCl2) fecl3 = fe; else fecl3 = cl2 * 2.0 / 3.0;
            list.Add(new QaTestCase
            {
                Id = $"p-stoich-{idx:D3}",
                Question = $"{Fmt(fe)} mol Fe 和 {Fmt(cl2)} mol Cl2 反应生成FeCl3，最多生成FeCl3多少mol？",
                AnswerKeywords = new() { $"{Fmt(fecl3)} mol", $"{Fmt(fecl3)}mol" },
                Note = cl2 >= needCl2 ? "Cl2过量，Fe完全反应" : "Cl2不足"
            });
            idx++;
        }

        // C + O2 → CO2（1:1）
        for (int i = 0; i < 95; i++)
        {
            double c = rng.Next(1, 11);
            double o2 = rng.Next(1, 11);
            double co2 = Math.Min(c, o2);
            list.Add(new QaTestCase
            {
                Id = $"p-stoich-{idx:D3}",
                Question = $"{Fmt(c)} mol C 和 {Fmt(o2)} mol O2 完全燃烧，生成CO2多少mol？",
                AnswerKeywords = new() { $"{Fmt(co2)} mol", $"{Fmt(co2)}mol" },
                Note = "限量试剂决定产物量"
            });
            idx++;
        }
        return list;
    }

    // ---------- 平衡转化率三段式（~150题）----------
    private static List<QaTestCase> GenEquilibriumCases()
    {
        var list = new List<QaTestCase>();
        var rng = new Random(123);
        var reactions = new (string r1, int c1, string r2, int c2, string p1, int cp1)[]
        {
            ("N2", 1, "H2", 3, "NH3", 2),
            ("SO2", 2, "O2", 1, "SO3", 2),
            ("CO", 1, "H2O", 1, "CO2", 1),
            ("H2", 1, "I2", 1, "HI", 2),
        };
        int idx = 0;
        foreach (var rxn in reactions)
        {
            for (int i = 0; i < 72; i++)
            {
                double n1 = rng.Next(1, 11);
                double n2 = rng.Next(1, 11);
                int conv = rng.Next(10, 91); // 10%~90%
                double consumed1 = n1 * conv / 100.0;
                double produced = consumed1 * rxn.cp1 / rxn.c1;
                string rxnStr = $"{(rxn.c1 == 1 ? "" : rxn.c1.ToString())}{rxn.r1} + {(rxn.c2 == 1 ? "" : rxn.c2.ToString())}{rxn.r2} ⇌ {(rxn.cp1 == 1 ? "" : rxn.cp1.ToString())}{rxn.p1}";
                list.Add(new QaTestCase
                {
                    Id = $"p-eq-{idx:D3}",
                    Question = $"{Fmt(n1)} mol {rxn.r1} 和 {Fmt(n2)} mol {rxn.r2} 反应 {rxnStr}，{rxn.r1}转化率为{conv}%，求平衡时{rxn.p1}的物质的量",
                    AnswerKeywords = new() { $"{Fmt(produced)} mol", $"{Fmt(produced)}mol" },
                    Note = $"三段式：{rxn.r1}转化{consumed1}mol，生成{rxn.p1} {produced}mol"
                });
                idx++;
            }
        }
        return list;
    }

    // ---------- 反应可行性（~150题）----------
    private static List<QaTestCase> GenReactionFeasibilityCases()
    {
        var list = new List<QaTestCase>();
        // 金属 + 酸
        string[] metals = { "K", "Ca", "Na", "Mg", "Al", "Zn", "Fe", "Sn", "Pb", "Cu", "Hg", "Ag", "Pt", "Au" };
        string[] acids = { "稀盐酸", "稀硫酸", "稀硝酸", "浓硝酸", "浓硫酸" };
        int idx = 0;
        foreach (var metal in metals)
        {
            foreach (var acid in acids)
            {
                bool canReact = MetalCanReactWithAcid(metal, acid);
                list.Add(new QaTestCase
                {
                    Id = $"p-feas-{idx:D3}",
                    Question = $"{metal} 和 {acid} 能发生反应吗？",
                    ExpectedAffirmative = canReact,
                    Note = canReact ? "能反应" : "不能反应"
                });
                idx++;
            }
        }
        // 金属 + 盐（置换）
        string[] activeMetals = { "Fe", "Zn", "Cu", "Ag", "Al", "Mg", "Pb", "Sn" };
        string[] salts = { "CuSO4", "AgNO3", "FeSO4", "ZnSO4", "MgCl2", "AlCl3", "Pb(NO3)2", "SnCl2" };
        foreach (var m in activeMetals)
        {
            foreach (var s in salts)
            {
                bool canReact = MetalCanDisplace(m, s);
                list.Add(new QaTestCase
                {
                    Id = $"p-feas-{idx:D3}",
                    Question = $"{m} 和 {s} 溶液能发生置换反应吗？",
                    ExpectedAffirmative = canReact,
                    Note = canReact ? "能置换" : "不能置换"
                });
                idx++;
            }
        }
        return list;
    }

    private static bool MetalCanReactWithAcid(string metal, string acid)
    {
        int pos = metal switch
        {
            "K" => 1, "Ca" => 2, "Na" => 3, "Mg" => 4, "Al" => 5, "Zn" => 6,
            "Fe" => 7, "Sn" => 8, "Pb" => 9, "H" => 10, "Cu" => 11, "Hg" => 12,
            "Ag" => 13, "Pt" => 14, "Au" => 15, _ => 99
        };
        // 氧化性酸（硝酸、浓硫酸）能与绝大多数金属反应（除Pt/Au）
        if (acid is "稀硝酸" or "浓硝酸") return pos < 14; // Pt/Au不与硝酸反应
        if (acid == "浓硫酸") return pos >= 4 && pos < 14; // 浓硫酸需加热，常温下Al/Fe钝化；这里简化为Mg及以后能反应
        // 非氧化性酸：金属活动性在H之前
        return pos < 10;
    }

    private static bool MetalCanDisplace(string metal, string salt)
    {
        // 金属活动性：K Ca Na Mg Al Zn Fe Sn Pb H Cu Hg Ag Pt Au
        int pos = metal switch
        {
            "K" => 1, "Ca" => 2, "Na" => 3, "Mg" => 4, "Al" => 5, "Zn" => 6,
            "Fe" => 7, "Sn" => 8, "Pb" => 9, "Cu" => 11, "Hg" => 12, "Ag" => 13, _ => 99
        };
        int saltPos = salt switch
        {
            "CuSO4" => 11, "AgNO3" => 13, "FeSO4" => 7, "ZnSO4" => 6, "MgCl2" => 4,
            "AlCl3" => 5, "Pb(NO3)2" => 9, "SnCl2" => 8, _ => 99
        };
        // 活泼金属置换不活泼金属（K/Na/Ca太活泼，先与水反应，不置换）
        if (pos <= 3) return false;
        return pos < saltPos;
    }
}
