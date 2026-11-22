using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// B-MpDispatch 批：多部分分发推理节点聚合器 <see cref="MultiPartReasoningNodes.Merge"/> 的正确性与 R4 安全护栏。
/// 因 eval 生产路径 multi_part=0（0/27 K.1 子问可全覆盖，分发恒返回 null），传播正确性只能靠本类单测证明。
/// 覆盖 AC1（全子带节点传播 + 汇总）/ AC2（全空不臆造）/ AC3（部分空仅传有节点子）/ AC4（Step 连续）/ AC11（子节点原样克隆）。
/// </summary>
public class MultiPartReasoningNodesTests
{
    private static List<(int Index, EngineResponse SubResp)> Parts(params (int Index, EngineResponse SubResp)[] parts)
        => parts.ToList();

    private static EngineResponse Sub(params ReasoningNode[] nodes) => new() { ReasoningNodes = nodes.ToList() };

    // AC1 + AC4：全部子带节点 → 全部传播 + Step 从 1 连续重排 + 末尾恰 1 汇总节点
    [Fact]
    public void Merge_AllSubsHaveNodes_PropagatesAll_RenumbersSteps_AddsOneSummary()
    {
        var sub1 = Sub(
            new ReasoningNode { Step = 99, Description = "判据甲", Premises = new List<string> { "p1" }, KnowledgeId = "k-a", Conclusion = "A", EvidenceLevel = EvidenceLevel.KnowledgeFact },
            new ReasoningNode { Step = 98, Description = "判据乙", RuleId = "r-1", KnowledgeId = "k-b", Conclusion = "B", EvidenceLevel = EvidenceLevel.EngineDerived });
        var sub2 = Sub(
            new ReasoningNode { Step = 7, Description = "判据丙", KnowledgeId = "k-c", Conclusion = "C", EvidenceLevel = EvidenceLevel.KnowledgeFact },
            new ReasoningNode { Step = 8, Description = "重复锚点", KnowledgeId = "k-a", Conclusion = "D", EvidenceLevel = EvidenceLevel.KnowledgeFact }); // 重复 kid 验证汇总去重
        const string aggregated = "(1)AB (2)CD";

        var nodes = MultiPartReasoningNodes.Merge(Parts((1, sub1), (2, sub2)), aggregated);

        // 2 + 2 子节点 + 1 汇总 = 5
        Assert.Equal(5, nodes.Count);
        // AC4：Step 恰为 1..5，无跳步无重复
        Assert.Equal(Enumerable.Range(1, 5), nodes.Select(n => n.Step));
        // 子节点标注子问归属
        Assert.StartsWith("(1) ", nodes[0].Description);
        Assert.StartsWith("(1) ", nodes[1].Description);
        Assert.StartsWith("(2) ", nodes[2].Description);
        Assert.StartsWith("(2) ", nodes[3].Description);
        // AC11：子节点真值字段原样克隆（未被改写）
        Assert.Equal("k-a", nodes[0].KnowledgeId);
        Assert.Equal("p1", nodes[0].Premises.Single());
        Assert.Equal("r-1", nodes[1].RuleId);
        Assert.Equal("k-b", nodes[1].KnowledgeId);
        Assert.Equal(EvidenceLevel.EngineDerived, nodes[1].EvidenceLevel);
        Assert.Equal("B", nodes[1].Conclusion);
        // 原始子响应节点未被修改（克隆而非原地改）
        Assert.Equal(99, sub1.ReasoningNodes[0].Step);
        Assert.Equal(7, sub2.ReasoningNodes[0].Step);
        // AC1：末尾恰 1 个汇总节点
        var summary = nodes[4];
        Assert.Equal(aggregated, summary.Conclusion);
        Assert.Equal(EvidenceLevel.KnowledgeFact, summary.EvidenceLevel);
        Assert.Equal(new[] { "k-a", "k-b", "k-c" }, summary.Premises); // 去重 + 首次出现顺序
    }

    // AC2：全部子空 → 空列表，不臆造汇总节点
    [Fact]
    public void Merge_AllSubsEmpty_ReturnsEmpty_NoSummaryFabricated()
    {
        var sub1 = Sub();                     // ReasoningNodes 默认空
        var sub2 = Sub();
        var sub3 = new EngineResponse { ReasoningNodes = null! }; // 强制 null，验证防御

        var nodes = MultiPartReasoningNodes.Merge(Parts((1, sub1), (2, sub2), (3, sub3)), "(1)x (2)y (3)z");

        Assert.Empty(nodes); // R4：无锚点不发节点、不臆造汇总
    }

    // AC3：部分子空 → 仅带节点的子贡献，空子 0 节点，汇总 Premises 仅含实际 kid
    [Fact]
    public void Merge_PartialEmpty_OnlySubsWithNodesContribute()
    {
        var sub1 = Sub(new ReasoningNode { KnowledgeId = "k-a", Conclusion = "A" });
        var sub2 = Sub(); // 空
        var sub3 = Sub(new ReasoningNode { KnowledgeId = "k-b", Conclusion = "C" });
        const string aggregated = "(1)A (2)B (3)C";

        var nodes = MultiPartReasoningNodes.Merge(Parts((1, sub1), (2, sub2), (3, sub3)), aggregated);

        // 1 + 1 子节点 + 1 汇总 = 3
        Assert.Equal(3, nodes.Count);
        Assert.Equal(Enumerable.Range(1, 3), nodes.Select(n => n.Step)); // AC4
        // 空子问 (2) 贡献 0 节点
        var subKids = nodes.Where(n => !ReferenceEquals(n, nodes.Last())).Select(n => n.KnowledgeId).ToList();
        Assert.Equal(new[] { "k-a", "k-b" }, subKids);
        // 汇总仅含实际存在的 kid
        var summary = nodes.Last();
        Assert.Equal(aggregated, summary.Conclusion);
        Assert.Equal(new[] { "k-a", "k-b" }, summary.Premises);
    }

    // AC4 显式：Step 严格连续 1..K，即使子节点原 Step 混乱/跳号
    [Fact]
    public void Merge_RenumbersStepsContinuously_RegardlessOfOriginalSteps()
    {
        var sub1 = Sub(
            new ReasoningNode { Step = 5, KnowledgeId = "a" },
            new ReasoningNode { Step = 100, KnowledgeId = "b" });
        var sub2 = Sub(
            new ReasoningNode { Step = 0, KnowledgeId = "c" });

        var nodes = MultiPartReasoningNodes.Merge(Parts((1, sub1), (2, sub2)), "agg");

        Assert.Equal(4, nodes.Count); // 3 子 + 1 汇总
        Assert.Equal(Enumerable.Range(1, 4), nodes.Select(n => n.Step));
        Assert.Distinct(nodes.Select(n => n.Step));
    }

    // 边界：parts 为空 → 空列表（不臆造）
    [Fact]
    public void Merge_EmptyParts_ReturnsEmpty()
    {
        var nodes = MultiPartReasoningNodes.Merge(Parts(), "");
        Assert.Empty(nodes);
    }

    // AC11：汇总节点仅在存在子节点时追加；全空时无汇总
    [Fact]
    public void Merge_SummaryNodeExists_IfAndOnlyIf_AtLeastOneSubNode()
    {
        // 有子节点 → 1 子 + 1 汇总 = 2
        var withNodes = MultiPartReasoningNodes.Merge(Parts((1, Sub(new ReasoningNode { KnowledgeId = "k" }))), "x");
        Assert.Equal(2, withNodes.Count);
        Assert.Equal("x", withNodes.Last().Conclusion);

        // 全子空 → 无汇总
        var none = MultiPartReasoningNodes.Merge(Parts((1, Sub())), "x");
        Assert.Empty(none);
    }
}
