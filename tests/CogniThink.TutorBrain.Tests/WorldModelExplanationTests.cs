using CogniThink.Chem.Core.WorldModel;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// P2-2 T3：世界模型 3 条示范解释链 + 富化不变性（AC-1/AC-3/AC-4/AC-7 + 不蒙数）。
/// 直接调用 Core WorldModel.Explain（绕过路由）与 Api WorldModelEnricher。
/// 真实题干取自 pdf_questions.json（pdf-213 / pdf-277），加一条构造的周期律题干。
/// 断言：节点非空 / 可溯源（RuleId 或 KnowledgeId 非空）/ 全 EvidenceLevel.Trend /
/// 定性无绝对值 / 富化不改 Ok·FinalResult / 无命中不臆造。
/// </summary>
public class WorldModelExplanationTests
{
    private static List<ReasoningNode> Enrich(string text, out EngineResponse resp)
    {
        resp = new EngineResponse { Ok = true, FinalResult = "判定结论占位", IsStudentAnswerCorrect = true };
        WorldModelEnricher.Enrich(resp, text);
        return resp.ExplanationNodes;
    }

    private static void AssertTrendNodes(IReadOnlyList<ReasoningNode> nodes, string label)
    {
        Assert.NotEmpty(nodes); // AC-4: 非空
        // 全为 Trend（帮助理解，不作判定依据）
        Assert.All(nodes, n => Assert.Equal(EvidenceLevel.Trend, n.EvidenceLevel));
        // 至少一个节点可溯源到 JSON 事实
        Assert.True(
            nodes.Any(n => !string.IsNullOrEmpty(n.RuleId) || !string.IsNullOrEmpty(n.KnowledgeId)),
            $"{label}: 没有任何节点携带 RuleId/KnowledgeId（假可溯源）");
        // Step 连续 1..N
        var steps = nodes.Select(n => n.Step).OrderBy(s => s).ToList();
        Assert.Equal(Enumerable.Range(1, steps.Count).ToList(), steps);
        // 全部带免责声明
        Assert.All(nodes, n => Assert.Contains(WorldModel.Disclaimer, n.Description));
        // 定性：不得出现 pH=数值 / Ksp 数值 / 电离能数值 等绝对值断言
        Assert.All(nodes, n =>
        {
            Assert.DoesNotMatch(@"pH\s*=?\s*\d", n.Description);
            Assert.DoesNotMatch(@"Ksp\s*=?\s*\d", n.Description);
        });
    }

    [Fact]
    public void M1_周期律递变_同主族同周期_产出两个Trend节点_AC4()
    {
        // 构造题干：同主族 + 同周期递变（命中 STR-002 + STR-001）
        var nodes = Enrich("关于元素周期律：同主族从上到下金属性增强，同周期从左到右非金属性增强。", out _);
        AssertTrendNodes(nodes, "M1");
        Assert.Equal(2, nodes.Count);
        var ruleIds = nodes.Select(n => n.RuleId).ToHashSet();
        Assert.Contains("STR-001", ruleIds);
        Assert.Contains("STR-002", ruleIds);
        Assert.All(nodes, n => Assert.Equal("KH-ST-02", n.KnowledgeId));
    }

    [Fact]
    public void M1_活动性金属顺序_产出RED001节点_AC4()
    {
        // 真实题 pdf-176：推断 Fe^3+、Cu^2+、MnO2 氧化性顺序（含 Fe、Cu 两个活动性金属）
        var stem = "根据“还原酸浸”“氧化”，推断 Fe^3+、Cu^2+、MnO2 的氧化性由强到弱的顺序为____。";
        var nodes = WorldModel.Explain(stem);
        Assert.NotEmpty(nodes);
        Assert.Contains(nodes, n => n.RuleId == "RED-001");
    }

    [Fact]
    public void M2_AgCl溶于氨水_产出配位溶解机理节点_真实题pdf213_AC4()
    {
        // 真实题 pdf-213 选项 B：AgCl 溶于氨水生成可溶性配合物
        var stem = "AgCl溶于氨水，AgCl与NH3反应生成了可溶性配合物[Ag(NH3)2]Cl。";
        var nodes = Enrich(stem, out _);
        AssertTrendNodes(nodes, "M2");
        Assert.Contains(nodes, n => n.RuleId == "SOL-EXC-001");
        // 机理节点引用 AgCl 溶解平衡反应（RX-PL-PPT-001，无 RuleId，带 KnowledgeId）
        Assert.Contains(nodes, n => n.KnowledgeId == "KH-RP-07" || n.Conclusion?.Contains("AgCl") == true);
    }

    [Fact]
    public void M3_Na2CO3水解显碱性_产出水解节点_真实题pdf277_AC4()
    {
        // 真实题 pdf-277：Na2CO3 溶液中存在水解平衡
        var stem = "浓度均为 0.2 mol/L 的 NaHCO3 和 Na2CO3 溶液中，均存在电离平衡和水解平衡。";
        var nodes = Enrich(stem, out _);
        AssertTrendNodes(nodes, "M3");
        Assert.Contains(nodes, n => n.Description.Contains("CO3^2-") && n.Description.Contains("水解"));
        Assert.Contains(nodes, n => n.KnowledgeId == "KH-RP-06"); // RX-PL-HYD-003 归属
        // 定性无 pH 数值声明
        Assert.Contains(nodes, n => n.Description.Contains("不涉及 pH 数值"));
    }

    [Fact]
    public void 无命中题干_不臆造解释节点_不蒙数()
    {
        // 域外题干 → 解释链无命中，返回空（不蒙数）
        var nodes = Enrich("今天天气怎么样？", out _);
        Assert.Empty(nodes);
    }

    [Fact]
    public void AC3_富化只读_不改Ok与FinalResult_判定字段不变()
    {
        var resp = new EngineResponse
        {
            Ok = true,
            FinalResult = "正确选项为 B",
            IsStudentAnswerCorrect = true,
            ReasoningNodes = { new ReasoningNode { Step = 1, Description = "原判定链", EvidenceLevel = EvidenceLevel.KnowledgeFact } },
        };
        var beforeOk = resp.Ok;
        var beforeFinal = resp.FinalResult;
        var beforeCorrect = resp.IsStudentAnswerCorrect;
        var beforeNodes = resp.ReasoningNodes.Count;

        WorldModelEnricher.Enrich(resp, "AgCl溶于氨水生成可溶性配合物。");

        // 判定字段不变（AC-3）
        Assert.Equal(beforeOk, resp.Ok);
        Assert.Equal(beforeFinal, resp.FinalResult);
        Assert.Equal(beforeCorrect, resp.IsStudentAnswerCorrect);
        // 判定链 ReasoningNodes 未被破坏
        Assert.Equal(beforeNodes, resp.ReasoningNodes.Count);
        Assert.Equal("原判定链", resp.ReasoningNodes[0].Description);
        // 解释链独立填充
        Assert.NotEmpty(resp.ExplanationNodes);
        Assert.All(resp.ExplanationNodes, n => Assert.Equal(EvidenceLevel.Trend, n.EvidenceLevel));
    }
}
