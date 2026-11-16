using CogniThink.Chem.Core.Chemistry;
using CogniThink.Chem.Core.Common;
using CogniThink.Chem.Core.Ionic;
using CogniThink.Chem.Core.Rules;
using Xunit;

namespace CogniThink.Chem.Tests;

/// <summary>
/// 高中 JSON 规则库与读取服务测试：
/// RuleStore 加载、溶解性表、金属活动性顺序、白名单守卫。
/// </summary>
public class HighSchoolRulesTests
{
    // ---------- RuleStore 加载 ----------

    [Fact]
    public void RuleStore_LoadsAllHighSchoolFiles()
    {
        var store = RuleStore.Load();
        Assert.NotEmpty(store.Query<SolubilityData>("solubility").Table);
        Assert.NotEmpty(store.Query<ElectrolyteData>("electrolytes").StrongAcids);
        Assert.NotEmpty(store.Query<IonInventoryData>("ions").Cations);
        Assert.NotEmpty(store.Query<ActivitySeriesData>("activity-series").Series);
        Assert.Equal(3, store.Query<DoubleDecompositionData>("double-decomposition").Conditions.Count);
    }

    [Fact]
    public void RuleStore_MissingKey_Throws()
    {
        var store = RuleStore.Load();
        Assert.Throws<FileNotFoundException>(() => store.Query<SolubilityData>("no-such-file"));
    }

    // ---------- 溶解性表 ----------

    [Theory]
    [InlineData("BaSO4", Solubility.Insoluble)]
    [InlineData("AgCl", Solubility.Insoluble)]
    [InlineData("Ca(OH)2", Solubility.SlightlySoluble)]
    [InlineData("NaCl", Solubility.Soluble)]
    [InlineData("NaOH", Solubility.Soluble)]
    public void SolubilityLookup_MatchesTextbook(string formula, Solubility expected) =>
        Assert.Equal(expected, SolubilityTable.Lookup(formula));

    [Fact]
    public void SolubilityLookup_Unknown_ReturnsNull() =>
        Assert.Null(SolubilityTable.Lookup("Xy2O3"));

    // ---------- 金属活动性顺序 ----------

    [Fact]
    public void ActivitySeries_OrderAndQueries()
    {
        Assert.True(ActivitySeries.MoreActive("Zn", "Fe"));
        Assert.False(ActivitySeries.MoreActive("Cu", "Fe"));
        Assert.True(ActivitySeries.BeforeHydrogen("Zn"));
        Assert.False(ActivitySeries.BeforeHydrogen("Cu"));
        Assert.True(ActivitySeries.IsKnownMetal("Fe"));
        Assert.False(ActivitySeries.IsKnownMetal("H"));
        Assert.False(ActivitySeries.IsKnownMetal("Xy"));
        // 表外元素不猜测
        Assert.False(ActivitySeries.MoreActive("Xy", "Fe"));
    }

    // ---------- 白名单守卫 ----------

    [Theory]
    [InlineData("Fe", true)]          // 金属单质（活动性顺序）
    [InlineData("NaCl", true)]        // 可溶盐（溶解性表）
    [InlineData("BaSO4", true)]       // 难溶盐（溶解性表）
    [InlineData("H2SO4", true)]       // 强酸
    [InlineData("CH3COOH", true)]     // 弱电解质
    [InlineData("CO2", true)]         // 气体/氧化物
    [InlineData("NH3·H2O", true)]     // 一水合氨
    [InlineData("Xy2O3", false)]      // 陌生物质
    [InlineData("C6H12O6", true)]     // 葡萄糖（substances.json 课内有机物清单收录）
    public void WhitelistGuard_NeutralSpecies(string formula, bool expectedKnown)
    {
        var (known, tag) = WhitelistGuard.Check(Formula(formula));
        Assert.Equal(expectedKnown, known);
        Assert.Equal(expectedKnown ? RiskTags.None : RiskTags.OutOfScope, tag);
    }

    [Theory]
    [InlineData("Fe^3+", true)]
    [InlineData("SO4^2-", true)]
    [InlineData("MnO4^-", true)]
    [InlineData("UO2^2+", false)]     // 超纲离子
    public void WhitelistGuard_Ions(string formula, bool expectedKnown)
    {
        var species = Formula(formula);
        var (known, tag) = WhitelistGuard.Check(species);
        Assert.Equal(expectedKnown, known);
        Assert.Equal(expectedKnown ? RiskTags.None : RiskTags.OutOfScope, tag);
    }

    [Fact]
    public void WhitelistGuard_CheckRange_ReportsFirstUnknown()
    {
        var species = new[] { Species.Parse("Fe"), Species.Parse("Xy2O3"), Species.Parse("NaCl") };
        var (known, tag, unknown) = WhitelistGuard.CheckRange(species);
        Assert.False(known);
        Assert.Equal(RiskTags.OutOfScope, tag);
        Assert.Equal("Xy2O3", unknown!.Body);
    }

    // ---------- ElectrolyteRules 门面（JSON 驱动后行为不变） ----------

    [Fact]
    public void ElectrolyteRules_DataFromJson()
    {
        Assert.Contains("HCl", ElectrolyteRules.StrongAcids);
        Assert.Contains("NaOH", ElectrolyteRules.StrongBases);
        Assert.Contains("CH3COOH", ElectrolyteRules.WeakElectrolytes);
        Assert.Contains("CO3^2-", ElectrolyteRules.WeakAcidRadicals);
        Assert.Contains("CO2", ElectrolyteRules.Gases);
        Assert.Equal(Solubility.Insoluble, ElectrolyteRules.LookupSolubility("BaSO4"));
    }

    private static Species Formula(string text) => CogniThink.Chem.Core.Parsing.FormulaParser.Parse(text);
}
