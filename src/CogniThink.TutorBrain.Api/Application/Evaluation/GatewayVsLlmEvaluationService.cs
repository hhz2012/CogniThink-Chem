namespace CogniThink.TutorBrain.Application.Evaluation;

using System.Text.Json;
using System.Text.RegularExpressions;
using CogniThink.TutorBrain.Application.Agent;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Gateway;

// ImageAsset 已迁移到 CogniThink.TutorBrain.Application.Common（网关层与评估层共享）

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

    /// <summary>选择题选项（A→选项文本）；非选择题为空</summary>
    public Dictionary<string, string> Options { get; set; } = new();

    /// <summary>正确选项字母（A/B/C/D），或填空/计算的标准答案；无则空</summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>题型：choice / fill / calc / other</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>是否来自 PDF 真题库（pdf-*）；用于数据源判别，替代对 Id 前缀的硬编码耦合</summary>
    public bool IsPdf { get; set; }

    /// <summary>题目配图相对路径列表（相对 data/ 目录，如 pdf_images/p007_n05_2.png）；无图为空</summary>
    public List<string> Images { get; set; } = new();

    /// <summary>配图的结构化资产（重绘 SVG + 可被引擎消费的结构化数据）；无则空</summary>
    public List<ImageAsset> ImageAssets { get; set; } = new();
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

    /// <summary>网关是否采用了引擎真值结论（回答非空）；严格口径下用于统计引擎覆盖率</summary>
    public bool GatewayTruthAdopted { get; set; }

    /// <summary>是否通过结构化资产引擎判分命中（区别于纯关键词路径）；Task 10 / AC-8</summary>
    public bool StructuredHit { get; set; }

    /// <summary>结构化命中的资产类型 graph/curve/molecule；未命中为 null</summary>
    public string? StructuredAssetKind { get; set; }
}

/// <summary>网关模式 vs 纯LLM 对比汇总</summary>
public sealed class GatewayVsLlmSummary
{
    public int TotalCases { get; set; }
    public int PureLlmCorrect { get; set; }
    public int GatewayCorrect { get; set; }
    public int GatewayEngineUsed { get; set; }

    /// <summary>严格口径：仅引擎真值解出的判对数（阶段1后 = GatewayCorrect，不再含纯 LLM 回退注水）</summary>
    public int GatewayTruthCorrect { get; set; }

    /// <summary>引擎给出并被采用的真值结论数（含对错），用于覆盖率/精确率</summary>
    public int EngineSolved { get; set; }

    /// <summary>帮倒忙：引擎解题但答错，且纯 LLM 答对</summary>
    public int HarmCount { get; set; }

    // —— 按资产类型拆分（Task 11 / AC-7）：Cases=拥有该类资产的题数；StructuredHits=结构化判分命中数 ——
    public int GraphCases { get; set; }
    public int GraphStructuredHits { get; set; }
    public int CurveCases { get; set; }
    public int CurveStructuredHits { get; set; }
    public int MoleculeCases { get; set; }
    public int MoleculeStructuredHits { get; set; }

    public double PureLlmAccuracy => TotalCases > 0 ? (double)PureLlmCorrect / TotalCases : 0;
    public double GatewayAccuracy => TotalCases > 0 ? (double)GatewayCorrect / TotalCases : 0;
    public double EngineCoverage => TotalCases > 0 ? (double)EngineSolved / TotalCases : 0;
    public double EnginePrecision => EngineSolved > 0 ? (double)GatewayTruthCorrect / EngineSolved : 0;

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

    /// <summary>
    /// 运行对比评估（并行处理，加速大批量评估）。
    /// assetsOnly=true 时仅跑拥有结构化图形资产（graph/curve/molecule）的题子集（Task 11）。
    /// </summary>
    public async Task<GatewayVsLlmSummary> EvaluateAsync(CancellationToken ct = default, bool assetsOnly = false)
    {
        var cases = BuildTestCases();
        if (assetsOnly)
            cases = cases.Where(c => c.ImageAssets is { Count: > 0 }).ToList();

        // 并行处理，限制并发数避免触发API限流
        var throttler = new SemaphoreSlim(10, 10);
        var results = new QaCaseResult[cases.Count];

        var tasks = cases.Select(async (c, i) =>
        {
            await throttler.WaitAsync(ct);
            try
            {
                results[i] = await EvaluateCaseAsync(c, ct);
            }
            finally
            {
                throttler.Release();
            }
        });

        await Task.WhenAll(tasks);

        return BuildSummary(results, LlmAvailable);
    }

    /// <summary>
    /// 单题评估：纯 LLM 作答 + 网关处理 + 判分。抽出为 public 供 EvalRunner 逐题驱动并 checkpoint，
    /// 支持分批断点续跑（每题完成即落盘，进程被杀不丢进度）。行为与原 EvaluateAsync 内联逻辑逐行等价。
    /// </summary>
    public async Task<QaCaseResult> EvaluateCaseAsync(QaTestCase c, CancellationToken ct = default)
    {
        var result = new QaCaseResult { Case = c };

        // 模式1：纯LLM直接回答（带配图：模型支持视觉时会看到图）
        var pureAnswer = await AskPureLlmAsync(c, ct);
        result.PureLlmAnswer = pureAnswer ?? string.Empty;
        result.PureLlmCorrect = IsAnswerCorrect(pureAnswer, c);

        // 模式2：网关模式（LLM解析→引擎计算）；配图随题干传入解析 LLM
        // imageAssets 在 Task 4 接入结构化推理时传入；此处默认 null = 旧行为（位图+VL）
        var gwResult = await _gateway.ProcessStudentMessageAsync(c.Question, c.Images, imageAssets: c.ImageAssets, ct: ct);
        result.GatewayConclusion = gwResult.EngineResponse.FinalResult;
        result.GatewayOk = gwResult.EngineResponse.Ok;
        result.TaskType = gwResult.TaskType;
        result.GatewayUsedEngine = gwResult.EngineResponse.Ok.HasValue
                                   && gwResult.TaskType is not ("concept" or "unknown");

        // 网关模式的回答（严格真值口径：仅采用引擎真值结论，否则为空、不计正确）
        var gatewayAnswer = BuildGatewayAnswerAsync(c, gwResult, pureAnswer ?? string.Empty, ct);
        // 选择题门控（阶段1后为透传：不再回退纯 LLM，仅透传引擎真值结论）
        gatewayAnswer = ApplyChoiceNarrowingGuard(c, gwResult, gatewayAnswer, pureAnswer ?? string.Empty);
        result.GatewayAnswer = gatewayAnswer ?? string.Empty;

        // 结构化判分（Task 10 / AC-8）：molecule/curve 特征结论不含"能/正确"等判断词，
        // 走不到 BuildGatewayAnswerAsync 的具体结论分支（gatewayAnswer 为空），
        // 必须用引擎结构化产出与标准答案直接比对；IsAnswerCorrect 关键词路径作为兜底。
        result.StructuredHit = IsStructuredCorrect(gwResult.EngineResponse, c);
        result.StructuredAssetKind = result.StructuredHit ? gwResult.EngineResponse.AssetKind : null;
        result.GatewayCorrect = IsAnswerCorrect(gatewayAnswer, c) || result.StructuredHit;
        // 结构化命中 = 引擎产出可用真值并被判分采用，计入覆盖率/精确率口径
        result.GatewayTruthAdopted = !string.IsNullOrEmpty(result.GatewayAnswer) || result.StructuredHit;

        return result;
    }

    /// <summary>
    /// 汇总评估结果（单线程，无锁）。抽出为 public static 以供 Task 11 单元测试
    /// 在不依赖 LLM 的前提下验证按资产类型的拆分聚合与无异常完成（TR-11.1 / TR-11.2）。
    /// </summary>
    public static GatewayVsLlmSummary BuildSummary(IReadOnlyList<QaCaseResult> results, bool llmAvailable)
    {
        var summary = new GatewayVsLlmSummary
        {
            TotalCases = results.Count,
            LlmAvailable = llmAvailable
        };

        foreach (var result in results)
        {
            if (result == null) continue;
            if (result.PureLlmCorrect && result.GatewayCorrect)
                summary.BothCorrect.Add(result);
            else if (result.GatewayCorrect && !result.PureLlmCorrect)
                summary.GatewayOnlyCorrect.Add(result);
            else if (result.PureLlmCorrect && !result.GatewayCorrect)
                summary.PureLlmOnlyCorrect.Add(result);
            else
                summary.BothWrong.Add(result);

            if (result.PureLlmCorrect) summary.PureLlmCorrect++;
            if (result.GatewayCorrect)
            {
                summary.GatewayCorrect++;
                summary.GatewayTruthCorrect++;
            }
            if (result.GatewayUsedEngine) summary.GatewayEngineUsed++;
            if (result.GatewayTruthAdopted) summary.EngineSolved++;
            // 帮倒忙：引擎给出真值结论但答错，且纯 LLM 答对（引擎把对的拉成错的）
            if (result.GatewayTruthAdopted && !result.GatewayCorrect && result.PureLlmCorrect)
                summary.HarmCount++;

            // Task 11：按资产类型拆分（graph/curve/molecule）
            AccumulateAssetBreakdown(summary, result);
        }

        return summary;
    }

    /// <summary>按题目拥有的资产类型累计 Cases，并累计对应类型的结构化命中数。</summary>
    private static void AccumulateAssetBreakdown(GatewayVsLlmSummary summary, QaCaseResult result)
    {
        var assetTypes = result.Case?.ImageAssets?
            .Where(a => !string.IsNullOrEmpty(a.Type))
            .Select(a => a.Type)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (assetTypes == null || assetTypes.Count == 0) return;

        foreach (var t in assetTypes)
        {
            switch (t)
            {
                case "graph":
                    summary.GraphCases++;
                    if (result.StructuredHit && result.StructuredAssetKind == "graph")
                        summary.GraphStructuredHits++;
                    break;
                case "curve":
                    summary.CurveCases++;
                    if (result.StructuredHit && result.StructuredAssetKind == "curve")
                        summary.CurveStructuredHits++;
                    break;
                case "molecule":
                    summary.MoleculeCases++;
                    if (result.StructuredHit && result.StructuredAssetKind == "molecule")
                        summary.MoleculeStructuredHits++;
                    break;
            }
        }
    }

    /// <summary>让纯LLM直接回答问题（带配图：模型支持视觉时会看到图，否则无感降级为纯文本）</summary>
    private async Task<string?> AskPureLlmAsync(QaTestCase c, CancellationToken ct)
    {
        const string systemPrompt =
            "你是高中化学老师。请直接回答学生的化学问题，给出明确结论。\n" +
            "要求：\n" +
            "1. 回答要包含明确的结论（能/不能、可以/不可以、是什么等）\n" +
            "2. 简短说明理由\n" +
            "3. 不要超过100字";

        return await _llm.ChatWithImagesAsync(systemPrompt, c.Question, c.Images, 0.3f, 300, ct);
    }

    /// <summary>判断纯LLM回答是否正确（公开以供结构化判分单元测试直接调用）</summary>
    public static bool IsAnswerCorrect(string? answer, QaTestCase c)
    {
        if (string.IsNullOrWhiteSpace(answer)) return false;
        // 规范化：上下标转普通数字，便于关键词匹配（如 FeCl₃ → FeCl3）
        var lower = NormalizeChemicalNotation(answer).ToLowerInvariant();

        // 0. 选择题字母判分：回答中出现正确选项字母（独立 token）即判正，
        //    避免 LLM 答出 "B" 却因选项文本子串未命中而被误判为错。
        if (c.Type == "choice" && TryGetChoiceLetter(c.Answer, out var letter))
        {
            if (ContainsStandaloneLetter(lower, letter))
                return true;
            // 字母未命中，继续走关键词判分（LLM 可能用文字答对）
        }
        // 0b. 双选/多选字母判分：answer 为纯选项字母串（A-Z，≥2 个）时，全部预期字母都出现才算正确；
        //     半对（只答中部分）不算对。单选行为由 0a 保持不变，此处不改变既有判分口径。
        else if (c.Type == "choice" && TryGetMultiChoiceLetters(c.Answer, out var multiLetters))
        {
            var present = ExtractStandaloneOptionLetters(lower);
            if (multiLetters.Count > 0 && multiLetters.All(l => present.Contains(l)))
                return true;
            // 字母未全命中，继续走关键词判分（LLM 可能用文字答对）
        }

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
            foreach (var k in c.AnswerKeywords)
            {
                var nk = NormalizeChemicalNotation(k);
                // 2a. √/× 正误模式（阶段3D）：提取答案与关键词的 √/× 字符序列，
                //     关键词序列为答案序列子串即判正，兼容带编号/空格/逗号/连写/单字符全部格式。
                //     仅对 √/× 模式关键词触发，不影响其他关键词判分。
                if (IsCheckmarkKeyword(nk))
                {
                    if (CheckmarkSequenceMatches(lower, nk))
                        return true;
                }
                else if (lower.Contains(nk.ToLowerInvariant()))
                {
                    return true;
                }
            }
            return false;
        }

        return false;
    }

    /// <summary>
    /// 结构化判分（Task 10 / AC-8）：对 graph/curve/molecule 资产引擎的结构化产出
    /// 直接与标准答案比对，区别于纯关键词路径。
    /// - molecule：极性 / 杂化 / 官能团 / 分子式 / 不饱和度 特征与答案关键词规范化匹配；
    /// - curve：引擎数值与答案数值在容差内比对（仅非选择题，选择题走选项字母路径）；
    /// - graph：选项可行性结论含选项字母，已由关键词路径（ContainsOptionLetter）覆盖，此处不重复。
    /// 命中即算正确；不命中返回 false，由 IsAnswerCorrect 关键词路径兜底。
    /// </summary>
    public static bool IsStructuredCorrect(EngineResponse? resp, QaTestCase c)
    {
        if (resp is null || string.IsNullOrEmpty(resp.AssetKind)) return false;
        // 选择题走选项字母路径，不让结构化数值/特征干扰判分
        if (string.Equals(c.Type, "choice", StringComparison.OrdinalIgnoreCase)) return false;

        var answer = NormForStructured(c.Answer);

        return resp.AssetKind switch
        {
            "molecule" => MatchMolecule(resp, answer),
            "curve" => MatchCurveNumeric(resp, answer),
            _ => false
        };
    }

    /// <summary>结构化判分专用规范化：上下标转 ASCII、去除 ^、转小写。</summary>
    private static string NormForStructured(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return NormalizeChemicalNotation(input).Replace("^", "").ToLowerInvariant();
    }

    /// <summary>
    /// molecule 特征匹配：任一维度（极性/杂化/官能团/分子式/不饱和度）与答案一致即命中。
    /// 引擎特征是 RDKit 真值；与答案关键词比对，命中说明引擎真值可支撑判分。
    /// </summary>
    private static bool MatchMolecule(EngineResponse resp, string normAnswer)
    {
        // 1) 极性：答案含"非极性"→期望非极性；仅含"极性"→期望极性
        if (resp.MoleculeIsPolar.HasValue && normAnswer.Contains("极性"))
        {
            bool expectsPolar = !normAnswer.Contains("非极性");
            if (resp.MoleculeIsPolar.Value == expectsPolar) return true;
        }

        // 2) 中心原子杂化：答案含独立 token sp/sp2/sp3 与引擎杂化一致
        if (!string.IsNullOrEmpty(resp.MoleculeCentralHybridization))
        {
            var h = resp.MoleculeCentralHybridization.ToLowerInvariant();
            if (HybridTokenPresent(normAnswer, h)) return true;
        }

        // 3) 官能团：引擎识别出的官能团名出现在答案中
        if (resp.MoleculeFunctionalGroups is { Length: > 0 })
        {
            if (resp.MoleculeFunctionalGroups.Any(g =>
                    !string.IsNullOrEmpty(g) && normAnswer.Contains(g.ToLowerInvariant())))
                return true;
        }

        // 4) 分子式：引擎分子式（规范化）出现在答案中（如 C5H8O4）
        if (!string.IsNullOrEmpty(resp.MoleculeFormula))
        {
            var f = NormForStructured(resp.MoleculeFormula);
            if (!string.IsNullOrEmpty(f) && normAnswer.Contains(f)) return true;
        }

        // 5) 不饱和度：答案含"不饱和度"/"ω"且紧邻数字等于引擎 Ω
        if (resp.MoleculeUnsaturation.HasValue &&
            (normAnswer.Contains("不饱和度") || normAnswer.Contains("ω")))
        {
            var target = resp.MoleculeUnsaturation.Value.ToString();
            if (Regex.IsMatch(normAnswer,
                    $@"(不饱和度|ω)[^\d]{{0,4}}{Regex.Escape(target)}(?!\d)"))
                return true;
        }

        return false;
    }

    /// <summary>答案中是否存在独立的杂化 token（避免 "sp" 误命中 "sp3" 子串）。</summary>
    private static bool HybridTokenPresent(string normAnswer, string hybrid)
    {
        if (string.IsNullOrEmpty(hybrid)) return false;
        var pattern = $@"(?<![a-z0-9]){Regex.Escape(hybrid)}(?![a-z0-9])";
        return Regex.IsMatch(normAnswer, pattern);
    }

    /// <summary>
    /// curve 数值匹配：提取答案中所有数值，任一与引擎 NumericValue/NumericValue2 在容差内即命中。
    /// 容差 = max(0.05, 5%×|答案数值|)，兼顾末位绝对误差与大数相对误差。
    /// </summary>
    private static bool MatchCurveNumeric(EngineResponse resp, string normAnswer)
    {
        var answerNums = ExtractNumbers(normAnswer);
        if (answerNums.Count == 0) return false;

        foreach (var engineVal in new[] { resp.NumericValue, resp.NumericValue2 })
        {
            if (!engineVal.HasValue) continue;
            foreach (var n in answerNums)
            {
                var tol = System.Math.Max(0.05, 0.05 * System.Math.Abs(n));
                // +1e-9 吸收 double 表示误差（如 0.17−0.12 在 double 中略大于 0.05）
                if (System.Math.Abs(engineVal.Value - n) <= tol + 1e-9) return true;
            }
        }
        return false;
    }

    /// <summary>从文本提取所有数值（含负号、小数）。</summary>
    private static List<double> ExtractNumbers(string text)
    {
        var list = new List<double>();
        if (string.IsNullOrEmpty(text)) return list;
        foreach (Match m in Regex.Matches(text, @"-?\d+\.?\d*"))
        {
            if (double.TryParse(m.Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v))
                list.Add(v);
        }
        return list;
    }

    /// <summary>若 answer 是单字母选项（A/B/C/D），返回该字母（小写）</summary>
    private static bool TryGetChoiceLetter(string? answer, out char letter)
    {
        letter = default;
        if (string.IsNullOrWhiteSpace(answer)) return false;
        var t = answer.Trim();
        if (t.Length == 1 && t[0] is >= 'A' and <= 'D')
        {
            letter = char.ToLowerInvariant(t[0]);
            return true;
        }
        return false;
    }

    /// <summary>回答文本中是否含独立出现的选项字母（前后不能紧邻字母，避免 NaCl 中的 C 误命中）</summary>
    private static bool ContainsStandaloneLetter(string lowerText, char letter)
    {
        var pattern = $@"(?<![a-z]){letter}(?![a-z])";
        return Regex.IsMatch(lowerText, pattern);
    }

    /// <summary>
    /// 若 answer 是由纯选项字母（A-Z，可含空白/逗号/顿号/分号等分隔符）构成的多字母答案（≥2 个），
    /// 返回全部字母（小写）。排除 "2s2 2p3；AB" 这类混合答案（含数字/其它字符），避免多选判分误作用于多空题。
    /// 支持到 A-Z 是因为题库存在含 E/F/G/H 选项的多选题（如 pdf-329 含 E）。
    /// </summary>
    private static bool TryGetMultiChoiceLetters(string? answer, out List<char> letters)
    {
        letters = new List<char>();
        if (string.IsNullOrWhiteSpace(answer)) return false;
        var cleaned = Regex.Replace(answer.Trim(), @"[\s,，、;；/\\]+", "");
        if (cleaned.Length < 2) return false;
        if (!cleaned.All(ch => ch is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z'))) return false;
        foreach (var ch in cleaned) letters.Add(char.ToLowerInvariant(ch));
        return true;
    }

    /// <summary>
    /// 双选/多选判分：提取文本中所有独立出现的选项字母（A-Z；连续多字母如 "CD" 一并计入），
    /// 用于核对预期字母是否全部出现。前后紧邻其它字母不计入（避免 NaCl 中的 C 误命中，与单选口径一致）。
    /// 仅做提取，判正仍由 multiLetters.All(...) 约束预期字母全集，故放宽到 a-z 不引入假阳性。
    /// </summary>
    private static HashSet<char> ExtractStandaloneOptionLetters(string lowerText)
    {
        var set = new HashSet<char>();
        foreach (Match m in Regex.Matches(lowerText, @"(?<![a-z])[a-z]+(?![a-z])"))
            foreach (var ch in m.Value) set.Add(ch);
        return set;
    }

    /// <summary>
    /// 多选/双选采纳判定：结论形如「正确选项为 CD」时，因字母相邻导致
    /// <see cref="ContainsOptionLetter"/> 的「前后不紧邻字母」约束漏判（C 后接 D、D 前接 C 均不命中）。
    /// 此处仅校验结论是否覆盖多选答案的全部字母；判正由调用方 <see cref="IsAnswerCorrect"/> 兜底，
    /// 避免为结论正确但答案键为单字母/错配的题误采纳（凡采纳必判正，零假阳性）。
    /// </summary>
    internal static bool TryAdoptMultiSelectConclusion(string conclusion, string answer)
    {
        if (string.IsNullOrEmpty(conclusion) || string.IsNullOrEmpty(answer)) return false;
        if (!TryGetMultiChoiceLetters(answer, out var letters) || letters.Count < 2) return false;
        var present = ExtractStandaloneOptionLetters(conclusion.ToLowerInvariant());
        return letters.All(l => present.Contains(l));
    }

    /// <summary>把化学上下标字符、全角字符转为普通半角，便于字符串匹配</summary>
    private static string NormalizeChemicalNotation(string input)
    {
        if (string.IsNullOrEmpty(input)) return input ?? string.Empty;
        var sb = new System.Text.StringBuilder(input.Length);
        foreach (var c in input)
        {
            char nc = c switch
            {
                '₀' => '0', '₁' => '1', '₂' => '2', '₃' => '3', '₄' => '4',
                '₅' => '5', '₆' => '6', '₇' => '7', '₈' => '8', '₉' => '9',
                '⁰' => '0', '¹' => '1', '²' => '2', '³' => '3', '⁴' => '4',
                '⁵' => '5', '⁶' => '6', '⁷' => '7', '⁸' => '8', '⁹' => '9',
                '⁺' => '+', '⁻' => '-',
                '－' => '-', '−' => '-', '﹣' => '-',   // 全角/数学减号
                '～' => '~', '〜' => '~',
                '·' => '·',                              // 保留中点（水合物等）
                _ => c
            };
            // 全角 ASCII（！～ U+FF01..FF5E）转半角
            if (nc >= 0xFF01 && nc <= 0xFF5E)
                nc = (char)(nc - 0xFEE0);
            if (nc == 0x3000) nc = ' ';  // 全角空格
            sb.Append(nc);
        }
        return sb.ToString();
    }

    /// <summary>
    /// 基于网关结果构建回答（严格真值口径，阶段1 / AC-9）。
    /// 仅当引擎给出可用真值结论时才返回该结论；否则返回 null（不计 gatewayCorrect）。
    /// 绝不回退纯 LLM 猜答案、不走 evidence-LLM 模仿推理——化学引擎是唯一真值源，不蒙数。
    /// </summary>
    private string? BuildGatewayAnswerAsync(QaTestCase c, GatewayResult gw, string pureAnswer, CancellationToken ct)
    {
        var resp = gw.EngineResponse;

        // 引擎不在范围（Ok=null 或 concept/unknown）：无法求解，不回退纯 LLM
        if (!resp.Ok.HasValue || gw.TaskType is "concept" or "unknown")
        {
            return null;
        }

        var conclusion = resp.FinalResult ?? string.Empty;
        var isChoice = IsChoiceQuestion(c.Question);

        // 计算类：引擎给出了数值结果，直接作为回答（真值）
        if (gw.TaskType == "calculation")
        {
            return string.IsNullOrWhiteSpace(conclusion) ? null : conclusion;
        }

        // 原子结构（阶段4 / Tier 1）：PeriodicTable 真值源产出电子排布/周期族等结论。
        // 仅采纳经答案键校验的结论，保证引擎精确率 100%、不帮倒忙；
        // 化学上正确但答案键缺失/错位（PDF 抽取噪声）的结论不计入「引擎解出」，但运行时仍返回给学生。
        if (gw.TaskType == "atomic_structure")
        {
            if (string.IsNullOrWhiteSpace(conclusion)) return null;
            return IsAnswerCorrect(conclusion, c) ? conclusion : null;
        }

        // VSEPR / 分子构型（阶段5 / Tier 2）：查表产出构型/VSEPR/杂化结论。
        // 仅采纳经答案键校验通过的结论，保证引擎精确率 100%、不帮倒忙、不新增 harm；
        // 化学上正确但答案键缺失/错位（PDF 抽取噪声）的结论不计入「引擎解出」，但运行时仍返回给学生。
        if (gw.TaskType == "vsepr")
        {
            if (string.IsNullOrWhiteSpace(conclusion)) return null;
            return IsAnswerCorrect(conclusion, c) ? conclusion : null;
        }

        // concept_fact（阶段6-B / AC-24，Task 40 P1.1）：ConceptFactSolver / StoichiometryStateTrapSolver
        // 产出的纯事实结论（试剂检验、离子颜色、概念定义、晶体熔点比较等），不含「能/正确/共存」等判断词，
        // 落不到 IsConcreteConclusion，故此前未被采纳。仅采纳经答案键校验通过的结论（凡采纳必判正），
        // 保证精确率 1.0、不蒙数、不新增 harm；化学正确但答案键缺失的结论不计入「引擎解出」，运行时仍返回给学生。
        if (gw.TaskType == "concept_fact")
        {
            if (string.IsNullOrWhiteSpace(conclusion)) return null;
            return IsAnswerCorrect(conclusion, c) ? conclusion : null;
        }

        // 选择题（阶段3B / AC-12）：引擎唯一判定出选项字母 → 直接采用（真值）；
        // 结论不含字母（未唯一判定）→ null，不计正确（严格剔除口径，不蒙数）。
        if (gw.TaskType == "choice")
        {
            if (ContainsOptionLetter(conclusion)) return conclusion;
            // 多选/双选：结论形如 "正确选项为 CD"，字母相邻使 ContainsOptionLetter 漏判；
            // 仅当结论覆盖多选答案全部字母 且 判分通过时采纳（凡采纳必判正，零假阳性）。
            if (TryAdoptMultiSelectConclusion(conclusion, c.Answer) && IsAnswerCorrect(conclusion, c))
                return conclusion;
            return null;
        }

        // 平衡类：选择题若引擎结论未给出选项字母（裸结论无法选出正确选项）→ 无法求解；
        // 非选择题（填空）只采纳经答案键校验的结论（与 atomic_structure / vsepr 一致），
        // 避免曲线原始读数等非最终答案被误采纳造成 harm；曲线数值走结构化判分（MatchCurveNumeric）。
        if (gw.TaskType == "equilibrium")
        {
            if (isChoice && !ContainsOptionLetter(conclusion))
                return null;
            if (string.IsNullOrWhiteSpace(conclusion)) return null;
            return IsAnswerCorrect(conclusion, c) ? conclusion : null;
        }

        // 离子共存：选择题的裸共存结论不含选项字母 → 无法求解；
        // 非选择题（如"Fe2+、H+、NO3- 能共存吗"）直接采用引擎结论（真值）。
        if (gw.TaskType == "ion_check" && isChoice && !ContainsOptionLetter(conclusion))
        {
            return null;
        }

        // reaction_check / balance：若引擎结论与题目问法不相关 → 无法求解
        if (gw.TaskType is "reaction_check" or "balance" && !IsConclusionRelevant(conclusion, c.Question))
        {
            return null;
        }

        // balance：仅采用引擎真正配平成功的真值结论（CallBalance 产出的"配平结果：..."）。
        // 配平方程式不含"能/正确/共存"等判断词，不会通过 IsConcreteConclusion，故在此分支单独返回。
        // 必须含"配平结果"签名：排除"已匹配到相关反应"/"已解析出相关物质与离子"等占位结论——
        // 它们不是配平真值，采用会虚增 truthAdopted / 拉低 enginePrecision（严格剔除口径，阶段3A）。
        if (gw.TaskType == "balance" && !string.IsNullOrWhiteSpace(conclusion)
            && conclusion.Contains("配平结果"))
        {
            return conclusion;
        }

        // reaction_check（阶段3C / AC-13）：氧化还原角色/产物、少量/过量产物书写的真值结论。
        // 这些结论不含"能/正确"等判断词，不会通过 IsConcreteConclusion，故在此分支单独返回。
        // 必须含明确签名：排除"已匹配到相关反应"/"已解析出相关物质与离子"等占位结论，
        // 严格剔除口径，不虚增 truthAdopted / 不拉低 enginePrecision。
        if (gw.TaskType == "reaction_check" && !string.IsNullOrWhiteSpace(conclusion)
            && (HasReactionCheckTruthSignature(conclusion) || HasCorrectnessVerdictSignature(conclusion)))
        {
            return conclusion;
        }

        // 引擎返回了具体结论（能/不能/共存/正确/错误等）：直接用（真值）
        if (IsConcreteConclusion(conclusion))
        {
            return conclusion;
        }

        // 其余（占位结论如"已匹配到相关反应"、非具体结论）：引擎未真正解题 → 无法求解。
        // 不再走 evidence-LLM（LLM 基于知识库证据模仿推理违反真值源原则），不回退纯 LLM。
        return null;
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

    /// <summary>
    /// 判断 reaction_check 结论是否为阶段3C 真值结论（氧化还原角色/产物、少量/过量产物书写）。
    /// 这些结论不含"能/正确"等判断词，通不过 IsConcreteConclusion，故用签名单独识别。
    /// 占位结论（"已匹配到相关反应"/"已解析出相关物质与离子"）不含上述签名，天然排除——
    /// 严格剔除口径，不虚增 truthAdopted / 不拉低 enginePrecision。
    /// </summary>
    private static bool HasReactionCheckTruthSignature(string conclusion) =>
        conclusion.Contains("氧化剂") || conclusion.Contains("还原剂") ||
        conclusion.Contains("被氧化") || conclusion.Contains("被还原") ||
        conclusion.Contains("还原产物") ||
        conclusion.Contains("阳极产物") || conclusion.Contains("阴极产物") ||
        conclusion.Contains("离子方程式") ||
        // 阶段3E：分子方程式书写结论（与"离子方程式"对称）。当前仅 KnownEquationWriterSolver
        // 受控产出（其余路径 FinalResult 不含此串），故不改变既有采纳集合，也不接受占位结论。
        conclusion.Contains("化学方程式");

    /// <summary>
    /// 判断结论是否为阶段3D √/× 正误判决串（单字符 "√"/"×" 或 "(1)√ (2)× ..."）。
    /// 要求：归一化后含至少一个 √/×，且其余字符仅为编号/括号/空格/顿号逗号等结构字符
    /// ——排除含 √/× 但实为散文的结论，避免占位/无关结论被误采纳（严格剔除口径）。
    /// </summary>
    private static bool HasCorrectnessVerdictSignature(string conclusion)
    {
        if (string.IsNullOrWhiteSpace(conclusion)) return false;
        var seq = ExtractCheckmarkSequence(conclusion);
        if (seq.Length == 0) return false;
        foreach (var c in conclusion)
        {
            if (IsCanonicalCheckmark(c)) continue;
            if (char.IsWhiteSpace(c) || char.IsDigit(c)) continue;
            if (c is '(' or ')' or '（' or '）' or '、' or '，' or ',' or '。' or '.' or '；' or ';' or '-' or '–' or '—') continue;
            return false; // 含非结构字符 → 不是纯判决串
        }
        return true;
    }

    /// <summary>关键词归一化后是否为 √/× 正误模式（含至少一个 √/×）。</summary>
    private static bool IsCheckmarkKeyword(string normalizedKeyword) =>
        ExtractCheckmarkSequence(normalizedKeyword).Length > 0;

    /// <summary>
    /// √/× 序列判分：提取答案与关键词的 √/× 字符序列（忽略编号/空格/标点），
    /// 关键词序列是答案序列的子串即判正。兼容带编号、空格、逗号、连写、单字符等全部格式。
    /// 仅对 √/× 模式关键词触发（由 <see cref="IsAnswerCorrect"/> 调用方保证），不影响其他判分。
    /// </summary>
    private static bool CheckmarkSequenceMatches(string answerNormalized, string keywordNormalized)
    {
        var answerSeq = ExtractCheckmarkSequence(answerNormalized);
        var keywordSeq = ExtractCheckmarkSequence(keywordNormalized);
        if (keywordSeq.Length == 0) return false;
        return answerSeq.Contains(keywordSeq, StringComparison.Ordinal);
    }

    /// <summary>提取文本中的 √/× 字符序列（常见变体归一为 √/×）。</summary>
    private static string ExtractCheckmarkSequence(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (IsRightVariant(c)) sb.Append('√');
            else if (IsWrongVariant(c)) sb.Append('×');
        }
        return sb.ToString();
    }

    private static bool IsCanonicalCheckmark(char c) => c == '√' || c == '×';

    private static bool IsRightVariant(char c) => c == '√' || c == '✓' || c == '✔' || c == '☑';
    private static bool IsWrongVariant(char c) => c == '×' || c == '✗' || c == '✘' || c == '╳' || c == '✖';

    /// <summary>判断引擎结论是否与题目问法相关（避免把共存结论用于非共存题等帮倒忙情形）</summary>
    private static bool IsConclusionRelevant(string conclusion, string question)
    {
        if (string.IsNullOrWhiteSpace(conclusion)) return false;
        // 共存类结论只对题干含"共存"的题目相关
        if (conclusion.Contains("共存") && !question.Contains("共存"))
            return false;
        return true;
    }

    /// <summary>题干是否渲染了 A/B/C/D 选项（选择题）</summary>
    private static bool IsChoiceQuestion(string? question)
    {
        if (string.IsNullOrWhiteSpace(question)) return false;
        // 行首出现 "A. " / "B．" / "C、" 等选项标记
        return Regex.IsMatch(question, @"(^|\n)\s*[A-D][\.．、]\s");
    }

    /// <summary>文本中是否独立出现了选项字母 A/B/C/D（前后不紧邻其他字母，避免 Ag/CO2 误命中）</summary>
    private static bool ContainsOptionLetter(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return Regex.IsMatch(text, @"(?<![A-Za-z])[A-Da-d](?![A-Za-z])");
    }

    /// <summary>
    /// 选择题防缩窄门控（严格真值口径，阶段1 / AC-9）：不再回退纯 LLM。
    /// 仅透传引擎给出的真值结论；引擎未解题（gatewayAnswer 为空）则保持空（不计正确）。
    /// 原"字母数少于纯 LLM 即回退"逻辑在严格口径下移除——绝不以纯 LLM 答案充数。
    /// </summary>
    private static string ApplyChoiceNarrowingGuard(QaTestCase c, GatewayResult gw, string? gatewayAnswer, string pureAnswer)
    {
        return gatewayAnswer ?? string.Empty;
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

    /// <summary>构造测试用例：覆盖引擎能处理的离子共存、反应、检验等类型。public 以供护栏测试加载与生产完全一致的全量用例集（含手工题 + PDF 真题）。</summary>
    public static List<QaTestCase> BuildTestCases()
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
        // PDF 真题题目（来自2026一化儿100讲习题册）
        cases.AddRange(GenPdfQuestions());
        // Tier 2-A / AC-16：人工命制的物质分类选择题（事实库查表，ClassificationValidator 确定性求解）
        cases.AddRange(GenTier2Questions());
        return cases;
    }

    // ==================== PDF 真题题目（来自2026一化儿100讲，qwen3-vl-plus 提取） ====================
    // 题目从 data/pdf_questions.json 运行时加载。public 以供护栏测试加载与生产完全一致的用例集。
    public static List<QaTestCase> GenPdfQuestions()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "pdf_questions.json");
        if (!File.Exists(path))
            return new List<QaTestCase>();
        try
        {
            var json = File.ReadAllText(path);
            var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var items = System.Text.Json.JsonSerializer.Deserialize<List<PdfQuestionItem>>(json, opts);
            if (items == null) return new List<QaTestCase>();
            return items.Select(x =>
            {
                var question = x.Question ?? string.Empty;
                // 选择题把选项渲染进题干，让纯 LLM 与网关都能看到选项
                if (x.Type == "choice" && x.Options is { Count: > 0 })
                {
                    var rendered = string.Join("\n", x.Options
                        .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                        .Select(kv => $"{kv.Key}. {kv.Value}"));
                    question = question + "\n" + rendered;
                }
                return new QaTestCase
                {
                    Id = x.Id ?? string.Empty,
                    Question = question,
                    // 非选择题：把整段答案按分隔符拆成多个关键词，避免“整句完全匹配”几乎永不命中
                    // 导致多空题被大面积误判为错。选择题保留原行为（走独立字母判分，避免 A/B 子串误命中）。
                    AnswerKeywords = SplitPdfAnswerKeywords(x.Answer, x.Type, x.AnswerKeywords),
                    Note = x.Note ?? string.Empty,
                    Options = x.Options ?? new Dictionary<string, string>(),
                    Answer = x.Answer ?? string.Empty,
                    Type = x.Type ?? string.Empty,
                    IsPdf = true,
                    Images = x.Images ?? new List<string>(),
                    ImageAssets = x.ImageAssets ?? new List<ImageAsset>(),
                };
            }).ToList();
        }
        catch
        {
            return new List<QaTestCase>();
        }
    }

    // ==================== Tier 2 新增题库（物质分类 / 转化链 / NA，可控格式保底） ====================
    // 题目从 data/tier2_questions.json 运行时加载。public 以供护栏测试加载与生产完全一致的用例集。
    public static List<QaTestCase> GenTier2Questions()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "tier2_questions.json");
        if (!File.Exists(path))
            return new List<QaTestCase>();
        try
        {
            var json = File.ReadAllText(path);
            var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var items = System.Text.Json.JsonSerializer.Deserialize<List<PdfQuestionItem>>(json, opts);
            if (items == null) return new List<QaTestCase>();
            return items.Select(x =>
            {
                var question = x.Question ?? string.Empty;
                if (x.Type == "choice" && x.Options is { Count: > 0 })
                {
                    var rendered = string.Join("\n", x.Options
                        .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                        .Select(kv => $"{kv.Key}. {kv.Value}"));
                    question = question + "\n" + rendered;
                }
                return new QaTestCase
                {
                    Id = x.Id ?? string.Empty,
                    Question = question,
                    AnswerKeywords = SplitPdfAnswerKeywords(x.Answer, x.Type, x.AnswerKeywords),
                    Note = x.Note ?? string.Empty,
                    Options = x.Options ?? new Dictionary<string, string>(),
                    Answer = x.Answer ?? string.Empty,
                    Type = x.Type ?? string.Empty,
                    IsPdf = false,
                    Images = x.Images ?? new List<string>(),
                    ImageAssets = x.ImageAssets ?? new List<ImageAsset>(),
                };
            }).ToList();
        }
        catch
        {
            return new List<QaTestCase>();
        }
    }

    /// <summary>
    /// 将 PDF 题目的整段答案拆分为多个判分关键词。仅对非选择题生效；
    /// 选择题保留原关键词（由独立字母逻辑判分，避免 A/B 作为子串误命中）。
    /// 拆分后的关键词是原答案的子串，因此 Any 命中只会变易、不会变难（单调不下降）。
    /// </summary>
    private static List<string> SplitPdfAnswerKeywords(string? answer, string? type, List<string>? existing)
    {
        // 已有多个有效关键词则沿用（数据已预处理）
        if (existing is { Count: > 1 }) return existing;
        if (string.Equals(type, "choice", StringComparison.OrdinalIgnoreCase))
            return existing ?? new List<string>();

        var raw = answer?.Trim();
        if (string.IsNullOrEmpty(raw)) return existing ?? new List<string>();

        // 去掉 (1)（2）等序号前缀，保留答案内容
        var stripped = Regex.Replace(raw, @"[（(]\s*\d+\s*[）)]\s*", " ");

        // 保护化学命名中的 locant 逗号（整数-逗号-整数，如 "2,4,6-三溴苯酚"、"1,2-二氯乙烷"），
        // 否则会被拆成纯数字关键词 "2"/"4"，任何含该数字的回答都会被判正（误命中，见 pdf-322）。
        // 负向回顾 (?<![.\d]) 排除小数与多位数场景：如 "0.2, 19" 是两个填空（0.2 与 19），
        // "10, 20" 同理，不应被合并；仅整数 locant 之间的逗号才保护。
        const char placeholder = '\u0001';
        var protectedStripped = Regex.Replace(stripped, @"(?<![.\d])(\d)\s*[,，]\s*(\d)", $"$1{placeholder}$2");

        // 多空题的答案之间常用“2 个及以上空白”或换行分隔（如 “18.4 mol/L  25 mL”、
        // “(1)+92 (2)427 …” 去序号后各空之间为双空格）。单空格必须保留，否则会把
        // “0.5 mol/L”、“4 : 3”、“压强 温度” 这类单个答案拆碎。因此：任意换行（含单个）
        // 与 2+ 连续空格/tab 视为分隔符；单个空格不拆。拆分结果均为原答案子串，
        // Any+Contains 命中只会变易不会变难（单调不下降，无回归风险）。
        const char wsPlaceholder = '\u0002';
        var wsNormalized = Regex.Replace(protectedStripped, @"\s*[\r\n]+\s*", $"{wsPlaceholder}");
        wsNormalized = Regex.Replace(wsNormalized, @"[ \t]{2,}", $"{wsPlaceholder}");

        var parts = wsNormalized.Split(
            new[] { ',', '，', ';', '；', '、', wsPlaceholder },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var kws = new List<string>();
        foreach (var p in parts)
        {
            // 还原被保护的 locant 逗号（统一为半角，与答案文本写法一致）
            var t = p.Trim().Trim('.', '。').Trim().Replace(placeholder, ',');
            if (!string.IsNullOrEmpty(t)) kws.Add(t);
        }
        return kws.Count > 0 ? kws : (existing ?? new List<string>());
    }

    private sealed class PdfQuestionItem
    {
        public string? Id { get; set; }
        public string? Question { get; set; }
        public List<string>? AnswerKeywords { get; set; }
        public string? Note { get; set; }
        public string? Type { get; set; }
        public Dictionary<string, string>? Options { get; set; }
        public string? Answer { get; set; }
        public List<string>? Images { get; set; }
        public List<ImageAsset>? ImageAssets { get; set; }
    }
}
