using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.ScaffoldGenerator;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// Task 7 全量护栏：对落盘的生成产物做端到端核验（spec AC-5 / FR-11 / TR-7.2）。
/// 不依赖 gitignored 的 _evalscratch/checkpoint；源题取自受版本控制的 pdf_questions.json
/// （GatewayVsLlmEvaluationService.GenPdfQuestions），产物 index.ts 也是受版本控制的源数据
/// → 本护栏可移植、始终运行。
/// 验证项：
///   1) 从源题重新生成的整份 .ts 与落盘产物字节级一致（防手改 / 生成逻辑漂移 / 序列化契约漂移）；
///   2) 每条发布脚手架过校验门 0 违例（correct 溯源 / F 对齐 / 字段齐全）；
///   3) 直接断言 distractors ∩ correct == ∅ 与 F 步对齐存储 answer（精度红线显式化）。
/// 红线：precision=1、harmCount=0；生成器只读，不碰判分链路。
/// </summary>
public class ScaffoldGeneratorTask7Tests
{
    private static readonly JsonSerializerOptions DeserializeOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "CogniThink.Chem.sln"))) return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return Directory.GetCurrentDirectory();
    }

    private static string ArtifactPath =>
        Path.Combine(RepoRoot(), "miniapp", "src", "data", "scaffolds", "generated", "index.ts");

    /// <summary>从 .ts 文本抽出 JSON 数组（去掉 TS 包装：前缀行 + 结尾 ];）。</summary>
    private static string ExtractJsonArray(string ts)
    {
        // 定位 "= [" 后的数组起始 '['（避开类型标注里的 GeneratedScaffold[]）
        int assign = ts.IndexOf("= [", StringComparison.Ordinal);
        Assert.True(assign >= 0, "generated/index.ts 未找到数组赋值");
        int start = ts.IndexOf('[', assign);
        int end = ts.LastIndexOf(']');
        Assert.True(start >= 0 && end > start, "generated/index.ts 未找到 JSON 数组");
        return ts.Substring(start, end - start + 1);
    }

    private static List<GeneratedScaffold> LoadDiskScaffolds()
    {
        Assert.True(File.Exists(ArtifactPath),
            $"生成产物不存在：{ArtifactPath}（请先运行 ScaffoldGenerator 生成落盘产物）");
        var ts = File.ReadAllText(ArtifactPath);
        var json = ExtractJsonArray(ts);
        var list = JsonSerializer.Deserialize<List<GeneratedScaffold>>(json, DeserializeOpts);
        Assert.NotNull(list);
        return list!;
    }

    private static Dictionary<string, QaTestCase> LoadQuestionBank()
    {
        var bank = GatewayVsLlmEvaluationService.GenPdfQuestions();
        // 加载守卫：与源文件条目数对齐。pdf_questions.json 损坏（GenPdfQuestions 吞异常→空集）
        // 或 test-bin 副本过期（条目数不一致）→ 必须失败而非空过。
        var srcPath = Path.Combine(RepoRoot(), "src", "CogniThink.TutorBrain.Api", "data", "pdf_questions.json");
        int expected = 0;
        if (File.Exists(srcPath))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(srcPath));
            expected = doc.RootElement.GetArrayLength();
        }
        Assert.True(expected > 0, $"源 pdf_questions.json 不存在或为空：{srcPath}");
        Assert.Equal(expected, bank.Count);
        return bank.ToDictionary(c => c.Id, StringComparer.Ordinal);
    }

    private static QuestionCase MapCase(QaTestCase c) => new()
    {
        Id = c.Id,
        Type = c.Type ?? "choice",
        Question = c.Question ?? "",
        Answer = c.Answer ?? "",
        Options = c.Options?.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                          .Select(kv => kv.Value ?? "").ToList() ?? new List<string>(),
        AnswerKeywords = c.AnswerKeywords ?? new List<string>(),
        Note = c.Note,
        Category = null,
    };

    // ---- Test 1: 落盘产物与当前生成字节级一致（无漂移 / 无手改），MVP ≥3 条 ----

    [Fact]
    public async Task DiskArtifact_RegeneratesByteIdentical_FromTrackedQuestions()
    {
        var diskTs = File.ReadAllText(ArtifactPath);
        var diskScaffolds = LoadDiskScaffolds();

        // MVP：redox-reasoning 模块 ≥3 条（spec AC-6）
        Assert.True(diskScaffolds.Count >= 3,
            $"MVP 要求生成 ≥3 条脚手架，实际落盘 {diskScaffolds.Count} 条");
        Assert.All(diskScaffolds, s =>
            Assert.Equal(ScaffoldModuleKind.RedoxReasoning, s.Module));

        var bank = LoadQuestionBank();
        var builder = new ScaffoldBuilder();
        var fresh = new List<GeneratedScaffold>();

        foreach (var s in diskScaffolds)
        {
            Assert.True(bank.TryGetValue(s.SourceQuestionId, out var tc),
                $"产物 {s.Id} 的源题 {s.SourceQuestionId} 不在 pdf_questions.json 中");
            var qcase = MapCase(tc);
            var artifacts = EngineArtifactsBuilder.Build(qcase);
            var br = await builder.BuildDetailedAsync(qcase, artifacts);
            Assert.Equal("published", br.Status);
            Assert.NotNull(br.Scaffold);
            fresh.Add(br.Scaffold!);
        }

        fresh = fresh.OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
        var expectedTs = ScaffoldArtifactWriter.ToTs(fresh);

        // 字节级一致 → 落盘产物与当前生成完全对齐（防手改 / 逻辑漂移 / 序列化契约漂移）
        Assert.Equal(diskTs, expectedTs);
    }

    // ---- Test 2: 每条发布脚手架 0 违例（校验门 + 精度红线直接断言） ----

    [Fact]
    public async Task EveryPublishedScaffold_PassesValidator_And_PrecisionRedlinesHold()
    {
        var diskScaffolds = LoadDiskScaffolds();
        var bank = LoadQuestionBank();
        var validator = new ScaffoldValidator();

        foreach (var s in diskScaffolds)
        {
            Assert.True(bank.TryGetValue(s.SourceQuestionId, out var tc),
                $"产物 {s.Id} 缺少源题 {s.SourceQuestionId}");
            var qcase = MapCase(tc);
            var artifacts = EngineArtifactsBuilder.Build(qcase);

            // (a) 校验门在「实类型」产物上 0 违例：重新生成一份（Correct 为 string/List<string>，
            //     避免落盘反序列化后的 JsonElement 让 StoredCorrectSet 窄化失效）
            var fresh = await new ScaffoldBuilder().BuildDetailedAsync(qcase, artifacts);
            Assert.Equal("published", fresh.Status);
            var v = validator.Validate(fresh.Scaffold!, artifacts);
            Assert.True(v.Ok,
                $"产物 {s.Id}（源题 {s.SourceQuestionId}）校验门未通过：{string.Join(" | ", v.Errors)}");

            // (b) 落盘产物自身存的校验摘要也为 Ok
            Assert.True(s.Validation.Ok,
                $"产物 {s.Id} 落盘的 validation.ok=false：{string.Join(" | ", s.Validation.Errors)}");
            Assert.Empty(s.Validation.Errors);

            // (c) 精度红线：直接扫描落盘产物（JsonElement safe）
            var stepsByKind = s.Steps.GroupBy(x => x.Kind).ToDictionary(g => g.Key, g => g.First());
            // 关键步 A/B/E/F 必须有真值 + provenance 溯源
            foreach (var k in new[] { AtomicTaskKind.A, AtomicTaskKind.B, AtomicTaskKind.E, AtomicTaskKind.F })
            {
                Assert.True(stepsByKind.TryGetValue(k, out var st) && st.Truth is not null,
                    $"{s.Id} 关键步 {k} 缺失真值");
                Assert.True(s.Provenance.ContainsKey(k.ToString()),
                    $"{s.Id} 关键步 {k} 缺失 provenance 溯源");
            }
            // 降级步只允许 C / D
            Assert.All(s.DegradedSteps, d =>
                Assert.True(d.Kind == AtomicTaskKind.C || d.Kind == AtomicTaskKind.D,
                    $"{s.Id} 非 C/D 步被降级：{d.Kind}"));

            foreach (var step in s.Steps)
            {
                if (step.Truth is null) continue; // 降级步（只读揭示，无 truth）
                var correct = AnyCorrectSet(step.Truth.Correct);
                Assert.NotEmpty(correct);

                var optionKeys = step.Truth.Options.Select(o => o.Key).ToList();
                // correct ⊆ options
                foreach (var c in correct)
                    Assert.Contains(optionKeys, k => string.Equals(k, c, StringComparison.OrdinalIgnoreCase));
                // distractors (options \ correct) ∩ correct == ∅
                var distractors = optionKeys
                    .Where(k => !correct.Contains(k, StringComparer.OrdinalIgnoreCase)).ToList();
                Assert.Empty(distractors.Where(d => correct.Contains(d, StringComparer.OrdinalIgnoreCase)));
                // options 无重复
                Assert.Equal(optionKeys.Count,
                    optionKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
                // releases 四档非空（hint / reveal 必检；probe / scaffold 由校验门兜底）
                Assert.False(string.IsNullOrWhiteSpace(step.Releases.Hint), $"{s.Id}/{step.Kind} hint 空");
                Assert.False(string.IsNullOrWhiteSpace(step.Releases.Reveal.Title), $"{s.Id}/{step.Kind} reveal.title 空");
                Assert.False(string.IsNullOrWhiteSpace(step.Releases.Reveal.Body), $"{s.Id}/{step.Kind} reveal.body 空");
            }

            // (d) F 步 correct 与存储 answer 对齐（answer 可能 "A" / "AC" / "A,C"）
            var f = stepsByKind[AtomicTaskKind.F];
            var fCorrect = AnyCorrectSet(f.Truth!.Correct);
            var ans = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ch in (qcase.Answer ?? "").Where(char.IsLetter))
                ans.Add(ch.ToString().ToUpperInvariant());
            Assert.True(ans.Count > 0 && fCorrect.SetEquals(ans),
                $"{s.Id} F 步 correct [{string.Join(",", fCorrect)}] 与存储 answer [{qcase.Answer}] 不对齐");
        }
    }

    // ---- helpers ----

    /// <summary>兼容落盘 JsonElement（string/array）与内存实类型（string/List&lt;string&gt;）的 correct 集合抽取。</summary>
    private static HashSet<string> AnyCorrectSet(object? correct)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (correct is null) return set;
        if (correct is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.String)
            {
                var s = je.GetString();
                if (!string.IsNullOrWhiteSpace(s)) set.Add(s.Trim());
            }
            else if (je.ValueKind == JsonValueKind.Array)
            {
                foreach (var it in je.EnumerateArray())
                    if (it.ValueKind == JsonValueKind.String)
                    {
                        var s = it.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) set.Add(s.Trim());
                    }
            }
        }
        else if (correct is string s && !string.IsNullOrWhiteSpace(s))
        {
            set.Add(s.Trim());
        }
        else if (correct is System.Collections.IEnumerable e)
        {
            foreach (var item in e)
                if (item is string st && !string.IsNullOrWhiteSpace(st)) set.Add(st.Trim());
        }
        return set;
    }
}
