using CogniThink.Chem.Core.Data;
using CogniThink.Chem.Core.Knowledge;
using CogniThink.Chem.Core.Rules;
using Xunit;

namespace CogniThink.Chem.Tests;

/// <summary>
/// 知识检索服务测试：验证基于关键词的知识点与规则检索能力。
/// </summary>
public class KnowledgeQueryServiceTests
{
    private static readonly string DataRoot = Core.Data.DataRoot.Resolve();
    private static readonly KnowledgeBase Knowledge = ChemDataLoader.LoadKnowledge(DataRoot);
    private static readonly IReadOnlyList<Rule> Rules = ChemDataLoader.LoadRules(DataRoot);

    [Fact]
    public void Search_KnowledgeByName_ReturnsMatchingItem()
    {
        var result = KnowledgeQueryService.Search("勒夏特列", Knowledge, Rules);
        Assert.Contains(result.KnowledgeHits, h => h.Item.Id == "KH-RP-03");
    }

    [Fact]
    public void Search_RuleByName_ReturnsMatchingRule()
    {
        var result = KnowledgeQueryService.Search("电子守恒", Knowledge, Rules);
        Assert.Contains(result.RuleHits, h => h.Rule.Id == "CONS-ELEC-001");
    }

    [Fact]
    public void Search_ByPitfall_ReturnsMatchingItem()
    {
        var result = KnowledgeQueryService.Search("苯 溴水", Knowledge, Rules);
        Assert.Contains(result.KnowledgeHits, h => h.Item.Id == "KH-OR-01");
    }

    [Fact]
    public void Search_NoMatch_ReturnsEmpty()
    {
        var result = KnowledgeQueryService.Search("zzqqxx99ww", Knowledge, Rules);
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Search_EmptyQuery_ReturnsEmpty()
    {
        var result = KnowledgeQueryService.Search("", Knowledge, Rules);
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Search_ById_ReturnsMatchingItem()
    {
        var result = KnowledgeQueryService.Search("KH-IN-13", Knowledge, Rules);
        Assert.Contains(result.KnowledgeHits, h => h.Item.Id == "KH-IN-13");
    }

    [Fact]
    public void Search_ResultsAreSortedByScoreDescending()
    {
        var result = KnowledgeQueryService.Search("化学平衡", Knowledge, Rules);
        for (int i = 1; i < result.KnowledgeHits.Count; i++)
        {
            Assert.True(result.KnowledgeHits[i - 1].Score >= result.KnowledgeHits[i].Score);
        }
    }
}
