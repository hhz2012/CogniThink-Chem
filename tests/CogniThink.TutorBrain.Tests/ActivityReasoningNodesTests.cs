using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// B-LlmMain Task 4 组 3：金属活动性顺序外置（activity-series.json）+ 线程化（AC5/AC6/AC7）。
/// 断言：① 构建器节点 KnowledgeId 指向真实文件锚点（R4 不臆造）、RuleId 派生稳定、RED-001 来自 rules.json；
/// ② TryJudgeReactionFeasibility 行为等价（判决字段不变）且活动性分支带节点、非活动性分支留空（R4）；
/// ③ activity-series.json 与原硬编码数组字节级一致（15 位、H 在第 10 位）。
/// </summary>
public class ActivityReasoningNodesTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    // ==================== 构建器直接单测（R4 锚点真实性） ====================

    [Fact]
    public void HydrogenPosition_ZnBeforeH_KnowledgeIdAndRuleIdReal()
    {
        var nodes = ActivityReasoningNodes.HydrogenPosition("Zn", beforeH: true);
        Assert.Single(nodes);
        var n = nodes[0];
        Assert.Equal(1, n.Step);
        Assert.Equal("activity-series", n.KnowledgeId);
        Assert.Equal("activity-series:Zn>H", n.RuleId);
        Assert.Equal(EvidenceLevel.KnowledgeFact, n.EvidenceLevel);
        Assert.Contains("Zn", n.Conclusion);
        Assert.Contains("H 之前", n.Conclusion);
        Assert.True(ActivitySeriesFileExists()); // R4：KnowledgeId 对应真实文件
    }

    [Fact]
    public void HydrogenPosition_CuAfterH_RuleIdReversed()
    {
        var n = ActivityReasoningNodes.HydrogenPosition("Cu", beforeH: false)[0];
        Assert.Equal("activity-series:H>Cu", n.RuleId);
        Assert.Contains("H 之后", n.Conclusion);
    }

    [Fact]
    public void MetalDisplacement_FeCu_TwoNodes_IncludingRed001()
    {
        var nodes = ActivityReasoningNodes.MetalDisplacement("Fe", "Cu");
        Assert.Equal(2, nodes.Count);

        var n1 = nodes[0];
        Assert.Equal(1, n1.Step);
        Assert.Equal("activity-series", n1.KnowledgeId);
        Assert.Equal("activity-series:Fe>Cu", n1.RuleId);
        Assert.Contains("Fe", n1.Conclusion);
        Assert.Contains("Cu", n1.Conclusion);

        var n2 = nodes[1];
        Assert.Equal(2, n2.Step);
        Assert.Equal("RED-001", n2.RuleId);
        Assert.Equal("KH-IN-06", n2.KnowledgeId); // rules.json RED-001 的 knowledgeId
        Assert.Contains("置换", n2.Conclusion);
        Assert.True(RuleDeclared("RED-001")); // R4：RuleId 真实声明于 rules.json
    }

    [Fact]
    public void HighlyActiveWaterFirst_Na_RuleIdNaBeforeH()
    {
        var n = ActivityReasoningNodes.HighlyActiveWaterFirst("Na")[0];
        Assert.Equal("activity-series:Na>H", n.RuleId);
        Assert.Contains("Na", n.Conclusion);
    }

    [Fact]
    public void InertMetalOxidizingAcid_Pt_RuleIdHAfterPt()
    {
        var n = ActivityReasoningNodes.InertMetalOxidizingAcid("Pt")[0];
        Assert.Equal("activity-series:H>Pt", n.RuleId);
        Assert.Contains("Pt", n.Conclusion);
    }

    [Fact]
    public void ActiveMetalOxidizingAcid_Cu_RuleIdCuBeforePt()
    {
        var n = ActivityReasoningNodes.ActiveMetalOxidizingAcid("Cu")[0];
        Assert.Equal("activity-series:Cu>Pt", n.RuleId);
    }

    // ==================== FeasibilityRuleReasoningNodes 构建器直接单测（T5 补线程 R4 锚点） ====================

    [Fact]
    public void OxidizingAcidNoH2_HNO3_KnowledgeIdRealAnchor()
    {
        var nodes = FeasibilityRuleReasoningNodes.OxidizingAcidNoH2("硝酸");
        Assert.Single(nodes);
        var n = nodes[0];
        Assert.Equal("redox-no3-dilute", n.KnowledgeId);
        Assert.Equal(EvidenceLevel.KnowledgeFact, n.EvidenceLevel);
        Assert.Contains("硝酸", n.Premises[0]);
        Assert.True(RedoxReductionProductExists("redox-no3-dilute")); // R4
    }

    [Fact]
    public void OxidizingAcidNoH2_ConcH2SO4_KnowledgeIdRealAnchor()
    {
        var n = FeasibilityRuleReasoningNodes.OxidizingAcidNoH2("浓硫酸")[0];
        Assert.Equal("redox-h2so4-concentrated", n.KnowledgeId);
        Assert.True(RedoxReductionProductExists("redox-h2so4-concentrated")); // R4
    }

    [Fact]
    public void FeDiluteAcidValence_KnowledgeIdKhIn06_RealAnchor()
    {
        var nodes = FeasibilityRuleReasoningNodes.FeDiluteAcidValence();
        Assert.Single(nodes);
        var n = nodes[0];
        Assert.Equal("KH-IN-06", n.KnowledgeId);
        Assert.Equal(EvidenceLevel.KnowledgeFact, n.EvidenceLevel);
        Assert.Contains("Fe2+", n.Conclusion);
        Assert.True(KnowledgeItemExists("KH-IN-06")); // R4
    }

    // ==================== TryJudgeReactionFeasibility 端到端（行为等价 + 节点贯通） ====================

    private static EngineResponse? InvokeFeasibility(string stem)
    {
        var svc = (LlmGatewayService)RuntimeHelpers.GetUninitializedObject(typeof(LlmGatewayService));
        var m = typeof(LlmGatewayService).GetMethod(
            "TryJudgeReactionFeasibility", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(m);
        return (EngineResponse?)m!.Invoke(svc, new object[] { stem });
    }

    [Fact]
    public void Feasibility_ZnDiluteHCl_ProducesH2_WithActivityNode()
    {
        // 用「氢气」触发 asksForH2（既有 \bH2\b 正则在 H2 后接 CJK 时无词边界，属原代码行为，AC7 不改动）
        var r = InvokeFeasibility("锌和稀盐酸反应能生成氢气吗");
        Assert.NotNull(r);
        Assert.True(r!.Ok == true, r.FinalResult);
        Assert.Contains("能生成H2", r.FinalResult);
        Assert.Contains("Zn", r.FinalResult);

        var nodes = r.ReasoningNodes;
        Assert.Single(nodes);
        Assert.Equal("activity-series", nodes[0].KnowledgeId);
        Assert.Equal("activity-series:Zn>H", nodes[0].RuleId);
    }

    [Fact]
    public void Feasibility_CuDiluteHCl_CannotReact_WithActivityNode()
    {
        var r = InvokeFeasibility("铜和稀盐酸能反应吗");
        Assert.NotNull(r);
        Assert.False(r!.Ok == true);
        Assert.Contains("不能反应", r.FinalResult);
        Assert.Contains("Cu", r.FinalResult);

        Assert.Single(r.ReasoningNodes);
        Assert.Equal("activity-series:H>Cu", r.ReasoningNodes[0].RuleId);
    }

    [Fact]
    public void Feasibility_FeDisplacesCuSO4_TwoNodes_IncludingRed001()
    {
        var r = InvokeFeasibility("Fe和CuSO4溶液能反应吗");
        Assert.NotNull(r);
        Assert.True(r!.Ok == true, r.FinalResult);
        Assert.Contains("能反应", r.FinalResult);
        Assert.Contains("置换", r.FinalResult);

        var nodes = r.ReasoningNodes;
        Assert.Equal(2, nodes.Count);
        Assert.Equal("activity-series:Fe>Cu", nodes[0].RuleId);
        Assert.Equal("RED-001", nodes[1].RuleId);
    }

    [Fact]
    public void Feasibility_FeDiluteAcidValence_ThreadedWithKhIn06()
    {
        // 规则3（Fe 价态）：T5 审计发现 KH-IN-06 pitfall 直接支撑 → 挂真实锚点（原留空已补线程）
        var r = InvokeFeasibility("铁和稀硫酸反应生成Fe2(SO4)3对吗");
        Assert.NotNull(r);
        Assert.False(r!.Ok == true);
        Assert.Contains("不能生成+3价", r.FinalResult);

        Assert.Single(r.ReasoningNodes);
        Assert.Equal("KH-IN-06", r.ReasoningNodes[0].KnowledgeId);
        Assert.Equal(EvidenceLevel.KnowledgeFact, r.ReasoningNodes[0].EvidenceLevel);
        Assert.Contains("Fe2+", r.ReasoningNodes[0].Conclusion);
        Assert.True(KnowledgeItemExists("KH-IN-06")); // R4：锚点真实存在于 knowledge.json
    }

    [Fact]
    public void Feasibility_OxidizingAcidHNO3_NoH2_ThreadedWithRedoxNo3()
    {
        // 规则1 氧化性酸（硝酸）不生 H2：T5 审计补线程 → redox-rules.json 条目 redox-no3-dilute
        var r = InvokeFeasibility("铜和硝酸反应能生成氢气吗");
        Assert.NotNull(r);
        Assert.False(r!.Ok == true);
        Assert.Contains("不能生成H2", r.FinalResult);

        Assert.Single(r.ReasoningNodes);
        Assert.Equal("redox-no3-dilute", r.ReasoningNodes[0].KnowledgeId);
        Assert.True(RedoxReductionProductExists("redox-no3-dilute")); // R4：锚点真实
    }

    [Fact]
    public void Feasibility_OxidizingAcidConcH2SO4_NoH2_ThreadedWithRedoxH2SO4()
    {
        // 规则1 氧化性酸（浓硫酸）不生 H2 → redox-rules.json 条目 redox-h2so4-concentrated
        var r = InvokeFeasibility("铜和浓硫酸反应能生成氢气吗");
        Assert.NotNull(r);
        Assert.False(r!.Ok == true);
        Assert.Contains("不能生成H2", r.FinalResult);

        Assert.Single(r.ReasoningNodes);
        Assert.Equal("redox-h2so4-concentrated", r.ReasoningNodes[0].KnowledgeId);
        Assert.True(RedoxReductionProductExists("redox-h2so4-concentrated"));
    }

    // ==================== AC5：外置文件与原硬编码字节级等价 ====================

    [Fact]
    public void ActivitySeriesJson_Exists_AndMatchesOriginalHardcodedArray()
    {
        var path = Path.Combine(RepoRoot, "data", "highschool", "activity-series.json");
        Assert.True(File.Exists(path), $"外置事实文件缺失：{path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var series = doc.RootElement.GetProperty("series").EnumerateArray().Select(e => e.GetString()!).ToList();

        // 与原方法内硬编码数组字节级一致：15 位，H 在第 10 位（index 9）
        Assert.Equal(new[] { "K", "Ca", "Na", "Mg", "Al", "Zn", "Fe", "Sn", "Pb", "H", "Cu", "Hg", "Ag", "Pt", "Au" }, series);
        Assert.Equal("H", series[9]);
    }

    // ==================== helpers ====================

    private static bool ActivitySeriesFileExists()
        => File.Exists(Path.Combine(RepoRoot, "data", "highschool", "activity-series.json"));

    private static bool RuleDeclared(string id)
    {
        var path = Path.Combine(RepoRoot, "data", "rules", "rules.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var r in doc.RootElement.GetProperty("items").EnumerateArray())
        {
            if (r.TryGetProperty("id", out var p) && p.GetString() == id) return true;
        }
        return false;
    }

    private static bool KnowledgeItemExists(string id)
    {
        var path = Path.Combine(RepoRoot, "data", "knowledge", "knowledge.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var item in doc.RootElement.GetProperty("items").EnumerateArray())
        {
            if (item.TryGetProperty("id", out var p) && p.GetString() == id) return true;
        }
        return false;
    }

    private static bool RedoxReductionProductExists(string id)
    {
        var path = Path.Combine(RepoRoot, "data", "knowledge", "redox-rules.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var r in doc.RootElement.GetProperty("reductionProducts").EnumerateArray())
        {
            if (r.TryGetProperty("id", out var p) && p.GetString() == id) return true;
        }
        return false;
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
}
