$ErrorActionPreference = 'Stop'
$path = 'd:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\Application\Evaluation\GatewayVsLlmEvaluationService.cs'
$lines = [System.IO.File]::ReadAllLines($path)

$bodyStart = -1; $callIdx = -1
for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($bodyStart -lt 0 -and $lines[$i] -match 'var result = new QaCaseResult \{ Case = c \};') { $bodyStart = $i }
    if ($callIdx -lt 0 -and $lines[$i] -match 'results\[i\] = await EvaluateCaseAsync\(c, ct\);') { $callIdx = $i }
}
if ($bodyStart -lt 0 -or $callIdx -lt 0) { throw "markers not found bodyStart=$bodyStart callIdx=$callIdx" }

# 删除内联体（bodyStart .. callIdx-1），保留 try { ... call ... }
$combined = $lines[0..($bodyStart - 1)] + $lines[$callIdx..($lines.Length - 1)]

# 在 BuildSummary 的文档注释前插入新方法
$docIdx = -1; $buildIdx = -1
for ($i = 0; $i -lt $combined.Length; $i++) {
    if ($docIdx -lt 0 -and $combined[$i] -match '汇总评估结果') { $docIdx = $i }
    if ($buildIdx -lt 0 -and $combined[$i] -match 'public static GatewayVsLlmSummary BuildSummary') { $buildIdx = $i }
}
if ($docIdx -lt 0 -or $buildIdx -lt 0) { throw "build markers not found docIdx=$docIdx buildIdx=$buildIdx" }
$insertAt = $docIdx - 1  # 行: /// <summary>

$methodText = @'
    /// <summary>
    /// 评估单题：纯 LLM 作答 + 网关（LLM 解析→引擎）作答，并判分。
    /// 抽出为 public，供断点续跑的控制台 runner 逐题驱动、逐题落盘 checkpoint。
    /// 行为与原 EvaluateAsync 内联逻辑完全一致。
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

'@
$methodLines = $methodText -split "`r?`n"

$result = $combined[0..($insertAt - 1)] + $methodLines + $combined[$insertAt..($combined.Length - 1)]

$bytes = [System.IO.File]::ReadAllBytes($path)
$hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
$enc = New-Object System.Text.UTF8Encoding($hasBom)
[System.IO.File]::WriteAllLines($path, $result, $enc)
Write-Output "OK bodyStart=$bodyStart callIdx=$callIdx insertAt=$insertAt buildIdx=$buildIdx totalBefore=$($lines.Length) totalAfter=$($result.Length)"
