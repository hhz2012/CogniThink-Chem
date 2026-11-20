using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// T3 任务独立测试：直接调用 OrganicChoiceValidator / StructurePeriodicValidator.TryDecide 断言。
/// 真值以 llmReference 反推 + 化学核对得到，集中在 Truth 字典里（单一事实源）。
/// 红线：零假阳性——任一校验器对“未列入真值表”的题返回非 null 即判失败。
/// </summary>
public class T3FactValidatorsTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();
    private static readonly List<T3Question> Questions = LoadT3();

    // ===== 真值表（确定性可解的题）=====
    // 有机：官能团性质/反应类型/同分异构/同系物/高分子/材料
    public static readonly IReadOnlyDictionary<string, string> OrganicTruth = new Dictionary<string, string>
    {
        ["pdf-335"] = "B", ["pdf-366"] = "C", ["pdf-369"] = "A", ["pdf-373"] = "B",
        ["pdf-374"] = "B", ["pdf-375"] = "B", ["pdf-379"] = "A", ["pdf-385"] = "C",
        ["pdf-392"] = "C", ["pdf-443"] = "B", ["pdf-447"] = "B", ["pdf-448"] = "D", ["pdf-449"] = "D",
        ["pdf-329"] = "ACE",  // 多选（含选项 E）；Organic 正确断言 ACE，原为真值表漏记
    };

    // 结构/周期律/化学用语：同位素/同素异形体/晶体类型/化学用语/周期律推断/分子极性与键角
    public static readonly IReadOnlyDictionary<string, string> StructureTruth = new Dictionary<string, string>
    {
        ["pdf-069"] = "B", ["pdf-154"] = "B", ["pdf-181"] = "C", ["pdf-183"] = "B",
        ["pdf-186"] = "A", ["pdf-192"] = "B", ["pdf-200"] = "C", ["pdf-209"] = "B",
        ["pdf-210"] = "A", ["pdf-320"] = "B", ["pdf-326"] = "D", ["pdf-430"] = "C",
        ["pdf-499"] = "B", ["pdf-500"] = "A", ["pdf-501"] = "B", ["pdf-502"] = "B",
        ["pdf-225"] = "CD",   // 双选；Structure 正确断言 CD（超分子特征 + B—N 配位键中 N 提供孤电子对），原为真值表漏记
    };

    public static IEnumerable<object[]> OrganicCases =>
        OrganicTruth.Select(kv => new object[] { kv.Key, kv.Value });

    public static IEnumerable<object[]> StructureCases =>
        StructureTruth.Select(kv => new object[] { kv.Key, kv.Value });

    [Theory]
    [MemberData(nameof(OrganicCases))]
    public void Organic_Solves_KnownQuestion(string id, string expected)
    {
        var r = OrganicChoiceValidator.TryDecide(Req(id));
        Assert.NotNull(r);
        Assert.Equal(expected, r!.AnswerLetter);
    }

    [Theory]
    [MemberData(nameof(StructureCases))]
    public void Structure_Solves_KnownQuestion(string id, string expected)
    {
        var r = StructurePeriodicValidator.TryDecide(Req(id));
        Assert.NotNull(r);
        Assert.Equal(expected, r!.AnswerLetter);
    }

    [Fact]
    public void Organic_ZeroFalsePositive_AcrossAllT3()
    {
        foreach (var q in Questions)
        {
            var r = OrganicChoiceValidator.TryDecide(new EngineRequest { RawText = q.question });
            if (r is null) continue;
            Assert.True(OrganicTruth.TryGetValue(q.id, out var exp),
                $"[零假阳性] OrganicChoiceValidator 对未列入真值表的题 {q.id} 返回 {r.AnswerLetter}");
            Assert.Equal(exp, r.AnswerLetter);
        }
    }

    [Fact]
    public void Structure_ZeroFalsePositive_AcrossAllT3()
    {
        foreach (var q in Questions)
        {
            var r = StructurePeriodicValidator.TryDecide(new EngineRequest { RawText = q.question });
            if (r is null) continue;
            Assert.True(StructureTruth.TryGetValue(q.id, out var exp),
                $"[零假阳性] StructurePeriodicValidator 对未列入真值表的题 {q.id} 返回 {r.AnswerLetter}");
            Assert.Equal(exp, r.AnswerLetter);
        }
    }

    [Fact]
    public void Validators_DoNotOverlap()
    {
        foreach (var q in Questions)
        {
            var o = OrganicChoiceValidator.TryDecide(new EngineRequest { RawText = q.question });
            var s = StructurePeriodicValidator.TryDecide(new EngineRequest { RawText = q.question });
            Assert.False(o != null && s != null,
                $"[路由二义] 两校验器同时断言 {q.id}: organic={o?.AnswerLetter}, structure={s?.AnswerLetter}");
        }
    }

    [Fact]
    public void Coverage_MatchesTruthTables()
    {
        var orgSolved = Questions.Count(q => OrganicChoiceValidator.TryDecide(new EngineRequest { RawText = q.question }) != null);
        var stSolved = Questions.Count(q => StructurePeriodicValidator.TryDecide(new EngineRequest { RawText = q.question }) != null);
        Assert.Equal(OrganicTruth.Count, orgSolved);
        Assert.Equal(StructureTruth.Count, stSolved);
    }

    // ===================== helpers =====================

    private static EngineRequest Req(string id)
    {
        var q = Questions.FirstOrDefault(x => x.id == id);
        Assert.NotNull(q);
        return new EngineRequest { RawText = q!.question };
    }

    private static string ResolveRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "data", "knowledge", "knowledge.json")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new DirectoryNotFoundException("找不到 repo 根（缺少 data/knowledge/knowledge.json）");
    }

    private static List<T3Question> LoadT3()
    {
        var path = Path.Combine(RepoRoot, "_evalscratch", "task_inputs", "T3.json");
        var json = File.ReadAllText(path);
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<List<T3Question>>(json, opts) ?? new List<T3Question>();
    }

    private sealed class T3Question
    {
        public string id { get; set; } = "";
        public string? taskType { get; set; }
        public string? topic { get; set; }
        public string question { get; set; } = "";
        public string? expected { get; set; }
        public string? llmReference { get; set; }
        public string? note { get; set; }
        public string? cluster { get; set; }
    }
}
