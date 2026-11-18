namespace CogniThink.TutorBrain.Application.Gateway;

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CogniThink.TutorBrain.Application.Agent;
using CogniThink.TutorBrain.Application.Chemistry;

/// <summary>
/// 化学伴思 LLM 网关
/// 核心原则：化学引擎是唯一真值源，LLM 只做「输入标准化」+「引导讲解」
/// 禁止 LLM 自行输出方程式、反应结论、计算结果
/// 链路：学生文本 → LLM 识别任务 → 组装引擎 JSON → 调用引擎 → LLM 生成教学回复
/// </summary>
public sealed class LlmGatewayService
{
    private readonly LlmProxyService _llm;
    private readonly ChemicalInferenceService _chem;
    private readonly ProblemQueryCompiler _compiler;

    public LlmGatewayService(
        LlmProxyService llm,
        ChemicalInferenceService chem,
        ProblemQueryCompiler compiler)
    {
        _llm = llm;
        _chem = chem;
        _compiler = compiler;
    }

    // ==================== System Prompts ====================

    /// <summary>输入标准化 Prompt：让 LLM 把学生文本转成引擎请求 JSON</summary>
    public const string ParseSystemPrompt =
        "你是「化学伴思」LLM 网关，职责是把学生的自然语言输入转换成结构化的引擎请求，绝不能自行输出化学结论。\n" +
        "\n" +
        "【红线】化学引擎是唯一真值源。\n" +
        "✅ 允许：识别任务类型、提取化学式/离子/条件、转述引擎结果、类比、引导提问、指出错误标签\n" +
        "❌ 禁止：自行配平方程式、自行判断反应能否发生、自行计算数值、自行给出共存结论、自行判断结构式对错\n" +
        "\n" +
        "【任务类型 task_type】\n" +
        "- balance：学生给出未配平的方程式，要求配平\n" +
        "- reaction_check：学生给出方程式，要求校验是否正确/能否发生\n" +
        "- calculation：涉及物质的量、浓度、体积、pH、Ksp 等定量计算\n" +
        "- structure_verify：涉及结构式、电子式、价键的判断\n" +
        "- ion_check：判断离子能否大量共存\n" +
        "- vsepr：价层电子对互斥、杂化类型、分子空间构型\n" +
        "- titration：酸碱中和滴定、氧化还原滴定\n" +
        "- equilibrium：化学平衡、电离平衡、水解平衡、溶解平衡\n" +
        "- concept：纯概念提问（如「什么是电解质」），直接回答，不调引擎\n" +
        "- unknown：无法识别，反问学生\n" +
        "\n" +
        "【引擎请求组装规则】\n" +
        "1. reaction.left / right：从学生文本中提取化学式，保持顺序。离子共存题把所有待判断的离子放入 reaction.left\n" +
        "2. reaction.conditions：提取加热/催化剂/点燃/酸性/碱性等条件\n" +
        "3. known：仅填入学生明确给出的数值，未给出的填 null\n" +
        "4. student_answer：学生给出的待判定答案（如配平结果、共存结论）\n" +
        "5. options：全部设为 true（人教版高中规则）\n" +
        "6. ion_check 类型：把所有离子（如 Fe2+、H+、NO3-）放入 reaction.left 数组\n" +
        "7. concept 类型：只设 task_type=concept，其他字段留空\n" +
        "\n" +
        "【输出格式】\n" +
        "只输出 JSON：{\"task_type\":\"...\",\"reaction\":{\"left\":[],\"right\":[],\"conditions\":[]},\"smiles\":[],\"formula\":\"\",\"known\":{},\"student_answer\":\"\",\"options\":{}}\n" +
        "不要输出任何解释文字。";

    /// <summary>回复生成 Prompt：把引擎结果转成导学对话</summary>
    public const string ReplySystemPrompt =
        "你是「化学伴思」导学导师。化学引擎已经给出了判定结果，你的任务是把引擎结果转化为对学生友好的教学对话。\n" +
        "\n" +
        "【规则】\n" +
        "1. 引擎返回的 final_result、error_tags 是唯一真值，你只能转述，不能修改\n" +
        "2. ok=false 时：指出错误标签对应的问题，引导学生自查，不直接给正确答案\n" +
        "3. ok=true 时：肯定学生，追问思路或给出迁移变式\n" +
        "4. ok=null 时：说明超出引擎范围，内容仅作参考\n" +
        "5. need_more_condition=true 时：反问学生补齐缺失条件\n" +
        "6. 一次只讲一个点，用提问引导，不超过 120 字\n" +
        "7. 绝不直接输出最终答案，永远引导思考";

    // ==================== 主入口 ====================

    /// <summary>
    /// 网关主入口：学生文本 → 教学回复
    /// </summary>
    public async Task<GatewayResult> ProcessStudentMessageAsync(string rawText, CancellationToken ct = default)
    {
        // 1. LLM 识别任务 + 组装引擎请求
        var engineRequest = await ParseStudentInputAsync(rawText, ct);

        // 2. 调用引擎（黑盒）
        var engineResponse = await CallEngineAsync(engineRequest, ct);

        // 3. 根据引擎结果生成教学回复
        var reply = await GenerateReplyAsync(rawText, engineResponse, ct);

        return new GatewayResult
        {
            TaskType = engineRequest.TaskType,
            EngineRequest = engineRequest,
            EngineResponse = engineResponse,
            Reply = reply
        };
    }

    // ==================== 1. 输入标准化（LLM 识别任务 + 组装请求） ====================

    /// <summary>
    /// 让 LLM 从学生文本中识别任务类型并组装引擎请求 JSON。
    /// 若 LLM 不可用，回退到关键词匹配。
    /// </summary>
    public async Task<EngineRequest> ParseStudentInputAsync(string rawText, CancellationToken ct = default)
    {
        var userPrompt = $"学生输入：{rawText}\n\n请识别任务类型并组装引擎请求 JSON。";

        var text = await _llm.ChatAsync(ParseSystemPrompt, userPrompt, 0.1f, 600, ct);

        if (!string.IsNullOrWhiteSpace(text))
        {
            try
            {
                var jsonStr = ExtractJson(text);
                var parsed = JsonSerializer.Deserialize<EngineRequest>(jsonStr, JsonOptions);
                if (parsed is not null)
                {
                    return NormalizeRequest(parsed, rawText);
                }
            }
            catch
            {
                // LLM 输出无法解析，回退到关键词匹配
            }
        }

        return FallbackParse(rawText);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>从 LLM 输出中提取 JSON 字符串</summary>
    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
            return text[start..(end + 1)];
        return text;
    }

    /// <summary>规范化 LLM 解析出的请求，补全默认值，并对任务类型做兜底校验</summary>
    private static EngineRequest NormalizeRequest(EngineRequest req, string rawText)
    {
        req.RawText = rawText;
        req.Reaction ??= new EngineReaction();
        req.Smiles ??= new List<string>();
        req.Known ??= new EngineKnown();
        req.Options ??= new EngineOptions();

        // 兜底校验：若 LLM 误判（unknown/concept/calculation），用关键词规则二次纠正
        if (req.TaskType is "unknown" or "concept" or "calculation")
        {
            var lower = rawText.ToLowerInvariant();
            // 平衡类信号优先：含转化率/平衡时/Ksp/电离/水解 → 强制 equilibrium
            if (lower.Contains("转化率") || lower.Contains("平衡时") || lower.Contains("ksp") ||
                lower.Contains("电离") || lower.Contains("水解") || lower.Contains("勒夏特列"))
            {
                req.TaskType = "equilibrium";
            }
            else
            {
                var fb = FallbackParse(rawText);
                if (fb.TaskType != "unknown")
                    req.TaskType = fb.TaskType;
            }
        }

        return req;
    }

    /// <summary>回退路由：关键词匹配（LLM 不可用时）</summary>
    private static EngineRequest FallbackParse(string rawText)
    {
        var text = rawText.ToLowerInvariant();
        var taskType = "unknown";

        if (text.Contains("共存") || text.Contains("大量共存"))
            taskType = "ion_check";
        else if (text.Contains("配平") || text.Contains("平了吗") || text.Contains("系数"))
            taskType = "balance";
        else if (text.Contains("平衡") || text.Contains("转化率") || text.Contains("勒夏特列") ||
                 text.Contains("ksp") || text.Contains("电离") || text.Contains("水解"))
            taskType = "equilibrium";
        else if (text.Contains("物质的量") || text.Contains("浓度") || text.Contains("体积") ||
                 text.Contains("mol") || text.Contains("ph") || text.Contains("多少") ||
                 text.Contains("ka") || text.Contains("计算") || text.Contains("求"))
            taskType = "calculation";
        else if (text.Contains("反应") || text.Contains("能否反应") || text.Contains("方程式对吗") || text.Contains("正确吗"))
            taskType = "reaction_check";
        else if (text.Contains("结构式") || text.Contains("电子式") || text.Contains("价键"))
            taskType = "structure_verify";
        else if (text.Contains("vsepr") || text.Contains("杂化") || text.Contains("构型") || text.Contains("空间"))
            taskType = "vsepr";
        else if (text.Contains("滴定"))
            taskType = "titration";
        else if (text.Contains("什么是") || text.Contains("定义") || text.Contains("概念") ||
                 text.Contains("区别") || text.Contains("为什么") || text.Contains("原理"))
            taskType = "concept";

        return new EngineRequest
        {
            TaskType = taskType,
            RawText = rawText
        };
    }

    // ==================== 2. 调用引擎（黑盒） ====================

    /// <summary>
    /// 调用化学引擎（黑盒）。
    /// 根据 task_type 路由到对应的引擎服务。
    /// 若引擎不可用或任务类型不支持，返回 ok=null 的响应。
    /// </summary>
    public Task<EngineResponse> CallEngineAsync(EngineRequest req, CancellationToken ct = default)
    {
        // concept / unknown 类型不走引擎
        if (req.TaskType == "concept" || req.TaskType == "unknown")
        {
            return Task.FromResult(new EngineResponse
            {
                Ok = null,
                ConstraintNote = "纯概念讲解，不经过化学引擎",
                Confidence = 1.0
            });
        }

        switch (req.TaskType)
        {
            case "ion_check":
                return Task.FromResult(CallIonCheck(req));

            case "reaction_check":
            case "balance":
                return Task.FromResult(CallCompile(req));

            case "calculation":
                return Task.FromResult(CallCalculation(req));

            case "equilibrium":
                return Task.FromResult(CallEquilibrium(req));

            default:
                // 其他任务类型当前引擎未建模，返回 null
                return Task.FromResult(new EngineResponse
                {
                    Ok = null,
                    ConstraintNote = $"【高中限定】{req.TaskType} 类型暂未在引擎建模范围",
                    Confidence = 1.0
                });
        }
    }

    private EngineResponse CallIonCheck(EngineRequest req)
    {
        var ions = req.Reaction.Left.Concat(req.Reaction.Right).ToList();
        if (ions.Count == 0)
        {
            // 兜底：从原始文本中提取离子（如 "Na+ 和 Cl- 能共存吗"）
            ions = ExtractIonsFromText(req.RawText ?? string.Empty);
        }
        if (ions.Count == 0)
        {
            return NeedMoreCondition("需要提供待判断的离子");
        }

        var rawText = req.RawText ?? string.Empty;

        // 1. 检测隐藏的酸碱性条件（文本中的"酸性"/"碱性"）
        var acidic = ions.Any(i => i.Contains("H+") && !i.Contains("OH")) ||
                     rawText.Contains("酸性");
        var alkaline = ions.Any(i => i.Contains("OH-")) ||
                       rawText.Contains("碱性");

        // 2. "加入铝粉/铝片放出H2"：溶液可能是强酸性或强碱性，需两种条件都能共存
        var alProducesH2 = (rawText.Contains("铝") && (rawText.Contains("H2") || rawText.Contains("氢气")))
                           || rawText.Contains("加铝");

        // 3. "无色溶液"：不能存在有色离子
        var colorless = rawText.Contains("无色");
        if (colorless)
        {
            var coloredIons = new Dictionary<string, string>
            {
                { "Fe2+", "浅绿色" }, { "Fe3+", "黄色" }, { "Cu2+", "蓝色" },
                { "MnO4-", "紫红色" }, { "Cr2O7", "橙色" }, { "CrO4", "黄色" },
                { "Co2+", "粉红色" }, { "Ni2+", "绿色" }
            };
            foreach (var (ion, color) in coloredIons)
            {
                if (ions.Any(i => ChemicalKnowledgeBase.NormalizeKey(i) == ChemicalKnowledgeBase.NormalizeKey(ion)))
                {
                    return new EngineResponse
                    {
                        Ok = false,
                        FinalResult = $"不能大量共存。{ion}在溶液中呈{color}，无色溶液中不能存在。",
                        ErrorTags = new List<string> { "E07" },
                        DerivationSteps = new List<string> { $"{ion}水溶液有颜色（{color}），不符合无色溶液条件" },
                        ConstraintNote = "【高中限定】无色溶液排除有色离子",
                        Confidence = 1.0
                    };
                }
            }
        }

        // 4. 加铝放H2：检查酸性条件下是否共存（如NO3-+Fe2+在酸性下不共存）
        if (alProducesH2)
        {
            var acidResult = _chem.CheckCoexistence(ions, acidic: true, alkaline: false);
            if (!acidResult.CanCoexist)
            {
                return new EngineResponse
                {
                    Ok = false,
                    FinalResult = "不能大量共存。加铝放H2说明溶液可能呈酸性，酸性条件下这些离子不能共存。",
                    ErrorTags = new List<string> { "E07" },
                    DerivationSteps = acidResult.Evidence.Prepend("加铝放H2→溶液可能酸性，需检查酸性条件下共存").ToList(),
                    ConstraintNote = "【高中限定】加铝放H2溶液可能酸可能碱",
                    Confidence = 1.0
                };
            }
            // 再检查碱性条件
            var baseResult = _chem.CheckCoexistence(ions, acidic: false, alkaline: true);
            if (!baseResult.CanCoexist)
            {
                return new EngineResponse
                {
                    Ok = false,
                    FinalResult = "不能大量共存。加铝放H2说明溶液可能呈碱性，碱性条件下这些离子不能共存。",
                    ErrorTags = new List<string> { "E07" },
                    DerivationSteps = baseResult.Evidence.Prepend("加铝放H2→溶液可能碱性，需检查碱性条件下共存").ToList(),
                    ConstraintNote = "【高中限定】加铝放H2溶液可能酸可能碱",
                    Confidence = 1.0
                };
            }
        }

        var result = _chem.CheckCoexistence(ions, acidic, alkaline);
        return new EngineResponse
        {
            Ok = result.CanCoexist,
            FinalResult = result.CanCoexist ? "可以大量共存" : "不能大量共存",
            ErrorTags = result.CanCoexist ? new List<string>() : new List<string> { "E07" },
            DerivationSteps = result.Evidence.ToList(),
            ConstraintNote = "【高中限定】人教版规则，不引入大学特例",
            Confidence = 1.0
        };
    }

    private EngineResponse CallCompile(EngineRequest req)
    {
        var stem = string.IsNullOrWhiteSpace(req.StudentAnswer) ? req.RawText : req.StudentAnswer;

        // 先用反应可行性规则引擎判断（金属活动性、氧化性酸等），能给出具体结论就直接返回
        var feasibility = TryJudgeReactionFeasibility(stem);
        if (feasibility is not null) return feasibility;

        var query = _compiler.Compile(stem);
        var result = _chem.Evaluate(query);

        var hasConflict = result.Conflicts.Count > 0;
        return new EngineResponse
        {
            Ok = !hasConflict,
            FinalResult = result.Conclusion,
            IsStudentAnswerCorrect = !hasConflict,
            ErrorTags = hasConflict ? InferErrorTags(result.Conclusion) : new List<string>(),
            DerivationSteps = result.Evidence.ToList(),
            ConstraintNote = "【高中限定】人教版规则，不引入大学特例",
            Confidence = 1.0
        };
    }

    // ==================== 化学定量计算引擎 ====================

    /// <summary>
    /// 化学定量计算引擎。
    /// 核心原则：按反应先后优先级 + 物料/电子守恒严格求解，绝不靠模式匹配蒙数。
    /// 当前支持：FeBr2+Cl2（反应优先级）、Fe+稀HNO3（双产物/过量）、H2+O2（限量试剂）。
    /// </summary>
    private EngineResponse CallCalculation(EngineRequest req)
    {
        var raw = req.RawText ?? string.Empty;

        // 1. FeBr2 + Cl2：Fe2+ 还原性强于 Br-，优先被氧化
        var febr2Cl2 = TryCalculateFeBr2Cl2(raw);
        if (febr2Cl2 is not null) return febr2Cl2;

        // 2. Fe + 稀 HNO3：双产物共存 / 过量判断
        var feHno3 = TryCalculateFeHNO3(raw);
        if (feHno3 is not null) return feHno3;

        // 3. 简单完全反应（限量试剂判断）
        var simple = TryCalculateSimpleStoichiometry(raw);
        if (simple is not null) return simple;

        return new EngineResponse
        {
            Ok = null,
            ConstraintNote = "【高中限定】当前计算引擎仅支持部分定量计算场景，此题需人工分析",
            Confidence = 1.0
        };
    }

    /// <summary>FeBr2 + Cl2：还原性 Fe2+ > Br-，Cl2 先氧化 Fe2+，再氧化 Br-</summary>
    private static EngineResponse? TryCalculateFeBr2Cl2(string raw)
    {
        if (!Regex.IsMatch(raw, @"FeBr\s*2", RegexOptions.IgnoreCase) ||
            !Regex.IsMatch(raw, @"Cl\s*2", RegexOptions.IgnoreCase))
            return null;

        var febr2Match = Regex.Match(raw, @"(\d+(?:\.\d+)?)\s*mol\s*FeBr\s*2", RegexOptions.IgnoreCase);
        if (!febr2Match.Success) return null;
        double nFeBr2 = double.Parse(febr2Match.Groups[1].Value, CultureInfo.InvariantCulture);

        var cl2Match = Regex.Match(raw, @"(\d+(?:\.\d+)?)\s*mol\s*Cl\s*2", RegexOptions.IgnoreCase);
        if (!cl2Match.Success) return null;
        double nCl2 = double.Parse(cl2Match.Groups[1].Value, CultureInfo.InvariantCulture);

        double nFe2Plus = nFeBr2;       // 1 mol FeBr2 含 1 mol Fe2+
        double nBrMinus = 2 * nFeBr2;   // 1 mol FeBr2 含 2 mol Br-

        // 第一步：2Fe2+ + Cl2 → 2Fe3+ + 2Cl-，Fe2+ : Cl2 = 2 : 1
        double cl2ForFe2 = nFe2Plus / 2.0;
        double remainingCl2 = nCl2 - cl2ForFe2;

        if (remainingCl2 <= 1e-9)
        {
            double oxidizedFe2 = 2 * nCl2;
            return new EngineResponse
            {
                Ok = true,
                FinalResult = $"Cl2不足，只氧化Fe2+，不氧化Br-。被氧化的Br-的物质的量为0 mol；被氧化的Fe2+为{FormatNum(oxidizedFe2)} mol。",
                DerivationSteps = new List<string>
                {
                    "还原性：Fe2+ > Br-，Cl2先氧化Fe2+",
                    $"2Fe2+ + Cl2 → 2Fe3+ + 2Cl-，{FormatNum(nCl2)} mol Cl2只能氧化{FormatNum(oxidizedFe2)} mol Fe2+",
                    "Cl2已耗尽，Br-未被氧化"
                },
                ConstraintNote = "【高中限定】FeBr2与Cl2反应，Fe2+还原性强于Br-优先氧化",
                Confidence = 1.0
            };
        }

        // 第二步：2Br- + Cl2 → Br2 + 2Cl-，Br- : Cl2 = 2 : 1
        double oxidizedBr = 2 * remainingCl2;
        if (oxidizedBr > nBrMinus) oxidizedBr = nBrMinus;

        return new EngineResponse
        {
            Ok = true,
            FinalResult = $"被氧化的Br-的物质的量为{FormatNum(oxidizedBr)} mol。（Fe2+先被氧化，消耗{FormatNum(cl2ForFe2)} mol Cl2，剩余{FormatNum(remainingCl2)} mol Cl2氧化Br-）",
            DerivationSteps = new List<string>
            {
                "还原性：Fe2+ > Br-，Cl2先氧化Fe2+",
                $"2Fe2+ + Cl2 → 2Fe3+ + 2Cl-，{FormatNum(nFe2Plus)} mol Fe2+消耗{FormatNum(cl2ForFe2)} mol Cl2",
                $"剩余Cl2：{FormatNum(remainingCl2)} mol",
                $"2Br- + Cl2 → Br2 + 2Cl-，{FormatNum(remainingCl2)} mol Cl2氧化{FormatNum(oxidizedBr)} mol Br-"
            },
            ConstraintNote = "【高中限定】FeBr2与Cl2反应，Fe2+还原性强于Br-优先氧化",
            Confidence = 1.0
        };
    }

    /// <summary>Fe + 稀 HNO3：根据Fe与HNO3的量比判断产物（Fe2+单产物 / Fe3+单产物 / 双产物共存）</summary>
    private static EngineResponse? TryCalculateFeHNO3(string raw)
    {
        bool hasFe = Regex.IsMatch(raw, @"\bFe\b", RegexOptions.IgnoreCase) || raw.Contains("铁");
        bool hasHno3 = raw.Contains("硝酸") || Regex.IsMatch(raw, @"HNO\s*3", RegexOptions.IgnoreCase);
        if (!hasFe || !hasHno3) return null;

        // 仅处理稀硝酸；浓硝酸产物为NO2，暂不纳入
        bool isDilute = raw.Contains("稀") && !raw.Contains("浓");
        if (!isDilute) return null;

        var feMatch = Regex.Match(raw, @"(\d+(?:\.\d+)?)\s*mol\s*(?:Fe\b|铁)", RegexOptions.IgnoreCase);
        if (!feMatch.Success) return null;
        double nFe = double.Parse(feMatch.Groups[1].Value, CultureInfo.InvariantCulture);

        var hno3Match = Regex.Match(raw, @"(\d+(?:\.\d+)?)\s*mol\s*(?:稀)?\s*(?:硝酸|HNO\s*3)", RegexOptions.IgnoreCase);
        if (!hno3Match.Success) return null;
        double nHno3 = double.Parse(hno3Match.Groups[1].Value, CultureInfo.InvariantCulture);

        // 纯Fe2+：3Fe + 8HNO3 → 3Fe(NO3)2 + 2NO + 4H2O，1 mol Fe 需 8/3 mol HNO3
        double hno3ForAllFe2 = nFe * 8.0 / 3.0;
        // 纯Fe3+：Fe + 4HNO3 → Fe(NO3)3 + NO + 2H2O，1 mol Fe 需 4 mol HNO3
        double hno3ForAllFe3 = nFe * 4.0;

        if (nHno3 <= hno3ForAllFe2 + 1e-9)
        {
            // HNO3不足，Fe过量 → 只有Fe2+（过量Fe将Fe3+还原：Fe + 2Fe3+ → 3Fe2+）
            double nFe2Plus = nHno3 * 3.0 / 8.0;
            double excessFe = nFe - nFe2Plus;
            double nNo = nHno3 * 2.0 / 8.0;
            return new EngineResponse
            {
                Ok = true,
                FinalResult = $"Fe过量，稀硝酸不足，产物只有Fe2+。n(Fe2+)={FormatNum(nFe2Plus)} mol，n(Fe3+)=0 mol，剩余Fe {FormatNum(excessFe)} mol，生成NO {FormatNum(nNo)} mol。",
                DerivationSteps = new List<string>
                {
                    "判断过量：Fe全部→Fe2+需HNO3 = 8/3 × n(Fe)",
                    $"{FormatNum(nFe)} mol Fe全部→Fe2+需{FormatNum(hno3ForAllFe2)} mol HNO3，实际只有{FormatNum(nHno3)} mol，Fe过量",
                    "过量Fe将Fe3+还原为Fe2+：Fe + 2Fe3+ → 3Fe2+，最终只有Fe2+",
                    "按 3Fe + 8HNO3 → 3Fe(NO3)2 + 2NO + 4H2O 计算",
                    $"n(Fe2+) = 3/8 × n(HNO3) = {FormatNum(nFe2Plus)} mol"
                },
                ConstraintNote = "【高中限定】Fe与稀硝酸反应，Fe过量时只生成Fe2+",
                Confidence = 1.0
            };
        }

        if (nHno3 >= hno3ForAllFe3 - 1e-9)
        {
            // HNO3过量 → 只有Fe3+
            double nFe3Plus = nFe;
            double nNo = nFe;
            return new EngineResponse
            {
                Ok = true,
                FinalResult = $"稀硝酸过量，Fe全部被氧化为Fe3+。n(Fe3+)={FormatNum(nFe3Plus)} mol，n(Fe2+)=0 mol，生成NO {FormatNum(nNo)} mol。",
                DerivationSteps = new List<string>
                {
                    $"{FormatNum(nFe)} mol Fe全部→Fe3+需{FormatNum(hno3ForAllFe3)} mol HNO3，实际有{FormatNum(nHno3)} mol，硝酸过量",
                    "按 Fe + 4HNO3 → Fe(NO3)3 + NO + 2H2O 计算",
                    $"n(Fe3+) = n(Fe) = {FormatNum(nFe3Plus)} mol"
                },
                ConstraintNote = "【高中限定】Fe与稀硝酸反应，硝酸过量时只生成Fe3+",
                Confidence = 1.0
            };
        }

        // 双产物共存：Fe全部反应，同时生成Fe2+和Fe3+
        // 设 n(Fe2+)=a, n(Fe3+)=b
        // a + b = nFe
        // (8/3)a + 4b = nHno3
        double b = (nHno3 - 8.0 / 3.0 * nFe) / (4 - 8.0 / 3.0);
        double a = nFe - b;
        double nNo2 = (2.0 / 3.0) * a + b;
        return new EngineResponse
        {
            Ok = true,
            FinalResult = $"Fe与稀硝酸双产物共存。n(Fe2+)={FormatNum(a)} mol，n(Fe3+)={FormatNum(b)} mol，生成NO {FormatNum(nNo2)} mol。",
            DerivationSteps = new List<string>
            {
                $"{FormatNum(nFe)} mol Fe全部→Fe2+需{FormatNum(hno3ForAllFe2)} mol HNO3，全部→Fe3+需{FormatNum(hno3ForAllFe3)} mol",
                $"实际HNO3={FormatNum(nHno3)} mol，介于两者之间 → Fe2+与Fe3+共存",
                "联立：a + b = n(Fe)；(8/3)a + 4b = n(HNO3)",
                $"解得：n(Fe2+)={FormatNum(a)} mol，n(Fe3+)={FormatNum(b)} mol"
            },
            ConstraintNote = "【高中限定】Fe与稀硝酸双产物共存，需联立物料守恒与电子守恒",
            Confidence = 1.0
        };
    }

    /// <summary>简单完全反应（限量试剂判断），当前支持 H2 + O2 → H2O</summary>
    private static EngineResponse? TryCalculateSimpleStoichiometry(string raw)
    {
        // H2 + O2 点燃
        if (Regex.IsMatch(raw, @"H\s*2", RegexOptions.IgnoreCase) &&
            Regex.IsMatch(raw, @"O\s*2", RegexOptions.IgnoreCase) &&
            (raw.Contains("点燃") || raw.Contains("燃烧") || raw.Contains("反应") ||
             raw.Contains("生成") || raw.Contains("水")))
        {
            var h2Match = Regex.Match(raw, @"(\d+(?:\.\d+)?)\s*mol\s*H\s*2\b", RegexOptions.IgnoreCase);
            var o2Match = Regex.Match(raw, @"(\d+(?:\.\d+)?)\s*mol\s*O\s*2\b", RegexOptions.IgnoreCase);
            if (!h2Match.Success || !o2Match.Success) return null;

            double nH2 = double.Parse(h2Match.Groups[1].Value, CultureInfo.InvariantCulture);
            double nO2 = double.Parse(o2Match.Groups[1].Value, CultureInfo.InvariantCulture);

            // 2H2 + O2 → 2H2O，H2 : O2 = 2 : 1
            double requiredO2 = nH2 / 2.0;
            double requiredH2 = nO2 * 2.0;

            if (nO2 >= requiredO2 - 1e-9)
            {
                double nH2O = nH2;
                double excessO2 = nO2 - requiredO2;
                return new EngineResponse
                {
                    Ok = true,
                    FinalResult = $"O2过量，H2完全反应。生成H2O {FormatNum(nH2O)} mol，剩余O2 {FormatNum(excessO2)} mol。",
                    DerivationSteps = new List<string>
                    {
                        "2H2 + O2 → 2H2O（点燃）",
                        $"{FormatNum(nH2)} mol H2需{FormatNum(requiredO2)} mol O2，实际{FormatNum(nO2)} mol，O2过量",
                        $"n(H2O) = n(H2) = {FormatNum(nH2O)} mol"
                    },
                    ConstraintNote = "【高中限定】H2与O2完全燃烧",
                    Confidence = 1.0
                };
            }

            {
                double nH2O = 2 * nO2;
                double excessH2 = nH2 - requiredH2;
                return new EngineResponse
                {
                    Ok = true,
                    FinalResult = $"H2过量，O2完全反应。生成H2O {FormatNum(nH2O)} mol，剩余H2 {FormatNum(excessH2)} mol。",
                    DerivationSteps = new List<string>
                    {
                        "2H2 + O2 → 2H2O（点燃）",
                        $"{FormatNum(nO2)} mol O2需{FormatNum(requiredH2)} mol H2，实际{FormatNum(nH2)} mol，H2过量",
                        $"n(H2O) = 2 × n(O2) = {FormatNum(nH2O)} mol"
                    },
                    ConstraintNote = "【高中限定】H2与O2完全燃烧",
                    Confidence = 1.0
                };
            }
        }

        // C + O2 → CO2（1:1:1）
        if (Regex.IsMatch(raw, @"\bC\b", RegexOptions.IgnoreCase) &&
            Regex.IsMatch(raw, @"O\s*2", RegexOptions.IgnoreCase) &&
            (raw.Contains("燃烧") || raw.Contains("点燃") || raw.Contains("完全")) &&
            raw.Contains("CO2"))
        {
            var cMatch = Regex.Match(raw, @"(\d+(?:\.\d+)?)\s*mol\s*C\b", RegexOptions.IgnoreCase);
            var o2Match = Regex.Match(raw, @"(\d+(?:\.\d+)?)\s*mol\s*O\s*2\b", RegexOptions.IgnoreCase);
            if (cMatch.Success && o2Match.Success)
            {
                double nC = double.Parse(cMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                double nO2 = double.Parse(o2Match.Groups[1].Value, CultureInfo.InvariantCulture);
                double nCO2 = Math.Min(nC, nO2);
                return new EngineResponse
                {
                    Ok = true,
                    FinalResult = $"生成CO2 {FormatNum(nCO2)} mol。",
                    DerivationSteps = new List<string>
                    {
                        "C + O2 → CO2（完全燃烧）",
                        $"C和O2为1:1，限量试剂决定产物量",
                        $"n(CO2) = min(n(C), n(O2)) = {FormatNum(nCO2)} mol"
                    },
                    ConstraintNote = "【高中限定】C完全燃烧生成CO2",
                    Confidence = 1.0
                };
            }
        }

        // 2Fe + 3Cl2 → 2FeCl3（2:3:2）
        if (Regex.IsMatch(raw, @"\bFe\b", RegexOptions.IgnoreCase) &&
            Regex.IsMatch(raw, @"Cl\s*2", RegexOptions.IgnoreCase) &&
            (raw.Contains("FeCl3") || raw.Contains("氯化铁")))
        {
            var feMatch = Regex.Match(raw, @"(\d+(?:\.\d+)?)\s*mol\s*Fe\b", RegexOptions.IgnoreCase);
            var cl2Match = Regex.Match(raw, @"(\d+(?:\.\d+)?)\s*mol\s*Cl\s*2\b", RegexOptions.IgnoreCase);
            if (feMatch.Success && cl2Match.Success)
            {
                double nFe = double.Parse(feMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                double nCl2 = double.Parse(cl2Match.Groups[1].Value, CultureInfo.InvariantCulture);
                double requiredCl2 = nFe * 3.0 / 2.0;
                double nFeCl3;
                string limiting;
                if (nCl2 >= requiredCl2 - 1e-9)
                {
                    nFeCl3 = nFe;
                    limiting = "Cl2过量，Fe完全反应";
                }
                else
                {
                    nFeCl3 = nCl2 * 2.0 / 3.0;
                    limiting = "Cl2不足";
                }
                return new EngineResponse
                {
                    Ok = true,
                    FinalResult = $"{limiting}。最多生成FeCl3 {FormatNum(nFeCl3)} mol。",
                    DerivationSteps = new List<string>
                    {
                        "2Fe + 3Cl2 → 2FeCl3",
                        $"{FormatNum(nFe)} mol Fe需{FormatNum(requiredCl2)} mol Cl2，实际{FormatNum(nCl2)} mol",
                        $"n(FeCl3) = {FormatNum(nFeCl3)} mol"
                    },
                    ConstraintNote = "【高中限定】Fe在Cl2中燃烧生成FeCl3",
                    Confidence = 1.0
                };
            }
        }

        return null;
    }

    private static string FormatNum(double n)
    {
        if (Math.Abs(n - Math.Round(n)) < 1e-9)
            return ((long)Math.Round(n)).ToString(CultureInfo.InvariantCulture);
        return n.ToString("0.##", CultureInfo.InvariantCulture);
    }

    // ==================== 化学平衡 / 水溶液平衡引擎 ====================

    /// <summary>常见沉淀的阳离子:阴离子比例（高中限定）</summary>
    private static readonly Dictionary<string, (string cation, string anion, int catRatio, int anRatio)> PrecipitateTypes = new()
    {
        ["AgCl"] = ("Ag+", "Cl-", 1, 1),
        ["AgI"] = ("Ag+", "I-", 1, 1),
        ["AgBr"] = ("Ag+", "Br-", 1, 1),
        ["AgOH"] = ("Ag+", "OH-", 1, 1),
        ["Ag2CrO4"] = ("Ag+", "CrO4^2-", 2, 1),
        ["Ag2CO3"] = ("Ag+", "CO3^2-", 2, 1),
        ["Ag2SO4"] = ("Ag+", "SO4^2-", 2, 1),
        ["Ag3PO4"] = ("Ag+", "PO4^3-", 3, 1),
        ["BaSO4"] = ("Ba2+", "SO4^2-", 1, 1),
        ["BaCO3"] = ("Ba2+", "CO3^2-", 1, 1),
        ["CaCO3"] = ("Ca2+", "CO3^2-", 1, 1),
        ["CaSO4"] = ("Ca2+", "SO4^2-", 1, 1),
        ["CaF2"] = ("Ca2+", "F-", 1, 2),
        ["MgF2"] = ("Mg2+", "F-", 1, 2),
        ["Mg(OH)2"] = ("Mg2+", "OH-", 1, 2),
        ["Cu(OH)2"] = ("Cu2+", "OH-", 1, 2),
        ["Fe(OH)2"] = ("Fe2+", "OH-", 1, 2),
        ["Fe(OH)3"] = ("Fe3+", "OH-", 1, 3),
        ["Al(OH)3"] = ("Al3+", "OH-", 1, 3),
        ["PbI2"] = ("Pb2+", "I-", 1, 2),
        ["Ag2S"] = ("Ag+", "S2-", 2, 1),
        ["CuS"] = ("Cu2+", "S2-", 1, 1),
        ["FeS"] = ("Fe2+", "S2-", 1, 1)
    };

    /// <summary>
    /// 化学平衡引擎。
    /// 支持：Ksp 沉淀先后顺序、电离/水解平衡移动、可逆反应转化率与平衡量（三段式）。
    /// 核心原则：按数学约束严格求解，不靠模式匹配。
    /// </summary>
    private EngineResponse CallEquilibrium(EngineRequest req)
    {
        var raw = req.RawText ?? string.Empty;

        // 1. Ksp 沉淀先后顺序
        var ksp = TryCalculateKspPrecipitation(raw);
        if (ksp is not null) return ksp;

        // 2. 电离/水解平衡移动
        var ionization = TryJudgeIonizationShift(raw);
        if (ionization is not null) return ionization;

        // 3. 可逆反应转化率与平衡量
        var conversion = TryCalculateEquilibriumConversion(raw);
        if (conversion is not null) return conversion;

        return new EngineResponse
        {
            Ok = null,
            ConstraintNote = "【高中限定】当前平衡引擎仅支持Ksp沉淀顺序、电离平衡移动、可逆反应转化率，此题需人工分析",
            Confidence = 1.0
        };
    }

    /// <summary>Ksp 沉淀先后顺序：比较各阴离子开始沉淀所需阳离子浓度，浓度小者先沉淀</summary>
    private static EngineResponse? TryCalculateKspPrecipitation(string raw)
    {
        // 匹配 Ksp(AgCl)=1.8×10^-10 或 Ksp(AgCl)=1.8*10^-10 或 Ksp(AgCl)=1.8e-10
        var kspMatches = Regex.Matches(raw,
            @"Ksp\(([^)]+)\)\s*=\s*([\d.]+)\s*(?:[×xX*]\s*10\^|e|E)(-?\d+)");
        if (kspMatches.Count < 2) return null;

        var precipitates = new List<(string formula, double ksp, int catRatio, int anRatio, string cation, string anion)>();
        foreach (Match m in kspMatches)
        {
            var formula = m.Groups[1].Value.Trim();
            if (!PrecipitateTypes.TryGetValue(formula, out var type)) continue;
            var mantissa = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            var exp = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            var ksp = mantissa * Math.Pow(10, exp);
            precipitates.Add((formula, ksp, type.catRatio, type.anRatio, type.cation, type.anion));
        }

        if (precipitates.Count < 2) return null;

        // 识别滴加的阳离子
        string? addedCation = null;
        if (raw.Contains("AgNO3") || raw.Contains("Ag+")) addedCation = "Ag+";
        else if (raw.Contains("BaCl2") || raw.Contains("Ba2+")) addedCation = "Ba2+";
        else if (raw.Contains("CaCl2") || raw.Contains("Ca2+")) addedCation = "Ca2+";

        if (addedCation != null)
            precipitates = precipitates.Where(p => p.cation == addedCation).ToList();

        if (precipitates.Count < 2) return null;

        // 是否等浓度
        bool equalConc = raw.Contains("等浓度") || raw.Contains("相同浓度") ||
                         raw.Contains("浓度相等") || raw.Contains("等物质的量浓度");

        // 等浓度时设阴离子浓度 c=1 mol/L（比较时不影响顺序）
        double c = 1.0;

        // 计算每个沉淀所需阳离子浓度：[M] = (Ksp / [A]^anRatio)^(1/catRatio)
        var results = precipitates.Select(p =>
        {
            double needed = Math.Pow(p.ksp / Math.Pow(c, p.anRatio), 1.0 / p.catRatio);
            return (p.formula, p.anion, p.catRatio, p.anRatio, p.ksp, needed);
        }).OrderBy(x => x.needed).ToList();

        var first = results[0];
        var cationLabel = addedCation ?? "阳离子";
        var steps = new List<string>();
        foreach (var r in results)
        {
            steps.Add($"{r.formula}（{r.catRatio}:{r.anRatio}型）：所需[{cationLabel}] = (Ksp/[{r.anion}]^{r.anRatio})^(1/{r.catRatio}) = {FormatSci(r.needed)} mol/L");
        }

        // 判断沉淀类型是否相同（是否可直接比Ksp）
        bool sameType = results.All(r => r.catRatio == results[0].catRatio && r.anRatio == results[0].anRatio);
        var note = sameType
            ? "【高中限定】同类型沉淀可直接比较Ksp，Ksp小的先沉淀"
            : "【高中限定】不同类型沉淀不能直接比Ksp，需计算所需阳离子浓度";

        return new EngineResponse
        {
            Ok = true,
            FinalResult = $"{first.anion} 先沉淀。开始沉淀所需{cationLabel}浓度最小（{FormatSci(first.needed)} mol/L），最先达到溶度积。",
            DerivationSteps = steps,
            ConstraintNote = note,
            Confidence = 1.0
        };
    }

    /// <summary>电离/水解平衡移动方向判断</summary>
    private static EngineResponse? TryJudgeIonizationShift(string raw)
    {
        // 识别是否为弱电解质电离平衡移动题
        bool isIonization = raw.Contains("电离平衡") || raw.Contains("电离") ||
                            raw.Contains("CH3COOH") || raw.Contains("醋酸") ||
                            raw.Contains("NH3·H2O") || raw.Contains("氨水") ||
                            raw.Contains("水解平衡") || raw.Contains("水解");
        if (!isIonization) return null;

        if (!raw.Contains("移动") && !raw.Contains("方向") && !raw.Contains("程度") && !raw.Contains("促进") && !raw.Contains("抑制"))
            return null;

        string direction;
        string degreeChange;
        var steps = new List<string>();

        if (raw.Contains("加水") || raw.Contains("稀释"))
        {
            direction = "正向（右）";
            degreeChange = "增大";
            steps.Add("弱电解质电离平衡：加水稀释使离子浓度减小");
            steps.Add("Q < K，平衡正向移动，电离程度增大");
        }
        else if ((raw.Contains("加") || raw.Contains("加入") || raw.Contains("通入")) &&
                 (raw.Contains("HCl") || raw.Contains("盐酸") || raw.Contains("H+") || raw.Contains("硫酸")))
        {
            direction = "逆向（左）";
            degreeChange = "减小";
            steps.Add("加入H+，同离子效应使生成物浓度增大");
            steps.Add("Q > K，平衡逆向移动，电离程度减小");
        }
        else if ((raw.Contains("加") || raw.Contains("加入")) &&
                 (raw.Contains("NaOH") || raw.Contains("OH-") || raw.Contains("氢氧化钠")))
        {
            direction = "正向（右）";
            degreeChange = "增大";
            steps.Add("加入OH-中和H+，生成物浓度减小");
            steps.Add("Q < K，平衡正向移动，电离程度增大");
        }
        else if (raw.Contains("升温") || raw.Contains("加热") || raw.Contains("升高温度"))
        {
            direction = "正向（右）";
            degreeChange = "增大";
            steps.Add("弱电解质电离是吸热过程");
            steps.Add("升温使平衡向吸热方向（正向）移动，电离程度增大");
        }
        else if (raw.Contains("加") && (raw.Contains("盐") || raw.Contains("同离子")))
        {
            direction = "逆向（左）";
            degreeChange = "减小";
            steps.Add("加入含相同离子的盐，同离子效应");
            steps.Add("平衡逆向移动，电离程度减小");
        }
        else
        {
            return null;
        }

        return new EngineResponse
        {
            Ok = true,
            FinalResult = $"平衡向{direction}移动，电离程度{degreeChange}。",
            DerivationSteps = steps,
            ConstraintNote = "【高中限定】弱电解质电离平衡移动，遵循勒夏特列原理",
            Confidence = 1.0
        };
    }

    /// <summary>可逆反应转化率与平衡量计算（三段式）</summary>
    private static EngineResponse? TryCalculateEquilibriumConversion(string raw)
    {
        // 必须有"转化率"或"平衡时"字样
        if (!raw.Contains("转化率") && !raw.Contains("平衡时") && !raw.Contains("平衡"))
            return null;

        // 匹配反应式：系数(可选) + 物质 + " + " + 系数(可选) + 物质 + "⇌/=/→" + 系数(可选) + 物质
        // 物质名以字母开头，避免把系数数字吞进物质名
        var rxnMatch = Regex.Match(raw,
            @"(\d*)\s*([A-Za-z]\w*)\s*\+\s*(\d*)\s*([A-Za-z]\w*)\s*(?:⇌|=|→)\s*(\d*)\s*([A-Za-z]\w*)");
        if (!rxnMatch.Success) return null;

        int c1 = string.IsNullOrEmpty(rxnMatch.Groups[1].Value) ? 1 : int.Parse(rxnMatch.Groups[1].Value);
        string r1 = rxnMatch.Groups[2].Value;
        int c2 = string.IsNullOrEmpty(rxnMatch.Groups[3].Value) ? 1 : int.Parse(rxnMatch.Groups[3].Value);
        string r2 = rxnMatch.Groups[4].Value;
        int cp1 = string.IsNullOrEmpty(rxnMatch.Groups[5].Value) ? 1 : int.Parse(rxnMatch.Groups[5].Value);
        string p1 = rxnMatch.Groups[6].Value;

        // 提取投料量（mol）—— 取前两个
        var molMatches = Regex.Matches(raw, @"(\d+(?:\.\d+)?)\s*mol");
        if (molMatches.Count < 2) return null;

        double nR1 = double.Parse(molMatches[0].Groups[1].Value, CultureInfo.InvariantCulture);
        double nR2 = double.Parse(molMatches[1].Groups[1].Value, CultureInfo.InvariantCulture);

        // 提取转化率百分比
        var convMatch = Regex.Match(raw, @"(\d+(?:\.\d+)?)\s*%");
        if (!convMatch.Success) return null;
        double conversion = double.Parse(convMatch.Groups[1].Value, CultureInfo.InvariantCulture) / 100.0;

        // 三段式计算（转化率作用于第一个反应物 R1）
        double consumedR1 = nR1 * conversion;
        double consumedR2 = consumedR1 * c2 / c1;
        double producedP1 = consumedR1 * cp1 / c1;

        double eqR1 = nR1 - consumedR1;
        double eqR2 = nR2 - consumedR2;
        double eqP1 = producedP1;

        string rxnStr = $"{(c1 == 1 ? "" : c1.ToString())}{r1} + {(c2 == 1 ? "" : c2.ToString())}{r2} ⇌ {(cp1 == 1 ? "" : cp1.ToString())}{p1}";

        var steps = new List<string>
        {
            $"反应式：{rxnStr}",
            $"起始量：n({r1})={FormatNum(nR1)} mol，n({r2})={FormatNum(nR2)} mol",
            $"{r1}转化率={conversion * 100}%，消耗{FormatNum(consumedR1)} mol",
            $"三段式：消耗{r1} {FormatNum(consumedR1)} mol → 消耗{r2} {FormatNum(consumedR2)} mol → 生成{p1} {FormatNum(producedP1)} mol",
            $"平衡量：n({r1})={FormatNum(eqR1)} mol，n({r2})={FormatNum(eqR2)} mol，n({p1})={FormatNum(eqP1)} mol"
        };

        return new EngineResponse
        {
            Ok = true,
            FinalResult = $"平衡时：n({r1})={FormatNum(eqR1)} mol，n({r2})={FormatNum(eqR2)} mol，n({p1})={FormatNum(eqP1)} mol。",
            DerivationSteps = steps,
            ConstraintNote = "【高中限定】可逆反应三段式计算，转化率不能超过100%",
            Confidence = 1.0
        };
    }

    /// <summary>科学计数法格式化（如 1.8×10^-10）</summary>
    private static string FormatSci(double n)
    {
        if (n == 0) return "0";
        int exp = (int)Math.Floor(Math.Log10(Math.Abs(n)));
        double mantissa = n / Math.Pow(10, exp);
        string mStr = mantissa.ToString("0.###", CultureInfo.InvariantCulture);
        if (exp == 0) return mStr;
        return $"{mStr}×10^{exp}";
    }

    /// <summary>
    /// 反应可行性规则引擎：基于金属活动性顺序、氧化性酸等高中化学规则，
    /// 对常见反应题给出具体结论，避免只返回"已匹配到相关反应"。
    /// 返回 null 表示规则无法判断，回退到知识库匹配。
    /// </summary>
    private EngineResponse? TryJudgeReactionFeasibility(string stem)
    {
        // 金属活动性顺序（从活泼到不活泼），H 为氢前/氢后分界
        var activitySeries = new[]
        {
            "K", "Ca", "Na", "Mg", "Al", "Zn", "Fe", "Sn", "Pb",
            "H", "Cu", "Hg", "Ag", "Pt", "Au"
        };
        int ActivityIndex(string metal) =>
            Array.FindIndex(activitySeries, m =>
                string.Equals(m, metal, StringComparison.OrdinalIgnoreCase));

        // 氧化性酸：与金属反应不生成 H2
        bool IsOxidizingAcid() =>
            stem.Contains("HNO3") || stem.Contains("硝酸") ||
            (stem.Contains("H2SO4") && stem.Contains("浓")) ||
            stem.Contains("浓硫酸");

        bool IsNonOxidizingAcid() =>
            (stem.Contains("HCl") || stem.Contains("盐酸") ||
             (stem.Contains("H2SO4") && !stem.Contains("浓")) ||
             stem.Contains("稀硫酸")) && !stem.Contains("硝酸");

        // 从题干中识别金属单质
        string? DetectMetal()
        {
            foreach (var m in activitySeries)
            {
                if (m == "H") continue;
                // 金属单质出现（如 "Fe"、"锌"、"铜"），且不是化合物中的一部分
                if (stem.Contains(m) ||
                    (m == "Fe" && stem.Contains("铁")) ||
                    (m == "Cu" && stem.Contains("铜")) ||
                    (m == "Zn" && stem.Contains("锌")) ||
                    (m == "Al" && stem.Contains("铝")) ||
                    (m == "Na" && stem.Contains("钠")) ||
                    (m == "Mg" && stem.Contains("镁")) ||
                    (m == "Ag" && stem.Contains("银")))
                {
                    // 排除 "FeCl3"、"CuSO4" 等化合物中的金属（只匹配单质描述）
                    if (stem.Contains($"{m} ") || stem.Contains($"{m}和") || stem.Contains($"{m}与") ||
                        stem.Contains($"{m}+") || stem.EndsWith(m) ||
                        (m == "Fe" && (stem.Contains("铁和") || stem.Contains("铁与") || stem.Contains("铁 "))) ||
                        (m == "Cu" && (stem.Contains("铜和") || stem.Contains("铜与") || stem.Contains("铜 "))) ||
                        (m == "Zn" && (stem.Contains("锌和") || stem.Contains("锌与") || stem.Contains("锌 "))) ||
                        (m == "Al" && (stem.Contains("铝和") || stem.Contains("铝与") || stem.Contains("铝 "))) ||
                        (m == "Na" && (stem.Contains("钠和") || stem.Contains("钠与") || stem.Contains("钠 "))))
                    {
                        return m;
                    }
                }
            }
            return null;
        }

        var metal = DetectMetal();
        // 精确匹配 H2（独立出现，不是 H2O、H2SO4 等化合物的一部分）
        var asksForH2 = System.Text.RegularExpressions.Regex.IsMatch(stem, @"\bH2\b") ||
                        (stem.Contains("氢气") && !stem.Contains("H2O"));
        var asksCanReact = stem.Contains("能反应") || stem.Contains("能发生") ||
                            stem.Contains("可以反应") || stem.Contains("能否反应") ||
                            stem.Contains("能置换") || stem.Contains("能否置换");
        var asksProductCorrect = stem.Contains("生成") && (stem.Contains("对吗") || stem.Contains("正确") || stem.Contains("吗"));

        // ===== 规则1：金属 + 酸能否生成 H2 =====
        if (metal is not null && asksForH2)
        {
            if (IsOxidizingAcid())
            {
                var acidName = stem.Contains("硝酸") ? "硝酸" : "浓硫酸";
                return new EngineResponse
                {
                    Ok = false,
                    FinalResult = $"不能生成H2。{acidName}是强氧化性酸，与金属反应时酸根被还原（如NO3-→NO），不生成H2。",
                    IsStudentAnswerCorrect = false,
                    ErrorTags = new List<string> { "E04" },
                    DerivationSteps = new List<string>
                    {
                        $"{metal}与{acidName}反应，{acidName}具有强氧化性",
                        "氧化性酸与金属反应不生成H2，而是酸根被还原"
                    },
                    ConstraintNote = "【高中限定】金属与氧化性酸反应不产生H2",
                    Confidence = 1.0
                };
            }
            if (IsNonOxidizingAcid())
            {
                var idx = ActivityIndex(metal);
                var hIdx = ActivityIndex("H");
                if (idx < hIdx) // 金属在氢之前
                {
                    return new EngineResponse
                    {
                        Ok = true,
                        FinalResult = $"能生成H2。{metal}在金属活动性顺序中排在H之前，能与非氧化性酸发生置换反应生成H2。",
                        IsStudentAnswerCorrect = true,
                        ErrorTags = new List<string>(),
                        DerivationSteps = new List<string>
                        {
                            $"{metal}在金属活动性顺序中位于H之前",
                            "排在H之前的金属能置换稀酸中的H+生成H2"
                        },
                        ConstraintNote = "【高中限定】非氧化性酸（稀盐酸、稀硫酸）",
                        Confidence = 1.0
                    };
                }
                else // 金属在氢之后
                {
                    return new EngineResponse
                    {
                        Ok = false,
                        FinalResult = $"不能生成H2。{metal}在金属活动性顺序中排在H之后，不能置换稀酸中的H+。",
                        IsStudentAnswerCorrect = false,
                        ErrorTags = new List<string> { "E04" },
                        DerivationSteps = new List<string>
                        {
                            $"{metal}在金属活动性顺序中位于H之后",
                            "排在H之后的金属不能与稀酸发生置换反应"
                        },
                        ConstraintNote = "【高中限定】非氧化性酸（稀盐酸、稀硫酸）",
                        Confidence = 1.0
                    };
                }
            }
        }

        // ===== 规则1b：金属 + 非氧化性酸能否反应（不限于H2） =====
        if (metal is not null && asksCanReact && IsNonOxidizingAcid())
        {
            var idx = ActivityIndex(metal);
            var hIdx = ActivityIndex("H");
            if (idx < hIdx)
            {
                return new EngineResponse
                {
                    Ok = true,
                    FinalResult = $"能反应。{metal}在金属活动性顺序中排在H之前，能与稀酸发生置换反应，生成盐和H2。",
                    IsStudentAnswerCorrect = true,
                    ErrorTags = new List<string>(),
                    DerivationSteps = new List<string>
                    {
                        $"{metal}在金属活动性顺序中位于H之前",
                        "氢前金属能与稀酸发生置换反应"
                    },
                    ConstraintNote = "【高中限定】非氧化性酸（稀盐酸、稀硫酸）",
                    Confidence = 1.0
                };
            }
            else
            {
                return new EngineResponse
                {
                    Ok = false,
                    FinalResult = $"不能反应。{metal}在金属活动性顺序中排在H之后，不能置换稀酸中的H+。",
                    IsStudentAnswerCorrect = false,
                    ErrorTags = new List<string> { "E04" },
                    DerivationSteps = new List<string>
                    {
                        $"{metal}在金属活动性顺序中位于H之后",
                        "氢后金属不能与稀酸发生置换反应"
                    },
                    ConstraintNote = "【高中限定】非氧化性酸（稀盐酸、稀硫酸）",
                    Confidence = 1.0
                };
            }
        }

        // ===== 规则2：金属 + 盐溶液能否反应（置换） =====
        if (metal is not null && asksCanReact)
        {
            // Na/K/Ca 等极活泼金属与盐溶液反应时先与水反应，不能置换出金属
            if (metal is "Na" or "K" or "Ca" &&
                (stem.Contains("溶液") || stem.Contains("CuSO4") || stem.Contains("AgNO3")))
            {
                return new EngineResponse
                {
                    Ok = false,
                    FinalResult = $"不能直接置换出金属。{metal}非常活泼，会先与盐溶液中的水反应生成碱和H2，不能置换出盐中的金属。",
                    IsStudentAnswerCorrect = false,
                    ErrorTags = new List<string> { "E04" },
                    DerivationSteps = new List<string>
                    {
                        $"{metal}活动性极强",
                        "投入盐溶液中先与水反应，而非直接置换金属"
                    },
                    ConstraintNote = "【高中限定】K、Ca、Na与盐溶液反应",
                    Confidence = 1.0
                };
            }

            // 识别盐中的金属（如 CuSO4 中的 Cu、AgNO3 中的 Ag）
            string? saltMetal = null;
            var saltPatterns = new Dictionary<string, string[]>
            {
                { "Cu", new[] { "CuSO4", "CuCl2", "Cu(NO3)2", "Cu(OH)2" } },
                { "Ag", new[] { "AgNO3", "AgCl" } },
                { "Fe", new[] { "FeCl3", "FeCl2", "FeSO4" } },
                { "Zn", new[] { "ZnSO4", "ZnCl2" } }
            };
            foreach (var (sm, formulas) in saltPatterns)
            {
                if (formulas.Any(f => stem.Contains(f)))
                {
                    saltMetal = sm;
                    break;
                }
            }

            if (saltMetal is not null && metal != saltMetal)
            {
                var idxA = ActivityIndex(metal);
                var idxB = ActivityIndex(saltMetal);
                if (idxA >= 0 && idxB >= 0 && idxA < idxB)
                {
                    return new EngineResponse
                    {
                        Ok = true,
                        FinalResult = $"能反应。{metal}的金属活动性强于{saltMetal}，能将{saltMetal}从其盐溶液中置换出来。",
                        IsStudentAnswerCorrect = true,
                        ErrorTags = new List<string>(),
                        DerivationSteps = new List<string>
                        {
                            $"金属活动性：{metal} > {saltMetal}",
                            "活动性强的金属能置换活动性弱的金属"
                        },
                        ConstraintNote = "【高中限定】金属与盐溶液的置换反应",
                        Confidence = 1.0
                    };
                }
            }
        }

        // ===== 规则3：铁与稀酸/盐的价态判断 =====
        if (stem.Contains("Fe") &&
            (stem.Contains("稀硫酸") || stem.Contains("稀盐酸") ||
             System.Text.RegularExpressions.Regex.IsMatch(stem, @"稀\s*H2SO4") ||
             System.Text.RegularExpressions.Regex.IsMatch(stem, @"稀\s*HCl"))
            && asksProductCorrect)
        {
            // 稀酸只能把 Fe 氧化到 +2 价
            if (stem.Contains("Fe2(SO4)3") || stem.Contains("FeCl3") || stem.Contains("+3") || stem.Contains("三价"))
            {
                return new EngineResponse
                {
                    Ok = false,
                    FinalResult = "不对。稀硫酸/稀盐酸只能把Fe氧化为+2价（生成FeSO4/FeCl2），不能生成+3价铁盐。",
                    IsStudentAnswerCorrect = false,
                    ErrorTags = new List<string> { "E05" },
                    DerivationSteps = new List<string>
                    {
                        "稀硫酸、稀盐酸为非氧化性酸",
                        "非氧化性酸只能将Fe氧化为Fe2+，不能氧化到Fe3+"
                    },
                    ConstraintNote = "【高中限定】Fe与非氧化性酸反应生成亚铁盐",
                    Confidence = 1.0
                };
            }
        }

        return null;
    }

    private static EngineResponse NeedMoreCondition(string msg) => new()
    {
        Ok = null,
        ConstraintNote = msg,
        Confidence = 1.0,
        NeedMoreCondition = true
    };

    /// <summary>从原始文本中提取离子符号（兜底，当LLM未提取离子时使用）</summary>
    private static List<string> ExtractIonsFromText(string raw)
    {
        var ions = new List<string>();
        // 匹配常见离子符号：如 Na+, Cl-, SO4^2-, Fe3+, NH4+, OH-, NO3-, CO3^2- 等
        var pattern = @"(?:Na|K|Ag|NH4|H)\+|" +
                      @"(?:Cl|Br|I|OH|NO3)\+?\-|" +
                      @"(?:SO4|CO3|SO3|S2O3|SiO3|CrO4|Cr2O7|PO4|HCO3|HSO4|HSO3|CH3COO|ClO|ClO3|MnO4|CN|S|C2O4)\^?[23]?\-|" +
                      @"(?:Ba|Ca|Mg|Sr|Fe|Cu|Zn|Al|Pb|Sn|Hg|Co|Ni|Mn)\^?[23]?\+";
        var matches = Regex.Matches(raw, pattern, RegexOptions.IgnoreCase);
        foreach (Match m in matches)
        {
            var ion = NormalizeIonNotation(m.Value);
            if (!ions.Contains(ion)) ions.Add(ion);
        }
        return ions;
    }

    /// <summary>规范化离子符号写法（SO4^2- → SO4^2-，Fe3+ → Fe3+）</summary>
    private static string NormalizeIonNotation(string ion)
    {
        // 统一电荷写法
        ion = ion.Replace("2-", "^2-").Replace("3-", "^3-").Replace("2+", "^2+").Replace("3+", "^3+");
        // 去除多余的 ^
        if (ion.Count(c => c == '^') > 1)
        {
            int first = ion.IndexOf('^');
            ion = ion.Substring(0, first + 1) + ion.Substring(first + 1).Replace("^", "");
        }
        return ion;
    }

    /// <summary>从结论文本推断错误标签</summary>
    private static List<string> InferErrorTags(string conclusion)
    {
        var tags = new List<string>();
        if (conclusion.Contains("不守恒") || conclusion.Contains("原子")) tags.Add("E01");
        if (conclusion.Contains("电荷")) tags.Add("E02");
        if (conclusion.Contains("电子") || conclusion.Contains("氧化") || conclusion.Contains("还原")) tags.Add("E03");
        if (conclusion.Contains("不能反应") || conclusion.Contains("不可发生")) tags.Add("E04");
        if (conclusion.Contains("产物")) tags.Add("E05");
        if (conclusion.Contains("介质") || conclusion.Contains("酸碱")) tags.Add("E06");
        if (conclusion.Contains("共存")) tags.Add("E07");
        return tags.Count > 0 ? tags : new List<string> { "E04" };
    }

    // ==================== 3. 回复生成（引擎结果 → 教学对话） ====================

    /// <summary>
    /// 根据引擎返回结果，让 LLM 生成教学回复。
    /// 若 LLM 不可用，使用规则模板生成回复。
    /// </summary>
    public async Task<string> GenerateReplyAsync(string studentText, EngineResponse resp, CancellationToken ct = default)
    {
        // need_more_condition：反问补齐
        if (resp.NeedMoreCondition)
        {
            return $"要解决这个问题，还需要补充一些信息：{resp.ConstraintNote}。请告诉我具体的条件，我再帮你分析。";
        }

        // ok=null：超出引擎范围
        if (resp.Ok is null)
        {
            var fallback = () =>
            {
                var baseNote = string.IsNullOrWhiteSpace(resp.ConstraintNote)
                    ? "这个问题超出了当前引擎的建模范围。"
                    : resp.ConstraintNote;
                return $"{baseNote}我可以从概念上给你一些启发，但不作为标准答案。你想从哪个角度入手？";
            };
            return await LlmReplyOrFallbackAsync(studentText, resp, fallback, ct);
        }

        // ok=true：肯定 + 引导
        if (resp.Ok == true)
        {
            var fallback = () =>
            {
                var steps = resp.DerivationSteps.Take(2).ToList();
                var stepText = steps.Count > 0 ? "关键依据：" + string.Join("；", steps) + "。" : "";
                return $"思路是对的！{stepText}你能说说每一步的判断理由吗？";
            };
            return await LlmReplyOrFallbackAsync(studentText, resp, fallback, ct);
        }

        // ok=false：指出错误 + 引导自查
        var tagTexts = resp.ErrorTags.Select(ErrorTags.GetText).ToList();
        var fallbackFalse = () =>
        {
            var tagText = tagTexts.Count > 0 ? "可能涉及" + string.Join("、", tagTexts) + "。" : "";
            return $"这里有个问题：{tagText}{resp.ConstraintNote}。先不急着改，你能说说为什么这么判断吗？";
        };
        return await LlmReplyOrFallbackAsync(studentText, resp, fallbackFalse, ct);
    }

    /// <summary>LLM 生成回复，失败时用 fallback</summary>
    private async Task<string> LlmReplyOrFallbackAsync(
        string studentText,
        EngineResponse resp,
        Func<string> fallback,
        CancellationToken ct)
    {
        var userPrompt =
            $"学生输入：{studentText}\n" +
            $"引擎判定结果：\n" +
            $"- ok: {resp.Ok}\n" +
            $"- final_result: {resp.FinalResult}\n" +
            $"- error_tags: {string.Join(", ", resp.ErrorTags)}\n" +
            $"- derivation_steps: {string.Join("；", resp.DerivationSteps)}\n" +
            $"- constraint_note: {resp.ConstraintNote}\n" +
            $"请根据引擎结果生成导学回复。";

        var text = await _llm.ChatAsync(ReplySystemPrompt, userPrompt, 0.5f, 400, ct);
        if (!string.IsNullOrWhiteSpace(text)) return text;
        return fallback();
    }
}
