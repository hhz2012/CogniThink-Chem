using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// B-LlmMain Task 3：11 个自有 JSON choice 校验器的线程化审计验证（AC4 / AC5 / AC7）。
/// 覆盖：5 个 THREAD 校验器（Equilibrium/MultiSelect/Classification/Na/Counting 事实表）
/// 命中时节点 KnowledgeId 为对应 JSON 真实键（R4 不臆造）；Counting 纯引擎枚举路径节点留空（R4）；
/// 判决字段 AnswerLetter 与既有测试完全一致（AC7 行为字节级不变）。
/// 6 个 LEAVE 校验器（Transformation/IonicEquation/Redox/Molecule/Curve/Stoichiometry）
/// 未被改动，保持默认空 ReasoningNodes（R4）。
/// </summary>
public class ChoiceValidatorNodesTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();
    private static EngineRequest Req(string raw) => new() { TaskType = "choice", RawText = raw };

    private static string PdfQuestion(string id) =>
        GatewayVsLlmEvaluationService.GenPdfQuestions().First(x => x.Id == id).Question;

    // ==================== THREAD：Equilibrium（rule.Id，显式 id） ====================

    [Fact]
    public void Equilibrium_HitRule_ThreadsRealRuleId()
    {
        var validIds = CollectField("equilibrium-rules.json", "id");
        Assert.NotEmpty(validIds);
        Assert.Contains("eq-005-c", validIds);

        var o = EquilibriumChoiceValidator.TryDecide(Req(PdfQuestion("pdf-005")));
        Assert.NotNull(o);
        Assert.Equal("C", o!.AnswerLetter); // AC7：判决不变

        Assert.NotEmpty(o.ReasoningNodes);
        AssertAllNodesHaveRealAnchors(o.ReasoningNodes, validIds);
    }

    // ==================== THREAD：Classification（formula，自然稳定键） ====================

    [Fact]
    public void Classification_BareSubstanceOption_ThreadsRealFormula()
    {
        var validFormulas = CollectField("substance-classification.json", "formula");
        Assert.NotEmpty(validFormulas);
        // 这些是 substance-classification.json 中真实存在的 formula
        Assert.Contains("SO2", validFormulas);
        Assert.Contains("HCl", validFormulas);

        // P4：类别来自题干，选项为裸物质 → 每选项命中一个物质 → formula 锚点
        var raw = "下列物质属于非电解质的是（ ）\nA. 硫酸钡\nB. 二氧化硫\nC. 氯化氢\nD. 氯气";
        var o = ClassificationValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("B", o!.AnswerLetter); // AC7

        Assert.NotEmpty(o.ReasoningNodes);
        AssertAllNodesHaveRealAnchors(o.ReasoningNodes, validFormulas);
        // 命中的 formula 应覆盖至少 SO2（正确项 B=二氧化硫）
        Assert.Contains(o.ReasoningNodes, n => n.KnowledgeId == "SO2");
    }

    // ==================== THREAD：Na（formula，自然稳定键） ====================

    [Fact]
    public void Na_ParticleOption_ThreadsRealFormula()
    {
        var validFormulas = CollectField("na-particle-facts.json", "formula");
        Assert.NotEmpty(validFormulas);
        Assert.Contains("H2O", validFormulas);
        Assert.Contains("CO2", validFormulas);

        var raw = "设 NA 为阿伏加德罗常数的值。下列说法正确的是（ ）\n" +
                  "A. 1 mol H2O 中含有的电子数为 10NA\n" +
                  "B. 1 mol H2O 中含有的中子数为 10NA\n" +
                  "C. 1 mol CO2 中含有的原子数为 2NA\n" +
                  "D. 1 mol NH3 中含有的质子数为 7NA";
        var o = NaChoiceValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("A", o!.AnswerLetter); // AC7

        Assert.NotEmpty(o.ReasoningNodes);
        AssertAllNodesHaveRealAnchors(o.ReasoningNodes, validFormulas);
    }

    // ==================== THREAD：MultiSelect（JudgeWithIds → 底层 FactStatement.Id） ====================

    [Fact]
    public void MultiSelect_ThreadsRealFactIdsFromJudgeWithIds()
    {
        var validIds = AllChoiceFactIds();
        Assert.NotEmpty(validIds);

        // pdf-225 是既有的已解多选真题（答案 CD）；剥去标记仍可解
        var raw = PdfQuestion("pdf-225").Replace("双选", "").Replace("多选", "");
        var o = MultiSelectChoiceValidator.TryDecide(new EngineRequest { RawText = raw });
        Assert.NotNull(o);
        Assert.Equal("CD", o!.AnswerLetter); // AC7

        Assert.NotEmpty(o.ReasoningNodes);
        AssertAllNodesHaveRealAnchors(o.ReasoningNodes, validIds);
    }

    // ==================== THREAD：Counting 事实表路径（CountingFact.Id） ====================

    [Fact]
    public void Counting_FactTablePath_ThreadsRealFactId()
    {
        var validIds = CollectField("counting-facts.json", "id");
        Assert.NotEmpty(validIds);
        Assert.Contains("BENZENE-1CL", validIds);

        var raw = "下列说法正确的是（ ）\n" +
                  "A. 立方烷的二氯代物有 3 种\n" +
                  "B. 立方烷的二氯代物有 2 种\n" +
                  "C. 立方烷的六氯代物有 2 种\n" +
                  "D. 苯的二氯代物有 2 种";
        var o = CountingChoiceValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("A", o!.AnswerLetter); // AC7

        Assert.NotEmpty(o.ReasoningNodes);
        AssertAllNodesHaveRealAnchors(o.ReasoningNodes, validIds);
    }

    // ==================== R4：Counting 纯引擎枚举路径 → 节点留空，不臆造 ====================

    [Fact]
    public void Counting_EnumerationPath_NoAnchor_ReturnsEmptyNodes()
    {
        // 羧酸异构计数是 ConstitutionalIsomerCounter 纯引擎计算，无 JSON 事实锚点 → ReasoningNodes 必须为空
        var raw = "下列说法正确的是（ ）\n" +
                  "A. C5H10O2 属于羧酸的同分异构体有 4 种\n" +
                  "B. C4H8O2 属于羧酸的同分异构体有 3 种\n" +
                  "C. C3H6O2 属于羧酸的同分异构体有 3 种\n" +
                  "D. C6H12O2 属于羧酸的同分异构体有 4 种";
        var o = CountingChoiceValidator.TryDecide(Req(raw));
        Assert.NotNull(o);
        Assert.Equal("A", o!.AnswerLetter); // AC7：判决不变
        Assert.Empty(o.ReasoningNodes);       // R4：无锚点不发节点
    }

    // ===================== R4 通用断言 =====================

    private static void AssertAllNodesHaveRealAnchors(List<ReasoningNode> nodes, HashSet<string> validAnchors)
    {
        // 每个带 KnowledgeId 的节点，其 id 必须真实存在于对应 JSON（不臆造）
        Assert.All(nodes, n =>
        {
            Assert.Equal(EvidenceLevel.KnowledgeFact, n.EvidenceLevel);
            if (!string.IsNullOrEmpty(n.KnowledgeId))
                Assert.Contains(n.KnowledgeId, validAnchors);
        });
        // Step 从 1 连续无跳步
        Assert.Equal(Enumerable.Range(1, nodes.Count), nodes.Select(n => n.Step));
    }

    // ===================== JSON 加载 helper =====================

    private static HashSet<string> CollectField(string fileName, string field)
    {
        var path = Path.Combine(RepoRoot, "data", "highschool", fileName);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var set = new HashSet<string>();
        Walk(doc.RootElement, field, set);
        return set;
    }

    private static HashSet<string> AllChoiceFactIds()
    {
        var files = new[]
        {
            "general-choice-facts.json", "experiment-facts.json", "organic-facts.json",
            "structure-periodic-facts.json", "choice-curated-facts.json", "judgment-statement-facts.json"
        };
        var set = new HashSet<string>();
        foreach (var f in files)
            foreach (var id in CollectField(f, "id"))
                set.Add(id);
        return set;
    }

    private static void Walk(JsonElement el, string field, HashSet<string> set)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty(field, out var p) && p.ValueKind == JsonValueKind.String)
            {
                var s = p.GetString();
                if (!string.IsNullOrWhiteSpace(s)) set.Add(s);
            }
            foreach (var prop in el.EnumerateObject()) Walk(prop.Value, field, set);
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray()) Walk(item, field, set);
        }
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
