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
bool force = cli.Flags.Contains("--force");
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

Directory.CreateDirectory(ocrDir);
Directory.CreateDirectory(renderDir);

Console.WriteLine($"[PdfOcrRepair] repoRoot={repoRoot}");
Console.WriteLine($"[PdfOcrRepair] pdf={pdfPath}");
Console.WriteLine($"[PdfOcrRepair] tesseract={tesseract}");
Console.WriteLine($"[PdfOcrRepair] ocrDir={ocrDir} renderDir={renderDir} dpi={dpi} span={span} force={force}");

if (!File.Exists(pdfPath)) { Console.Error.WriteLine($"[PdfOcrRepair] PDF 不存在: {pdfPath}"); return 2; }
if (!File.Exists(tesseract)) { Console.Error.WriteLine($"[PdfOcrRepair] tesseract 不存在: {tesseract}（可用 --tesseract 指定）"); return 2; }
if (!File.Exists(questionsJson)) { Console.Error.WriteLine($"[PdfOcrRepair] 题目数据不存在: {questionsJson}"); return 2; }

int pageCount = GetPageCount(pdfPath);
Console.WriteLine($"[PdfOcrRepair] pdf pages={pageCount}");

// 加载题目（用于：取题干/已有选项/定位缺失字母；src_page 来自 questions_parsed.json）
var entries = LoadQuestions(questionsJson);
var entryById = entries.ToDictionary(e => e.Id, StringComparer.Ordinal);
var pageById = LoadParsedPages(parsedJson);
var manifest = File.Exists(manifestPath) ? LoadManifest(manifestPath) : new Dictionary<string, List<int>>();

if (scan)
{
    RunScan(pageCount, entries, pdfPath, tesseract, dpi, force, renderDir, ocrDir, manifestPath);
    return 0;
}

if (string.IsNullOrEmpty(id))
{
    Console.Error.WriteLine("用法: dotnet run -- <pdf-id> [--pages 126,127] [--span 1] [--force] [--scan]");
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
    for (int i = 0; i < argv.Length; i++)
    {
        var a = argv[i];
        if (a.StartsWith("--", StringComparison.Ordinal))
        {
            if (i + 1 < argv.Length && !argv[i + 1].StartsWith("--", StringComparison.Ordinal))
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
    [property: System.Text.Json.Serialization.JsonPropertyName("answer")] string? Answer);

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
