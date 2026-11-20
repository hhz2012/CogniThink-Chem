using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using CogniThink.TutorBrain.Application.Agent;
using CogniThink.TutorBrain.Application.Chemistry;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Microsoft.Extensions.Configuration;

// ==================== 参数解析 ====================
var parsed = ParseArgs(args);
string tag = parsed.TryGetValue("--tag", out var t) && !string.IsNullOrWhiteSpace(t) ? t : "current";
int batchSize = parsed.TryGetValue("--batch-size", out var bs) && int.TryParse(bs, out var bsv) && bsv > 0 ? bsv : 40;
int concurrency = parsed.TryGetValue("--concurrency", out var cc) && int.TryParse(cc, out var ccv) && ccv > 0 ? ccv : 10;

string repoRoot = FindRepoRoot();
string defaultOutDir = Path.Combine(repoRoot, "_evalscratch");
string outDir = parsed.TryGetValue("--out-dir", out var od) && !string.IsNullOrWhiteSpace(od) ? od : defaultOutDir;
if (!Path.IsPathRooted(outDir)) outDir = Path.GetFullPath(Path.Combine(repoRoot, outDir));
Directory.CreateDirectory(outDir);

string apiDir = Path.Combine(repoRoot, "src", "CogniThink.TutorBrain.Api");

Console.WriteLine($"[EvalRunner] tag={tag} batchSize={batchSize} concurrency={concurrency}");
Console.WriteLine($"[EvalRunner] repoRoot={repoRoot}");
Console.WriteLine($"[EvalRunner] outDir={outDir}");

// ==================== 配置加载（与生产 Program.cs 同源） ====================
string env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
var config = new ConfigurationBuilder()
    .SetBasePath(apiDir)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: false)
    .Build();
var llmOptions = config.GetSection("Llm").Get<LlmOptions>() ?? new LlmOptions();

// ==================== 服务构造（复刻 Program.cs 的 DI 图，仅评估所需子集） ====================
var kb = ChemicalKnowledgeBase.Load(apiDir);
if (kb.LoadErrors.Count > 0)
{
    Console.WriteLine("[EvalRunner] WARNING: 知识库加载错误:");
    foreach (var e in kb.LoadErrors) Console.WriteLine($"  - {e}");
}
var chem = new ChemicalInferenceService(kb);
var compiler = new ProblemQueryCompiler(kb);
using var http = new HttpClient();
var llm = new LlmProxyService(http, llmOptions);
var gateway = new LlmGatewayService(llm, chem, compiler);
var eval = new GatewayVsLlmEvaluationService(llm, gateway);

Console.WriteLine($"[EvalRunner] LlmAvailable={llm.IsAvailable} Model={llmOptions.Model} BaseUrl={llmOptions.BaseUrl}");

// ==================== 加载全量用例 ====================
var allCases = GatewayVsLlmEvaluationService.BuildTestCases();
Console.WriteLine($"[EvalRunner] total cases={allCases.Count}");

// ==================== checkpoint / 续跑 ====================
string checkpointPath = Path.Combine(outDir, $"checkpoint_{tag}.jsonl");
string doneMarkerPath = Path.Combine(outDir, $"checkpoint_{tag}.done");

var doneIds = LoadDoneIds(checkpointPath);
Console.WriteLine($"[EvalRunner] already done in checkpoint={doneIds.Count}");

var pending = allCases.Where(c => !doneIds.Contains(c.Id)).ToList();
Console.WriteLine($"[EvalRunner] pending this run={pending.Count}");

var jsonOpts = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Converters = { new JsonStringEnumConverter() },
    WriteIndented = false
};

if (pending.Count > 0)
{
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    int completedThisRun = 0;
    var failedThisRun = new ConcurrentQueue<string>();
    var appendLock = new object();
    var throttler = new SemaphoreSlim(concurrency, concurrency);

    var tasks = pending.Select(async c =>
    {
        await throttler.WaitAsync(cts.Token);
        try
        {
            QaCaseResult r;
            try
            {
                r = await eval.EvaluateCaseAsync(c, cts.Token);
            }
            catch (Exception ex)
            {
                // 单题失败：不写 checkpoint，下次续跑会重试该题，保证最终快照完整
                failedThisRun.Enqueue(c.Id);
                Console.WriteLine($"[EvalRunner] FAIL {c.Id}: {ex.GetType().Name}: {ex.Message}");
                return;
            }

            // 逐题立即落盘：进程在 90s 被杀时，已完成题的结果不会丢
            var line = JsonSerializer.Serialize(r, jsonOpts);
            lock (appendLock)
            {
                File.AppendAllText(checkpointPath, line + Environment.NewLine);
            }
            int n = Interlocked.Increment(ref completedThisRun);
            if (n % batchSize == 0 || n == pending.Count)
                Console.WriteLine($"[EvalRunner] progress {n}/{pending.Count} done (last id={c.Id})");
        }
        finally
        {
            throttler.Release();
        }
    });

    await Task.WhenAll(tasks);
    Console.WriteLine($"[EvalRunner] run finished: completed={completedThisRun} failed={failedThisRun.Count}");
    if (failedThisRun.Count > 0)
        Console.WriteLine($"[EvalRunner] failed ids: {string.Join(",", failedThisRun)}");
}

// ==================== 汇总：从 checkpoint 重载全部结果，生成快照 ====================
var allResults = LoadResults(checkpointPath);
Console.WriteLine($"[EvalRunner] results loaded from checkpoint={allResults.Count} (expected={allCases.Count})");

var summary = GatewayVsLlmEvaluationService.BuildSummary(allResults, llm.IsAvailable);

var snapshot = BuildSnapshot(summary);
string snapshotPath = Path.Combine(outDir, $"snapshot_{tag}.json");
File.WriteAllText(snapshotPath, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
{
    // 匿名对象属性名已是小写，原样输出（与 EvaluationController 投影一致）
    PropertyNamingPolicy = null,
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() }
}));

string summaryTxt = Path.Combine(outDir, $"checkpoint_{tag}_summary.txt");
File.WriteAllText(summaryTxt,
    $"tag={tag} total={summary.TotalCases} llmAvailable={summary.LlmAvailable}\n" +
    $"pureLlmCorrect={summary.PureLlmCorrect} ({Math.Round(summary.PureLlmAccuracy * 100, 2)}%)\n" +
    $"gatewayCorrect={summary.GatewayCorrect} gatewayTruthCorrect={summary.GatewayTruthCorrect}\n" +
    $"gatewayEngineUsed={summary.GatewayEngineUsed} engineSolved={summary.EngineSolved} coverage={Math.Round(summary.EngineCoverage, 4)} precision={Math.Round(summary.EnginePrecision, 4)}\n" +
    $"harmCount={summary.HarmCount}\n" +
    $"bothCorrect={summary.BothCorrect.Count} gatewayOnly={summary.GatewayOnlyCorrect.Count} pureLlmOnly={summary.PureLlmOnlyCorrect.Count} bothWrong={summary.BothWrong.Count}\n");

bool allDone = allResults.Count == allCases.Count;
if (allDone)
{
    File.WriteAllText(doneMarkerPath, $"done {DateTimeOffset.Now:O}\n");
    Console.WriteLine($"[EvalRunner] ALL DONE. marker={doneMarkerPath}");
}
else
{
    Console.WriteLine($"[EvalRunner] NOT done: {allCases.Count - allResults.Count} cases remaining (failed/missing). Will retry on next run.");
}

Console.WriteLine($"[EvalRunner] snapshot={snapshotPath}");

return allDone ? 0 : 1;

// ==================== 本地辅助函数 ====================
static Dictionary<string, string> ParseArgs(string[] argv)
{
    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (int i = 0; i < argv.Length; i++)
    {
        var a = argv[i];
        if (a.StartsWith("--", StringComparison.Ordinal) && i + 1 < argv.Length && !argv[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            dict[a] = argv[i + 1];
            i++;
        }
        else if (a.StartsWith("--", StringComparison.Ordinal))
        {
            dict[a] = "true";
        }
    }
    return dict;
}

static string FindRepoRoot()
{
    var dir = AppContext.BaseDirectory;
    while (!string.IsNullOrEmpty(dir))
    {
        if (File.Exists(Path.Combine(dir, "CogniThink.Chem.sln"))) return dir;
        dir = Path.GetDirectoryName(dir);
    }
    return Directory.GetCurrentDirectory();
}

/// <summary>从 checkpoint 读已完成的题 id 集合（用于续跑去重）。</summary>
static HashSet<string> LoadDoneIds(string path)
{
    var set = new HashSet<string>(StringComparer.Ordinal);
    if (!File.Exists(path)) return set;
    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    foreach (var line in File.ReadLines(path))
    {
        if (string.IsNullOrWhiteSpace(line)) continue;
        try
        {
            var r = JsonSerializer.Deserialize<QaCaseResult>(line, opts);
            if (r?.Case?.Id is { Length: > 0 } id) set.Add(id);
        }
        catch { /* 忽略损坏行，续跑时重试该题 */ }
    }
    return set;
}

/// <summary>从 checkpoint 重载全部结果（按 id 去重，后写覆盖）。</summary>
static List<QaCaseResult> LoadResults(string path)
{
    var byId = new Dictionary<string, QaCaseResult>(StringComparer.Ordinal);
    if (!File.Exists(path)) return new List<QaCaseResult>();
    var opts = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
    foreach (var line in File.ReadLines(path))
    {
        if (string.IsNullOrWhiteSpace(line)) continue;
        try
        {
            var r = JsonSerializer.Deserialize<QaCaseResult>(line, opts);
            if (r?.Case?.Id is { Length: > 0 } id) byId[id] = r;
        }
        catch { /* 忽略损坏行 */ }
    }
    return byId.Values.ToList();
}

/// <summary>复刻 EvaluationController.GatewayVsLlm 的匿名投影，保证快照与 after_stage6.json 同构。</summary>
static object BuildSnapshot(GatewayVsLlmSummary s) => new
{
    totalCases = s.TotalCases,
    pureLlmAccuracy = Math.Round(s.PureLlmAccuracy, 4),
    pureLlmCorrect = s.PureLlmCorrect,
    gatewayAccuracy = Math.Round(s.GatewayAccuracy, 4),
    gatewayCorrect = s.GatewayCorrect,
    gatewayTruthCorrect = s.GatewayTruthCorrect,
    gatewayEngineUsed = s.GatewayEngineUsed,
    engineSolved = s.EngineSolved,
    engineCoverage = Math.Round(s.EngineCoverage, 4),
    enginePrecision = Math.Round(s.EnginePrecision, 4),
    harmCount = s.HarmCount,
    assetBreakdown = new
    {
        graph = new { cases = s.GraphCases, structuredHits = s.GraphStructuredHits },
        curve = new { cases = s.CurveCases, structuredHits = s.CurveStructuredHits },
        molecule = new { cases = s.MoleculeCases, structuredHits = s.MoleculeStructuredHits }
    },
    llmAvailable = s.LlmAvailable,
    bothCorrect = s.BothCorrect.Count,
    gatewayOnlyCorrect = s.GatewayOnlyCorrect.Count,
    pureLlmOnlyCorrect = s.PureLlmOnlyCorrect.Count,
    bothWrong = s.BothWrong.Count,
    bothCorrectCases = s.BothCorrect.Select(MapQaCase),
    gatewayOnlyCorrectCases = s.GatewayOnlyCorrect.Select(MapQaCase),
    pureLlmOnlyCorrectCases = s.PureLlmOnlyCorrect.Select(MapQaCase),
    bothWrongCases = s.BothWrong.Select(MapQaCase)
};

static object MapQaCase(QaCaseResult r) => new
{
    id = r.Case.Id,
    question = r.Case.Question,
    expected = r.Case.ExpectedAffirmative.HasValue
        ? (r.Case.ExpectedAffirmative.Value ? "肯定/能" : "否定/不能")
        : "关键词: " + string.Join(",", r.Case.AnswerKeywords),
    note = r.Case.Note,
    pureLlmAnswer = r.PureLlmAnswer,
    pureLlmCorrect = r.PureLlmCorrect,
    taskType = r.TaskType,
    gatewayConclusion = r.GatewayConclusion,
    gatewayOk = r.GatewayOk,
    gatewayAnswer = r.GatewayAnswer,
    gatewayCorrect = r.GatewayCorrect,
    gatewayUsedEngine = r.GatewayUsedEngine,
    gatewayTruthAdopted = r.GatewayTruthAdopted
};
