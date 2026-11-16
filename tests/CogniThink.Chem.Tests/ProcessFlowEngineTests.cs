using CogniThink.Chem.Core.Data;
using CogniThink.Chem.Core.Inference;
using Xunit;

namespace CogniThink.Chem.Tests;

// ===========================================================================
// 工业流程综合推断引擎：转化链回溯赋值（复用 ProcessInferenceEngine）
// + 每步"目的/原理"节点（按 PROC-001..004 规则条目原文机械匹配）。
// ===========================================================================

public class ProcessFlowEngineTests
{
    /// <summary>粗盐提纯：转化链唯一赋值，各步目的可溯源，过滤步目的如实留空。</summary>
    [Fact]
    public void 推断_粗盐提纯流程_唯一赋值且各步目的可溯源()
    {
        var result = ProcessFlowEngine.Infer(new ProcessFlowRequest
        {
            CandidateSpecies = new List<string>
            {
                "Na2SO4", "MgCl2", "CaCl2", "Na2CO3", "BaSO4", "Mg(OH)2", "CaCO3", "NaCl",
            },
            Steps = new List<ProcessFlowStep>
            {
                new() { Step = 1, From = "A", To = "D", Reagents = new List<string> { "BaCl2(过量)" }, Phenomenon = "产生白色沉淀", Purpose = "除杂：加过量BaCl2溶液除去Na2SO4" },
                new() { Step = 2, From = "B", To = "E", Reagents = new List<string> { "NaOH(过量)" }, Phenomenon = "产生白色沉淀", Purpose = "除杂：加过量NaOH溶液除去MgCl2" },
                new() { Step = 3, From = "C", To = "F", Reagents = new List<string> { "Na2CO3(过量)" }, Phenomenon = "产生白色沉淀", Purpose = "除杂：加过量Na2CO3溶液除去CaCl2" },
                new() { Step = 4, From = "悬浊液", To = "滤液", Purpose = "固液分离：过滤除去BaSO4、CaCO3、Mg(OH)2沉淀" },
                new() { Step = 5, From = "G", To = "H", Reagents = new List<string> { "HCl(适量)" }, Phenomenon = "有无色气泡产生", Purpose = "除杂：加适量盐酸除去滤液中过量的Na2CO3" },
                new() { Step = 6, From = "滤液", To = "精盐", Conditions = new List<string> { "蒸发" }, Purpose = "蒸发结晶：蒸发NaCl溶液获得精盐晶体" },
            },
        });

        Assert.Equal(InferenceStatus.Solved, result.Status);

        // 转化链唯一赋值。
        Assert.Equal("Na2SO4", result.Assignment["A"]);
        Assert.Equal("BaSO4", result.Assignment["D"]);
        Assert.Equal("NaCl", result.Assignment["H"]);

        // 化学转化步方程式全部来自知识库条目，逐条可溯源。
        Assert.Equal(4, result.StepResults.Count);
        Assert.All(result.StepResults, r => Assert.False(string.IsNullOrWhiteSpace(r.Equation)));

        // 完整推理链：赋值节点 + 每步目的节点，步骤数 ≥ 4 且每步 Description 非空。
        Assert.True(result.Steps.Count >= 4);
        Assert.All(result.Steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Description)));

        // 目的节点：命中唯一规则的步骤必须带 RuleId/KnowledgeId/Source（可溯源）。
        var knowledgeIds = ChemDataLoader.LoadKnowledge(DataRoot.Resolve())
            .Search().Select(k => k.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var purpose in result.Purposes.Where(p => p.Purpose.Length > 0))
        {
            Assert.StartsWith("PROC-", purpose.RuleId, StringComparison.Ordinal);
            Assert.True(knowledgeIds.Contains(purpose.KnowledgeId),
                $"目的节点知识点 {purpose.KnowledgeId} 不在知识库中");
            Assert.False(string.IsNullOrWhiteSpace(purpose.Source));
        }

        // 除杂步目的取 PROC-001 原文，蒸发结晶步取 PROC-004 分句。
        Assert.Contains("不引入新杂质", result.PurposeMap["1"]);
        Assert.StartsWith("蒸发结晶：", result.PurposeMap["6"], StringComparison.Ordinal);

        // 过滤步（第 4 步）无对应规则关键词：目的如实留空并说明。
        Assert.False(result.PurposeMap.ContainsKey("4"));
        Assert.Contains(result.Purposes, p => p.Step == 4 && p.Purpose == "");
    }

    /// <summary>转化链多解（铁价态未记录）：整题如实 Underdetermined，不猜测、不产出目的节点。</summary>
    [Fact]
    public void 推断_转化链多解_如实传播欠定()
    {
        var result = ProcessFlowEngine.Infer(new ProcessFlowRequest
        {
            CandidateSpecies = new List<string> { "Fe^2+", "Fe^3+", "Fe(OH)2", "Fe(OH)3" },
            Steps = new List<ProcessFlowStep>
            {
                new() { Step = 1, From = "A", To = "B", Reagents = new List<string> { "NaOH(过量)" }, Phenomenon = "产生沉淀（颜色未记录）", Purpose = "pH调控：加NaOH溶液沉淀浸出液中的铁离子" },
            },
        });

        Assert.Equal(InferenceStatus.Underdetermined, result.Status);
        Assert.Contains("转化链未能唯一确定", result.Message, StringComparison.Ordinal);
        Assert.Empty(result.Assignment);
        Assert.Empty(result.Purposes);
    }

    /// <summary>无任何流程步骤：如实 NoSolution。</summary>
    [Fact]
    public void 推断_无流程步骤_返回无解()
    {
        var result = ProcessFlowEngine.Infer(new ProcessFlowRequest
        {
            CandidateSpecies = new List<string> { "NaCl" },
        });

        Assert.Equal(InferenceStatus.NoSolution, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    /// <summary>沉淀转化关键词：目的引用 PROC-002 原文（更难溶判据，呼应 SOL-004）。</summary>
    [Fact]
    public void 推断_沉淀转化关键词_引用PROC002更难溶判据()
    {
        var result = ProcessFlowEngine.Infer(new ProcessFlowRequest
        {
            CandidateSpecies = new List<string> { "MgCl2", "Mg(OH)2" },
            Steps = new List<ProcessFlowStep>
            {
                new() { Step = 1, From = "A", To = "B", Reagents = new List<string> { "NaOH" }, Phenomenon = "产生白色沉淀", Purpose = "沉淀：加NaOH使Mg^2+沉淀" },
                new() { Step = 2, From = "悬浊液", To = "转化产物", Purpose = "沉淀转化：向悬浊液中加转化剂生成更难溶的沉淀" },
            },
        });

        Assert.Equal(InferenceStatus.Solved, result.Status);
        var hit = result.Purposes.Single(p => p.Step == 2);
        Assert.Equal("PROC-002", hit.RuleId);
        Assert.Contains("更难溶", hit.Purpose, StringComparison.Ordinal);
    }

    /// <summary>一步同时命中 PROC-004 两个分句（洗涤+循环）：目的不唯一，如实留空并说明。</summary>
    [Fact]
    public void 推断_多关键词命中_目的不唯一如实留空()
    {
        var result = ProcessFlowEngine.Infer(new ProcessFlowRequest
        {
            CandidateSpecies = new List<string> { "MgCl2", "Mg(OH)2" },
            Steps = new List<ProcessFlowStep>
            {
                new() { Step = 1, From = "A", To = "B", Reagents = new List<string> { "NaOH" }, Phenomenon = "产生白色沉淀", Purpose = "沉淀：加NaOH使Mg^2+沉淀" },
                new() { Step = 2, From = "悬浊液", To = "滤饼", Purpose = "洗涤并循环利用洗涤液" },
            },
        });

        Assert.Equal(InferenceStatus.Solved, result.Status);
        var hit = result.Purposes.Single(p => p.Step == 2);
        Assert.Equal("", hit.Purpose);
        Assert.False(result.PurposeMap.ContainsKey("2"));
        Assert.Contains(result.Steps, s => s.Description.Contains("目的不唯一", StringComparison.Ordinal));
    }
}
