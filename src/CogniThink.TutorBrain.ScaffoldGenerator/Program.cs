// 动态脚手架批量生成器（Task 5）。
// 用法：
//   dotnet run --project src/CogniThink.TutorBrain.ScaffoldGenerator -- \
//     --checkpoint <path> --out <path> --module redox-reasoning --limit 50 --only-correct true
//
// 读取 eval checkpoint（JSONL of QaCaseResult，复用 EvalRunner 同一份反序列化契约），
// 对 gatewayCorrect=true 的题跑 ScaffoldBuilder 管线，发布通过校验门的产物到 --out（.ts）。
// 幂等：同输入 → 字节级一致产物（STJ 稳定序列化 + 稳定 seed + 按 id 排序）。
// 可审计：每题打印 published / unsuitable(reason) / conflict，无静默丢弃。

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using CogniThink.TutorBrain.Application.ScaffoldGenerator;

var parsed = ParseArgs(args);
string checkpoint = GetArg(parsed, "--checkpoint", DefaultCheckpointPath());
string outPath = GetArg(parsed, "--out", DefaultOutPath());
string moduleArg = GetArg(parsed, "--module", "redox-reasoning");
int limit = int.TryParse(GetArg(parsed, "--limit", ""), out var lv) && lv > 0 ? lv : int.MaxValue;
bool onlyCorrect = GetArg(parsed, "--only-correct", "true") != "false";

var requestedModule = ParseModule(moduleArg) ?? ScaffoldModuleKind.RedoxReasoning;

var results = LoadCheckpoint(checkpoint);
Console.WriteLine($"[ScaffoldGen] checkpoint={checkpoint} loaded={results.Count} onlyCorrect={onlyCorrect} module={moduleArg} limit={limit}");

int processed = 0, published = 0, unsuitable = 0, conflict = 0, skipped = 0;
var publishedScaffolds = new List<GeneratedScaffold>();
var builder = new ScaffoldBuilder();

foreach (var r in results)
{
    if (onlyCorrect && !r.GatewayCorrect)
    {
        Log(r.Case?.Id ?? "?", "skip", "gatewayCorrect=false");
        skipped++;
        continue;
    }
    if (processed >= limit) break;
    processed++;

    var qcase = MapCase(r);
    var artifacts = EngineArtifactsBuilder.Build(qcase);
    // 从 checkpoint 回填引擎结论（不重新调 gateway / LLM，零回归、可复现）
    artifacts.Response = new EngineResponse
    {
        FinalResult = r.GatewayConclusion ?? "",
        Ok = r.GatewayOk,
        IsStudentAnswerCorrect = r.GatewayCorrect,
        ResultProvenance = r.GatewayResultProvenance ?? "",
    };

    var br = await builder.BuildDetailedAsync(qcase, artifacts);

    if (br.Scaffold is not null && br.Status == "published")
    {
        if (br.Scaffold.Module != requestedModule)
        {
            Log(qcase.Id, "skip", $"module={br.Scaffold.Module} != requested {requestedModule}");
            skipped++;
            continue;
        }
        publishedScaffolds.Add(br.Scaffold);
        published++;
        var degraded = br.Scaffold.DegradedSteps.Count > 0
            ? $" degraded=[{string.Join(",", br.Scaffold.DegradedSteps.Select(d => d.Kind.ToString()))}]"
            : "";
        Log(qcase.Id, "published", degraded.TrimStart());
    }
    else
    {
        if (br.Status == "conflict") conflict++; else unsuitable++;
        Log(qcase.Id, br.Status, string.Join(";", br.Errors));
    }
}

// 幂等：按 id 排序后落盘
publishedScaffolds = publishedScaffolds.OrderBy(s => s.Id, StringComparer.Ordinal).ToList();
ScaffoldArtifactWriter.WriteTs(outPath, publishedScaffolds);

Console.WriteLine($"[ScaffoldGen] DONE processed={processed} published={published} unsuitable={unsuitable} conflict={conflict} skipped={skipped}");
Console.WriteLine($"[ScaffoldGen] out={outPath}");

// ===================== helpers =====================

static Dictionary<string, string> ParseArgs(string[] raw)
{
    var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (int i = 0; i < raw.Length; i++)
    {
        var k = raw[i];
        if (!k.StartsWith("--", StringComparison.Ordinal)) continue;
        string? v = (i + 1 < raw.Length && !raw[i + 1].StartsWith("--", StringComparison.Ordinal)) ? raw[++i] : "true";
        d[k] = v;
    }
    return d;
}

static string GetArg(Dictionary<string, string> a, string key, string def)
    => a.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : def;

static ScaffoldModuleKind? ParseModule(string s) => s.Trim().ToLowerInvariant() switch
{
    "redox-reasoning" => ScaffoldModuleKind.RedoxReasoning,
    "ionic-system" => ScaffoldModuleKind.IonicSystem,
    "aqueous-equilibrium" => ScaffoldModuleKind.AqueousEquilibrium,
    "sequential-reaction" => ScaffoldModuleKind.SequentialReaction,
    "electrochem-reasoning" => ScaffoldModuleKind.ElectrochemReasoning,
    _ => null,
};

// 复用 EvalRunner 同一份反序列化契约：case-insensitive + 字符串枚举，字段对齐上游 QaCaseResult
static List<QaCaseResult> LoadCheckpoint(string path)
{
    var byId = new Dictionary<string, QaCaseResult>(StringComparer.Ordinal);
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"[ScaffoldGen] WARN checkpoint not found: {path}");
        return new List<QaCaseResult>();
    }
    var opts = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
    var badLines = 0;
    var lineNo = 0;
    foreach (var line in File.ReadLines(path))
    {
        lineNo++;
        if (string.IsNullOrWhiteSpace(line)) continue;
        try
        {
            var r = JsonSerializer.Deserialize<QaCaseResult>(line, opts);
            if (r?.Case?.Id is { Length: > 0 } id) byId[id] = r;   // 同 id 取最后一条（与 EvalRunner 一致）
            else badLines++;   // 反序列化成功但缺 id：计入可审计计数，不中断整批
        }
        catch (Exception ex)
        {
            badLines++;
            Console.Error.WriteLine($"[ScaffoldGen] WARN 跳过坏行 {path}:{lineNo}: {ex.Message}");
        }
    }
    if (badLines > 0)
        Console.Error.WriteLine($"[ScaffoldGen] WARN 共跳过 {badLines} 条坏行/缺 id 行（不中断整批）");
    return byId.Values.OrderBy(x => x.Case.Id, StringComparer.Ordinal).ToList();
}

static QuestionCase MapCase(QaCaseResult r) => new()
{
    Id = r.Case.Id,
    Type = r.Case.Type ?? "choice",
    Question = r.Case.Question ?? "",
    Answer = r.Case.Answer ?? "",
    Options = r.Case.Options?.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Value ?? "").ToList() ?? new List<string>(),
    AnswerKeywords = r.Case.AnswerKeywords ?? new List<string>(),
    Note = r.Case.Note,
    Category = r.GatewayResultProvenance,
};

static void Log(string id, string status, string reason)
{
    var r = string.IsNullOrEmpty(reason) ? "" : $"  reason={reason}";
    Console.WriteLine($"[ScaffoldGen] {id,-20} {status,-12}{r}");
}

static string RepoRoot()
{
    var dir = AppContext.BaseDirectory;
    while (!string.IsNullOrEmpty(dir))
    {
        if (File.Exists(Path.Combine(dir, "CogniThink.Chem.sln"))) return dir;
        dir = Path.GetDirectoryName(dir);
    }
    return Directory.GetCurrentDirectory();
}

static string DefaultCheckpointPath() => Path.Combine(RepoRoot(), "_evalscratch", "checkpoint_final.jsonl");
static string DefaultOutPath() => Path.Combine(RepoRoot(), "miniapp", "src", "data", "scaffolds", "generated", "index.ts");
