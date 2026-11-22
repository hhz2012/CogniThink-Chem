using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// B-LlmMain Task 2：T1 传播点深拷贝（<see cref="GatewayReasoningNodes.Clone"/>，AC2）+
/// T3 酸顺序线程化（<see cref="AcidOrderReasoningNodes"/>，AC4）的正确性 / R4 安全测试。
/// 覆盖 AC2（克隆隔离/子空父空）、AC4（KnowledgeId 真实锚点/Step 连续/空命中留空）、AC7（判决字段不漂移）。
/// </summary>
public class GatewayReasoningNodesTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    // ==================== AC2：GatewayReasoningNodes.Clone 深拷贝 ====================

    [Fact]
    public void Clone_PreservesAllFields_AndDeepCopiesPremises()
    {
        var source = new List<ReasoningNode>
        {
            new()
            {
                Step = 1,
                Description = "源节点1",
                Premises = new List<string> { "p1", "p2" },
                RuleId = "R-1",
                KnowledgeId = "K-1",
                Conclusion = "c1",
                EvidenceLevel = EvidenceLevel.KnowledgeFact,
            },
            new()
            {
                Step = 2,
                Description = "源节点2",
                Premises = new List<string> { "p3" },
                KnowledgeId = null,
                Conclusion = "c2",
                EvidenceLevel = EvidenceLevel.EngineDerived,
            },
        };

        var cloned = GatewayReasoningNodes.Clone(source);

        Assert.Equal(2, cloned.Count);
        // 字段全部保留（Step 等原样）
        Assert.Equal(new[] { 1, 2 }, cloned.Select(n => n.Step));
        Assert.Equal(new[] { "源节点1", "源节点2" }, cloned.Select(n => n.Description));
        Assert.Equal(new[] { "R-1", null }, cloned.Select(n => n.RuleId));
        Assert.Equal(new[] { "K-1", null }, cloned.Select(n => n.KnowledgeId));
        Assert.Equal(new[] { "c1", "c2" }, cloned.Select(n => n.Conclusion));
        Assert.Equal(new[] { EvidenceLevel.KnowledgeFact, EvidenceLevel.EngineDerived },
            cloned.Select(n => n.EvidenceLevel));

        // 深拷贝：源 Premises 变更不影响克隆
        source[0].Premises.Add("MUTATED");
        Assert.Equal(new[] { "p1", "p2" }, cloned[0].Premises);
        // 克隆 Premises 变更不影响源
        cloned[1].Premises.Add("CLONE_MUT");
        Assert.Equal(new[] { "p3" }, source[1].Premises);
        // 节点实例不同（非共享引用）
        Assert.NotSame(source[0], cloned[0]);
    }

    [Fact]
    public void Clone_NullOrEmpty_ReturnsEmptyNotNull()
    {
        // CS8601 教训：绝不返回 null
        Assert.NotNull(GatewayReasoningNodes.Clone(null));
        Assert.Empty(GatewayReasoningNodes.Clone(null));
        Assert.NotNull(GatewayReasoningNodes.Clone(new List<ReasoningNode>()));
        Assert.Empty(GatewayReasoningNodes.Clone(new List<ReasoningNode>()));
    }

    [Fact]
    public void Clone_SkipsNullElements()
    {
        var list = new List<ReasoningNode> { null!, new ReasoningNode { Step = 1, Description = "x" } };
        var cloned = GatewayReasoningNodes.Clone(list);
        Assert.Single(cloned);
        Assert.Equal("x", cloned[0].Description);
    }

    [Fact]
    public void Clone_OutcomeNodes_PropagationSemantics()
    {
        // 模拟 T1 传播：子 Outcome 带节点 → 父 EngineResponse 取克隆；子空 → 父空
        var outcome = new ChoiceVerifier.Outcome(
            "B", "正确选项为 B", new List<string> { "r" }, "");
        // 给 outcome 注入节点（模拟 Task 1 FactChoiceMatcher 已写节点）
        outcome.ReasoningNodes.Add(new ReasoningNode
        {
            Step = 1, Description = "d", Premises = new List<string> { "p" },
            KnowledgeId = "GC-001-B", Conclusion = "B", EvidenceLevel = EvidenceLevel.KnowledgeFact,
        });

        var propagated = GatewayReasoningNodes.Clone(outcome.ReasoningNodes);
        Assert.Single(propagated);
        Assert.Equal("GC-001-B", propagated[0].KnowledgeId);

        // 子空（未线程化的校验器）→ 父空
        var empty = new ChoiceVerifier.Outcome("A", "x", new List<string>(), "");
        Assert.Empty(GatewayReasoningNodes.Clone(empty.ReasoningNodes));
    }

    // ==================== AC4：AcidOrderReasoningNodes.Build 结构正确性 ====================

    [Fact]
    public void AcidBuild_WithHits_ProducesAcidNodesPlusOneSummary()
    {
        var acids = new List<AcidOrderReasoningNodes.AcidHit>
        {
            new("HClO", "HClO", "3.0×10^-8"),
            new("CH3COOH", "CH3COOH", "1.75×10^-5"),
            new("HF", "HF", "6.6×10^-4"),
        };
        var nodes = AcidOrderReasoningNodes.Build(acids, "由弱到强：HClO < CH3COOH < HF");

        // 3 酸节点 + 1 汇总 = 4
        Assert.Equal(4, nodes.Count);
        Assert.Equal(Enumerable.Range(1, 4), nodes.Select(n => n.Step));

        var acidNodes = nodes.Take(3).ToList();
        // KnowledgeId == kaKey（真实锚点），EvidenceLevel KnowledgeFact，Conclusion == 酸名
        Assert.Equal(new[] { "HClO", "CH3COOH", "HF" }, acidNodes.Select(n => n.KnowledgeId));
        Assert.Equal(new[] { "HClO", "CH3COOH", "HF" }, acidNodes.Select(n => n.Conclusion));
        Assert.All(acidNodes, n => Assert.Equal(EvidenceLevel.KnowledgeFact, n.EvidenceLevel));
        Assert.All(acidNodes, n => Assert.True(
            n.Premises.Any(p => p.Contains(n.KnowledgeId!)),
            $"acid node premises should reference kaKey {n.KnowledgeId}"));

        // 汇总：Conclusion == FinalResult，Premises == 去重非空 kaKey
        var summary = nodes[nodes.Count - 1];
        Assert.Equal("由弱到强：HClO < CH3COOH < HF", summary.Conclusion);
        Assert.Equal(new[] { "HClO", "CH3COOH", "HF" }, summary.Premises);
    }

    [Fact]
    public void AcidBuild_AllKaKeysEmptyOrWhitespace_ReturnsEmpty_NoSummary()
    {
        var acids = new List<AcidOrderReasoningNodes.AcidHit>
        {
            new("HClO", "", "3.0×10^-8"),
            new("HF", "   ", "6.6×10^-4"),
        };
        // R4：全空锚点 → 空列表，不臆造汇总
        Assert.Empty(AcidOrderReasoningNodes.Build(acids, "x"));
        Assert.Empty(AcidOrderReasoningNodes.Build(new List<AcidOrderReasoningNodes.AcidHit>(), "x"));
    }

    [Fact]
    public void AcidBuild_PartialWhitespace_FiltersBlank_KeepsReal()
    {
        var acids = new List<AcidOrderReasoningNodes.AcidHit>
        {
            new("HClO", "HClO", "3.0×10^-8"),
            new("X", "  ", "1.0×10^-5"), // 空白锚点被过滤
        };
        var nodes = AcidOrderReasoningNodes.Build(acids, "x");
        // 1 真实酸节点 + 1 汇总 = 2
        Assert.Equal(2, nodes.Count);
        Assert.Equal("HClO", nodes[0].KnowledgeId);
        Assert.Equal(new[] { "HClO" }, nodes[1].Premises);
    }

    // ==================== AC4 端到端：真实 JSON kaKey 经 TryOrderAcidStrength 流入节点 ====================

    [Fact]
    public void TryOrderAcidStrength_ThreadsRealKaKeys_AndPreservesFinalResult()
    {
        var kaKeys = LoadAcidKaKeys();
        Assert.NotEmpty(kaKeys);
        // CH3COOH / HClO / HF 都是 JSON 中的真实 Ka1 键
        Assert.Contains("CH3COOH", kaKeys);
        Assert.Contains("HClO", kaKeys);
        Assert.Contains("HF", kaKeys);

        var raw = "下列酸的酸性由弱到强排序：CH3COOH、HClO、HF";
        var resp = InvokeTryOrderAcidStrength(raw);

        Assert.NotNull(resp);
        Assert.True(resp!.Ok);

        // AC7：FinalResult 字节级不变（仅提取为变量，字符串构造不变）
        var expected = "由弱到强：HClO < CH3COOH < HF（依据 25 ℃ 电离常数 Ka：HClO=3.0×10^-8、CH3COOH=1.75×10^-5、HF=6.6×10^-4）";
        Assert.Equal(expected, resp.FinalResult);

        // AC4：节点非空，每个 KnowledgeId 都是 acid-constants.json 中真实存在的 ka 键（不臆造）
        var nodes = resp.ReasoningNodes;
        Assert.NotEmpty(nodes);
        Assert.All(nodes, n =>
        {
            if (!string.IsNullOrEmpty(n.KnowledgeId))
                Assert.Contains(n.KnowledgeId, kaKeys);
            Assert.Equal(EvidenceLevel.KnowledgeFact, n.EvidenceLevel);
        });

        // Step 连续 1..N
        Assert.Equal(Enumerable.Range(1, nodes.Count), nodes.Select(n => n.Step));
        // 3 酸节点 + 1 汇总 = 4
        Assert.Equal(4, nodes.Count);
        // 酸节点 KnowledgeId == 命中酸的 kaKey（顺序：升序 = 弱到强 → HClO, CH3COOH, HF）
        Assert.Equal(new[] { "HClO", "CH3COOH", "HF" }, nodes.Take(3).Select(n => n.KnowledgeId));
        // 汇总 Conclusion == FinalResult
        Assert.Equal(resp.FinalResult, nodes[nodes.Count - 1].Conclusion);
    }

    [Fact]
    public void TryOrderAcidStrength_StrongToWeak_ThreadsRealKaKeys()
    {
        var kaKeys = LoadAcidKaKeys();
        var raw = "由强到弱排序 HF、CH3COOH、HClO 的酸性";
        var resp = InvokeTryOrderAcidStrength(raw);
        Assert.NotNull(resp);
        Assert.StartsWith("由强到弱", resp!.FinalResult);
        // 强到弱：HF > CH3COOH > HClO
        Assert.Equal(new[] { "HF", "CH3COOH", "HClO" }, resp.ReasoningNodes.Take(3).Select(n => n.KnowledgeId));
        Assert.All(resp.ReasoningNodes, n =>
        {
            if (!string.IsNullOrEmpty(n.KnowledgeId))
                Assert.Contains(n.KnowledgeId, kaKeys);
        });
    }

    [Fact]
    public void TryOrderAcidStrength_NoDirection_ReturnsNull()
    {
        // R4 / 域外：缺方向 → null（不蒙数）
        Assert.Null(InvokeTryOrderAcidStrength("比较 CH3COOH 和 HClO 的酸性"));
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

    private static HashSet<string> LoadAcidKaKeys()
    {
        var path = Path.Combine(RepoRoot, "data", "highschool", "acid-constants.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var keys = new HashSet<string>();
        foreach (var prop in doc.RootElement.GetProperty("ka").EnumerateObject())
        {
            keys.Add(prop.Name);
        }
        return keys;
    }

    private static EngineResponse? InvokeTryOrderAcidStrength(string raw)
    {
        var method = typeof(LlmGatewayService).GetMethod(
            "TryOrderAcidStrength", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (EngineResponse?)method!.Invoke(null, new object[] { raw });
    }
}
