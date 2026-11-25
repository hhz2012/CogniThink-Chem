using System.Linq;
using System.Threading.Tasks;
using CogniThink.Chem.Core.Rules;
using CogniThink.TutorBrain.Application.ScaffoldGenerator;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// P0-2 验收：C 步（约束条件）/ D 步（竞争排序）真值提取 + 选项构造 + 保序。
/// 铁律：correct 必有来源（引擎/知识表，带 provenance）；无来源 → null 降级，绝不猜测。
/// 全部走公共 API（EngineArtifactsBuilder / StepTruthExtractor / OptionsBuilder / ScaffoldBuilder）。
/// </summary>
public class ScaffoldGeneratorCDTruthTests
{
    private static EngineArtifacts Artifacts(string question, string answer = "A")
        => EngineArtifactsBuilder.Build(new QuestionCase
        {
            Id = "t", Type = "choice", Question = question, Answer = answer,
        });

    // ---------- 数据加载自检：还原序表可溯源、加载完整、顺序稳定 ----------

    [Fact]
    public void RedoxReactivitySeries_LoadsSevenEntries_InStrongToWeakOrder()
    {
        var agents = RedoxReactivitySeries.ReducingAgents;
        Assert.Equal(7, agents.Count);
        // 还原性由强到弱：S²⁻ > SO₃²⁻ > I⁻ > Fe²⁺ > Br⁻ > Cl⁻ > OH⁻
        Assert.Equal(new[] { "S^2-", "SO3^2-", "I-", "Fe^2+", "Br-", "Cl-", "OH-" },
            agents.Select(a => a.Key).ToArray());
        Assert.All(agents, a => Assert.False(string.IsNullOrWhiteSpace(a.Match)));
        Assert.All(agents, a => Assert.False(string.IsNullOrWhiteSpace(a.Label)));
    }

    // ---------- C 步：题干关键词 → correct key + provenance + scaffold.Conditions ----------

    [Theory]
    [InlineData("少量", "little", "quantity", "少量")]
    [InlineData("过量", "excess", "quantity", "过量")]
    [InlineData("足量", "excess", "quantity", "过量")]
    [InlineData("等物质的量", "equal", "quantity", "等物质的量")]
    [InlineData("酸性", "acidic", "medium", "酸性")]
    [InlineData("碱性", "basic", "medium", "碱性")]
    [InlineData("中性", "neutral", "medium", "中性")]
    [InlineData("加热", "heat", "temperature", "加热")]
    [InlineData("高温", "heat", "temperature", "加热")]
    [InlineData("常温", "room", "temperature", "常温")]
    public async Task Extract_C_Positive_GivesCorrect_AndWritesConditions(
        string keyword, string expectedKey, string dim, string condValue)
    {
        var artifacts = Artifacts($"在{keyword}条件下反应：2Fe + 3Cl2 = 2FeCl3。下列说法正确的是？");

        var c = new StepTruthExtractor().Extract(AtomicTaskKind.C, artifacts);
        Assert.NotNull(c);
        Assert.Equal(AtomicTaskInteraction.SingleSelect, c!.Interaction);
        Assert.Equal(expectedKey, c.Value);
        Assert.Contains(keyword, c.Provenance); // 溯源到具体关键词

        // 端到端：scaffold.Conditions 同步写入对应字段
        var br = await new ScaffoldBuilder().BuildDetailedAsync(artifacts.Case, artifacts);
        Assert.Equal("published", br.Status);
        var bc = br.Scaffold!.Conditions;
        Assert.NotNull(bc);
        var filled = dim switch
        {
            "quantity" => bc!.Quantity,
            "medium" => bc!.Medium,
            "concentration" => bc!.Concentration,
            "temperature" => bc!.Temperature,
            _ => null,
        };
        Assert.Equal(condValue, filled);
    }

    [Fact]
    public void Extract_C_NoKeyword_ReturnsNull()
    {
        var artifacts = Artifacts("反应：Zn + CuSO4 = ZnSO4 + Cu。");
        Assert.Null(new StepTruthExtractor().Extract(AtomicTaskKind.C, artifacts));
    }

    [Fact]
    public void Build_C_OptionsComeFromFixedCatalog_AndContainCorrect()
    {
        // quantity 维度：固定目录 4 项，不混干扰项；correct 为检测到的 little
        var artifacts = Artifacts("少量Cl2参与反应：2Fe + 3Cl2 = 2FeCl3。");
        var correct = new StepTruthExtractor().Extract(AtomicTaskKind.C, artifacts);
        Assert.NotNull(correct);

        var truth = new OptionsBuilder().Build(correct!, AtomicTaskKind.C, artifacts);
        Assert.Equal("single-select", truth.Type);
        var keys = truth.Options.Select(o => o.Key).OrderBy(k => k).ToArray();
        Assert.Equal(new[] { "equal", "excess", "little", "none" }, keys);
        Assert.Equal("little", truth.Correct);
    }

    // ---------- D 步：还原序表过滤体系粒子 → 有序 correct，保序不被字母重排 ----------

    [Fact]
    public void Extract_D_IronIodideRedox_GivesIbeforeFe2_OrderPreserved()
    {
        // 体系含 I- 与 Fe2+（均为还原序表内粒子）→ D 激活；还原性 I- > Fe2+。
        var artifacts = Artifacts("反应：2Fe^3+ + 2I- = 2Fe^2+ + I2。下列说法正确的是？");
        var parsed = EngineArtifactsBuilder.GetParsed(artifacts);
        Assert.NotNull(parsed);
        var formulas = string.Join(",", parsed!.AllSpecies.Select(s => s.Formula));

        var d = new StepTruthExtractor().Extract(AtomicTaskKind.D, artifacts);
        Assert.True(d is not null,
            $"D 应激活但降级。parsed 物种=[{formulas}]；若为空/格式不符需调整归一化匹配键。");
        Assert.Equal(AtomicTaskInteraction.Order, d!.Interaction);
        Assert.Equal(new[] { "I-", "Fe^2+" }, d.Set); // 表序：I- 强于 Fe^2+
        Assert.Contains("I⁻", d.Provenance);
        Assert.Contains("Fe²⁺", d.Provenance);
    }

    [Fact]
    public void Build_D_OptionsAreSpecies_NoDistractors_CorrectListPreservesOrder()
    {
        var artifacts = Artifacts("反应：2Fe^3+ + 2I- = 2Fe^2+ + I2。");
        var correct = new StepTruthExtractor().Extract(AtomicTaskKind.D, artifacts);
        Assert.NotNull(correct);

        var truth = new OptionsBuilder().Build(correct!, AtomicTaskKind.D, artifacts);
        Assert.Equal("order", truth.Type);
        // 选项只有命中物种，无干扰项
        var keys = truth.Options.Select(o => o.Key).OrderBy(k => k).ToArray();
        Assert.Equal(new[] { "Fe^2+", "I-" }, keys);
        // 持久化 correct 保序（CorrectValue 对 Order 不字母重排）
        var corr = Assert.IsType<System.Collections.Generic.List<string>>(truth.Correct);
        Assert.Equal(new[] { "I-", "Fe^2+" }, corr);
    }

    [Fact]
    public void Extract_D_FewerThanTwoTableSpecies_ReturnsNull()
    {
        // 体系无 ≥2 个还原序表内粒子 → 无排序意义 → 降级（绝不猜测）
        var artifacts = Artifacts("反应：Zn + CuSO4 = ZnSO4 + Cu。");
        Assert.Null(new StepTruthExtractor().Extract(AtomicTaskKind.D, artifacts));
    }

    // ---------- CorrectValue 保序回归 ----------

    [Fact]
    public void CorrectValue_Order_PreservesEngineOrder_NotAlphabetized()
    {
        // 表序 I- > Fe^2+；若按字母序会变成 Fe^2+ 在前 → 破坏正确答案
        var correct = new StepCorrect
        {
            Interaction = AtomicTaskInteraction.Order,
            Set = new() { "I-", "Fe^2+" },
        };
        var list = Assert.IsType<System.Collections.Generic.List<string>>(
            ScaffoldGeneratorUtils.CorrectValue(correct));
        Assert.Equal(new[] { "I-", "Fe^2+" }, list);
    }

    [Fact]
    public void CorrectValue_MultiCheck_StillSortedForReproducibility()
    {
        var correct = new StepCorrect
        {
            Interaction = AtomicTaskInteraction.MultiCheck,
            Set = new() { "Zn", "Cu", "Fe" },
        };
        var list = Assert.IsType<System.Collections.Generic.List<string>>(
            ScaffoldGeneratorUtils.CorrectValue(correct));
        Assert.Equal(new[] { "Cu", "Fe", "Zn" }, list);
    }
}
