using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using Docnet.Core;
using Docnet.Core.Models;

// ====================================================================
// PdfOcrRepair — 开发期 OCR 修复工具（不进生产运行时）
//
// 用法：
//   dotnet run --project src/CogniThink.TutorBrain.PdfOcrRepair -- pdf-381
//       渲染+OCR 指定题目所在页（含溢出页），输出 _evalscratch/ocr/pdf-381.txt
//       并在控制台打印“缺失选项建议”（仅建议，不写回 JSON，须人工核对）。
//   dotnet run -- --scan
//       全量渲染+OCR 所有页（带缓存），按题干匹配生成 _evalscratch/ocr/_page_manifest.json
//       （用于校验/补全 questions_parsed.json 的 src_page）。
//   dotnet run -- pdf-381 --pages 126,127 --span 1 --force
//
// 页面定位优先级：--pages 显式 > questions_parsed.json 的 src_page > 条目 images 字段 p\d+
//                > _page_manifest.json > （无则报错，提示用 --pages 或 --scan）。
// 渲染与 OCR 均带缓存（_evalscratch/renders、_evalscratch/ocr），重复运行瞬时完成。
// ====================================================================

var cli = ParseArgs(args);
bool scan = cli.Flags.Contains("--scan");
bool verify = cli.Flags.Contains("--verify");
bool recheck = cli.Flags.Contains("--recheck");
bool force = cli.Flags.Contains("--force");
bool ocrFlag = cli.Flags.Contains("--ocr");
bool fromReport = cli.Flags.Contains("--from-report");
bool qwenOcr = cli.Flags.Contains("--qwen-ocr");
bool answerPdfFlag = cli.Flags.Contains("--answer-pdf");
string id = cli.Positional.FirstOrDefault() ?? string.Empty;

string repoRoot = FindRepoRoot();
string apiDataDir = Path.Combine(repoRoot, "src", "CogniThink.TutorBrain.Api", "data");
string questionsJson = Path.Combine(apiDataDir, "pdf_questions.json");
string parsedJson = Path.Combine(repoRoot, "pdfs", "questions_parsed.json");
string defaultPdf = Path.Combine(repoRoot, "pdfs", "2026一化儿100讲（基础+中档）习题册.pdf");
string pdfPath = cli.Get("--pdf", defaultPdf);
string tesseract = cli.Get("--tesseract", @"C:\Program Files\Tesseract-OCR\tesseract.exe");
string ocrDir = Path.GetFullPath(Path.Combine(repoRoot, cli.Get("--out-dir", "_evalscratch/ocr")));
string renderDir = Path.GetFullPath(Path.Combine(repoRoot, cli.Get("--render-dir", "_evalscratch/renders")));
string manifestPath = Path.Combine(ocrDir, "_page_manifest.json");
int dpi = cli.GetInt("--dpi", 225);
int span = cli.GetInt("--span", 1);
var explicitPages = cli.GetList("--pages");

// 校验/重读新增路径（只读官方抽取产物 + scratch 报告目录，绝不写回题库）
string answerPdf = Path.Combine(repoRoot, "pdfs", "2026一化儿100讲（基础+中档）答案册.pdf");
string exerciseDir = Path.Combine(repoRoot, "pdfs", "_extract", "exercise");
string answerDir = Path.Combine(repoRoot, "pdfs", "_extract", "answer");
string verifyDir = Path.GetFullPath(Path.Combine(repoRoot, "_evalscratch", "verify"));

Directory.CreateDirectory(ocrDir);
Directory.CreateDirectory(renderDir);
Directory.CreateDirectory(verifyDir);

Console.WriteLine($"[PdfOcrRepair] repoRoot={repoRoot}");
Console.WriteLine($"[PdfOcrRepair] pdf={pdfPath}");
Console.WriteLine($"[PdfOcrRepair] tesseract={tesseract}");
Console.WriteLine($"[PdfOcrRepair] ocrDir={ocrDir} renderDir={renderDir} dpi={dpi} span={span} force={force}");

if (!File.Exists(pdfPath)) { Console.Error.WriteLine($"[PdfOcrRepair] PDF 不存在: {pdfPath}"); return 2; }
// verify 走 PDF 文本层不需要 tesseract；recheck 在确需 OCR 时再检查；qwen-ocr 走千问 API 不依赖 tesseract，故这三条路径不提前阻断
if (!File.Exists(tesseract) && !verify && !recheck && !qwenOcr) { Console.Error.WriteLine($"[PdfOcrRepair] tesseract 不存在: {tesseract}（可用 --tesseract 指定，或用 --qwen-ocr 走千问 API）"); return 2; }
if (!File.Exists(questionsJson)) { Console.Error.WriteLine($"[PdfOcrRepair] 题目数据不存在: {questionsJson}"); return 2; }

int pageCount = GetPageCount(pdfPath);
Console.WriteLine($"[PdfOcrRepair] pdf pages={pageCount}");

// 加载题目（用于：取题干/已有选项/定位缺失字母；src_page 来自 questions_parsed.json）
var entries = LoadQuestions(questionsJson);
var entryById = entries.ToDictionary(e => e.Id, StringComparer.Ordinal);
var pageById = LoadParsedPages(parsedJson);
var parsedMap = LoadParsedMap(parsedJson); // id -> (srcPage, ansPage, num)，校验/重读用
var manifest = File.Exists(manifestPath) ? LoadManifest(manifestPath) : new Dictionary<string, List<int>>();

// ---- 子命令分发：verify / recheck 为只读报告，绝不写回题库 ----
if (verify)
{
    var exercisePages = LoadExercisePages(exerciseDir);
    var answerPages = LoadAnswerPages(answerDir);
    RunVerify(entries, parsedMap, exercisePages, answerPages, verifyDir);
    return 0;
}

if (recheck)
{
    var exercisePages = LoadExercisePages(exerciseDir);
    var answerPages = LoadAnswerPages(answerDir);
    RunRecheck(cli.Positional, fromReport, ocrFlag, force, entryById, parsedMap, exercisePages, answerPages,
        manifest, pageById, explicitPages, span, pageCount, pdfPath, answerPdf, tesseract, dpi, renderDir, ocrDir, verifyDir);
    return 0;
}

if (scan)
{
    RunScan(pageCount, entries, pdfPath, tesseract, dpi, force, renderDir, ocrDir, manifestPath);
    return 0;
}

if (qwenOcr)
{
    RunQwenOcr(explicitPages, id, entryById, pageById, manifest, span, pageCount,
        answerPdfFlag ? answerPdf : pdfPath, dpi, force, renderDir, ocrDir);
    return 0;
}

if (string.IsNullOrEmpty(id))
{
    Console.Error.WriteLine("用法:");
    Console.Error.WriteLine("  dotnet run -- <pdf-id> [--pages 126,127] [--span 1] [--force]   渲染+OCR 单题并打印建议");
    Console.Error.WriteLine("  dotnet run -- --scan            全量渲染+OCR，生成 _page_manifest.json");
    Console.Error.WriteLine("  dotnet run -- --verify          读 PDF 文本层+官方抽取，全库校验产出嫌疑报告（只读）");
    Console.Error.WriteLine("  dotnet run -- --recheck <id...> [--from-report] [--ocr] [--force]   重读混乱题，生成库内 vs 原文对照");
    Console.Error.WriteLine("  dotnet run -- --qwen-ocr --pages 93,94 [--answer-pdf] [--force]   千问视觉 API OCR 页（需 $env:QWEN_API_KEY）");
    return 2;
}
if (!entryById.TryGetValue(id, out var entry))
{
    Console.Error.WriteLine($"[PdfOcrRepair] 未找到题目: {id}");
    return 2;
}

var pages = ResolvePages(id, entry, pageById, manifest, explicitPages, span, pageCount);
if (pages.Count == 0)
{
    Console.Error.WriteLine($"[PdfOcrRepair] 无法定位 {id} 的页码。请用 --pages 显式指定，或先 --scan 生成清单。");
    return 2;
}
Console.WriteLine($"[PdfOcrRepair] {id} -> pages=[{string.Join(",", pages)}]");

// 逐页渲染+OCR（带缓存），拼接成 <id>.txt
var sb = new System.Text.StringBuilder();
sb.AppendLine($"===== PdfOcrRepair id={id} pages=[{string.Join(",", pages)}] dpi={dpi} =====");
foreach (var p in pages)
{
    string png = Path.Combine(renderDir, $"page_{p:D3}.png");
    string txt = Path.Combine(ocrDir, $"page_{p:D3}.txt");
    EnsureRender(pdfPath, p, png, dpi, force);
    var ocr = EnsureOcr(png, txt, tesseract, force);
    sb.AppendLine($"----- page {p} (render: {Path.GetFileName(png)}) -----");
    sb.AppendLine(ocr);
}
sb.AppendLine("===== end =====");

string outPath = Path.Combine(ocrDir, $"{id}.txt");
File.WriteAllText(outPath, sb.ToString(), System.Text.Encoding.UTF8);
Console.WriteLine($"[PdfOcrRepair] 写出: {outPath}");

PrintSuggestions(entry, sb.ToString());
return 0;

// ====================================================================
// 本地函数
// ====================================================================

static List<int> ResolvePages(string id, QuestionEntry entry, Dictionary<string, int> pageById,
    Dictionary<string, List<int>> manifest, List<int>? explicitPages, int span, int pageCount)
{
    IEnumerable<int> seed;
    if (explicitPages is { Count: > 0 }) seed = explicitPages;
    else if (pageById.TryGetValue(id, out var sp)) seed = new[] { sp };
    else if (entry.Images is { Count: > 0 } imgs) seed = ExtractImagePages(imgs);
    else if (manifest.TryGetValue(id, out var ml)) seed = ml;
    else seed = Enumerable.Empty<int>();

    var set = new SortedSet<int>();
    foreach (var p in seed)
    {
        for (int s = 0; s <= span; s++) // 正向溢出：题干页 + 后续 span 页
        {
            int q = p + s;
            if (q >= 0 && q < pageCount) set.Add(q);
        }
    }
    return set.ToList();
}

static List<int> ExtractImagePages(List<string> images)
{
    var set = new SortedSet<int>();
    foreach (var im in images)
    {
        var m = Regex.Match(im, @"p(\d+)");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var n)) set.Add(n);
    }
    return set.ToList();
}

static int GetPageCount(string pdfPath)
{
    var docLib = DocLib.Instance;
    using var reader = docLib.GetDocReader(pdfPath, new PageDimensions(100, 100));
    return reader.GetPageCount();
}

static void EnsureRender(string pdfPath, int pageIndex, string outPng, int dpi, bool force)
{
    if (!force && File.Exists(outPng)) return;
    int w = (int)(8.27 * dpi);
    int h = (int)(11.69 * dpi);
    var docLib = DocLib.Instance;
    using var reader = docLib.GetDocReader(pdfPath, new PageDimensions(w, h));
    using var page = reader.GetPageReader(pageIndex);
    int rw = page.GetPageWidth();
    int rh = page.GetPageHeight();
    byte[] bytes = page.GetImage();
    WritePng(outPng, rw, rh, bytes);
    Console.WriteLine($"  [render] page {pageIndex} -> {Path.GetFileName(outPng)} ({rw}x{rh})");
}

static string EnsureOcr(string pngPath, string outTxt, string tesseract, bool force)
{
    if (!force && File.Exists(outTxt)) return File.ReadAllText(outTxt);
    string outBase = Path.Combine(Path.GetDirectoryName(outTxt)!, Path.GetFileNameWithoutExtension(outTxt));
    var psi = new ProcessStartInfo
    {
        FileName = tesseract,
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    psi.ArgumentList.Add(pngPath);
    psi.ArgumentList.Add(outBase);
    psi.ArgumentList.Add("-l");
    psi.ArgumentList.Add("chi_sim");
    using var p = Process.Start(psi) ?? throw new InvalidOperationException("tesseract 启动失败");
    if (!p.WaitForExit(180_000)) { try { p.Kill(); } catch { } throw new TimeoutException($"tesseract 超时: {pngPath}"); }
    if (p.ExitCode != 0)
        throw new InvalidOperationException($"tesseract exit={p.ExitCode}: {p.StandardError.ReadToEnd()}");
    if (!File.Exists(outTxt)) return string.Empty;
    var txt = File.ReadAllText(outTxt);
    Console.WriteLine($"  [ocr] {Path.GetFileName(outTxt)} ({txt.Length} chars)");
    return txt;
}

static void PrintSuggestions(QuestionEntry e, string fullText)
{
    string Norm(string s) => new string(s.Where(c => char.IsLetterOrDigit(c)).ToArray());
    var existing = e.Options?.Keys.ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);

    // 题干定位针：去掉前导 (2025·...) 来源标记后取前 12 个非空白字符
    var q = e.Question ?? string.Empty;
    var m = Regex.Match(q, @"[)）]\s*(.+)");
    var frag = m.Success ? m.Groups[1].Value : q;
    var needle = new string(frag.Where(c => char.IsLetterOrDigit(c)).Take(12).ToArray());

    Console.WriteLine();
    Console.WriteLine("===== 缺失选项建议（仅建议，须人工对照渲染图核对，绝不臆造） =====");
    Console.WriteLine($"[suggest] id={e.Id} answer={e.Answer} 已有选项=[{string.Join(",", existing.OrderBy(x => x))}]");
    Console.WriteLine($"[suggest] 题干定位针='{needle}'");

    var lines = fullText.Replace("\r\n", "\n").Split('\n');
    bool afterStem = false;
    var optionRe = new Regex(@"^\s*([A-D])[\.．、]\s*(.+)$");
    var found = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var ln in lines)
    {
        if (!afterStem)
        {
            if (Norm(ln).Contains(needle, StringComparison.Ordinal)) afterStem = true;
            continue;
        }
        // 遇到下一题题号（如 10.）即停，避免串入相邻题
        if (Regex.IsMatch(ln, @"^\s*\d{1,2}[\.．、(（]")) break;
        var om = optionRe.Match(ln);
        if (om.Success)
        {
            var letter = om.Groups[1].Value;
            var text = Regex.Replace(om.Groups[2].Value, @"\s+", "").Trim();
            found.TryAdd(letter, text);
        }
    }

    foreach (var letter in new[] { "A", "B", "C", "D" })
    {
        if (existing.Contains(letter))
            Console.WriteLine($"  {letter} [已存在] {e.Options![letter]}");
        else if (found.TryGetValue(letter, out var t))
            Console.WriteLine($"  {letter} [缺失→建议] {t}   ← 请对照渲染图核对形近字/符号");
        else
            Console.WriteLine($"  {letter} [缺失] OCR 未定位到，请直接查看上方页面 OCR 文本与渲染图");
    }
    Console.WriteLine("===== 建议结束 =====");
}

static void RunScan(int pageCount, List<QuestionEntry> entries, string pdfPath, string tesseract,
    int dpi, bool force, string renderDir, string ocrDir, string manifestPath)
{
    Console.WriteLine($"[scan] 渲染+OCR 全部 {pageCount} 页（带缓存）...");
    for (int i = 0; i < pageCount; i++)
    {
        string png = Path.Combine(renderDir, $"page_{i:D3}.png");
        string txt = Path.Combine(ocrDir, $"page_{i:D3}.txt");
        EnsureRender(pdfPath, i, png, dpi, force);
        EnsureOcr(png, txt, tesseract, force);
        if ((i + 1) % 10 == 0) Console.WriteLine($"  [scan] {i + 1}/{pageCount}");
    }

    string Norm(string s) => new string(s.Where(c => char.IsLetterOrDigit(c)).ToArray());
    string Needle(QuestionEntry e)
    {
        var m = Regex.Match(e.Question ?? "", @"[)）]\s*(.+)");
        var frag = m.Success ? m.Groups[1].Value : (e.Question ?? "");
        return new string(frag.Where(c => char.IsLetterOrDigit(c)).Take(12).ToArray());
    }

    Console.WriteLine("[scan] 按题干匹配生成 _page_manifest.json ...");
    var pageText = new string[pageCount];
    for (int i = 0; i < pageCount; i++)
    {
        string txt = Path.Combine(ocrDir, $"page_{i:D3}.txt");
        pageText[i] = File.Exists(txt) ? Norm(File.ReadAllText(txt)) : "";
    }
    var manifest = new Dictionary<string, int>();
    int matched = 0;
    foreach (var e in entries)
    {
        var needle = Needle(e);
        if (needle.Length == 0) continue;
        for (int i = 0; i < pageCount; i++)
        {
            if (pageText[i].Contains(needle, StringComparison.Ordinal)) { manifest[e.Id] = i; matched++; break; }
        }
    }
    File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"[scan] 完成：entries={entries.Count} matched={matched} -> {manifestPath}");
}

// ---------------- 千问视觉 OCR（不依赖 tesseract；基础设施，不含化学逻辑） ----------------

static void RunQwenOcr(List<int> explicitPages, string id,
    Dictionary<string, QuestionEntry> entryById, Dictionary<string, int> pageById,
    Dictionary<string, List<int>> manifest, int span, int pageCount,
    string pdfPath, int dpi, bool force, string renderDir, string ocrDir)
{
    string? apiKey = Environment.GetEnvironmentVariable("QWEN_API_KEY");
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        Console.Error.WriteLine("[qwen-ocr] 未设置环境变量 QWEN_API_KEY（千问视觉 API 密钥）。请先 $env:QWEN_API_KEY='...' 再运行。");
        return;
    }

    List<int> pages;
    if (explicitPages is { Count: > 0 }) pages = explicitPages;
    else if (!string.IsNullOrEmpty(id) && entryById.TryGetValue(id, out var entry))
        pages = ResolvePages(id, entry, pageById, manifest, null, span, pageCount);
    else
    {
        Console.Error.WriteLine("[qwen-ocr] 用法: --qwen-ocr --pages 93,94  或  --qwen-ocr pdf-261（从映射取页）；加 --answer-pdf 渲染答案册");
        return;
    }

    bool isAnswerPdf = pdfPath.Contains("答案册", StringComparison.Ordinal);
    string suffix = isAnswerPdf ? "_answer" : "";
    string pdfLabel = Path.GetFileName(pdfPath);
    const string prompt = "请完整输出这张页面上的所有文字内容，原样保留题号、选项字母、上下标、化学符号、反应式与解析；不要总结、不要补全页面上没有的内容。";
    string failedPath = Path.Combine(ocrDir, "_failed.txt");

    Console.WriteLine($"[qwen-ocr] pdf={pdfLabel} pages=[{string.Join(",", pages)}] suffix='{suffix}'");
    foreach (var p in pages)
    {
        string png = Path.Combine(renderDir, $"page_{p:D3}{suffix}.png");
        string txt = Path.Combine(ocrDir, $"page_{p:D3}{suffix}.txt");
        if (!force && File.Exists(txt) && new FileInfo(txt).Length > 50)
        {
            Console.WriteLine($"  [qwen-ocr] cache hit page {p} 跳过");
            continue;
        }
        try
        {
            EnsureRender(pdfPath, p, png, dpi, force);
            if (!File.Exists(png)) throw new InvalidOperationException("渲染未产出 PNG");
            byte[] pngBytes = File.ReadAllBytes(png);
            string text = CallQwenVision(pngBytes, prompt, apiKey);
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("千问返回空内容");
            File.WriteAllText(txt, text, System.Text.Encoding.UTF8);
            Console.WriteLine($"  [qwen-ocr] page {p} -> {text.Length} chars");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  [qwen-ocr] page {p} 失败: {ex.Message}");
            AppendFailed(failedPath, $"{p}\t{pdfLabel}\t{ex.Message}");
        }
    }
}

static string CallQwenVision(byte[] png, string prompt, string apiKey)
{
    const string url = "https://maas.qianwenaiapi.com/compatible-mode/v1/chat/completions";
    string b64 = Convert.ToBase64String(png);
    var content = new List<object>
    {
        new Dictionary<string, object> { ["type"] = "text", ["text"] = prompt },
        new Dictionary<string, object> { ["type"] = "image_url", ["image_url"] = new Dictionary<string, object> { ["url"] = $"data:image/png;base64,{b64}" } }
    };
    var payload = new Dictionary<string, object>
    {
        ["model"] = "qwen3-vl-plus",
        ["messages"] = new List<object> { new Dictionary<string, object> { ["role"] = "user", ["content"] = content } },
        ["max_tokens"] = 3000,
        ["temperature"] = 0
    };

    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(180) };
    for (int attempt = 1; attempt <= 3; attempt++)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("Authorization", $"Bearer {apiKey}");
            req.Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
            using var resp = http.SendAsync(req).GetAwaiter().GetResult();
            string body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"  [qwen] attempt {attempt} HTTP {(int)resp.StatusCode}: {Trunc(body, 300)}");
                if (attempt < 3) { System.Threading.Thread.Sleep(2000 * attempt); continue; }
                throw new InvalidOperationException($"千问 API 失败 HTTP {(int)resp.StatusCode}");
            }
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        }
        catch (Exception ex) when (attempt < 3)
        {
            Console.Error.WriteLine($"  [qwen] attempt {attempt} 异常: {ex.Message}");
            System.Threading.Thread.Sleep(2000 * attempt);
        }
    }
    throw new InvalidOperationException("千问 API 重试耗尽");
}

static void AppendFailed(string path, string line)
{
    File.AppendAllText(path, $"{DateTime.Now:o}\t{line}{Environment.NewLine}", System.Text.Encoding.UTF8);
}

// ---------------- 校验 / 重读（只读报告，绝不写回题库） ----------------

static void RunVerify(List<QuestionEntry> entries, Dictionary<string, (int src, int ans, int num)> parsedMap,
    Dictionary<int, List<ExerciseItem>> exercisePages, Dictionary<int, List<AnswerItem>> answerPages, string verifyDir)
{
    Console.WriteLine($"[verify] entries={entries.Count} exercisePages={exercisePages.Count} answerPages={answerPages.Count}");
    var findings = new List<DefectFinding>();
    foreach (var e in entries)
        findings.AddRange(CheckEntry(e, parsedMap, exercisePages, answerPages));

    var byClass = findings.GroupBy(f => f.Class).OrderByDescending(g => g.Count()).ToList();
    Console.WriteLine();
    Console.WriteLine("===== verify 汇总（只读报告，绝不写回 pdf_questions.json）=====");
    Console.WriteLine($"[verify] 总条目={entries.Count}  findings={findings.Count}");
    foreach (var g in byClass)
        Console.WriteLine($"  {g.Key}: {g.Count()}");
    var needReview = findings.Where(f => f.Severity != "info").ToList();
    var suspectIds = needReview.Select(f => f.Id).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
    Console.WriteLine($"[verify] 需人工复核(error+suspect)={needReview.Count} 条，涉及 {suspectIds.Count} 个 id");
    Console.WriteLine($"[verify] 嫌疑 id 清单: {string.Join(",", suspectIds)}");

    var report = new
    {
        generatedAt = DateTime.Now.ToString("o"),
        entryCount = entries.Count,
        findingCount = findings.Count,
        suspectIdCount = suspectIds.Count,
        suspectIds,
        findings
    };
    string outPath = Path.Combine(verifyDir, "verify_report.json");
    File.WriteAllText(outPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), System.Text.Encoding.UTF8);
    Console.WriteLine($"[verify] 报告写出: {outPath}");

    // 简洁 TSV 便于快速扫视（error/suspect 优先）
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("id\tseverity\tclass\treason\tevidence");
    foreach (var f in findings.OrderBy(f => f.Severity == "error" ? 0 : f.Severity == "suspect" ? 1 : 2).ThenBy(f => f.Id))
    {
        string ev = (f.Evidence ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
        string rs = f.Reason.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
        sb.AppendLine($"{f.Id}\t{f.Severity}\t{f.Class}\t{rs}\t{ev}");
    }
    File.WriteAllText(Path.Combine(verifyDir, "verify_findings.tsv"), sb.ToString(), System.Text.Encoding.UTF8);

    // 合并复核对照报告（按缺陷类聚合 + 登记册交叉引用 + 逐题对照链接），供人工高效分诊
    WriteReviewMarkdown(entries, findings, suspectIds, verifyDir);
    Console.WriteLine($"[verify] 复核对照: {Path.Combine(verifyDir, "verify_review.md")}");
}

static void WriteReviewMarkdown(List<QuestionEntry> entries, List<DefectFinding> findings,
    List<string> suspectIds, string verifyDir)
{
    var entryById = entries.ToDictionary(e => e.Id, StringComparer.Ordinal);
    string registerPath = Path.GetFullPath(Path.Combine(verifyDir, "..", "..", "data-defect-register.md"));
    string registerText = File.Exists(registerPath) ? File.ReadAllText(registerPath) : "";
    bool IsRegistered(string id) => registerText.Contains(id, StringComparison.Ordinal);

    string Md(string? s) => (s ?? "").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ").Trim();

    // 每 id 的主类：取最高严重度，同级按 V1..V6 顺序
    static int SevRank(string s) => s == "error" ? 0 : s == "suspect" ? 1 : 2;
    var idFindings = findings.GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.ToList());
    string PrimaryClass(string id)
    {
        if (!idFindings.TryGetValue(id, out var fs)) return "V6";
        return fs.Where(f => f.Severity != "info")
                 .OrderBy(f => SevRank(f.Severity)).ThenBy(f => f.Class)
                 .Select(f => f.Class).FirstOrDefault() ?? "V6";
    }

    var byClass = suspectIds.GroupBy(PrimaryClass).OrderBy(g => g.Key).ToList();
    int registeredCount = suspectIds.Count(IsRegistered);
    int needReviewCount = findings.Count(f => f.Severity != "info");
    // info-only：仅含 info 级 finding、未进入嫌疑集的 id（V6 映射失败为主，无法做内容校验）
    var infoOnlyById = findings.GroupBy(f => f.Id)
        .Where(g => g.All(f => f.Severity == "info") && !suspectIds.Contains(g.Key))
        .ToDictionary(g => g.Key, g => g.ToList());
    int cleanCount = entries.Count - findings.Select(f => f.Id).Distinct().Count();

    var sb = new System.Text.StringBuilder();
    sb.AppendLine("# verify 复核对照报告");
    sb.AppendLine();
    sb.AppendLine($"> 生成时间：{DateTime.Now:yyyy-MM-dd HH:mm}  ｜  来源：`src/CogniThink.TutorBrain.PdfOcrRepair --verify`（**只读，未写回 `pdf_questions.json`**）");
    sb.AppendLine($"> 逐题详细对照（新鲜 PDF 文本层 / OCR / 官方抽取 / findings）见同目录 `{{id}}_recheck.txt`，可用 `--recheck <id>...` 或 `--recheck --from-report` 生成。");
    sb.AppendLine();
    sb.AppendLine("## 1. 总览");
    sb.AppendLine();
    sb.AppendLine($"- 总条目：{entries.Count}  ｜  findings：{findings.Count}  ｜  需复核(error+suspect)：{needReviewCount}  ｜  嫌疑 id：{suspectIds.Count}  ｜  info-only（未达嫌疑）：{infoOnlyById.Count}  ｜  干净：{cleanCount}");
    sb.AppendLine($"- 已在 `data-defect-register.md` 登记：**{registeredCount}**  ｜  本工具新发现（未登记）：**{suspectIds.Count - registeredCount}**");
    sb.AppendLine();
    sb.AppendLine("## 2. 按缺陷类计数");
    sb.AppendLine();
    sb.AppendLine("| 类 | 含义 | findings 数 | 涉及 id 数 |");
    sb.AppendLine("|----|------|------------|-----------|");
    var classMeaning = new Dictionary<string, string>
    {
        ["V1"] = "答案键损坏（垃圾/空/全选污染/answer≠kw0）",
        ["V2"] = "选项缺失/错配（vs 官方 exercise 页）",
        ["V3"] = "题干截断/拼接（vs 官方 exercise 页）",
        ["V4"] = "答案册 explanation 交叉矛盾",
        ["V5"] = "note 自相矛盾",
        ["V6"] = "映射/字段缺失（待人工）",
    };
    foreach (var g in findings.GroupBy(f => f.Class).OrderBy(g => g.Key))
    {
        int idCount = g.Select(f => f.Id).Distinct().Count();
        sb.AppendLine($"| {g.Key} | {classMeaning.GetValueOrDefault(g.Key, "")} | {g.Count()} | {idCount} |");
    }
    sb.AppendLine();

    // 未登记的新发现单独列出（最值得关注）
    var newlyFound = suspectIds.Where(id => !IsRegistered(id)).ToList();
    sb.AppendLine("## 3. 本工具新发现（未在登记册登记，建议优先核对）");
    sb.AppendLine();
    if (newlyFound.Count == 0) sb.AppendLine("（全部嫌疑 id 均已在登记册登记）");
    else
    {
        sb.AppendLine($"共 **{newlyFound.Count}** 个：{string.Join(", ", newlyFound)}");
        sb.AppendLine();
    }

    // 逐类逐题表
    sb.AppendLine("## 4. 嫌疑题逐题对照（按主类分组）");
    sb.AppendLine();
    sb.AppendLine("列说明：`登记`=✓已在登记册 / ✗未登记；`kw0`=answerKeywords[0]；证据为截取片段，全文见 `<id>_recheck.txt`。");
    sb.AppendLine();
    foreach (var cg in byClass)
    {
        string cls = cg.Key;
        sb.AppendLine($"### {cls} — {classMeaning.GetValueOrDefault(cls, "")}（{cg.Count()} 题）");
        sb.AppendLine();
        sb.AppendLine("| id | 登记 | type | askDir | answer | kw0 | note(前40) | 主 finding | 证据(前60) | 对照 |");
        sb.AppendLine("|----|------|------|--------|--------|-----|------------|------------|------------|------|");
        foreach (var id in cg.OrderBy(x => x, StringComparer.Ordinal))
        {
            entryById.TryGetValue(id, out var e);
            string type = e?.Type ?? "";
            string askDir = e != null ? InferAskDir(e.Question ?? "") : "";
            string answer = e?.Answer ?? "";
            string kw0 = (e?.AnswerKeywords?.FirstOrDefault()) ?? "";
            string note = Trunc(e?.Note, 40);
            var fs = idFindings.GetValueOrDefault(id) ?? new List<DefectFinding>();
            var primary = fs.Where(f => f.Severity != "info").OrderBy(f => SevRank(f.Severity)).ThenBy(f => f.Class).FirstOrDefault()
                          ?? fs.OrderBy(f => SevRank(f.Severity)).First();
            string reason = Md(primary.Reason);
            string evidence = Md(Trunc(primary.Evidence, 60));
            string reg = IsRegistered(id) ? "✓" : "✗";
            sb.AppendLine($"| {id} | {reg} | {Md(type)} | {askDir} | {Md(answer)} | {Md(kw0)} | {Md(note)} | {primary.Severity}/{primary.Class} {reason} | {evidence} | [对照]({id}_recheck.txt) |");
        }
        sb.AppendLine();
    }

    // info 级条目（未达嫌疑），按类分组
    if (infoOnlyById.Count > 0)
    {
        sb.AppendLine("## 5. info 级条目（未达嫌疑，仅供参考）");
        sb.AppendLine();
        sb.AppendLine("以下 id 仅有 info 级 finding，未进入 98 嫌疑集；V6 多为映射失败导致无法做内容校验，V2/V3/V4 为弱信号（文本微差 / 官方册 answer 字段污染）。");
        sb.AppendLine();
        var infoByClass = infoOnlyById.GroupBy(kv => kv.Value.OrderBy(f => f.Class, StringComparer.Ordinal).First().Class)
                                     .OrderBy(g => g.Key).ToList();
        foreach (var cg in infoByClass)
        {
            string cls = cg.Key;
            sb.AppendLine($"### {cls} — {classMeaning.GetValueOrDefault(cls, "")}（{cg.Count()} 题）");
            sb.AppendLine();
            sb.AppendLine("| id | 登记 | type | info 原因(前60) |");
            sb.AppendLine("|----|------|------|-----------------|");
            foreach (var id in cg.Select(kv => kv.Key).OrderBy(x => x, StringComparer.Ordinal))
            {
                entryById.TryGetValue(id, out var e);
                var fs = infoOnlyById[id];
                string reason = Md(Trunc(fs[0].Reason, 60));
                string reg = IsRegistered(id) ? "✓" : "✗";
                sb.AppendLine($"| {id} | {reg} | {Md(e?.Type ?? "")} | {reason} |");
            }
            sb.AppendLine();
        }
    }

    sb.AppendLine("## 6. 使用说明");
    sb.AppendLine();
    sb.AppendLine("- 本报告**只读**，不修改任何数据。所有结论须人工按 `<id>_recheck.txt` 与 PDF 原页逐字核对。");
    sb.AppendLine("- `V4` 仅标潜在矛盾（答案册 explanation 可能被 OCR 污染），不作断言；`answer` 字段永不作真值，真值以 `answerKeywords[0]` + 官方解析 + 化学真值三者收敛为准。");
    sb.AppendLine("- 确认需修 answer 键时，必须有官方源（答案册/书原页）留痕，并在 `data-defect-register.md` 登记，遵守 precision=1 / harm=0 / LOST=0 红线。");

    File.WriteAllText(Path.Combine(verifyDir, "verify_review.md"), sb.ToString(), System.Text.Encoding.UTF8);
}

static List<DefectFinding> CheckEntry(QuestionEntry e, Dictionary<string, (int src, int ans, int num)> parsedMap,
    Dictionary<int, List<ExerciseItem>> exercisePages, Dictionary<int, List<AnswerItem>> answerPages)
{
    var res = new List<DefectFinding>();
    string id = e.Id;
    string answer = (e.Answer ?? "").Trim();
    var kw = e.AnswerKeywords ?? new List<string>();
    string note = e.Note ?? "";
    string q = e.Question ?? "";
    bool isChoice = string.Equals(e.Type, "choice", StringComparison.OrdinalIgnoreCase);

    bool hasMap = parsedMap.TryGetValue(id, out var map);
    ExerciseItem? ex = null;
    AnswerItem? ans = null;
    if (hasMap)
    {
        if (map.src >= 0 && exercisePages.TryGetValue(map.src, out var exList))
            ex = exList.FirstOrDefault(x => x.Num == map.num);
        if (map.ans >= 0 && answerPages.TryGetValue(map.ans, out var ansList))
            ans = ansList.FirstOrDefault(x => x.Num == map.num);
    }

    // V6 映射 / 字段缺失
    if (!hasMap) res.Add(F(id, "info", "V6", "id 不在 questions_parsed.json 映射中，无法定位 src_page/ans_page", ""));
    if (hasMap && ex == null) res.Add(F(id, "info", "V6", $"官方 exercise 页 src_page={map.src} 未找到 num={map.num}（题面缺失或切片错位）", ""));
    if (hasMap && ans == null) res.Add(F(id, "info", "V6", $"官方 answer 册页 ans_page={map.ans} 未找到 num={map.num}", ""));
    if (string.IsNullOrWhiteSpace(q)) res.Add(F(id, "error", "V6", "question 字段为空", ""));
    if (isChoice && (e.Options == null || e.Options.Count == 0)) res.Add(F(id, "error", "V6", "options 字段为空（choice 应有 A-D 选项）", ""));

    // V1 答案键损坏 —— 字母格式类仅对 type==choice 生效：
    // 非 choice（fill/calc）的 answer 本就是化学方程式/数值，含 B/C/F 等字母属正常，不能误报。
    if (answer.Length == 0)
        res.Add(F(id, "error", "V1", "answer 为空", ""));
    else if (isChoice && !Regex.IsMatch(answer, @"^[A-G]+(,[A-G]+)*$"))
        res.Add(F(id, "error", "V1", $"answer 格式异常: {Trunc(answer)}", ""));
    if (isChoice)
    {
        var distinct = DistinctLetters(answer);
        bool cleanMulti = Regex.IsMatch(answer, @"^[A-G]{2,}$"); // 连写多选如 ACD，视为合法，不标污染
        if (distinct.Count >= 3 && !cleanMulti)
            res.Add(F(id, "suspect", "V1", $"answer 含 ≥3 个不同字母({Trunc(answer)})，疑似全选污染（连写多选已排除，少数仍为合法多选须人工核对）", ""));
    }
    if (kw.Count == 0)
        res.Add(F(id, "error", "V1", "answerKeywords 为空", ""));
    string kw0 = kw.FirstOrDefault()?.Trim() ?? "";
    if (isChoice && Regex.IsMatch(answer, @"^[A-G]$") && Regex.IsMatch(kw0, @"^[A-G]$") && answer != kw0)
        res.Add(F(id, "suspect", "V1", $"answer={answer} 与 answerKeywords[0]={kw0} 不一致", ""));

    // V2 选项 vs 官方 exercise
    if (ex != null && ex.Options != null && e.Options != null)
    {
        foreach (var letter in new[] { "A", "B", "C", "D" })
        {
            bool libHas = e.Options.ContainsKey(letter);
            bool offHas = ex.Options.ContainsKey(letter);
            if (libHas && !offHas)
                res.Add(F(id, "suspect", "V2", $"库含选项 {letter} 但官方页无此选项（可能错位）", $"lib={Trunc(e.Options[letter])}"));
            else if (!libHas && offHas)
                res.Add(F(id, "suspect", "V2", $"库缺选项 {letter}（官方页存在）", $"official={Trunc(ex.Options[letter])}"));
            else if (libHas && offHas)
            {
                var libN = NormCmp(e.Options[letter]);
                var offN = NormCmp(ex.Options[letter]);
                if (libN != offN && libN.Length > 0 && offN.Length > 0)
                    res.Add(F(id, "info", "V2", $"选项 {letter} 文本与官方不一致", $"lib={Trunc(e.Options[letter])} | official={Trunc(ex.Options[letter])}"));
            }
        }
    }

    // V3 题干截断 / 拼接
    if (ex != null && !string.IsNullOrEmpty(ex.Question) && !string.IsNullOrEmpty(q))
    {
        var libN = NormCmp(q);
        var offN = NormCmp(ex.Question);
        if (libN != offN)
        {
            bool concat = Regex.Matches(q, @"下列").Count >= 2 || q.Length > ex.Question!.Length * 1.6;
            res.Add(F(id, concat ? "suspect" : "info", "V3",
                concat ? "题干疑似拼接/截断（与官方页差异大）" : "题干文本与官方页不一致",
                $"lib={Trunc(q)} | official={Trunc(ex.Question)}"));
        }
    }

    // V4 官方答案册交叉核对（仅标潜在矛盾，不断言；answer 字段永不作真值）
    if (ans != null && !string.IsNullOrEmpty(ans.Explanation))
    {
        string expl = ans.Explanation;
        if (isChoice)
        {
            var ansDistinct = DistinctLetters(ans.Answer ?? "");
            if (ansDistinct.Count >= 3)
                res.Add(F(id, "info", "V4", "答案册 answer 字段疑似多字母污染（已忽略，仅用 explanation）", $"answer册answer={Trunc(ans.Answer)}"));
        }
        foreach (var letter in new[] { "A", "B", "C", "D", "E", "F" })
        {
            if (Regex.IsMatch(expl, $@"(故选|答案为|应选){letter}") && Regex.IsMatch(answer, @"^[A-G]$") && answer != letter)
            {
                res.Add(F(id, "suspect", "V4", $"答案册解析指向「{letter}」，但库 answer={answer}", $"official={Trunc(expl)}"));
                break;
            }
        }
        string askDir = InferAskDir(q);
        if (Regex.IsMatch(answer, @"^[A-G]$"))
        {
            if (askDir == "CORR" && Regex.IsMatch(expl, $@"{answer}(选项|项)?(错误|不正确)"))
                res.Add(F(id, "suspect", "V4", $"askDir=CORR 但答案册解析称 {answer} 错误", $"official={Trunc(expl)}"));
            if (askDir == "WRONG" && Regex.IsMatch(expl, $@"{answer}(选项|项)?(正确|成立|符合)"))
                res.Add(F(id, "suspect", "V4", $"askDir=WRONG 但答案册解析称 {answer} 正确", $"official={Trunc(expl)}"));
        }
    }

    // V5 note 自相矛盾（移植 scan_analyze.ps1 的 R5/R7/R8）
    if (!string.IsNullOrEmpty(note))
    {
        if (Regex.IsMatch(note, @"误标|答案应为|推断本题答案|可能答案|原题答案|标答有误"))
            res.Add(F(id, "suspect", "V5", "note 含自相矛盾关键词", $"note={Trunc(note)}"));
        if (note.Length > 0 && note.Length < 12 && answer.Length > 0)
            res.Add(F(id, "suspect", "V5", $"note 过短({note.Length})疑似截断", $"note={Trunc(note)}"));
        if (Regex.IsMatch(answer, @"^[A-G]$"))
        {
            foreach (var letter in new[] { "A", "B", "C", "D", "E", "F" })
            {
                if (letter != answer && Regex.IsMatch(note, $@"(故选|答案为|应选){letter}|{letter}选项?正确.*故选{letter}"))
                {
                    res.Add(F(id, "suspect", "V5", $"note 指向其他字母 {letter}（库 answer={answer}）", $"note={Trunc(note)}"));
                    break;
                }
            }
        }
    }

    return res;
}

static void RunRecheck(List<string> positionals, bool fromReport, bool ocr, bool force,
    Dictionary<string, QuestionEntry> entryById, Dictionary<string, (int src, int ans, int num)> parsedMap,
    Dictionary<int, List<ExerciseItem>> exercisePages, Dictionary<int, List<AnswerItem>> answerPages,
    Dictionary<string, List<int>> manifest, Dictionary<string, int> pageById, List<int> explicitPages, int span, int pageCount,
    string pdfPath, string answerPdf, string tesseract, int dpi, string renderDir, string ocrDir, string verifyDir)
{
    var ids = new List<string>();
    if (fromReport)
    {
        string rep = Path.Combine(verifyDir, "verify_report.json");
        if (!File.Exists(rep)) { Console.Error.WriteLine($"[recheck] 未找到 {rep}，请先运行 --verify"); return; }
        using var doc = JsonDocument.Parse(File.ReadAllText(rep));
        if (!doc.RootElement.TryGetProperty("findings", out var arr)) { Console.Error.WriteLine("[recheck] report 无 findings"); return; }
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in arr.EnumerateArray())
        {
            string? sev = f.TryGetProperty("Severity", out var se) ? se.GetString() : null;
            string? fid = f.TryGetProperty("Id", out var ie) ? ie.GetString() : null;
            if (sev != "info" && !string.IsNullOrEmpty(fid)) set.Add(fid);
        }
        ids.AddRange(set.OrderBy(x => x, StringComparer.Ordinal));
        Console.WriteLine($"[recheck] 从报告载入嫌疑 id {ids.Count} 个");
    }
    else
    {
        ids = positionals.Where(p => entryById.ContainsKey(p)).Distinct(StringComparer.Ordinal).ToList();
    }
    if (ids.Count == 0) { Console.Error.WriteLine("[recheck] 未指定有效 id（用法: --recheck pdf-197 ...  或  --recheck --from-report）"); return; }

    foreach (var qid in ids)
    {
        if (!entryById.TryGetValue(qid, out var e)) { Console.Error.WriteLine($"[recheck] 跳过未知 id: {qid}"); continue; }
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"===== Recheck {qid}（只读对照，不写回题库）=====");

        sb.AppendLine("--- 库内 (pdf_questions.json) ---");
        sb.AppendLine($"question: {e.Question}");
        sb.AppendLine($"options: {JsonSerializer.Serialize(e.Options)}");
        sb.AppendLine($"answer: {e.Answer}   answerKeywords: {JsonSerializer.Serialize(e.AnswerKeywords)}");
        sb.AppendLine($"note: {e.Note}");

        bool hasMap = parsedMap.TryGetValue(qid, out var map);
        sb.AppendLine($"--- 页码映射 (questions_parsed.json) --- hasMap={hasMap} src={map.src} ans={map.ans} num={map.num}");

        // 新鲜读取 PDF 文本层（绕过缓存 _extract，实现「重新读取」）
        string? freshEx = null;
        if (hasMap && map.src >= 0)
        {
            freshEx = GetPageText(pdfPath, map.src);
            if (freshEx != null && freshEx.Trim().Length >= 40)
                sb.AppendLine($"--- 新鲜 PDF 文本层 (exercise 习题册 p{map.src}) ---\n{freshEx}");
            else
                sb.AppendLine($"--- 新鲜 PDF 文本层 (exercise p{map.src}): 过短/疑似扫描页（仅抽到：{Trunc(freshEx, 30)}），建议 OCR 复核 ---");
        }
        if (hasMap && map.ans >= 0)
        {
            var txt = GetPageText(answerPdf, map.ans);
            if (txt != null && txt.Trim().Length >= 40) sb.AppendLine($"--- 新鲜 PDF 文本层 (answer 答案册 p{map.ans}) ---\n{txt}");
        }

        // OCR：显式 --ocr；或文本层缺失/过短且非批量(--from-report)时自动兜底
        bool shortEx = freshEx == null || freshEx.Trim().Length < 40;
        bool needOcr = ocr || (!fromReport && hasMap && map.src >= 0 && shortEx);
        if (needOcr)
        {
            if (!File.Exists(tesseract)) { sb.AppendLine("[ocr] 跳过：tesseract 不存在（可用 --tesseract 指定）"); }
            else
            {
                var pages = ResolvePages(qid, e, pageById, manifest, explicitPages, span, pageCount);
                sb.AppendLine($"--- OCR (force={force}) pages=[{string.Join(",", pages)}] ---");
                foreach (var p in pages)
                {
                    string png = Path.Combine(renderDir, $"page_{p:D3}.png");
                    string otxt = Path.Combine(ocrDir, $"page_{p:D3}.txt");
                    EnsureRender(pdfPath, p, png, dpi, force);
                    var o = EnsureOcr(png, otxt, tesseract, force);
                    sb.AppendLine($"-- page {p} --\n{o}");
                }
            }
        }

        // 官方抽取产物对照
        if (hasMap && map.src >= 0 && exercisePages.TryGetValue(map.src, out var exList))
        {
            var ex = exList.FirstOrDefault(x => x.Num == map.num);
            if (ex != null)
            {
                sb.AppendLine("--- 官方 exercise 抽取 (_extract/exercise) ---");
                sb.AppendLine($"question: {ex.Question}");
                sb.AppendLine($"options: {JsonSerializer.Serialize(ex.Options)}");
            }
        }
        if (hasMap && map.ans >= 0 && answerPages.TryGetValue(map.ans, out var ansList))
        {
            var an = ansList.FirstOrDefault(x => x.Num == map.num);
            if (an != null)
            {
                sb.AppendLine("--- 官方 answer 册抽取 (_extract/answer，answer 字段可能污染已忽略) ---");
                sb.AppendLine($"answer(供参考): {an.Answer}");
                sb.AppendLine($"explanation: {an.Explanation}");
            }
        }

        // 重跑该校验，把矛盾点附在末尾
        var findings = CheckEntry(e, parsedMap, exercisePages, answerPages);
        sb.AppendLine("--- 校验 findings（重算）---");
        foreach (var f in findings)
            sb.AppendLine($"  [{f.Severity}/{f.Class}] {f.Reason}  {f.Evidence}");

        string outPath = Path.Combine(verifyDir, $"{qid}_recheck.txt");
        File.WriteAllText(outPath, sb.ToString(), System.Text.Encoding.UTF8);
        Console.WriteLine($"[recheck] {qid}: {findings.Count} findings -> {outPath}");
    }
}

// ---- 文本层抽取（Docnet GetText，真正「读取 PDF」） ----
static string? GetPageText(string pdfPath, int pageIndex)
{
    try
    {
        var docLib = DocLib.Instance;
        using var reader = docLib.GetDocReader(pdfPath, new PageDimensions(100, 100));
        using var page = reader.GetPageReader(pageIndex);
        var txt = page.GetText();
        return string.IsNullOrWhiteSpace(txt) ? null : txt;
    }
    catch (Exception ex) { Console.Error.WriteLine($"  [GetPageText] {Path.GetFileName(pdfPath)} p{pageIndex} 失败: {ex.Message}"); return null; }
}

// ---- 小工具 ----
static DefectFinding F(string id, string sev, string cls, string reason, string evidence) => new(id, sev, cls, reason, evidence);
static string Trunc(string? s, int n = 60) { if (string.IsNullOrEmpty(s)) return ""; return s.Length <= n ? s : s.Substring(0, n) + "…"; }
static List<string> DistinctLetters(string a) => string.IsNullOrEmpty(a) ? new() : Regex.Matches(a, @"[A-G]").Cast<Match>().Select(m => m.Value).Distinct().ToList();
static string NormCmp(string? s)
{
    if (string.IsNullOrEmpty(s)) return "";
    var t = Regex.Replace(s, @"\s+", "");
    t = t.Replace("（", "(").Replace("）", ")").Replace("，", ",").Replace("。", ".").Replace("：", ":").Replace("；", ";");
    return t.Trim();
}
static string InferAskDir(string q)
{
    if (string.IsNullOrEmpty(q)) return "CORR";
    if (Regex.IsMatch(q, @"错误的是|不正确的是|不能|不属于|不具有|不符|错误的一项|不正确的一项|不相符|不合理|不可以")) return "WRONG";
    return "CORR";
}

// ---------------- 数据加载 ----------------

static List<QuestionEntry> LoadQuestions(string path)
{
    var json = File.ReadAllText(path);
    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var list = JsonSerializer.Deserialize<List<QuestionEntry>>(json, opts) ?? new();
    return list;
}

static Dictionary<string, int> LoadParsedPages(string path)
{
    var dict = new Dictionary<string, int>(StringComparer.Ordinal);
    if (!File.Exists(path)) return dict;
    using var doc = JsonDocument.Parse(File.ReadAllText(path));
    foreach (var el in doc.RootElement.EnumerateArray())
    {
        if (el.TryGetProperty("id", out var idEl) && el.TryGetProperty("src_page", out var pgEl)
            && pgEl.ValueKind == JsonValueKind.Number)
        {
            dict[idEl.GetString()!] = pgEl.GetInt32();
        }
    }
    return dict;
}

static Dictionary<string, List<int>> LoadManifest(string path)
{
    var dict = new Dictionary<string, List<int>>(StringComparer.Ordinal);
    if (!File.Exists(path)) return dict;
    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path), opts) ?? new();
    foreach (var kv in raw)
    {
        var list = new List<int>();
        if (kv.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var x in kv.Value.EnumerateArray())
                if (x.TryGetInt32(out var item)) list.Add(item);
        }
        else if (kv.Value.TryGetInt32(out var single))
        {
            list.Add(single);
        }
        if (list.Count > 0) dict[kv.Key] = list;
    }
    return dict;
}

// id -> (src_page, ans_page, num)，校验/重读用；缺失字段记 -1
static Dictionary<string, (int src, int ans, int num)> LoadParsedMap(string path)
{
    var dict = new Dictionary<string, (int, int, int)>(StringComparer.Ordinal);
    if (!File.Exists(path)) return dict;
    using var doc = JsonDocument.Parse(File.ReadAllText(path));
    foreach (var el in doc.RootElement.EnumerateArray())
    {
        if (!el.TryGetProperty("id", out var idEl)) continue;
        string id = idEl.GetString() ?? "";
        if (id.Length == 0) continue;
        int src = el.TryGetProperty("src_page", out var s) ? GetJsonInt(s) : -1;
        int ans = el.TryGetProperty("ans_page", out var a) ? GetJsonInt(a) : -1;
        int num = el.TryGetProperty("num", out var n) ? GetJsonInt(n) : -1;
        dict[id] = (src, ans, num);
    }
    Console.WriteLine($"[load] parsedMap: {dict.Count} ids");
    return dict;
}

// 官方题面抽取：pdfs/_extract/exercise/page_{N}.json -> page -> [ExerciseItem]
static Dictionary<int, List<ExerciseItem>> LoadExercisePages(string dir)
{
    var res = new Dictionary<int, List<ExerciseItem>>();
    if (!Directory.Exists(dir)) { Console.Error.WriteLine($"[load] exercise 目录不存在: {dir}"); return res; }
    foreach (var f in Directory.GetFiles(dir, "page_*.json").OrderBy(x => x, StringComparer.Ordinal))
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(f));
            var root = doc.RootElement;
            int pageIdx = root.TryGetProperty("page_idx", out var pi) ? GetJsonInt(pi) : -1;
            JsonElement arr = default;
            if (root.TryGetProperty("questions", out var q) && q.ValueKind == JsonValueKind.Array) arr = q.Clone();
            else if (root.TryGetProperty("raw", out var r) && r.ValueKind == JsonValueKind.String)
            {
                using var rawDoc = JsonDocument.Parse(r.GetString() ?? "[]");
                if (rawDoc.RootElement.ValueKind == JsonValueKind.Array) arr = rawDoc.RootElement.Clone();
            }
            if (pageIdx < 0 || arr.ValueKind != JsonValueKind.Array) continue;
            var list = new List<ExerciseItem>();
            foreach (var el in arr.EnumerateArray())
            {
                int num = el.TryGetProperty("num", out var n) ? GetJsonInt(n) : -1;
                string? question = el.TryGetProperty("question", out var qe) && qe.ValueKind == JsonValueKind.String ? qe.GetString() : null;
                list.Add(new ExerciseItem(num, question, ReadOptionsDict(el)));
            }
            if (list.Count > 0) res[pageIdx] = list;
        }
        catch (Exception ex) { Console.Error.WriteLine($"  [load] 跳过 exercise {Path.GetFileName(f)}: {ex.Message}"); }
    }
    Console.WriteLine($"[load] exercise pages: {res.Count}");
    return res;
}

// 官方答案册抽取：pdfs/_extract/answer/page_{N}.json -> page -> [AnswerItem]
static Dictionary<int, List<AnswerItem>> LoadAnswerPages(string dir)
{
    var res = new Dictionary<int, List<AnswerItem>>();
    if (!Directory.Exists(dir)) { Console.Error.WriteLine($"[load] answer 目录不存在: {dir}"); return res; }
    foreach (var f in Directory.GetFiles(dir, "page_*.json").OrderBy(x => x, StringComparer.Ordinal))
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(f));
            var root = doc.RootElement;
            int pageIdx = root.TryGetProperty("page_idx", out var pi) ? GetJsonInt(pi) : -1;
            JsonElement arr = default;
            if (root.TryGetProperty("answers", out var a) && a.ValueKind == JsonValueKind.Array) arr = a.Clone();
            else if (root.TryGetProperty("raw", out var r) && r.ValueKind == JsonValueKind.String)
            {
                using var rawDoc = JsonDocument.Parse(r.GetString() ?? "[]");
                if (rawDoc.RootElement.ValueKind == JsonValueKind.Array) arr = rawDoc.RootElement.Clone();
            }
            if (pageIdx < 0 || arr.ValueKind != JsonValueKind.Array) continue;
            var list = new List<AnswerItem>();
            foreach (var el in arr.EnumerateArray())
            {
                int num = el.TryGetProperty("num", out var n) ? GetJsonInt(n) : -1;
                string? answer = el.TryGetProperty("answer", out var ae) && ae.ValueKind == JsonValueKind.String ? ae.GetString() : null;
                string? explanation = el.TryGetProperty("explanation", out var ee) && ee.ValueKind == JsonValueKind.String ? ee.GetString() : null;
                string? module = el.TryGetProperty("module", out var me) && me.ValueKind == JsonValueKind.String ? me.GetString() : null;
                list.Add(new AnswerItem(num, answer, explanation, module));
            }
            if (list.Count > 0) res[pageIdx] = list;
        }
        catch (Exception ex) { Console.Error.WriteLine($"  [load] 跳过 answer {Path.GetFileName(f)}: {ex.Message}"); }
    }
    Console.WriteLine($"[load] answer pages: {res.Count}");
    return res;
}

static int GetJsonInt(JsonElement el)
{
    if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var v)) return v;
    if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var v2)) return v2;
    return -1;
}

static Dictionary<string, string>? ReadOptionsDict(JsonElement el)
{
    if (!el.TryGetProperty("options", out var oe) || oe.ValueKind != JsonValueKind.Object) return null;
    var d = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var p in oe.EnumerateObject())
        if (p.Value.ValueKind == JsonValueKind.String) d[p.Name] = p.Value.GetString() ?? "";
    return d;
}

// ---------------- 最小 PNG 编码器（BGRA -> RGBA，不依赖 System.Drawing） ----------------

static void WritePng(string path, int width, int height, byte[] bgra)
{
    using var fs = File.Create(path);
    fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8); // PNG signature

    var ihdr = new byte[13];
    WriteBEBytes(ihdr, 0, width);
    WriteBEBytes(ihdr, 4, height);
    ihdr[8] = 8;  // bit depth
    ihdr[9] = 6;  // color type = RGBA
    // ihdr[10..12] = 0 (compression/filter/interlace)
    WriteChunk(fs, "IHDR", ihdr);

    int stride = width * 4;
    var raw = new byte[(stride + 1) * height];
    int dst = 0, src = 0;
    for (int y = 0; y < height; y++)
    {
        raw[dst++] = 0; // filter: None
        for (int x = 0; x < width; x++)
        {
            byte b = bgra[src], g = bgra[src + 1], r = bgra[src + 2], a = bgra[src + 3];
            raw[dst++] = r; raw[dst++] = g; raw[dst++] = b; raw[dst++] = a;
            src += 4;
        }
    }

    byte[] idat;
    using (var ms = new MemoryStream())
    {
        using (var zs = new ZLibStream(ms, CompressionLevel.Optimal))
            zs.Write(raw, 0, raw.Length);
        idat = ms.ToArray();
    }
    WriteChunk(fs, "IDAT", idat);
    WriteChunk(fs, "IEND", Array.Empty<byte>());
}

static void WriteChunk(Stream s, string type, byte[] data)
{
    Span<byte> len = stackalloc byte[4];
    WriteBE(len, data.Length);
    s.Write(len);
    var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
    s.Write(typeBytes, 0, typeBytes.Length);
    s.Write(data, 0, data.Length);
    var crcInput = new byte[typeBytes.Length + data.Length];
    Buffer.BlockCopy(typeBytes, 0, crcInput, 0, typeBytes.Length);
    if (data.Length > 0) Buffer.BlockCopy(data, 0, crcInput, typeBytes.Length, data.Length);
    Span<byte> crc = stackalloc byte[4];
    WriteBE(crc, (int)Crc32(crcInput));
    s.Write(crc);
}

// IEEE CRC32（反射多项式 0xEDB88320），不依赖 System.IO.Hashing 包，保持工具离线零额外依赖。
static uint Crc32(byte[] data)
{
    uint crc = 0xFFFFFFFFu;
    foreach (byte b in data)
    {
        crc ^= b;
        for (int k = 0; k < 8; k++)
            crc = (crc >> 1) ^ ((crc & 1u) != 0 ? 0xEDB88320u : 0u);
    }
    return crc ^ 0xFFFFFFFFu;
}

static void WriteBEBytes(byte[] buf, int offset, int value)
{
    buf[offset] = (byte)(value >> 24);
    buf[offset + 1] = (byte)(value >> 16);
    buf[offset + 2] = (byte)(value >> 8);
    buf[offset + 3] = (byte)value;
}

static void WriteBE(Span<byte> buf, int value)
{
    buf[0] = (byte)(value >> 24);
    buf[1] = (byte)(value >> 16);
    buf[2] = (byte)(value >> 8);
    buf[3] = (byte)value;
}

// ---------------- 参数解析 / 路径 ----------------

static CliArgs ParseArgs(string[] argv)
{
    var positional = new List<string>();
    var flags = new HashSet<string>(StringComparer.Ordinal);
    var named = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    // 已知布尔标志：不消费后续位置参数（否则 --recheck pdf-197 会被误判为命名参数）
    var boolFlags = new HashSet<string>(StringComparer.Ordinal) { "--scan", "--verify", "--recheck", "--force", "--ocr", "--from-report", "--qwen-ocr", "--answer-pdf" };
    for (int i = 0; i < argv.Length; i++)
    {
        var a = argv[i];
        if (a.StartsWith("--", StringComparison.Ordinal))
        {
            if (boolFlags.Contains(a))
            {
                flags.Add(a);
            }
            else if (i + 1 < argv.Length && !argv[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                named[a] = argv[i + 1]; i++;
            }
            else flags.Add(a);
        }
        else positional.Add(a);
    }
    return new CliArgs(positional, flags, named);
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

// ====================================================================
// 类型声明
// ====================================================================

public sealed record QuestionEntry(
    [property: System.Text.Json.Serialization.JsonPropertyName("id")] string Id,
    [property: System.Text.Json.Serialization.JsonPropertyName("question")] string Question,
    [property: System.Text.Json.Serialization.JsonPropertyName("options")] Dictionary<string, string>? Options,
    [property: System.Text.Json.Serialization.JsonPropertyName("images")] List<string>? Images,
    [property: System.Text.Json.Serialization.JsonPropertyName("answer")] string? Answer,
    [property: System.Text.Json.Serialization.JsonPropertyName("answerKeywords")] List<string>? AnswerKeywords,
    [property: System.Text.Json.Serialization.JsonPropertyName("note")] string? Note,
    [property: System.Text.Json.Serialization.JsonPropertyName("type")] string? Type);

// 官方题面抽取单题（来自 pdfs/_extract/exercise/page_*.json）
public sealed record ExerciseItem(int Num, string? Question, Dictionary<string, string>? Options);

// 官方答案册抽取单条（来自 pdfs/_extract/answer/page_*.json；Answer 字段可能污染，Explanation 为可靠源）
public sealed record AnswerItem(int Num, string? Answer, string? Explanation, string? Module);

// 校验发现（报告单元；severity: error/suspect/info）
public sealed record DefectFinding(string Id, string Severity, string Class, string Reason, string Evidence);

public sealed record CliArgs(List<string> Positional, HashSet<string> Flags, Dictionary<string, string> Named)
{
    public string Get(string key, string def) => Named.TryGetValue(key, out var v) ? v : def;
    public int GetInt(string key, int def) => Named.TryGetValue(key, out var v) && int.TryParse(v, out var n) ? n : def;
    public List<int> GetList(string key)
    {
        if (!Named.TryGetValue(key, out var v) || string.IsNullOrWhiteSpace(v)) return new List<int>();
        return v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out var n) ? n : -1)
                .Where(n => n >= 0).ToList();
    }
}
