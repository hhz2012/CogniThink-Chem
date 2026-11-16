using CogniThink.Chem.Core;
using CogniThink.Chem.Core.Common;
using CogniThink.Chem.Core.Reactions;
using CogniThink.Chem.Core.Rules;
using Xunit;

namespace CogniThink.Chem.Tests;

/// <summary>
/// 反应知识库自洽测试：
/// 知识库是知识驱动推断的地基，其每条方程式必须通过 ChemicalCheckerService
/// 完整管线（白名单/守恒/配平/状态符号/离子拆写），保证"库自身即教材级正确"；
/// 同时验证索引检索、条件筛选与现象索引的行为。
/// </summary>
public class ReactionLibraryTests
{
    private static ReactionLibrary Library => ReactionLibrary.Default.Value;

    public static IEnumerable<object[]> AllEntries() =>
        Library.All.Select(e => new object[] { e.Id, e.Equation });

    // ---------- 规模与模块覆盖 ----------

    [Fact]
    public void Library_LoadsThreeModules_WithExpectedScale()
    {
        Assert.True(Library.Count >= 150, $"反应库规模不足：{Library.Count}");
        Assert.Contains(Library.All, e => e.Id.StartsWith("RX-IN-"));
        Assert.Contains(Library.All, e => e.Id.StartsWith("RX-OR-"));
        Assert.Contains(Library.All, e => e.Id.StartsWith("RX-PL-"));
    }

    // ---------- 核心自洽：每条方程式过完整校验管线 ----------

    [Theory]
    [MemberData(nameof(AllEntries))]
    public void EveryEntry_PassesChemicalChecker(string id, string equation)
    {
        var report = ChemicalCheckerService.Check(new CheckRequest { Equation = equation });
        Assert.True(report.IsValid, $"{id}: {equation}\n{report.FailureSummary()}");
    }

    [Fact]
    public void EveryEntry_HasTraceableMetadata()
    {
        foreach (var e in Library.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Id), "存在缺 id 的条目");
            Assert.False(string.IsNullOrWhiteSpace(e.Name), $"{e.Id}: 缺名称");
            Assert.NotEmpty(e.Reactants);
            Assert.NotEmpty(e.Products);
            Assert.False(string.IsNullOrWhiteSpace(e.KnowledgeId), $"{e.Id}: 缺知识点归属");
            Assert.False(string.IsNullOrWhiteSpace(e.Source), $"{e.Id}: 缺教材出处");
        }
    }

    // ---------- 索引与条件筛选 ----------

    [Fact]
    public void FindByReactants_SodiumOxygen_ReturnsConditionVariants()
    {
        var hits = Library.FindByReactants(new[] { "Na", "O2" });
        Assert.True(hits.Count >= 2);
        Assert.Contains(hits, e => e.Id == "RX-IN-NA-002");
        Assert.Contains(hits, e => e.Id == "RX-IN-NA-003");
    }

    [Fact]
    public void ConditionsSatisfied_SelectsCorrectVariant()
    {
        var hits = Library.FindByReactants(new[] { "Na", "O2" });
        var ignited = hits.Where(e => ReactionLibrary.ConditionsSatisfied(new[] { "点燃" }, e.Conditions)).ToList();
        var roomTemp = hits.Where(e => ReactionLibrary.ConditionsSatisfied(Array.Empty<string>(), e.Conditions)).ToList();

        // 常温查询只命中无条件条目；点燃条目需要"点燃"令牌，不满足常温查询
        Assert.Contains(roomTemp, e => e.Id == "RX-IN-NA-003");
        Assert.DoesNotContain(roomTemp, e => e.Id == "RX-IN-NA-002");

        // 点燃查询命中点燃条目；无条件条目恒满足（文档语义），
        // 推断引擎在多命中时应优先采纳条件特异的匹配
        Assert.Contains(ignited, e => e.Id == "RX-IN-NA-002");
        Assert.Contains(ignited, e => e.Id == "RX-IN-NA-003");
    }

    [Fact]
    public void Involving_Iron3Plus_ReturnsIonicReactions()
    {
        var hits = Library.Involving("Fe^3+");
        Assert.NotEmpty(hits);
        Assert.Contains(hits, e => e.Id == "RX-IN-ION-015");
        Assert.Contains(hits, e => e.Id == "RX-IN-ION-022");
    }

    // ---------- 现象索引 ----------

    [Fact]
    public void PhenomenaIndex_LoadsAndIsWellFormed()
    {
        var store = RuleStore.Load();
        var data = store.Query<PhenomenaData>("phenomena");
        Assert.NotEmpty(data.Items);
        foreach (var item in data.Items)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Id), "现象条目缺 id");
            Assert.False(string.IsNullOrWhiteSpace(item.Keyword), $"{item.Id}: 缺现象关键词");
            Assert.NotEmpty(item.Species);
        }
    }

    [Fact]
    public void PhenomenaIndex_CoversBenchmarkKeyEvidence()
    {
        var store = RuleStore.Load();
        var data = store.Query<PhenomenaData>("phenomena");

        // 成分推断基准题依赖的关键现象证据
        Assert.Contains(data.Items, i => i.Keyword.Contains("石蕊") && i.Species.Contains("NH3"));
        Assert.Contains(data.Items, i => i.Keyword.Contains("浅绿色") && i.Species.Contains("Fe^2+"));
        Assert.Contains(data.Items, i => i.Keyword.Contains("蓝色溶液") && i.Species.Contains("Cu^2+"));
        Assert.Contains(data.Items, i => i.Keyword.Contains("品红") && i.Species.Contains("SO2"));
        Assert.Contains(data.Items, i => i.Keyword.Contains("石灰水") && i.Species.Contains("CO2"));
    }
}
