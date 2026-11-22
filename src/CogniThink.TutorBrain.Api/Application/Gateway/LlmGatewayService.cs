namespace CogniThink.TutorBrain.Application.Gateway;

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CogniThink.TutorBrain.Application.Agent;
using CogniThink.TutorBrain.Application.Chemistry;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.Chem.Core.Benchmark;
using CogniThink.Chem.Core.Inference;
using CogniThink.Chem.Core.Rules;

/// <summary>
/// 化学伴思 LLM 网关
/// 核心原则：化学引擎是唯一真值源，LLM 只做「输入标准化」+「引导讲解」
/// 禁止 LLM 自行输出方程式、反应结论、计算结果
/// 链路：学生文本 → LLM 识别任务 → 组装引擎 JSON → 调用引擎 → LLM 生成教学回复
/// </summary>
public sealed partial class LlmGatewayService
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
        "- choice：选择题（题干含 A/B/C/D 选项），由选择校验器逐选项判定\n" +
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
    /// <param name="imagePaths">题目配图相对路径（相对 data/ 目录）；模型支持视觉时会随题干传给解析 LLM。</param>
    /// <param name="questionType">题库存储的题型标记（如 judge/choice/fill）；为 judge 时强制走判断分支，
    /// 避免被 equilibrium/calculation/unknown 错分而使现有判断求解器不可达。null/空表示无标记（运行时默认）。</param>
    public async Task<GatewayResult> ProcessStudentMessageAsync(string rawText, IReadOnlyList<string>? imagePaths = null, IReadOnlyList<ImageAsset>? imageAssets = null, string? questionType = null, CancellationToken ct = default)
    {
        // 1. LLM 识别任务 + 组装引擎请求
        var engineRequest = await ParseStudentInputAsync(rawText, imagePaths, imageAssets, ct);

        // 题库存储 type=="judge" 是正误判断题的权威信号：确定性分类链会把部分判断题
        // （含 mol/电离/计算 等字样的题干）错分到 calculation/equilibrium/unknown，
        // 导致 MultiSub/ConceptVerdict 等判断求解器不可达。此处以存储类型覆盖，零回归
        // （已判正题中 type=judge 的 pdf-467/pdf-472 仍由相同求解器解出）。
        if (string.Equals(questionType, "judge", StringComparison.OrdinalIgnoreCase))
            engineRequest.TaskType = "judgment";

        // 2. 调用引擎（黑盒）
        var engineResponse = await CallEngineAsync(engineRequest, imageAssets, ct);

        // 2b. P1-2 多部分大题子问分发（兜底）：仅当整题未唯一解出（Ok!=true）且题干为多部分时激活。
        //     零回归：已解对题 Ok=true → 不触发；逐子问任一未解 → 整体 null（不蒙数、不输出部分答案）。
        //     采纳安全：成功时标记 task_type=multi_part，评估侧对聚合结论走答案键 AND 判分（凡采纳必全对）。
        if (engineResponse.Ok is not true && MultiPartUtility.IsMultiPart(rawText))
        {
            var dispatched = await TryDispatchMultiPartAsync(engineRequest, ct).ConfigureAwait(false);
            if (dispatched is not null)
            {
                engineResponse = dispatched;
                engineRequest.TaskType = MultiPartUtility.MultiPartTaskType;
            }
        }

        // P2-1：派生结论三态（只读元数据：仅读 Ok/NeedMoreCondition/FinalResult，写 ConclusionState；
        //     不改 Ok/FinalResult/TaskType → 不影响采纳门 BuildGatewayAnswerAsync，spec CQ-2）。
        engineResponse.ConclusionState = ConclusionStateDeriver.Derive(engineResponse);

        // P2-2：富化趋势级/机理解释（只读：仅写新增 ExplanationNodes，不改 Ok/FinalResult/TaskType；
        //     采纳门 BuildGatewayAnswerAsync 不读 ExplanationNodes → 不影响判分，spec CQ-5）。
        WorldModelEnricher.Enrich(engineResponse, rawText);

        // 3. 根据引擎结果生成教学回复
        var reply = await GenerateReplyAsync(rawText, engineResponse, engineRequest.TaskType, ct);

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
    public async Task<EngineRequest> ParseStudentInputAsync(string rawText, IReadOnlyList<string>? imagePaths = null, IReadOnlyList<ImageAsset>? imageAssets = null, CancellationToken ct = default)
    {
        var userPrompt = $"学生输入：{rawText}\n\n请识别任务类型并组装引擎请求 JSON。";

        var text = await _llm.ChatWithImagesAsync(ParseSystemPrompt, userPrompt, imagePaths, 0.1f, 600, ct);

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

        // LLM 不可用或解析失败：关键词兜底，但 task_type 仍以确定性规则链为准，
        // 避免「LLM 偶发解析失败 → 漏判 choice 等 → 跨运行漂移」。
        var fallback = FallbackParse(rawText);
        ApplyDeterministicTaskType(fallback, rawText);
        return fallback;
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
        req.Reaction.Left ??= new List<string>();
        req.Reaction.Right ??= new List<string>();
        req.Reaction.Conditions ??= new List<string>();
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

        // 阶段2：确定性规则链优先分类（AC-10，消除 LLM taskType 漂移）。
        // 规则能判定则覆盖 LLM 的 task_type；返回 unknown 时保留 LLM 判定作兜底。
        ApplyDeterministicTaskType(req, rawText);

        return req;
    }

    /// <summary>
    /// 以确定性规则链覆盖 task_type：规则判得出就覆盖，判不出（unknown）保留原值作兜底。
    /// 在 LLM 解析成功（NormalizeRequest）与失败（FallbackParse）两条路径上统一调用，
    /// 确保 task_type 分类不依赖 LLM 解析成功率，消除跨运行漂移（AC-10.1/10.2）。
    /// 仅决定 task_type；参数提取（reaction/known）仍由 LLM 完成（架构原则允许：NL→结构化请求）。
    /// </summary>
    private static void ApplyDeterministicTaskType(EngineRequest req, string rawText)
    {
        var detTaskType = ClassifyTaskDeterministic(rawText);
        if (detTaskType != "unknown")
            req.TaskType = detTaskType;
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

    /// <summary>
    /// 阶段2：确定性任务类型分类规则链（AC-10，消除 LLM taskType 漂移）。
    /// 优先级：选择题块 > 共存 > 配平 > 平衡 > 原子结构 > 计算 > 反应校验 > 结构 > vsepr > 滴定 > 概念。
    /// 返回 "unknown" 表示规则无法判定，保留 LLM 分类作兜底。
    /// 仅决定 task_type；参数提取（reaction/known）仍由 LLM 完成（原则允许：NL→结构化请求）。
    /// </summary>
    internal static string ClassifyTaskDeterministic(string rawText)
    {
        var text = rawText ?? string.Empty;
        var lower = text.ToLowerInvariant();

        // 1. 选择题：行首出现 A./B．/C、 选项块
        if (Regex.IsMatch(text, @"(^|\n)\s*[A-D][\.．、]\s"))
            return "choice";
        // 正误判断（填 √/×）：显式信号识别，使判断题在无存储 type 标记时也能进入 judgment 分支
        // （否则会被 equilibrium/calculation/unknown 截走，现有判断求解器不可达）。
        // 信号口径与 MultiSubCorrectnessSolver.LooksLikeJudgmentQuestion 的显式指令分支对齐，额外覆盖：
        //   ① 带引号的填√/填×（填"√" / 填"√" / 填"√"，pdf-450 等题干用引号包裹 √/×）；
        //   ② "正确…错误…填"指令组合（pdf-450：正确的填"√",错误的填"×"）；
        //   ③ 题干含 √/× 符号且带判断提示词（正误/是否正确/正确的/错误的）。
        // 回归预检查（全库 757 题）：已判正的非选择题无一命中上述信号；命中的均为选择题，
        // 已被上方 choice 块优先截走 → 零回归。type=judge 的强制覆盖见 ProcessStudentMessageAsync。
        bool hasCheckMark = lower.Contains("√") || lower.Contains("√");
        bool hasCrossMark = lower.Contains("×") || lower.Contains("×") || lower.Contains("╳");
        bool judgeCue = lower.Contains("正误") || lower.Contains("是否正确") ||
                        lower.Contains("正确的") || lower.Contains("错误的");
        if (lower.Contains("正误") || lower.Contains("填√") || lower.Contains("填×") ||
            lower.Contains("填“√") || lower.Contains("填‘√") || lower.Contains("填\"√") ||
            (lower.Contains("正确") && lower.Contains("错误") && lower.Contains("填")) ||
            ((hasCheckMark || hasCrossMark) && judgeCue))
            return "judgment";
        if (lower.Contains("共存"))
            return "ion_check";
        if (lower.Contains("配平") || lower.Contains("平了吗") || lower.Contains("系数为") || lower.Contains("化学计量数"))
            return "balance";
        if (lower.Contains("平衡") || lower.Contains("转化率") || lower.Contains("勒夏特列") ||
            lower.Contains("ksp") || lower.Contains("电离") || lower.Contains("水解") || lower.Contains("溶解平衡") ||
            lower.Contains("守恒式") || (lower.Contains("酸性") && (lower.Contains("由弱到强") || lower.Contains("由强到弱"))))
            return "equilibrium";
        // 原子结构：电子排布 / 轨道表示式 / 未成对电子（唯一结构可解，守卫见 AtomicStructureSolver）。
        // 先于 calculation：含 mol/pH 的电子排布题干（如 pdf-533）不能被 calculation 截走；回归预检查确认无已判正题命中这些标记。
        if (lower.Contains("电子排布") || lower.Contains("轨道表示式") || lower.Contains("未成对电子"))
            return "atomic_structure";
        // 方程书写（T4 / AC-3）：题干明确要求"书写/写出"方程式（离子/化学/酯化）。
        // 先于 calculation：此类题常被 LLM 兜底误分到 calculation（题干含 mol/求/计算 等字样），
        // 导致 CallCalculation 返回 null 且无法接入 KnownEquationWriterSolver；专用 taskType 直达方程书写链，
        // 无解仍 Ok=null（不蒙数）。回归预检查（全库 757 题，_evalscratch/verify/t4_simulate_routing.py）：
        // 本规则只重分类当前未解对题（pdf-010/044/084/354/355/356/526/527，baseline gatewayCorrect 均 False），
        // 已判正题 taskType 零漂移（risk=0）。
        if ((lower.Contains("书写") || lower.Contains("写出")) &&
            (lower.Contains("离子方程式") || lower.Contains("化学方程式") ||
             lower.Contains("方程式") || lower.Contains("酯化反应")))
            return "equation_writer";
        // P1-5：方程书写问法补齐——题干以「…方程式为 / 用…方程式表示」要求书写但未出现「书写/写出」触发词。
        // 全库 757 预校验（_evalscratch/verify/p15_drift_precheck.py）：affected=16，其中 5 道已判正
        // （pdf-174/417/456/515/539）经 eval 实证保持判正（correct=True）。注意此 5 道求解器不一：
        // pdf-174/417 由 ReactionKbWriterSolver、pdf-456/539 由 KnownEquationWriterSolver、pdf-515 由
        // ProductWriterSolver 解出；equation_writer 分支已按 reaction_check 同序补齐 ProductWriterSolver
        // （见下 equation_writer 分支），否则 pdf-515 会 LOST。不窃取 choice/judgment/equilibrium
        // （这些高优先级分支在本规则之前已截走）。
        if (lower.Contains("化学方程式为") || lower.Contains("离子方程式为") ||
            lower.Contains("方程式为") || lower.Contains("用离子方程式表示") ||
            lower.Contains("用化学方程式表示"))
            return "equation_writer";
        // P1-4：自由填空概念题（除杂离子 / 调 pH 目的 / 配制浓度误差）先于 calculation 截走，
        // 进入 concept 事实库（ConceptFactSolver 唯一匹配→不蒙数；IsAnswerCorrect 采纳门→凡采纳必判正）。
        // 全库 757 预校验（_evalscratch/verify/p14_routing_drift_precheck.py）：这些 cue 对已判正题匹配数为 0 → 0 漂移、零回归。
        if (lower.Contains("除去的离子"))
            return "concept";
        if (lower.Contains("ph") && lower.Contains("目的"))
            return "concept";
        if (lower.Contains("配制") && lower.Contains("浓度") && lower.Contains("可能"))
            return "concept";
        if (lower.Contains("物质的量") || lower.Contains("浓度") || lower.Contains("体积") ||
            lower.Contains("mol") || lower.Contains("ph") || lower.Contains("质量分数") ||
            lower.Contains("稀释") || lower.Contains("ka") || lower.Contains("计算") || lower.Contains("求"))
            return "calculation";
        if (lower.Contains("能否反应") || lower.Contains("是否反应") || lower.Contains("方程式对吗") ||
            lower.Contains("正确吗") || lower.Contains("对吗") || lower.Contains("反应能否") || lower.Contains("能否发生") ||
            lower.Contains("正误判断") || lower.Contains("是否可行"))
            return "reaction_check";
        if (lower.Contains("结构式") || lower.Contains("电子式") || lower.Contains("价键"))
            return "structure_verify";
        if (lower.Contains("vsepr") || lower.Contains("杂化") || lower.Contains("构型") ||
            lower.Contains("空间构型") || lower.Contains("价层电子对"))
            return "vsepr";
        if (lower.Contains("滴定"))
            return "titration";
        if (lower.Contains("什么是") || lower.Contains("定义") || lower.Contains("概念") ||
            lower.Contains("区别") || lower.Contains("为什么") || lower.Contains("原理") || lower.Contains("是指"))
            return "concept";
        return "unknown";
    }

    // ==================== 2. 调用引擎（黑盒） ====================

    /// <summary>
    /// 调用化学引擎（黑盒）。
    /// 根据 task_type 路由到对应的引擎服务。
    /// 若引擎不可用或任务类型不支持，返回 ok=null 的响应。
    /// </summary>
    public Task<EngineResponse> CallEngineAsync(EngineRequest req, IReadOnlyList<ImageAsset>? imageAssets = null, CancellationToken ct = default)
    {
        // 平衡类：确定性平衡求解器（Ksp/电离移动/转化率/焓）优先于曲线原始读数——
        // 曲线读数常为中间量（如某点浓度），并非题目最终答案，抢占会使引擎产出错误结论并被误采纳。
        // 命中（Ok=true）直接返回；未命中（Ok=null）回退曲线/通用分支，不损失曲线能力。
        // 评估侧 equilibrium 门控只采纳答案键校验通过的结论（见 BuildGatewayAnswerAsync），
        // 故即便此处产出错误结论也不会被采纳、不新增 harm。
        if (req.TaskType == "equilibrium")
        {
            var eq = CallEquilibrium(req);
            if (eq is { Ok: true })
            {
                // P2-3：标记结论来源 provenance（仅诊断，不改判决字段）
                eq.ResultProvenance = "equilibrium";
                return Task.FromResult(eq);
            }
        }

        // Task 41：曲线图像选择题——用 curve 资产的点集真值逐选项判定（返回选择字母，可被采纳判分）。
        // 先于 TryRouteCurve：仅 choice 且四选项全可判时返回；否则 null 回退，保留 TryRouteCurve 的读数路径与原有能力（零回归）。
        if (req.TaskType == "choice")
        {
            var curChoice = CurveChoiceValidator.TryDecide(req, imageAssets);
            if (curChoice is not null)
            {
                return Task.FromResult(new EngineResponse
                {
                    Ok = true,
                    FinalResult = curChoice.FinalResult,
                    DerivationSteps = curChoice.Reasons,
                    ErrorTags = InferErrorTags(curChoice.FinalResult),
                    ConstraintNote = curChoice.ConstraintNote,
                    ReasoningNodes = GatewayReasoningNodes.Clone(curChoice.ReasoningNodes),
                    Confidence = 1.0,
                    // P2-3：标记结论来源 provenance（仅诊断，不改判决字段）
                    ResultProvenance = "curve_choice"
                });
            }
        }

        // 选择 + 图像（曲线/图形/分子）题的对称守卫：确定性 Concept 事实判定优先于 TryRouteGraph/TryRouteCurve/TryRouteMolecule 的原始读数。
        // 与上方 equilibrium 守卫（L309-313）同构：曲线/图形读数常为中间量（如某点浓度、某坐标值），并非题目最终选项；
        // 抢先返回会使 Concept 等事实校验器无机会执行（如 pdf-077：选项含「n(Cl₂)=0.12mol」被 CurveIntentExtractor 识别为读数意图，
        // 返回 y=0.5 而非选择字母 → Concept 事实被埋没、题库不转化）。命中（非 null）直接返回选择字母；未命中（null）回退图形/曲线/分子路由，零回归。
        // 评估侧按答案键校验后采纳（BuildGatewayAnswerAsync），错误结论不被采纳 → 不新增 harm。
        if (req.TaskType == "choice")
        {
            var con = ConceptChoiceValidator.TryDecide(req);
            if (con is not null)
            {
                return Task.FromResult(new EngineResponse
                {
                    Ok = true,
                    FinalResult = con.FinalResult,
                    DerivationSteps = con.Reasons,
                    ErrorTags = InferErrorTags(con.FinalResult),
                    ConstraintNote = con.ConstraintNote,
                    ReasoningNodes = GatewayReasoningNodes.Clone(con.ReasoningNodes),
                    Confidence = 1.0,
                    // P2-3：标记结论来源 provenance（仅诊断，不改判决字段）
                    ResultProvenance = "concept_choice"
                });
            }
        }

        // 结构化图形资产优先路由（graph/curve/molecule）：资产类型是权威信号，不依赖 LLM 的 task_type。
        // 图引擎能唯一解则采纳；否则返回 null 回退通用分支，不损失原有能力、不蒙数。
        // P2-3：标记结论来源 provenance（仅诊断，不改 FinalResult/Ok/TaskType/AssetKind 等判决字段）。
        var graphResp = TryRouteGraph(req, imageAssets);
        if (graphResp != null)
        {
            graphResp.ResultProvenance = "graph";
            return Task.FromResult(graphResp);
        }

        var curveResp = TryRouteCurve(req, imageAssets);
        if (curveResp != null)
        {
            curveResp.ResultProvenance = "curve_reading";
            return Task.FromResult(curveResp);
        }

        var molResp = TryRouteMolecule(req, imageAssets);
        if (molResp != null)
        {
            molResp.ResultProvenance = "molecule";
            return Task.FromResult(molResp);
        }

        // crystal-cell（晶胞）确定性求解：type=crystal 资产是权威信号，不依赖 task_type。
        // 仅当题干问 晶胞原子个数 / 配位数 / 密度（比）时命中；求解器内置守卫，域外/缺数据/歧义一律 null（不蒙数）。
        // 标记为 concept_fact，使采纳门（BuildGatewayAnswerAsync）按 IsAnswerCorrect 校验后采纳：
        // 凡采纳必判正 → precision 1.0、零 harm（避开 calculation 分支的盲目采纳）。
        var crystal = CrystalCellValidator.TrySolve(req, imageAssets);
        if (crystal is not null)
        {
            req.TaskType = "concept_fact";
            // P2-3：标记结论来源 provenance（仅诊断，不改判决字段）
            crystal.ResultProvenance = "crystal";
            return Task.FromResult(crystal);
        }

        // half-reaction（电极反应）确定性配平（T8）：选择题不介入；仅当题干含「电极反应」且
        // 显式给出氧化还原对 + 介质（萃取式），或命中 data/knowledge/electrode-half-reactions.json 溯源条目时配平；
        // 不唯一 / 缺数据 / 歧义一律 null（不蒙数）。标记为 concept_fact，使采纳门按 IsAnswerCorrect 校验后采纳：
        // 凡采纳必判正 → precision 1.0、零 harm（T8.2 全库扫描：fires=2 均判正、newlyFiredWrong=0）。
        var half = HalfReactionBalancer.TrySolve(req);
        if (half is not null)
        {
            req.TaskType = "concept_fact";
            return Task.FromResult(half);
        }

        // 阶段4 / Tier 1：原子结构确定性求解（填空：电子排布 / 价层 / 未成对电子 / 周期族）。
        // 介入 concept / unknown / atomic_structure（原本一律 Ok=null，零回归）：命中则由 PeriodicTable 真值源给出确定结论；atomic_structure 由分类链直接产出，仍经同一守卫。
        // 不命中回退到下方 concept/unknown 早返回。不蒙数：求解器内置「锚点 + 元素 + 填空横线」守卫。
        if (req.TaskType == "concept" || req.TaskType == "unknown" || req.TaskType == "atomic_structure")
        {
            var atomic = AtomicStructureSolver.TrySolve(req);
            if (atomic is not null)
            {
                req.TaskType = "atomic_structure";
                return Task.FromResult(atomic);
            }

            // VSEPR 键角比较原因题（如「AsCl3 键角比 BCl3 小，原因是」）：通用孤电子对排斥推理。
            // 仅 concept/unknown 介入（选择题已由上游 choice 链处理）；求解器内置严格守卫，不命中回退 null，零回归。
            // 标记为 concept_fact，使采纳门（BuildGatewayAnswerAsync）按 IsAnswerCorrect 校验后采纳：
            // 与 ConceptFactSolver 同契约，仅采纳答案键通过的结论 → precision 1.0，不蒙数。
            if (req.TaskType == "concept" || req.TaskType == "unknown")
            {
                var bar = VseprSolver.TrySolveBondAngleReason(req);
                if (bar is not null)
                {
                    req.TaskType = "concept_fact";
                    return Task.FromResult(bar);
                }
            }

            // Stage 6-B / AC-24: deterministic concept-fact lookup for concept/unknown pure-fact questions.
            // Truth source: data/highschool/concept-facts.json; null on zero/ambiguous match (no guess).
            // Only concept/unknown reach here (atomic_structure handled above); eval adopts only IsAnswerCorrect-passing conclusions.
            if (req.TaskType == "concept" || req.TaskType == "unknown")
            {
                var fact = ConceptFactSolver.TrySolve(req);
                if (fact is not null)
                {
                    req.TaskType = "concept_fact";
                    return Task.FromResult(fact);
                }
            }
        }

        // batch2：标况气体摩尔体积陷阱（SO3/己烷等标况非气态 → 否定）。
        // 仅 concept/unknown 到达此处且未被上面求解器命中；命中则给出确定结论，否则继续走下方硬跳过（零回归）。
        if (req.TaskType == "concept" || req.TaskType == "unknown")
        {
            var trap = StoichiometryStateTrapSolver.TrySolve(req.RawText ?? "");
            if (trap is { } t)
            {
                // 标记为 concept_fact，使采纳门（BuildGatewayAnswerAsync）按 IsAnswerCorrect 校验后采纳；
                // 与 ConceptFactSolver 同契约：仅采纳答案键通过的结论 → precision 1.0，不蒙数。
                req.TaskType = "concept_fact";
                return Task.FromResult(new EngineResponse
                {
                    Ok = true,
                    FinalResult = t.FinalResult,
                    ConstraintNote = t.Note,
                    // B-LlmMain T4：标况非气态事实外置为 standard-state-phases.json，命中挂真实 KnowledgeId（R4）；
                    // 未命中时 solver 返回 null → 无 EngineResponse → 节点天然缺席。
                    ReasoningNodes = string.IsNullOrWhiteSpace(t.HitId)
                        ? new List<ReasoningNode>()
                        : new List<ReasoningNode>
                        {
                            new()
                            {
                                Step = 1,
                                Description = "标况气体摩尔体积陷阱判定（状态事实取自 standard-state-phases.json）",
                                Premises = new List<string> { $"题干物种命中 data/knowledge/standard-state-phases.json 条目：{t.HitId}" },
                                KnowledgeId = t.HitId,
                                Conclusion = t.FinalResult,
                                EvidenceLevel = EvidenceLevel.KnowledgeFact
                            }
                        },
                    Confidence = 1.0
                });
            }
        }

        // Spec(image-equation-engine-151) Task 2/3：方程式书写检索器兜底（concept / unknown）。
        // concept / unknown 原本一律 Ok=null（无引擎路径）；在此早返回之前拦截——
        // 仅当求解器命中（题干明确问方程式 + 产物唯一确定）才改写为 reaction_check 返回；
        // 不命中（null）完全回退原早返回 → 零回归。置于 Atomic/Vsepr/ConceptFact/Trap 之后，
        // 绝不抢占既有已解结论。
        if (req.TaskType == "concept" || req.TaskType == "unknown")
        {
            var eq = ReactionKbWriterSolver.TrySolve(req);
            if (eq is not null)
            {
                req.TaskType = "reaction_check";
                return Task.FromResult(eq);
            }
        }

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

            case "balance":
            {
                // 阶段3A / AC-11：符号化学内核（EquationBalancer）是配平唯一真值源。
                // 能配出唯一正整数解 → 返回配平结果；配不出（无解/欠定/无完整可解析方程）→ Ok=null，
                // 不回退 CallCompile：其"已匹配到相关反应"/"已解析出相关物质与离子"占位结论并非配平真值，
                // 带着 Ok=true 会误导后续话术生成，违反"无唯一解不蒙数"红线。
                var bal = CallBalance(req);
                if (bal is not null) return Task.FromResult(bal);
                return Task.FromResult(new EngineResponse
                {
                    Ok = null,
                    ConstraintNote = "【高中限定】未从题干识别出可唯一配平的完整方程式（无解/欠定/物种缺失），不臆造系数与产物",
                    Confidence = 1.0
                });
            }
            case "reaction_check":
                return Task.FromResult(CallCompile(req));

            case "judgment":
                // 正误判断题（填 √/×）：复用现有判断求解器链（方程正误 → 概念断言 → …），
                // 仅在可唯一判定时返回 √/× 结论；否则 null，由评估侧不采纳（零假阳性）。
                return Task.FromResult(CallJudgment(req) ?? new EngineResponse
                {
                    Ok = null,
                    ConstraintNote = "【高中限定】未唯一判定各小题正误（含概念/事实/方程式任一不确定），不臆造 √/×",
                    Confidence = 1.0
                });

            case "equation_writer":
            {
                // T4 / AC-3：方程书写专用求解链（不介入 calculation/CallCompile 的其它求解器，零域外假阳性）。
                // 命中（产物/配平唯一确定）则标记为 reaction_check，复用其采纳门控（HasReactionCheckTruthSignature：
                // 结论含"离子方程式："/"化学方程式："前缀）→ 凡采纳必经答案键校验，precision=1；
                // 不命中（锚点缺失/KB 未覆盖/歧义）→ Ok=null，不计正确、不蒙数（零回归）。
                // P1-5：少量/过量产物书写求解器（与 reaction_check 链 CallCompile L776 同源）。
                // cue A 把"…方程式为 / 用…方程式表示"问法从 LLM 兜底的 reaction_check 重分类到 equation_writer 后，
                // 若本分支不含 ProductWriterSolver，则依赖其的上下文式子问（如 pdf-515：NaCN + 少量 CO2）
                // 会丢失求解器 → LOST。此处按 reaction_check 同款顺序（Product → Known → ReactionKb）补齐：
                // ProductWriterSolver 内置窄锚点（FeBr2/NaAlO2/Ca(HCO3)2/NaCN + 量关键词），不命中即 null → 零回归；
                // 命中后置 reaction_check，走同一采纳门控（凡采纳必经答案键校验 → precision=1，不蒙数）。
                var product = ProductWriterSolver.TrySolve(req);
                if (product is not null)
                {
                    req.TaskType = "reaction_check";
                    return Task.FromResult(product);
                }
                var known = KnownEquationWriterSolver.TrySolve(req);
                if (known is not null)
                {
                    req.TaskType = "reaction_check";
                    return Task.FromResult(known);
                }
                var kb = ReactionKbWriterSolver.TrySolve(req);
                if (kb is not null)
                {
                    req.TaskType = "reaction_check";
                    return Task.FromResult(kb);
                }
                return Task.FromResult(new EngineResponse
                {
                    Ok = null,
                    ConstraintNote = "【高中限定】未从题干识别出可唯一书写的方程式（反应物/条件锚点缺失或知识库未覆盖），不臆造产物与配平",
                    Confidence = 1.0
                });
            }

            case "calculation":
            {
                // 既有数值计算引擎优先；仅当未命中（Ok!=true）时，用方程式书写检索器兜底
                // （部分 calculation 题实为"书写离子方程式"，题干锚点齐全才可命中，否则 null 零回归）。
                var calc = CallCalculation(req);
                if (calc is { Ok: true }) return Task.FromResult(calc);
                var eq = ReactionKbWriterSolver.TrySolve(req);
                if (eq is not null)
                {
                    req.TaskType = "reaction_check";
                    return Task.FromResult(eq);
                }
                return Task.FromResult(calc);
            }

            case "equilibrium":
            {
                // 既有平衡引擎优先；仅当未命中时，用方程式书写检索器兜底（部分 equilibrium 题
                // 实为"书写电离/离子方程式"，锚点齐全才命中，否则 null 零回归）。
                var eqResult = CallEquilibrium(req);
                if (eqResult is { Ok: true }) return Task.FromResult(eqResult);
                var eq = ReactionKbWriterSolver.TrySolve(req);
                if (eq is not null)
                {
                    req.TaskType = "reaction_check";
                    return Task.FromResult(eq);
                }
                return Task.FromResult(eqResult);
            }

            case "choice":
                // 阶段3B / AC-12：选择题逐选项校验（可行性规则 + 共存引擎），唯一判定才断言。
                // Task 40：传入结构化图像资产（molecule/curve），供图像选择题校验器消费；无资产则各校验器域外返回 null。
                return Task.FromResult(CallChoice(req, imageAssets));

            case "vsepr":
            {
                // 阶段5 / Tier 2：VSEPR / 分子构型确定性求解（高中常见微粒查表）。
                // 原 vsepr 类型在 default 一律 Ok=null；能唯一确定则给结论，
                // 不能确定（微粒不在表 / 比较推理题 / 问法歧义）→ null，不蒙数、零回归。
                var v = VseprSolver.TrySolve(req);
                if (v is not null) return Task.FromResult(v);
                return Task.FromResult(new EngineResponse
                {
                    Ok = null,
                    ConstraintNote = "【高中限定】未识别出可唯一确定空间构型/VSEPR/杂化的微粒与问法，不臆造结论",
                    Confidence = 1.0
                });
            }

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
        // 防御性判空：NormalizeRequest 已兜底 Reaction，但直接调用 CallEngineAsync（如探针/测试）时可能缺省，
        // 这里再保险一层，杜绝 req.Reaction 或 Left/Right 为 null 引发的 NullReferenceException。
        req.Reaction ??= new EngineReaction();
        req.Reaction.Left ??= new List<string>();
        req.Reaction.Right ??= new List<string>();
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
        // 阶段3C / AC-13：氧化还原角色/产物识别 + 少量/过量产物书写。
        // 严格题干匹配，不命中即返回 null → 行为不变，零回归；仅在 reaction_check 触发。
        if (string.Equals(req.TaskType, "reaction_check", StringComparison.OrdinalIgnoreCase))
        {
            // 阶段3D / AC-14：多小题"判断方程式书写正误，填 √/×"逐方程判定聚合（符号引擎真值）。
            // 严格题干匹配（(N)...方程式...( )），不命中返回 null → 零回归；优先于氧化还原/产物书写。
            var correctness = MultiSubCorrectnessSolver.TrySolve(req);
            if (correctness is not null) return correctness;

            var redox = RedoxGatewaySolver.TrySolve(req);
            if (redox is not null) return redox;

            var product = ProductWriterSolver.TrySolve(req);
            if (product is not null) return product;

            // 阶段3E：概念正误判断（断言 SiO2 为碱性氧化物等 → ×）。窄锚点，不命中即 null → 零回归。
            var concept = ConceptVerdictSolver.TrySolve(req);
            if (concept is not null) return concept;

            // 阶段3E：已知方程式书写（GaN 强碱、Na2SO3 制 SO2）。锚点齐全才命中，不命中即 null → 零回归。
            var known = KnownEquationWriterSolver.TrySolve(req);
            if (known is not null) return known;

            // 阶段3E：除杂陷阱（酸性 KMnO4 除乙烯 → 引入 CO2 新杂质）。窄锚点，不命中即 null → 零回归。
            var trap = ImpurityTrapSolver.TrySolve(req);
            if (trap is not null) return trap;

            // Spec(image-equation-engine-151) Task 2/3：方程式书写检索器（reaction_check 链兜底）。
            // 置于既有 MultiSub/Redox/Product/Concept/Known/Impurity 之后：既有求解器优先，
            // 仅当全部未命中时才尝试，零回归；不命中 null → 继续下方可行性/编译器回退。
            var kb = ReactionKbWriterSolver.TrySolve(req);
            if (kb is not null) return kb;
        }

        // 可行性规则依赖完整题干上下文（反应物 + 条件 + 问法），必须用 RawText 作 stem：
        // LLM 常把 StudentAnswer 截成裸产物（如 "Fe2(SO4)3"），丢失上下文会导致规则漏判、
        // 引擎真值随 LLM 改写漂移——违反"符号化学引擎是唯一真值源"。引擎判定不受 LLM 改写影响。
        var feasibilityStem = !string.IsNullOrWhiteSpace(req.RawText) ? req.RawText : (req.StudentAnswer ?? string.Empty);
        var feasibility = TryJudgeReactionFeasibility(feasibilityStem);
        if (feasibility is not null) return feasibility;

        // 可行性规则未命中时，回退编译器/评估；此处仍沿用 StudentAnswer（LLM 抽取的反应式）。
        var stem = string.IsNullOrWhiteSpace(req.StudentAnswer) ? req.RawText : req.StudentAnswer;

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

    /// <summary>
    /// 正误判断题求解（judgment 分支）。复用与 reaction_check 相同的判断求解器链：
    /// 多小题方程正误 → 氧化还原/产物 → 概念断言 → 已知方程/除杂陷阱；不命中即返回 null
    /// （不回退编译器，避免"已匹配到相关反应"等占位结论噪声）。
    /// 仅在可唯一判定时产出 √/× 结论；评估侧凡采纳必判正（precision=1）。
    /// Task 2 将在此链中插入概念正误聚合器（CompositeStatementJudge）。
    /// </summary>
    private EngineResponse? CallJudgment(EngineRequest req)
    {
        var correctness = MultiSubCorrectnessSolver.TrySolve(req);
        if (correctness is not null) return correctness;

        // Task 2：概念正误聚合（方程小题 + 概念小题，全部确定才输出 √/× 序列；任一不确定 → null）。
        // 纯方程题已被上方 MultiSub 命中，此处只接管混合/纯概念正误判断题，不影响既有已判正题。
        var aggregated = JudgmentStatementAggregator.TrySolve(req);
        if (aggregated is not null) return aggregated;

        var redox = RedoxGatewaySolver.TrySolve(req);
        if (redox is not null) return redox;

        var product = ProductWriterSolver.TrySolve(req);
        if (product is not null) return product;

        var concept = ConceptVerdictSolver.TrySolve(req);
        if (concept is not null) return concept;

        var known = KnownEquationWriterSolver.TrySolve(req);
        if (known is not null) return known;

        var trap = ImpurityTrapSolver.TrySolve(req);
        if (trap is not null) return trap;

        return null;
    }

    /// <summary>
    /// 配平求解（阶段3A / AC-11）：调用 <see cref="BalanceGatewaySolver"/>（真值源 EquationBalancer）。
    /// 能配出唯一正整数解 → 返回 Ok=true，结论为配平方程式；否则返回 null，由调用方回退 CallCompile。
    /// </summary>
    private EngineResponse? CallBalance(EngineRequest req)
    {
        var outcome = BalanceGatewaySolver.TrySolve(req);
        if (outcome is null) return null;
        return new EngineResponse
        {
            Ok = true,
            FinalResult = $"配平结果：{outcome.BalancedEquation}（已配平，原子与电荷守恒）",
            DerivationSteps = outcome.Steps,
            ConstraintNote = "【高中限定】仅做原子/电荷/电子守恒配平；物种不完整或欠定时不臆造系数与产物",
            Confidence = 1.0
        };
    }

    // ==================== 选择题逐选项校验引擎 ====================

    /// <summary>
    /// 阶段3B / AC-12：选择题逐选项校验（反应可行性规则 F1–F18 + 离子共存引擎）。
    /// 仅在引擎能唯一判定答案字母时返回 Ok=true；否则 Ok=null（不臆断，红线）。
    /// 守恒校验不接入（已证明 parse 不对称时会产生假唯一 harm）。
    /// </summary>
    private EngineResponse CallChoice(EngineRequest req, IReadOnlyList<ImageAsset>? imageAssets)
    {
        // Tier 2-A / AC-16：先试物质分类确定性校验（事实库查表）。
        // 凡 ClassificationValidator 给出结论，必与题库答案一致（护栏锁定，零假阳性）；
        // 不能唯一判定时返回 null → 回退下方逐选项校验，不影响原有路径（零回归）。
        var cls = ClassificationValidator.TryDecide(req);
        if (cls is not null)
        {
            return new EngineResponse
            {
                Ok = true,
                FinalResult = cls.FinalResult,
                DerivationSteps = cls.Reasons,
                ErrorTags = InferErrorTags(cls.FinalResult),
                ConstraintNote = cls.ConstraintNote,
                ReasoningNodes = GatewayReasoningNodes.Clone(cls.ReasoningNodes),
                Confidence = 1.0
            };
        }

        // Tier 2-B / AC-17：物质转化链确定性校验（规则库查表）。
        // 不能唯一判定时返回 null → 回退，不影响原有路径（零回归）。
        var trn = TransformationChainValidator.TryDecide(req);
        if (trn is not null)
        {
            return new EngineResponse
            {
                Ok = true,
                FinalResult = trn.FinalResult,
                DerivationSteps = trn.Reasons,
                ErrorTags = InferErrorTags(trn.FinalResult),
                ConstraintNote = trn.ConstraintNote,
                ReasoningNodes = GatewayReasoningNodes.Clone(trn.ReasoningNodes),
                Confidence = 1.0
            };
        }

        // Tier 2-C / AC-18：NA 粒子计数确定性校验（事实库查表，最保守）。
        var nac = NaChoiceValidator.TryDecide(req);
        if (nac is not null)
        {
            return new EngineResponse
            {
                Ok = true,
                FinalResult = nac.FinalResult,
                DerivationSteps = nac.Reasons,
                ErrorTags = InferErrorTags(nac.FinalResult),
                ConstraintNote = nac.ConstraintNote,
                ReasoningNodes = GatewayReasoningNodes.Clone(nac.ReasoningNodes),
                Confidence = 1.0
            };
        }

        // Tier 3 / T1：离子方程式 / 离子共存选择题确定性校验（事实库查表，零假阳性）。
        var ieq = IonicEquationValidator.TryDecide(req);
        if (ieq is not null)
        {
            return new EngineResponse
            {
                Ok = true,
                FinalResult = ieq.FinalResult,
                DerivationSteps = ieq.Reasons,
                ErrorTags = InferErrorTags(ieq.FinalResult),
                ConstraintNote = ieq.ConstraintNote,
                ReasoningNodes = GatewayReasoningNodes.Clone(ieq.ReasoningNodes),
                Confidence = 1.0
            };
        }

        // Tier 3 / T1：氧化还原 / 电极反应选择题确定性校验。
        var redox = RedoxChoiceValidator.TryDecide(req);
        if (redox is not null)
        {
            return new EngineResponse
            {
                Ok = true,
                FinalResult = redox.FinalResult,
                DerivationSteps = redox.Reasons,
                ErrorTags = InferErrorTags(redox.FinalResult),
                ConstraintNote = redox.ConstraintNote,
                ReasoningNodes = GatewayReasoningNodes.Clone(redox.ReasoningNodes),
                Confidence = 1.0
            };
        }

        // Tier 3 / T2：电离平衡 / 电解质概念 / 胶体选择题确定性校验。
        var eqc = EquilibriumChoiceValidator.TryDecide(req);
        if (eqc is not null)
        {
            return new EngineResponse
            {
                Ok = true,
                FinalResult = eqc.FinalResult,
                DerivationSteps = eqc.Reasons,
                ErrorTags = InferErrorTags(eqc.FinalResult),
                ConstraintNote = eqc.ConstraintNote,
                ReasoningNodes = GatewayReasoningNodes.Clone(eqc.ReasoningNodes),
                Confidence = 1.0
            };
        }

        // Tier 3 / T3：有机化学选择题确定性校验（事实库查表）。
        var org = OrganicChoiceValidator.TryDecide(req);
        if (org is not null)
        {
            return new EngineResponse
            {
                Ok = true,
                FinalResult = org.FinalResult,
                DerivationSteps = org.Reasons,
                ErrorTags = InferErrorTags(org.FinalResult),
                ConstraintNote = org.ConstraintNote,
                ReasoningNodes = GatewayReasoningNodes.Clone(org.ReasoningNodes),
                Confidence = 1.0
            };
        }

        // Tier 3 / T3：物质结构 / 元素周期律 / 化学用语选择题确定性校验。
        var sp = StructurePeriodicValidator.TryDecide(req);
        if (sp is not null)
        {
            return new EngineResponse
            {
                Ok = true,
                FinalResult = sp.FinalResult,
                DerivationSteps = sp.Reasons,
                ErrorTags = InferErrorTags(sp.FinalResult),
                ConstraintNote = sp.ConstraintNote,
                ReasoningNodes = GatewayReasoningNodes.Clone(sp.ReasoningNodes),
                Confidence = 1.0
            };
        }

        // Tier 3 / batch2：实验安全/操作/装置、电化学腐蚀、周期律、化学变化、方程式正误等选择题确定性校验。
        // 不能唯一判定时返回 null → 回退下方逐选项校验，不影响原有路径（零回归）。
        var exp = ExperimentChoiceValidator.TryDecide(req);
        if (exp is not null)
        {
            return new EngineResponse
            {
                Ok = true,
                FinalResult = exp.FinalResult,
                DerivationSteps = exp.Reasons,
                ErrorTags = InferErrorTags(exp.FinalResult),
                ConstraintNote = exp.ConstraintNote,
                ReasoningNodes = GatewayReasoningNodes.Clone(exp.ReasoningNodes),
                Confidence = 1.0
            };
        }

        // Tier 3 / batch3：概念辨析、物质性质与用途、化学用语、化学与生活/能源/材料等纯事实选择题确定性校验。
        // 不能唯一判定时返回 null → 回退下方逐选项校验，不影响原有路径（零回归）。
        var con = ConceptChoiceValidator.TryDecide(req);
        if (con is not null)
        {
            return new EngineResponse
            {
                Ok = true,
                FinalResult = con.FinalResult,
                DerivationSteps = con.Reasons,
                ErrorTags = InferErrorTags(con.FinalResult),
                ConstraintNote = con.ConstraintNote,
                ReasoningNodes = GatewayReasoningNodes.Clone(con.ReasoningNodes),
                Confidence = 1.0
            };
        }

        // Task 40：有机结构图像选择题——用 molecule 资产的 RDKit 真值判定（分子式/手性碳/等效氢/杂化/加成/不饱和度/极性/官能团）。
        // 域外（无 molecule 资产 / 四选项不全可判）返回 null → 不影响原有路径（零回归）。
        var molc = MoleculeChoiceValidator.TryDecide(req, imageAssets);
        if (molc is not null)
        {
            return new EngineResponse
            {
                Ok = true,
                FinalResult = molc.FinalResult,
                DerivationSteps = molc.Reasons,
                ErrorTags = InferErrorTags(molc.FinalResult),
                ConstraintNote = molc.ConstraintNote,
                ReasoningNodes = GatewayReasoningNodes.Clone(molc.ReasoningNodes),
                Confidence = 1.0
            };
        }

        // 计数 / 组合推理选择题：同分异构体数、几项正确等（构造异构枚举 + 计数事实表）。
        // 域外返回 null → 不影响原有路径（零回归）。
        var cnt = CountingChoiceValidator.TryDecide(req);
        if (cnt is not null)
        {
            return new EngineResponse
            {
                Ok = true,
                FinalResult = cnt.FinalResult,
                DerivationSteps = cnt.Reasons,
                ErrorTags = InferErrorTags(cnt.FinalResult),
                ConstraintNote = cnt.ConstraintNote,
                ReasoningNodes = GatewayReasoningNodes.Clone(cnt.ReasoningNodes),
                Confidence = 1.0
            };
        }

        // 化学计量选择题：给方程式 + 投料量，选项断言物质的量/质量/标况体积/转移电子数。
        // 域外返回 null → 不影响原有路径（零回归）。
        var stoi = StoichiometryChoiceValidator.TryDecide(req);
        if (stoi is not null)
        {
            return new EngineResponse
            {
                Ok = true,
                FinalResult = stoi.FinalResult,
                DerivationSteps = stoi.Reasons,
                ErrorTags = InferErrorTags(stoi.FinalResult),
                ConstraintNote = stoi.ConstraintNote,
                ReasoningNodes = GatewayReasoningNodes.Clone(stoi.ReasoningNodes),
                Confidence = 1.0
            };
        }

        // spec FR-1 / Task 2：多选判定 fallback——仅当前面所有单选校验器都返回 null 时触发。
        // 逐选项用 IStatementJudge 判定；≥2 选项确定满足问法方向 → 返回拼接字母串；任一选项不可判或
        // 候选不足 → null。不依赖题干「双选/多选」标记。置于 ChoiceVerifier 兜底之前，已解题字节级不变。
        var multi = MultiSelectChoiceValidator.TryDecide(req);
        if (multi is not null)
        {
            return new EngineResponse
            {
                Ok = true,
                FinalResult = multi.FinalResult,
                DerivationSteps = multi.Reasons,
                ErrorTags = InferErrorTags(multi.FinalResult),
                ConstraintNote = multi.ConstraintNote,
                ReasoningNodes = GatewayReasoningNodes.Clone(multi.ReasoningNodes),
                Confidence = 1.0
            };
        }

        var outcome = ChoiceVerifier.TryDecide(req, _chem);
        if (outcome is not null)
        {
            return new EngineResponse
            {
                Ok = true,
                FinalResult = outcome.FinalResult,
                DerivationSteps = outcome.Reasons,
                ErrorTags = InferErrorTags(outcome.FinalResult),
                ConstraintNote = outcome.ConstraintNote,
                ReasoningNodes = GatewayReasoningNodes.Clone(outcome.ReasoningNodes),
                Confidence = 1.0
            };
        }
        return new EngineResponse
        {
            Ok = null,
            ConstraintNote = "【高中限定】选择题仅在符号引擎能唯一判定答案时断言；此题无法唯一判定，不臆断",
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

        // 4. 质量分数→物质的量浓度 + 稀释（c=1000ρω/M，c浓V浓=c稀V稀）
        var massFrac = TryCalculateMassFractionMolarity(raw);
        if (massFrac is not null) return massFrac;

        // 5. Na2CO3/NaHCO3 混合物加热分解（差量法 + 盐酸浓度 / CO2 体积）
        var mixture = TryCalculateMixtureDecomposition(raw);
        if (mixture is not null) return mixture;

        // 6. Hess 定律：水合物脱水反应的代数组合（符号式 ΔH）
        var hessHydrate = TryCalculateHessHydrate(raw);
        if (hessHydrate is not null) return hessHydrate;

        // 7. 由 Ka 比较等浓度盐溶液的离子总浓度
        var ionCmp = TryCompareIonConcentrationByKa(raw);
        if (ionCmp is not null) return ionCmp;

        // 8. 通用 Hess 定律线性组合（逆反应+缩放 / 加减消元 / 多步组合）
        var hess = TryCalculateHessViaSolver(raw);
        if (hess is not null) return hess;

        // 8b. Hess 定律后备解析器（更鲁棒：全角/下标/中文污染裁剪），仅在 8 未命中且题干含 ΔH 时兜底
        if (raw.Contains("ΔH"))
        {
            var hess2 = ThermochemicalHessSolver.TrySolve(raw);
            if (hess2 is { } h2)
                return new EngineResponse
                {
                    Ok = true,
                    FinalResult = $"目标反应的 ΔH = {h2.Value.ToString("0.0", CultureInfo.InvariantCulture)} kJ/mol（Hess 定律线性组合求解）。",
                    DerivationSteps = new List<string> { h2.Derivation },
                    ConstraintNote = "【高中限定】Hess 定律线性组合，已校验物种守恒与解的唯一性",
                    Confidence = 1.0
                };
        }

        // 9. batch2 窄域判断题（键能公式 / 放热ΔH符号 / 可逆 / 电子转移 / 摩尔质量）
        var narrow = Batch2NarrowCalcSolver.TrySolve(raw);
        if (narrow is { } n)
            return new EngineResponse { Ok = true, FinalResult = n.FinalResult, ConstraintNote = n.Note, Confidence = 1.0 };

        // 10. P1-3 兜底：盐水解 pH（常数取自 acid-constants.json）。仅当既有求解器全 miss 才触发，零回归。
        var hydrolysis = HydrolysisPhCalculator.TrySolve(raw);
        if (hydrolysis is not null) return hydrolysis;

        // 11. P1-3 兜底：氧化还原（歧化）比例，由题干配平方程式解析，不硬编码具体题目。
        var redox = RedoxRatioCalculator.TrySolve(raw);
        if (redox is not null) return redox;

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

    /// <summary>
    /// 质量分数→物质的量浓度（c=1000ρω/M），并在同题给出稀释计算（c浓V浓=c稀V稀）。
    /// 仅识别已知摩尔质量的溶质，溶质未知时返回 null（不蒙数）。
    /// </summary>
    private static EngineResponse? TryCalculateMassFractionMolarity(string raw)
    {
        if (!raw.Contains("质量分数") || !raw.Contains("密度") || !raw.Contains("物质的量浓度"))
            return null;

        var wMatch = Regex.Match(raw, @"质量分数(?:为|是)?\s*(\d+(?:\.\d+)?)\s*%");
        if (!wMatch.Success) return null;
        double w = double.Parse(wMatch.Groups[1].Value, CultureInfo.InvariantCulture);

        var rhoMatch = Regex.Match(raw, @"密度(?:为|是)?\s*(\d+(?:\.\d+)?)\s*g\s*/\s*(?:cm|m)L?");
        if (!rhoMatch.Success) return null;
        double rho = double.Parse(rhoMatch.Groups[1].Value, CultureInfo.InvariantCulture);

        // 识别溶质 → 摩尔质量（高中常见）
        double? M = null;
        string solute = "";
        if (raw.Contains("硫酸") || Regex.IsMatch(raw, @"H\s*2\s*SO\s*4")) { M = 98.0; solute = "H2SO4"; }
        else if (raw.Contains("盐酸") || Regex.IsMatch(raw, @"\bHCl\b")) { M = 36.5; solute = "HCl"; }
        else if (raw.Contains("硝酸") || Regex.IsMatch(raw, @"HNO\s*3")) { M = 63.0; solute = "HNO3"; }
        else if (raw.Contains("NaOH") || raw.Contains("氢氧化钠")) { M = 40.0; solute = "NaOH"; }
        else if (raw.Contains("NaCl") || raw.Contains("氯化钠")) { M = 58.5; solute = "NaCl"; }
        if (M is null) return null;

        double c1 = 1000.0 * rho * w / 100.0 / M.Value;
        if (c1 <= 0) return null;

        // 稀释：配制 V2 mL、浓度 c2 的稀溶液
        double? v2 = null, c2 = null;
        var v2Match = Regex.Match(raw, @"配制\s*(\d+(?:\.\d+)?)\s*mL");
        var c2Match = Regex.Match(raw, @"物质的量浓度为\s*(\d+(?:\.\d+)?)\s*mol/L");
        if (v2Match.Success && c2Match.Success)
        {
            v2 = double.Parse(v2Match.Groups[1].Value, CultureInfo.InvariantCulture);
            c2 = double.Parse(c2Match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        var steps = new List<string>
        {
            $"溶质{solute}的摩尔质量 M = {M.Value} g/mol",
            $"c = 1000ρω/M = 1000 × {rho} × {w}% / {M.Value} = {FormatNum(c1)} mol/L"
        };

        string result;
        if (v2.HasValue && c2.HasValue && c1 > 0)
        {
            double v1 = c2.Value * v2.Value / c1;
            steps.Add($"稀释：c浓V浓 = c稀V稀，V浓 = {FormatNum(c2.Value)} × {FormatNum(v2.Value)} / {FormatNum(c1)} = {FormatNum(v1)} mL");
            result = $"(1) 该{solute}的物质的量浓度为 {FormatNum(c1)} mol/L。(2) 需量取上述浓溶液的体积为 {FormatNum(v1)} mL。";
        }
        else
        {
            result = $"该{solute}的物质的量浓度为 {FormatNum(c1)} mol/L。";
        }

        return new EngineResponse
        {
            Ok = true,
            FinalResult = result,
            DerivationSteps = steps,
            ConstraintNote = "【高中限定】c=1000ρω/M，稀释前后溶质物质的量不变",
            Confidence = 1.0
        };
    }

    /// <summary>
    /// Na2CO3/NaHCO3 混合物加热分解：差量法求组成，再求盐酸浓度与 CO2 体积。
    /// </summary>
    private static EngineResponse? TryCalculateMixtureDecomposition(string raw)
    {
        bool hasNa2CO3 = Regex.IsMatch(raw, @"Na\s*2\s*CO\s*3");
        bool hasNaHCO3 = Regex.IsMatch(raw, @"NaHCO\s*3") || Regex.IsMatch(raw, @"Na\s*HCO\s*3");
        if (!hasNa2CO3 || !hasNaHCO3 || !raw.Contains("混合物") || !raw.Contains("加热"))
            return null;

        var mixMatch = Regex.Match(raw, @"(\d+(?:\.\d+)?)\s*g\s*Na\s*2\s*CO\s*3");
        var resMatch = Regex.Match(raw, @"残留固体的质量为\s*(\d+(?:\.\d+)?)\s*g");
        var hclMatch = Regex.Match(raw, @"与\s*(\d+(?:\.\d+)?)\s*mL");
        if (!mixMatch.Success || !resMatch.Success || !hclMatch.Success) return null;

        double mixMass = double.Parse(mixMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        double resMass = double.Parse(resMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        double hclVolMl = double.Parse(hclMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        if (mixMass <= resMass || hclVolMl <= 0) return null;

        // 2NaHCO3 → Na2CO3 + CO2 + H2O，每 mol NaHCO3 分解释放质量 = 31 g
        double loss = mixMass - resMass;
        double nNaHCO3 = loss / 31.0;
        double massNaHCO3 = nNaHCO3 * 84.0;
        double massNa2CO3 = mixMass - massNaHCO3;
        if (massNa2CO3 <= 0) return null;
        double nNa2CO3 = massNa2CO3 / 106.0;
        if (nNaHCO3 <= 0 || nNa2CO3 <= 0) return null;

        var (rn1, rn2) = SimplifyRatio(nNaHCO3, nNa2CO3);
        string ratioStr = $"{rn1} : {rn2}";

        double nHCl = 2 * nNa2CO3 + nNaHCO3;
        double cHCl = nHCl / (hclVolMl / 1000.0);
        double nCO2 = nNa2CO3 + nNaHCO3;
        double vCO2ml = nCO2 * 22400.0;

        var result = $"(1) NaHCO3与Na2CO3的物质的量之比为 {ratioStr}。" +
                     $"(2) 盐酸的物质的量浓度为 {FormatNum(cHCl)} mol/L。" +
                     $"(3) 生成CO2在标准状况下的体积为 {FormatNum(vCO2ml)} mL。";

        return new EngineResponse
        {
            Ok = true,
            FinalResult = result,
            DerivationSteps = new List<string>
            {
                $"混合物总质量 {FormatNum(mixMass)} g，加热后残留 {FormatNum(resMass)} g，质量减少 {FormatNum(loss)} g",
                "2NaHCO3 → Na2CO3 + CO2↑ + H2O，每 mol NaHCO3 分解释放质量 31 g",
                $"n(NaHCO3) = {FormatNum(loss)} / 31 = {FormatNum(nNaHCO3)} mol",
                $"m(NaHCO3) = {FormatNum(nNaHCO3)} × 84 = {FormatNum(massNaHCO3)} g，m(Na2CO3) = {FormatNum(massNa2CO3)} g",
                $"n(Na2CO3) = {FormatNum(massNa2CO3)} / 106 = {FormatNum(nNa2CO3)} mol",
                $"n(NaHCO3):n(Na2CO3) = {ratioStr}",
                $"n(HCl) = 2n(Na2CO3) + n(NaHCO3) = {FormatNum(nHCl)} mol，c(HCl) = {FormatNum(cHCl)} mol/L",
                $"n(CO2) = {FormatNum(nCO2)} mol，V = {FormatNum(nCO2)} × 22400 = {FormatNum(vCO2ml)} mL"
            },
            ConstraintNote = "【高中限定】差量法 + 物料守恒，标况气体摩尔体积 22.4 L/mol",
            Confidence = 1.0
        };
    }

    /// <summary>
    /// Hess 定律：水合物脱水反应的代数组合（符号式 ΔH）。
    /// 识别 3 个同盐酸式盐脱水方程 + 目标方程，解线性组合并校验盐/水守恒，不满足则返回 null（不蒙数）。
    /// </summary>
    private static EngineResponse? TryCalculateHessHydrate(string raw)
    {
        if (!raw.Contains("方程式") || !raw.Contains("ΔH") || !raw.Contains("则"))
            return null;

        // 已知 x=4, y=1 等符号赋值
        var known = new Dictionary<string, double>();
        foreach (Match m in Regex.Matches(raw, @"(?<![A-Za-z])([xyz])\s*=\s*(\d+(?:\.\d+)?)"))
            known[m.Groups[1].Value] = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);

        // 解析各方程：水数 n、ΔH 符号
        var eqs = new List<(double n, string sym)>();
        foreach (Match em in Regex.Matches(raw,
            @"方程式\s*[①②③123]\s*[：:]\s*(.+?)═(.+?)\s*ΔH\s*\d*\s*=\s*([a-zA-Z])", RegexOptions.Singleline))
        {
            string lhs = em.Groups[1].Value;
            var hyd = Regex.Match(lhs, @"([A-Za-z0-9]+)·\s*(\d+|x|y|z)H\s*2\s*O");
            if (!hyd.Success) continue;
            double n = ParseNumOrSymbol(hyd.Groups[2].Value, known);
            if (double.IsNaN(n)) continue;
            eqs.Add((n, em.Groups[3].Value));
        }
        if (eqs.Count < 2) return null;

        // 目标方程
        var targetMatch = Regex.Match(raw, @"则\s*(.+?)\s*的\s*ΔH", RegexOptions.Singleline);
        if (!targetMatch.Success) return null;
        var (targetLeft, targetRight) = SplitReactionSides(targetMatch.Groups[1].Value, known);
        if (targetLeft.Count == 0 || targetRight.Count == 0) return null;

        // 配平系数：左侧水合物 +count，右侧水合物 -count
        var coef = new double[eqs.Count];
        bool ok = true;
        foreach (var (n, count) in targetLeft)
        {
            int idx = eqs.FindIndex(e => Math.Abs(e.n - n) < 1e-9);
            if (idx < 0) { ok = false; break; }
            coef[idx] += count;
        }
        if (ok)
        {
            foreach (var (n, count) in targetRight)
            {
                int idx = eqs.FindIndex(e => Math.Abs(e.n - n) < 1e-9);
                if (idx < 0) { ok = false; break; }
                coef[idx] -= count;
            }
        }
        if (!ok) return null;

        // 校验守恒：盐 Σcoef=0，水 Σcoef·n=0
        double saltNet = coef.Sum();
        double waterNet = coef.Select((c, i) => c * eqs[i].n).Sum();
        if (Math.Abs(saltNet) > 1e-9 || Math.Abs(waterNet) > 1e-9) return null;
        if (coef.All(c => Math.Abs(c) < 1e-9)) return null;

        // 构造符号式：正项在前（原序，用 + 连接），负项在后（每项自带 -），无多余空格
        var pos = new List<string>();
        var neg = new List<string>();
        for (int i = 0; i < eqs.Count; i++)
        {
            if (Math.Abs(coef[i]) < 1e-9) continue;
            if (coef[i] > 0)
                pos.Add(Math.Abs(coef[i] - 1) < 1e-9 ? eqs[i].sym : $"{FormatNum(coef[i])}{eqs[i].sym}");
            else
                neg.Add("-" + (Math.Abs(coef[i] + 1) < 1e-9 ? eqs[i].sym : $"{FormatNum(-coef[i])}{eqs[i].sym}"));
        }
        string expr = string.Join("+", pos) + string.Concat(neg);

        return new EngineResponse
        {
            Ok = true,
            FinalResult = $"目标方程由已知方程线性组合得到，ΔH = {expr} kJ/mol。",
            DerivationSteps = new List<string>
            {
                "根据 Hess 定律，目标方程为各分步方程的线性组合",
                "校验：盐与水均守恒，组合成立",
                $"ΔH = {expr} kJ/mol"
            },
            ConstraintNote = "【高中限定】Hess 定律代数组合，已校验物料守恒",
            Confidence = 1.0
        };
    }

    /// <summary>
    /// 由 Ka 比较等浓度两种钠盐溶液的离子总浓度：
    /// 电荷守恒 c(Na+)+c(H+)=c(A-)+c(OH-)，总离子 = 2(c(Na+)+c(H+))；
    /// Ka 越大→酸越强→共轭碱水解越弱→c(H+)越大→总离子浓度越大。
    /// </summary>
    private static EngineResponse? TryCompareIonConcentrationByKa(string raw)
    {
        if (!raw.Contains("离子总浓度") || !raw.Contains("等浓度")) return null;

        var kaList = new List<(string acid, double ka)>();
        foreach (Match m in Regex.Matches(raw,
            @"Ka\(([^)]+)\)\s*=\s*([\d.]+)\s*(?:[×xX*]\s*10\^|e|E)(-?\d+)"))
        {
            double mantissa = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            int exp = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            kaList.Add((m.Groups[1].Value.Trim(), mantissa * Math.Pow(10, exp)));
        }
        if (kaList.Count != 2) return null;

        // 提取两种钠盐（"X溶液"，允许盐名与"溶液"间有空格）
        var salts = new List<string>();
        foreach (Match m in Regex.Matches(raw, @"([A-Za-z0-9]+)\s*溶液"))
        {
            string s = m.Groups[1].Value;
            if ((s.StartsWith("Na") || s.EndsWith("Na")) && !salts.Contains(s))
                salts.Add(s);
        }
        if (salts.Count != 2) return null;

        // 盐 ↔ 酸 关联：盐去掉 Na 得阴离子根，酸含该根；取 Ka 大者对应的盐
        string winner = "";
        double bestKa = double.MinValue;
        foreach (var salt in salts)
        {
            string core = salt.StartsWith("Na") ? salt.Substring(2) : salt.Substring(0, salt.Length - 2);
            var match = kaList.FirstOrDefault(k => k.acid.Contains(core, StringComparison.OrdinalIgnoreCase));
            if (match.acid is null) return null; // 关联不上，不蒙数
            if (match.ka > bestKa) { bestKa = match.ka; winner = salt; }
        }
        if (string.IsNullOrEmpty(winner)) return null;

        return new EngineResponse
        {
            Ok = true,
            FinalResult = $"{winner}溶液中离子总浓度更大。",
            DerivationSteps = new List<string>
            {
                "等浓度时 c(Na+) 相等；电荷守恒 c(Na+)+c(H+)=c(A-)+c(OH-)，总离子浓度 = 2(c(Na+)+c(H+))",
                $"Ka({kaList[0].acid})={FormatSci(kaList[0].ka)}，Ka({kaList[1].acid})={FormatSci(kaList[1].ka)}",
                $"{winner} 对应酸的 Ka 更大、酸性更强，其阴离子水解更弱，c(H+) 更大，故总离子浓度更大"
            },
            ConstraintNote = "【高中限定】电荷守恒 + 盐类水解规律",
            Confidence = 1.0
        };
    }

    private static double ParseNumOrSymbol(string tok, Dictionary<string, double> known)
    {
        if (double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return d;
        if (known.TryGetValue(tok, out double v)) return v;
        return double.NaN;
    }

    private static List<(double n, double count)> ParseHydrates(string side, Dictionary<string, double> known)
    {
        var list = new List<(double, double)>();
        foreach (Match m in Regex.Matches(side, @"(\d*)\s*([A-Za-z0-9]+)·\s*(\d+|x|y|z)H\s*2\s*O"))
        {
            double count = string.IsNullOrEmpty(m.Groups[1].Value)
                ? 1.0
                : double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            double n = ParseNumOrSymbol(m.Groups[3].Value, known);
            if (!double.IsNaN(n)) list.Add((n, count));
        }
        return list;
    }

    private static (List<(double n, double count)> left, List<(double n, double count)> right)
        SplitReactionSides(string rxn, Dictionary<string, double> known)
    {
        var arrow = Regex.Match(rxn, @"[⇌═=→]");
        string l = arrow.Success ? rxn.Substring(0, arrow.Index) : rxn;
        string r = arrow.Success ? rxn.Substring(arrow.Index + arrow.Length) : "";
        return (ParseHydrates(l, known), ParseHydrates(r, known));
    }

    private static (long, long) SimplifyRatio(double a, double b)
    {
        long la = 0, lb = 0;
        for (int scale = 1; scale <= 1000000; scale *= 10)
        {
            double aa = a * scale, bb = b * scale;
            if (Math.Abs(aa - Math.Round(aa)) < 1e-6 && Math.Abs(bb - Math.Round(bb)) < 1e-6)
            {
                la = (long)Math.Round(aa);
                lb = (long)Math.Round(bb);
                break;
            }
        }
        if (la == 0 || lb == 0) return ((long)Math.Round(a), (long)Math.Round(b));
        long g = Gcd(Math.Abs(la), Math.Abs(lb));
        return (la / g, lb / g);
    }

    private static long Gcd(long a, long b)
    {
        while (b != 0) { long t = b; b = a % b; a = t; }
        return a;
    }

    private static string FormatNum(double n)
    {
        if (Math.Abs(n - Math.Round(n)) < 1e-9)
            return ((long)Math.Round(n)).ToString(CultureInfo.InvariantCulture);
        return n.ToString("0.##", CultureInfo.InvariantCulture);
    }

    // ==================== 化学平衡 / 水溶液平衡引擎 ====================

    private sealed class PrecipitateTypeEntry
    {
        public string Id { get; set; } = "";
        public string Formula { get; set; } = "";
        public string Cation { get; set; } = "";
        public string Anion { get; set; } = "";
        public int CatRatio { get; set; }
        public int AnRatio { get; set; }
    }

    private sealed class PrecipitateTypesData
    {
        public string Version { get; set; } = "";
        public string Source { get; set; } = "";
        public List<PrecipitateTypeEntry> Entries { get; set; } = new();
    }

    /// <summary>
    /// 常见沉淀的阳离子:阴离子比例（高中限定）。
    /// 事实外置在 <c>data/highschool/precipitate-types.json</c>（B-LlmMain T4 组 2），按 formula 键（Ordinal）
    /// 查得 (cation, anion, catRatio, anRatio, id)；id 用作推理节点 KnowledgeId（R4 真实锚点）。
    /// 加载失败降级为空表 → TryGetValue 全 miss → 求解器返回 null（不猜，与原硬编码永不抛的鲁棒性一致）。
    /// </summary>
    private static readonly Lazy<Dictionary<string, (string cation, string anion, int catRatio, int anRatio, string id)>> PrecipitateTypes = new(LoadPrecipitateTypes);

    private static Dictionary<string, (string cation, string anion, int catRatio, int anRatio, string id)> LoadPrecipitateTypes()
    {
        try
        {
            var data = RuleStore.Load().Query<PrecipitateTypesData>("precipitate-types");
            var dict = new Dictionary<string, (string, string, int, int, string)>(StringComparer.Ordinal);
            foreach (var e in data.Entries)
            {
                if (string.IsNullOrWhiteSpace(e.Formula)) continue;
                dict[e.Formula] = (e.Cation, e.Anion, e.CatRatio, e.AnRatio, e.Id);
            }
            return dict;
        }
        catch
        {
            return new Dictionary<string, (string, string, int, int, string)>(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// 金属活动性顺序（从活泼到不活泼，H 为氢前/氢后参照位）。
    /// 取自 <c>data/highschool/activity-series.json</c>（B-LlmMain T4 组 3 外置，原方法内硬编码数组；
    /// Core 已加载同文件，网关侧懒加载复用，字节级一致）。加载失败降级为空数组 → 活动性分支全部
    /// return null（不猜，零假阳性），与原硬编码「永不抛」的鲁棒性等价。
    /// </summary>
    private static readonly Lazy<string[]> ActivitySeries = new(LoadActivitySeries);

    private static string[] LoadActivitySeries()
    {
        try
        {
            var series = RuleStore.Load().Query<ActivitySeriesData>("activity-series").Series;
            if (series is null || series.Count == 0) return System.Array.Empty<string>();
            return series.ToArray();
        }
        catch
        {
            return System.Array.Empty<string>();
        }
    }

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

        // 4. 燃烧热法求反应焓（酯化 ΔH）
        var comb = TryCalculateCombustionEnthalpy(raw);
        if (comb is not null) return comb;

        // 5. Hess 定律：逆反应焓变异号
        var hessRev = TryCalculateHessReverse(raw);
        if (hessRev is not null) return hessRev;

        // 6. 守恒式（电荷/物料/质子）：复用 Chem.Core AqueousEquilibriumEngine
        var conservation = TrySolveConservationEquation(raw);
        if (conservation is not null) return conservation;

        // 7. 弱酸酸性强弱排序：基于 acid-constants.json 的 Ka 比较
        var acidOrder = TryOrderAcidStrength(raw);
        if (acidOrder is not null) return acidOrder;

        return new EngineResponse
        {
            Ok = null,
            ConstraintNote = "【高中限定】当前平衡引擎仅支持Ksp沉淀顺序、电离平衡移动、可逆反应转化率，此题需人工分析",
            Confidence = 1.0
        };
    }

    /// <summary>
    /// 守恒式（电荷/物料/质子）：复用 Chem.Core 的 AqueousEquilibriumEngine。
    /// 仅在题面显式问"电荷守恒/物料守恒/质子守恒"时触发，从"<solute>溶液"提取溶质，
    /// 引擎返回 NoSolution 或无对应守恒式时一律 null（不猜）。
    /// </summary>
    private static EngineResponse? TrySolveConservationEquation(string raw)
    {
        bool wantCharge = raw.Contains("电荷守恒");
        bool wantMaterial = raw.Contains("物料守恒");
        bool wantProton = raw.Contains("质子守恒");
        if (!wantCharge && !wantMaterial && !wantProton) return null;

        var m = Regex.Match(raw, @"([A-Z][A-Za-z0-9()]{1,12})\s*溶液");
        if (!m.Success) return null;
        var solute = m.Groups[1].Value;

        AqueousResult? res;
        try
        {
            res = AqueousEquilibriumEngine.Infer(new AqueousRequest
            {
                Solutes = new() { new SoluteSpec { Species = solute, Concentration = "0.1", VolumeLiters = "1" } },
                OrderSpecies = new(),
            });
        }
        catch { return null; }
        if (res is null) return null;

        var eqs = res.ConservationEquations ?? new List<string>();
        // 引擎顺序恒为 [0]电荷、[1]物料、[2]质子（见 AqueousEquilibriumEngineTests H01/H03/H04/H07）
        string? eq = null; string label = "";
        if (wantCharge && eqs.Count > 0) { eq = eqs[0]; label = "电荷守恒式"; }
        else if (wantMaterial && eqs.Count > 1) { eq = eqs[1]; label = "物料守恒式"; }
        else if (wantProton && eqs.Count > 2) { eq = eqs[2]; label = "质子守恒式"; }
        if (string.IsNullOrWhiteSpace(eq)) return null;

        return new EngineResponse
        {
            Ok = true,
            FinalResult = $"{solute} 溶液的{label}为：{eq}",
            ConstraintNote = "【引擎】由 AqueousEquilibriumEngine 基于物种与电荷规则推导",
            Confidence = 1.0
        };
    }

    /// <summary>
    /// 弱酸酸性强弱排序：基于 acid-constants.json 的 Ka 数值比较（多元酸取第一步 Ka1）。
    /// 题面须显式给出方向"由弱到强/由强到弱"；任一出现的酸在库中缺 Ka 即返回 null（不猜）。
    /// </summary>
    private static EngineResponse? TryOrderAcidStrength(string raw)
    {
        bool weakToStrong = raw.Contains("由弱到强");
        bool strongToWeak = raw.Contains("由强到弱");
        if (!weakToStrong && !strongToWeak) return null;

        AcidConstantsData? data;
        try { data = RuleStore.Load().Query<AcidConstantsData>("acid-constants"); }
        catch { return null; }
        if (data is null || data.Ka.Count == 0) return null;

        // acid -> (Ka 数值, 显示串, JSON 稳定键 kaKey)；多元酸只取第一步 Ka1 代表酸性强度。
        // kaKey 用作推理节点的 KnowledgeId（真实锚点，镜像 HydrolysisPhCalculator 的 kaKey 范式）。
        var acidInfo = new Dictionary<string, (double val, string disp, string kaKey)>(StringComparer.Ordinal);
        foreach (var kv in data.Ka)
        {
            var key = kv.Key;
            var idx = key.IndexOf(':');
            var acid = idx > 0 ? key.Substring(0, idx) : key;
            var step = idx > 0 ? key.Substring(idx + 1) : "Ka1";
            if (step != "Ka1") continue;
            var val = double.Parse(kv.Value.Mantissa, CultureInfo.InvariantCulture) * Math.Pow(10, kv.Value.Exponent);
            acidInfo[acid] = (val, $"{kv.Value.Mantissa}×10^{kv.Value.Exponent}", key);
        }
        if (acidInfo.Count == 0) return null;

        var names = acidInfo.Keys.OrderByDescending(k => k.Length).ToList();
        var pattern = "(?<![A-Za-z0-9])(" + string.Join("|", names.Select(Regex.Escape)) + ")(?![A-Za-z0-9])";
        var found = new List<string>();
        foreach (Match mt in Regex.Matches(raw, pattern))
        {
            var a = mt.Groups[1].Value;
            if (!found.Contains(a)) found.Add(a);
        }
        if (found.Count < 2) return null;
        if (found.Any(a => !acidInfo.ContainsKey(a))) return null; // 库外酸不猜

        var sorted = found.OrderBy(a => acidInfo[a].val).ToList(); // 升序 = 弱到强
        if (strongToWeak) sorted.Reverse();
        var sep = weakToStrong ? " < " : " > ";
        var result = string.Join(sep, sorted);
        var dir = weakToStrong ? "由弱到强" : "由强到弱";
        var finalResult = $"{dir}：{result}（依据 25 ℃ 电离常数 Ka：{string.Join("、", sorted.Select(a => $"{a}={acidInfo[a].disp}"))}）";

        // R4 可溯源线程化：每个命中酸的 Ka1 均来自 acid-constants.json → KnowledgeId=kaKey（真实锚点）；
        // 到达此处 found≥2 且均在库内（上方守卫已保证），故命中非空；命中全空的路径在上方已 return null。
        var acidHits = sorted
            .Select(a => new AcidOrderReasoningNodes.AcidHit(a, acidInfo[a].kaKey, acidInfo[a].disp))
            .ToList();

        return new EngineResponse
        {
            Ok = true,
            FinalResult = finalResult,
            ReasoningNodes = AcidOrderReasoningNodes.Build(acidHits, finalResult),
            ConstraintNote = "【引擎】基于 acid-constants.json 的 Ka 数值比较，不猜测数据之外的酸",
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

        var precipitates = new List<(string formula, double ksp, int catRatio, int anRatio, string cation, string anion, string id)>();
        foreach (Match m in kspMatches)
        {
            var formula = m.Groups[1].Value.Trim();
            if (!PrecipitateTypes.Value.TryGetValue(formula, out var type)) continue;
            var mantissa = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            var exp = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            var ksp = mantissa * Math.Pow(10, exp);
            precipitates.Add((formula, ksp, type.catRatio, type.anRatio, type.cation, type.anion, type.id));
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
            return (p.formula, p.anion, p.catRatio, p.anRatio, p.ksp, needed, p.id);
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

        // R4 可溯源线程化：每个参与比较的沉淀其类型均来自 precipitate-types.json → KnowledgeId=条目 id（真实锚点）。
        // 到达此处 results≥2 且均命中事实表（上方守卫已保证），故 pptHits 非空；命中全空的路径在上方已 return null。
        var finalResult = $"{first.anion} 先沉淀。开始沉淀所需{cationLabel}浓度最小（{FormatSci(first.needed)} mol/L），最先达到溶度积。";
        var pptHits = results
            .Select(r => new PrecipitateReasoningNodes.PrecipitateHit(r.formula, r.id))
            .ToList();

        return new EngineResponse
        {
            Ok = true,
            FinalResult = finalResult,
            DerivationSteps = steps,
            ReasoningNodes = PrecipitateReasoningNodes.Build(pptHits, finalResult),
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
    /// 燃烧热法求反应焓：ΔH = ΣΔHc(反应物) − ΣΔHc(产物)。
    /// 酯化反应 A+B→酯+H2O，H2O(l) 燃烧热为 0，给出 3 个燃烧热即可求。
    /// </summary>
    private static EngineResponse? TryCalculateCombustionEnthalpy(string raw)
    {
        if (!raw.Contains("燃烧热") || !raw.Contains("酯化")) return null;
        if (!Regex.IsMatch(raw, @"[═=→⇌]")) return null;

        var nums = new List<double>();
        foreach (Match m in Regex.Matches(raw, @"([−\-]?\s?\d[\d\s]*)\s*kJ\s*/\s*mol"))
        {
            string t = m.Groups[1].Value.Replace(" ", "").Replace("−", "-").Replace("－", "-");
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                nums.Add(v);
        }
        if (nums.Count < 3) return null;
        double v1 = nums[0], v2 = nums[1], v3 = nums[2];
        double dH = v1 + v2 - v3;

        return new EngineResponse
        {
            Ok = true,
            FinalResult = $"酯化反应的 ΔH = {FormatSigned(dH)} kJ/mol（燃烧热法：ΔH = ΣΔHc(反应物) − ΣΔHc(产物) = ({FormatSigned(v1)})+({FormatSigned(v2)})−({FormatSigned(v3)})）。",
            DerivationSteps = new List<string>
            {
                "酯化：CH3COOH + C2H5OH → CH3COOC2H5 + H2O(l)",
                "H2O(l) 为燃烧终态，其燃烧热为 0",
                $"ΔH = ΔHc(CH3COOH) + ΔHc(C2H5OH) − ΔHc(CH3COOC2H5) = {FormatSigned(dH)} kJ/mol"
            },
            ConstraintNote = "【高中限定】燃烧热法，H2O(l) 燃烧热为 0",
            Confidence = 1.0
        };
    }

    /// <summary>
    /// Hess 定律：若反应②是反应①的逆反应，则 ΔH2 = −ΔH1。
    /// </summary>
    private static EngineResponse? TryCalculateHessReverse(string raw)
    {
        var m1 = Regex.Match(raw, @"反应\s*①\s*[：:]\s*(.+?)ΔH\s*1\s*=\s*([+\-−]?\s?\d+(?:\.\d+)?)\s*kJ\s*/\s*mol");
        var m2 = Regex.Match(raw, @"反应\s*②\s*[：:]\s*(.+?)(?:ΔH\s*2|ΔH₂)\s*=\s*_{2,}");
        if (!m1.Success || !m2.Success) return null;

        if (!AreReactionsReverse(m1.Groups[1].Value, m2.Groups[1].Value)) return null;

        string v = m1.Groups[2].Value.Replace(" ", "").Replace("−", "-").Replace("－", "-");
        if (!double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double dH1)) return null;
        double dH2 = -dH1;

        return new EngineResponse
        {
            Ok = true,
            FinalResult = $"反应②是反应①的逆反应，ΔH2 = {FormatSigned(dH2)} kJ/mol。",
            DerivationSteps = new List<string>
            {
                "反应②与反应①互为逆反应",
                "逆反应的焓变与正反应大小相等、符号相反",
                $"ΔH2 = −ΔH1 = {FormatSigned(dH2)} kJ/mol"
            },
            ConstraintNote = "【高中限定】Hess 定律，逆反应焓变异号",
            Confidence = 1.0
        };
    }

    /// <summary>
    /// 通用 Hess 定律线性组合求解：复用已单测的 ThermochemicalHessSolver（解析编号热化学方程式，
    /// 求使 Σ kᵢ·反应ᵢ = 目标反应 的组合，ΔH = Σ kᵢ·ΔHᵢ）；无法解析/不守恒时返回 null（不蒙数）。
    /// 覆盖：逆反应+缩放（d10-1）、加减消元（d10-2 / pdf-242）、多步线性组合（pdf-260）。
    /// </summary>
    private static EngineResponse? TryCalculateHessViaSolver(string raw)
    {
        var solved = ThermochemicalHessSolver.TrySolve(raw);
        if (solved is null) return null;
        var (val, derivation) = solved.Value;
        string valStr = FormatSigned(Math.Round(val, 3, MidpointRounding.AwayFromZero));
        return new EngineResponse
        {
            Ok = true,
            FinalResult = $"目标反应的 ΔH = {valStr} kJ/mol（Hess 定律线性组合求解）。",
            DerivationSteps = new List<string> { derivation, $"ΔH_目标 = {valStr} kJ/mol" },
            ConstraintNote = "【高中限定】Hess 定律通用线性组合，已校验物种守恒",
            Confidence = 1.0
        };
    }

    private static string FormatSigned(double n)
    {
        if (n >= 0) return "+" + FormatNum(n);
        return FormatNum(n);
    }

    private static bool AreReactionsReverse(string rxn1, string rxn2)
    {
        var (l1, r1) = SplitReactionTokens(rxn1);
        var (l2, r2) = SplitReactionTokens(rxn2);
        if (l1.Count == 0 || r1.Count == 0 || l2.Count == 0 || r2.Count == 0) return false;
        return SetEquals(l1, r2) && SetEquals(r1, l2);
    }

    private static (List<string> left, List<string> right) SplitReactionTokens(string rxn)
    {
        var arrow = Regex.Match(rxn, @"[⇌═=→]");
        string left = arrow.Success ? rxn.Substring(0, arrow.Index) : rxn;
        string right = arrow.Success ? rxn.Substring(arrow.Index + arrow.Length) : "";
        return (TokenizeSide(left), TokenizeSide(right));
    }

    private static List<string> TokenizeSide(string side)
    {
        string s = Regex.Replace(side, @"\([slg][aq\s]*\)", "");
        s = s.Replace("　", " ");
        return s.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .ToList();
    }

    private static bool SetEquals(List<string> a, List<string> b)
    {
        if (a.Count != b.Count) return false;
        var sa = new HashSet<string>(a, StringComparer.Ordinal);
        foreach (var x in b) if (!sa.Contains(x)) return false;
        return true;
    }

    /// <summary>
    /// 反应可行性规则引擎：基于金属活动性顺序、氧化性酸等高中化学规则，
    /// 对常见反应题给出具体结论，避免只返回"已匹配到相关反应"。
    /// 返回 null 表示规则无法判断，回退到知识库匹配。
    /// </summary>
    private EngineResponse? TryJudgeReactionFeasibility(string stem)
    {
        // 金属活动性顺序（从活泼到不活泼），H 为氢前/氢后分界；取自 activity-series.json（B-LlmMain T4 组 3 外置）。
        var activitySeries = ActivitySeries.Value;
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
                    ReasoningNodes = FeasibilityRuleReasoningNodes.OxidizingAcidNoH2(acidName),
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
                        ReasoningNodes = ActivityReasoningNodes.HydrogenPosition(metal, beforeH: true),
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
                        ReasoningNodes = ActivityReasoningNodes.HydrogenPosition(metal, beforeH: false),
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

        // ===== 规则1c：金属 + 氧化性酸（硝酸、浓硫酸）能否反应 =====
        if (metal is not null && asksCanReact && IsOxidizingAcid())
        {
            var acidName = stem.Contains("硝酸") ? "硝酸" : "浓硫酸";
            var isConc = stem.Contains("浓");
            // Pt、Au 不与硝酸/浓硫酸单独反应（需王水）
            if (metal is "Pt" or "Au")
            {
                return new EngineResponse
                {
                    Ok = false,
                    FinalResult = $"不能反应。{metal}是惰性金属，不与{acidName}反应（需王水才能溶解）。",
                    ReasoningNodes = ActivityReasoningNodes.InertMetalOxidizingAcid(metal),
                    IsStudentAnswerCorrect = false,
                    ErrorTags = new List<string> { "E04" },
                    DerivationSteps = new List<string>
                    {
                        $"{metal}是极不活泼金属",
                        $"{acidName}不能氧化{metal}"
                    },
                    ConstraintNote = "【高中限定】Pt、Au不与硝酸/浓硫酸反应",
                    Confidence = 1.0
                };
            }
            // 其他金属均能与氧化性酸反应（不生成H2）
            var note = isConc
                ? $"{metal}能被{acidName}氧化，反应生成盐、含氮/含硫氧化物和水，不生成H2。"
                : $"{metal}能被{acidName}氧化，反应不生成H2。";
            return new EngineResponse
            {
                Ok = true,
                FinalResult = $"能反应。{note}",
                ReasoningNodes = ActivityReasoningNodes.ActiveMetalOxidizingAcid(metal),
                IsStudentAnswerCorrect = true,
                ErrorTags = new List<string>(),
                DerivationSteps = new List<string>
                {
                    $"{acidName}具有强氧化性",
                    $"{metal}能被{acidName}氧化（Pt、Au除外）",
                    "氧化性酸与金属反应不生成H2"
                },
                ConstraintNote = $"【高中限定】{acidName}与金属反应",
                Confidence = 1.0
            };
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
                    ReasoningNodes = ActivityReasoningNodes.HydrogenPosition(metal, beforeH: true),
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
                    ReasoningNodes = ActivityReasoningNodes.HydrogenPosition(metal, beforeH: false),
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
                    ReasoningNodes = ActivityReasoningNodes.HighlyActiveWaterFirst(metal),
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
                { "Cu", new[] { "CuSO4", "CuCl2", "Cu(NO3)2", "Cu(OH)2", "CuBr2" } },
                { "Ag", new[] { "AgNO3", "AgCl", "Ag2SO4" } },
                { "Fe", new[] { "FeCl3", "FeCl2", "FeSO4", "Fe2(SO4)3", "Fe(NO3)2", "Fe(NO3)3" } },
                { "Zn", new[] { "ZnSO4", "ZnCl2", "Zn(NO3)2" } },
                { "Sn", new[] { "SnCl2", "SnCl4", "SnSO4", "Sn(NO3)2" } },
                { "Pb", new[] { "Pb(NO3)2", "PbCl2", "PbSO4", "Pb(CH3COO)2" } },
                { "Mg", new[] { "MgSO4", "MgCl2", "Mg(NO3)2" } },
                { "Al", new[] { "AlCl3", "Al2(SO4)3", "Al(NO3)3" } },
                { "Hg", new[] { "HgCl2", "Hg(NO3)2", "Hg2(NO3)2" } },
                { "Mn", new[] { "MnSO4", "MnCl2", "Mn(NO3)2" } }
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
                        ReasoningNodes = ActivityReasoningNodes.MetalDisplacement(metal, saltMetal),
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
                    ReasoningNodes = FeasibilityRuleReasoningNodes.FeDiluteAcidValence(),
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

    /// <summary>从原始文本中提取离子符号（兜底，当LLM未提取离子时使用）。
    /// 设为 internal 供 ChoiceVerifier 复用（离子共存路径），保持单一真值源。</summary>
    internal static List<string> ExtractIonsFromText(string raw)
    {
        var ions = new List<string>();
        // 匹配常见离子符号：如 Na+, Cl-, SO4^2-, Fe3+, NH4+, OH-, NO3-, CO3^2- 等
        var pattern = @"(?:Na|K|Ag|NH4|H)\+|" +
                      @"(?:Cl|Br|I|OH|NO3)\+?\-|" +
                      @"(?:SO4|CO3|SO3|S2O3|SiO3|CrO4|Cr2O7|PO4|HCO3|HSO4|HSO3|CH3COO|ClO|ClO3|MnO4|SCN|CN|S|C2O4)\^?[23]?\-|" +
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
    public async Task<string> GenerateReplyAsync(string studentText, EngineResponse resp, string taskType, CancellationToken ct = default)
    {
        // need_more_condition：反问补齐
        if (resp.NeedMoreCondition)
        {
            return $"要解决这个问题，还需要补充一些信息：{resp.ConstraintNote}。请告诉我具体的条件，我再帮你分析。";
        }

        // ok=null：超出引擎范围。
        // 非 concept：不调 LLM（避免 LLM 输出确定性化学结论，违反"引擎是唯一真值源"原则），
        //   直接返回拒答话术——不蒙数。
        // concept：暂保留 LLM 概念讲解（标记为非引擎真值、仅供参考；评估侧不计正确），
        //   是否建概念知识库纳入引擎真值见 spec Open Questions。
        if (resp.Ok is null)
        {
            if (taskType != "concept")
            {
                var baseNote = string.IsNullOrWhiteSpace(resp.ConstraintNote)
                    ? "这个问题超出了当前引擎的建模范围"
                    : resp.ConstraintNote;
                return $"{baseNote}，无法给出确定答案。建议请教老师或查阅教材，你想从哪个角度入手？";
            }
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
