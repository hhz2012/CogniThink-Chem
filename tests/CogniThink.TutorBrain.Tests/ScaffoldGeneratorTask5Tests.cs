using CogniThink.TutorBrain.Application.ScaffoldGenerator;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// Task 5 验收：生成器入口管线组装正确（真值 + 降级 + 校验门），产物幂等。
/// </summary>
public class ScaffoldGeneratorTask5Tests
{
    private static QuestionCase RedoxCase() => new()
    {
        Id = "redox1",
        Type = "choice",
        Question = "反应：Zn + CuSO4 = ZnSO4 + Cu。下列说法正确的是",
        Answer = "A",
    };

    private static QuestionCase NonRedoxCase() => new()
    {
        Id = "复分解1",
        Type = "choice",
        Question = "复分解反应：AgNO3 + NaCl = AgCl↓ + NaNO3。",
        Answer = "B",
    };

    // ---- 管线：氧化还原题发布，A/B/E/F 有真值，C/D 降级，校验通过 ----

    [Fact]
    public async Task BuildDetailed_RedoxCase_PublishesWithDegradedSteps()
    {
        var artifacts = EngineArtifactsBuilder.Build(RedoxCase());
        var result = await new ScaffoldBuilder().BuildDetailedAsync(RedoxCase(), artifacts);

        Assert.Equal("published", result.Status);
        Assert.NotNull(result.Scaffold);
        var s = result.Scaffold!;
        Assert.Equal(ScaffoldModuleKind.RedoxReasoning, s.Module);
        Assert.Equal("redox1", s.SourceQuestionId);
        Assert.True(s.Validation.Ok, string.Join(" | ", result.Errors));

        // A/B/E/F 有真值
        Assert.NotNull(Step(s, AtomicTaskKind.A)!.Truth);
        Assert.NotNull(Step(s, AtomicTaskKind.B)!.Truth);
        Assert.NotNull(Step(s, AtomicTaskKind.E)!.Truth);
        Assert.NotNull(Step(s, AtomicTaskKind.F)!.Truth);
        // C/D 降级（无真值）
        Assert.Null(Step(s, AtomicTaskKind.C)!.Truth);
        Assert.Null(Step(s, AtomicTaskKind.D)!.Truth);

        Assert.Contains(s.DegradedSteps, d => d.Kind == AtomicTaskKind.C);
        Assert.Contains(s.DegradedSteps, d => d.Kind == AtomicTaskKind.D);

        // provenance 只记录有真值的步
        Assert.Contains("A", s.Provenance.Keys);
        Assert.Contains("B", s.Provenance.Keys);
        Assert.Contains("E", s.Provenance.Keys);
        Assert.Contains("F", s.Provenance.Keys);
        Assert.DoesNotContain("C", s.Provenance.Keys);
        Assert.DoesNotContain("D", s.Provenance.Keys);
    }

    // ---- 非氧化还原题 → module_mismatch → 整题 null（不发布） ----

    [Fact]
    public async Task BuildDetailed_NonRedoxCase_UnsuitableNull()
    {
        var artifacts = EngineArtifactsBuilder.Build(NonRedoxCase());
        var result = await new ScaffoldBuilder().BuildDetailedAsync(NonRedoxCase(), artifacts);

        Assert.Equal("unsuitable", result.Status);
        Assert.Null(result.Scaffold);
        Assert.Contains(result.Errors, e => e.Contains("module_mismatch"));
    }

    // ---- 接口契约：BuildAsync 成功→产物，失败→null ----

    [Fact]
    public async Task BuildAsync_ApiContract_ReturnsScaffoldOrNull()
    {
        IScaffoldBuilder builder = new ScaffoldBuilder();
        Assert.NotNull(await builder.BuildAsync(RedoxCase()));
        Assert.Null(await builder.BuildAsync(NonRedoxCase()));
    }

    // ---- TR-5.2 幂等：同输入 → 字节级一致 ----

    [Fact]
    public async Task Writer_SameInput_ByteIdentical()
    {
        var artifacts = EngineArtifactsBuilder.Build(RedoxCase());
        var scaffold = (await new ScaffoldBuilder().BuildDetailedAsync(RedoxCase(), artifacts)).Scaffold!;
        var list = new List<GeneratedScaffold> { scaffold };

        var a = ScaffoldArtifactWriter.ToTs(list);
        var b = ScaffoldArtifactWriter.ToTs(list);
        Assert.Equal(a, b);   // 同输入 → 完全一致

        // 写两次盘 → 文件内容一致
        var f1 = Path.GetTempFileName() + ".ts";
        var f2 = Path.GetTempFileName() + ".ts";
        try
        {
            ScaffoldArtifactWriter.WriteTs(f1, list);
            ScaffoldArtifactWriter.WriteTs(f2, list);
            Assert.Equal(File.ReadAllText(f1), File.ReadAllText(f2));
        }
        finally
        {
            File.Delete(f1); File.Delete(f2);
        }
    }

    // ---- 端到端幂等：同一题跑两次管线 → 产物 JSON 一致 ----

    [Fact]
    public async Task Pipeline_Twice_SameJson()
    {
        var c = RedoxCase();
        var s1 = (await new ScaffoldBuilder().BuildDetailedAsync(c, EngineArtifactsBuilder.Build(c))).Scaffold!;
        var s2 = (await new ScaffoldBuilder().BuildDetailedAsync(c, EngineArtifactsBuilder.Build(c))).Scaffold!;

        var j1 = System.Text.Json.JsonSerializer.Serialize(s1, WriterOpts());
        var j2 = System.Text.Json.JsonSerializer.Serialize(s2, WriterOpts());
        Assert.Equal(j1, j2);
    }

    // ---- 题干为空 → unsuitable，不崩 ----

    [Fact]
    public async Task BuildDetailed_EmptyQuestion_UnsuitableNotThrow()
    {
        var result = await new ScaffoldBuilder().BuildDetailedAsync(new QuestionCase { Id = "x", Question = "" }, null);
        Assert.Equal("unsuitable", result.Status);
        Assert.Null(result.Scaffold);
    }

    // ---- TR-5.2 跨进程幂等回归锁：StableSeed 必须字节级稳定（防回退到 string.GetHashCode 的进程随机化）----
    //
    // 历史教训（本任务）：初版 StableSeed 用 string.GetHashCode(StringComparison.Ordinal)，
    // 单进程内稳定故进程内单测全过，但 .NET Core 起按进程随机化 → 两次 dotnet 运行产物 SHA-256 不同，
    // 幂等失败。已改为手动 FNV-1a over chars。下面的 golden 值锁定实现：
    // 若有人改回 GetHashCode 或改动算法，本测试必然失败。
    // 值 = FNV-1a（offset 2166136261 / prime 16777619），先哈希 kind.ToString()，再哈希按 Ordinal 排序后的 values，
    // 末步 (int) 强转（unchecked，>int.MaxValue 变负）。已与 C# 实现逐字符对齐。

    [Fact]
    public void StableSeed_GoldenValues_LockedAgainstGetHashCodeRegression()
    {
        Assert.Equal(-1999240765, ScaffoldGeneratorUtils.StableSeed(AtomicTaskKind.B, new[] { "Fe", "Cl" }));
        // 重排输入 → 同一种子（correct 集合无序）
        Assert.Equal(-1999240765, ScaffoldGeneratorUtils.StableSeed(AtomicTaskKind.B, new[] { "Cl", "Fe" }));
        Assert.Equal(-1005848884, ScaffoldGeneratorUtils.StableSeed(AtomicTaskKind.A, Array.Empty<string>()));
        Assert.Equal(-2083296280, ScaffoldGeneratorUtils.StableSeed(AtomicTaskKind.F, new[] { "A" }));
        Assert.Equal(-699072186, ScaffoldGeneratorUtils.StableSeed(AtomicTaskKind.E, new[] { "ZnSO4", "Cu" }));
        Assert.Equal(1770254380, ScaffoldGeneratorUtils.StableSeed(AtomicTaskKind.A, new[] { "Zn", "CuSO4", "ZnSO4", "Cu" }));
    }

    // ---- 前端契约锁：生成产物 JSON 必须与 miniapp/src/types/scaffold.ts 逐字段对齐 ----
    // 经验（参数/产物链路）：上游写入形态必须匹配下游读取契约，否则前端 MODULE_CHAINS 查不到链、
    // strictNullChecks 下 `conditions?: T` 不接受 null。本测试锁定序列化契约，防字段漂移。

    [Fact]
    public void Serialize_FrontendContract_ModuleKebab_TruthNullable_OptionalNullsOmitted()
    {
        var scaffold = new GeneratedScaffold
        {
            Id = "gen_x",
            Title = "t",
            Module = ScaffoldModuleKind.RedoxReasoning,
            Conditions = null,
            BranchEntryId = null,
            SourceQuestionId = "q1",
            Provenance = { ["A"] = "ParsedEquation.AllSpecies" },
            DegradedSteps = { new DegradedStep { Kind = AtomicTaskKind.C, Reason = "no engine truth" } },
            Validation = { Ok = true },
            Steps =
            {
                new StepPractice
                {
                    Kind = AtomicTaskKind.A,
                    Truth = new StepGroundTruth
                    {
                        Type = "multi-check",
                        Options = { new StepOption { Key = "a", Label = "A" } },
                        Correct = new List<string> { "a" },
                    },
                    Releases = new StepScaffoldReleases(),
                },
                new StepPractice { Kind = AtomicTaskKind.C, Truth = null, Releases = new StepScaffoldReleases() },
            },
        };

        var json = System.Text.Json.JsonSerializer.Serialize(scaffold, WriterOpts());

        // module 必须是前端 ScaffoldModuleKind 联合字面量（连字符小写），不能是 PascalCase 枚举名
        Assert.Contains("\"module\":\"redox-reasoning\"", json);
        Assert.DoesNotContain("\"module\":\"RedoxReasoning\"", json);
        // 降级步 truth 显式为 null（前端据此渲染只读揭示，不渲染作答控件）
        Assert.Contains("\"truth\":null", json);
        // 可选字段为 null 时省略（strictNullChecks 下 `conditions?: T` 不接受 null 字面量）
        Assert.DoesNotContain("\"conditions\"", json);
        Assert.DoesNotContain("\"branchEntryId\"", json);
        // 交互类型是前端判别联合字面量
        Assert.Contains("\"type\":\"multi-check\"", json);
    }

    private static StepPractice? Step(GeneratedScaffold s, AtomicTaskKind k) => s.Steps.FirstOrDefault(x => x.Kind == k);

    private static System.Text.Json.JsonSerializerOptions WriterOpts() => new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        // 不在此注册 JsonStringEnumConverter：枚举名由类型上的 [JsonConverter(EnumMemberJsonConverter<T>)] 处理。
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
