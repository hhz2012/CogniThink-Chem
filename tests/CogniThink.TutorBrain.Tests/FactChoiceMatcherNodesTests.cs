using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// B-LlmMain Task 1：ChoiceVerifier.Outcome 加性携带 ReasoningNodes +
/// FactChoiceMatcher 命中事实 Id 线程化的正确性 / R4 安全测试。
/// 覆盖 AC1（结构正确）、AC6（命中全空→不臆造汇总）、AC7（判决字段不漂移）。
/// </summary>
public class FactChoiceMatcherNodesTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    // ===== 纯构建器：结构正确性（AC1）=====

    [Fact]
    public void Build_WithHitIds_ProducesOptionNodesPlusOneSummary()
    {
        var hits = new List<ChoiceReasoningNodes.OptionHit>
        {
            new("A", new List<string> { "GC-001-A" }),
            new("B", new List<string> { "GC-001-B" }),
            new("C", new List<string> { "GC-001-C" }),
            new("D", new List<string> { "GC-001-D" }),
        };

        var nodes = ChoiceReasoningNodes.Build(hits, "正确选项为 B");

        // 4 选项节点 + 1 汇总 = 5
        Assert.Equal(5, nodes.Count);
        // Step 从 1 连续无跳步无重复（AC1②）
        Assert.Equal(Enumerable.Range(1, 5), nodes.Select(n => n.Step));

        var optNodes = nodes.Take(4).ToList();
        // 每命中 id 一节点，KnowledgeId==事实 Id、EvidenceLevel==KnowledgeFact（AC1①）
        Assert.Equal(new[] { "GC-001-A", "GC-001-B", "GC-001-C", "GC-001-D" },
            optNodes.Select(n => n.KnowledgeId));
        Assert.All(optNodes, n => Assert.Equal(EvidenceLevel.KnowledgeFact, n.EvidenceLevel));
        Assert.All(optNodes, n => Assert.True(
            n.Premises.Any(p => p.Contains(n.KnowledgeId!)),
            $"option node premises should reference KnowledgeId {n.KnowledgeId}"));

        // 末尾恰 1 个汇总节点：Conclusion==FinalResult、Premises==去重非空 KnowledgeId（AC1③）
        var summary = nodes[nodes.Count - 1];
        Assert.Equal("正确选项为 B", summary.Conclusion);
        Assert.Equal(new[] { "GC-001-A", "GC-001-B", "GC-001-C", "GC-001-D" },
            summary.Premises);
    }

    [Fact]
    public void Build_OptionWithMultipleIds_ProducesOneNodePerId()
    {
        var hits = new List<ChoiceReasoningNodes.OptionHit>
        {
            new("A", new List<string> { "X-1", "X-2" }),
            new("B", new List<string> { "Y-1" }),
        };
        var nodes = ChoiceReasoningNodes.Build(hits, "错误选项为 B");
        // 2 (A) + 1 (B) + 1 汇总 = 4
        Assert.Equal(4, nodes.Count);
        Assert.Equal(Enumerable.Range(1, 4), nodes.Select(n => n.Step));
        Assert.Equal(new[] { "X-1", "X-2", "Y-1" }, nodes.Take(3).Select(n => n.KnowledgeId));
        Assert.Equal("错误选项为 B", nodes[nodes.Count - 1].Conclusion);
        Assert.Equal(new[] { "X-1", "X-2", "Y-1" }, nodes[nodes.Count - 1].Premises);
    }

    [Fact]
    public void Build_DuplicateIdsAcrossOptions_SummaryPremisesDeduplicated()
    {
        var hits = new List<ChoiceReasoningNodes.OptionHit>
        {
            new("A", new List<string> { "SAME" }),
            new("B", new List<string> { "SAME" }),
        };
        var nodes = ChoiceReasoningNodes.Build(hits, "正确选项为 A");
        Assert.Equal(new[] { "SAME" }, nodes[nodes.Count - 1].Premises);
    }

    // ===== R4 安全：命中全空→不臆造（AC6）=====

    [Fact]
    public void Build_AllHitIdsEmpty_ReturnsEmpty_NoFabricatedSummary()
    {
        var hits = new List<ChoiceReasoningNodes.OptionHit>
        {
            new("A", new List<string>()),
            new("B", new List<string> { "" }),
            new("C", new List<string> { "   " }),
        };
        var nodes = ChoiceReasoningNodes.Build(hits, "正确选项为 B");
        Assert.Empty(nodes); // 不臆造汇总节点
    }

    [Fact]
    public void Build_EmptyOrNullHits_ReturnsEmpty()
    {
        Assert.Empty(ChoiceReasoningNodes.Build(new List<ChoiceReasoningNodes.OptionHit>(), "x"));
    }

    [Fact]
    public void Outcome_DefaultReasoningNodes_IsEmptyNotNull()
    {
        // CS8601 教训：默认非 null 空列表
        var o = new ChoiceVerifier.Outcome("A", "正确选项为 A", new List<string>(), "");
        Assert.NotNull(o.ReasoningNodes);
        Assert.Empty(o.ReasoningNodes);
    }

    // ===== 端到端：真实 JSON id 流入节点（AC1 / 不蒙数 / 不臆造）=====

    [Fact]
    public void ConceptChoiceValidator_TryDecide_ThreadsRealKnowledgeIds()
    {
        var (idSet, groups) = LoadGeneralChoiceFacts();
        Assert.NotEmpty(idSet);
        Assert.NotEmpty(groups);

        int solvedTotal = 0;
        int solvedWithNodes = 0;
        foreach (var g in groups)
        {
            var raw = BuildRawText(g);
            if (raw is null) continue;
            var outcome = ConceptChoiceValidator.TryDecide(new EngineRequest { RawText = raw });
            if (outcome is null) continue;
            solvedTotal++;

            // 判决字段不漂移（AC7）：仍有非空结论
            Assert.False(string.IsNullOrEmpty(outcome.AnswerLetter));
            Assert.False(string.IsNullOrEmpty(outcome.FinalResult));

            var nodes = outcome.ReasoningNodes;
            // AC1 / R4：所有节点 KnowledgeId 必须是 JSON 中真实存在的 id（不臆造）
            Assert.All(nodes, n =>
            {
                if (!string.IsNullOrEmpty(n.KnowledgeId))
                    Assert.Contains(n.KnowledgeId, idSet);
                Assert.Equal(EvidenceLevel.KnowledgeFact, n.EvidenceLevel);
            });

            if (nodes.Count == 0) continue;
            solvedWithNodes++;

            // AC1：Step 连续 1..N
            Assert.Equal(Enumerable.Range(1, nodes.Count), nodes.Select(n => n.Step));
            // AC1：末尾恰 1 汇总节点，Conclusion==FinalResult
            var summary = nodes[nodes.Count - 1];
            Assert.Equal(outcome.FinalResult, summary.Conclusion);
            // 汇总 Premises ⊆ 该题组真实 id
            Assert.All(summary.Premises, p => Assert.Contains(p, g.Ids));
        }

        // 线程化确实在真实案例上生效（eval baseline 250 concept_choice 命中）
        Assert.True(solvedWithNodes > 0,
            $"没有任何解出的 concept_choice 携带 ReasoningNodes（solvedTotal={solvedTotal}）");
    }

    // ===================== helpers =====================

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

    private static (HashSet<string> idSet, List<Group> groups) LoadGeneralChoiceFacts()
    {
        var path = Path.Combine(RepoRoot, "data", "highschool", "general-choice-facts.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var stmts = new List<Stmt>();
        foreach (var el in doc.RootElement.GetProperty("statements").EnumerateArray())
        {
            var id = el.GetProperty("id").GetString() ?? "";
            var match = el.GetProperty("match").EnumerateArray().Select(x => x.GetString() ?? "").ToList();
            var stem = el.GetProperty("stemMatch").EnumerateArray().Select(x => x.GetString() ?? "").ToList();
            var verdict = el.GetProperty("verdict").GetBoolean();
            stmts.Add(new Stmt(id, match, stem, verdict));
        }
        var idSet = stmts.Select(s => s.Id).Where(id => !string.IsNullOrEmpty(id)).ToHashSet();
        var groups = stmts
            .Where(s => !string.IsNullOrEmpty(s.Id))
            .GroupBy(s => QuestionOf(s.Id))
            .Where(g => g.Count() >= 2)
            .Select(g => new Group(g.Key, g.ToList()))
            .ToList();
        return (idSet, groups);
    }

    private static string QuestionOf(string id)
    {
        var idx = id.LastIndexOf('-');
        return idx > 0 ? id.Substring(0, idx) : id;
    }

    private static string? BuildRawText(Group g)
    {
        // 仅处理选项字母为 A/B/C/D 的题组
        var byLetter = new SortedDictionary<string, Stmt>();
        foreach (var s in g.Stmts)
        {
            var letter = s.Id.Substring(s.Id.LastIndexOf('-') + 1);
            if (letter.Length != 1 || letter[0] < 'A' || letter[0] > 'D') return null;
            if (!byLetter.TryAdd(letter, s)) return null; // 同字母重复 → 跳过
        }
        if (byLetter.Count < 2) return null;
        if (g.Stmts.Any(s => s.StemMatch.Count == 0 || s.Match.Count == 0)) return null;

        var stem = string.Join("，", g.Stmts[0].StemMatch);
        var lines = new List<string> { stem };
        foreach (var kv in byLetter)
        {
            lines.Add($"{kv.Key}. {string.Join("，", kv.Value.Match)}");
        }
        return string.Join("\n", lines);
    }

    private sealed record Stmt(string Id, List<string> Match, List<string> StemMatch, bool Verdict);

    private sealed record Group(string Question, List<Stmt> Stmts)
    {
        public List<string> Ids => Stmts.Select(s => s.Id).ToList();
    }
}
