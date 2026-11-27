namespace CogniThink.TutorBrain.Tests;

using CogniThink.TutorBrain.Application.Chemistry;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

/// <summary>
/// solve 投影富化器（SolveStructuredEnricher）单元测试。
/// <para>铁律：不蒙数——只取题给数据的确定性算术或引擎 status=Determined 的结果；缺数据留空。</para>
/// <para>best-effort：任何异常返回 null，绝不抛入响应管线、绝不影响判决字段。</para>
/// </summary>
public class SolveStructuredEnricherTests
{
    private static SolveStructuredEnricher NewEnricher()
        => new(
            new EquilibriumStateSimulator(EquilibriumTestConstants.Make()),
            new ChemicalSimulator(ChemistryFixture.Knowledge, ChemistryFixture.Parser));

    private static GatewayResult MakeResult(string taskType, EngineRequest? req = null)
        => new()
        {
            TaskType = taskType,
            EngineRequest = req ?? new EngineRequest(),
            EngineResponse = new EngineResponse(),
            Reply = ""
        };

    [Fact]
    public void Enrich_NullResult_ReturnsNull()
    {
        var enricher = NewEnricher();
        Assert.Null(enricher.Enrich(null!, "任何文本"));
    }

    [Fact]
    public void Enrich_CalcGivenNAndV_ComputesConcentrationDeterministically()
    {
        var enricher = NewEnricher();
        var req = new EngineRequest { Known = new EngineKnown { N = 0.5, V = 0.5 } };
        var result = MakeResult("calculation", req);
        const string text = "500 mL NaOH 溶液中含有 0.5 mol NaOH，该溶液的物质的量浓度是多少？";

        var structured = enricher.Enrich(result, text);

        Assert.NotNull(structured);
        Assert.NotNull(structured!.Numeric);
        Assert.Equal(1.0, structured.Numeric!.Value!.Value, 6);
        Assert.Equal("mol/L", structured.Numeric.Unit);
        Assert.Equal("NaOH", structured.Numeric.Species);
        Assert.Equal("c = n / V", structured.Numeric.Formula);
    }

    [Fact]
    public void Enrich_CalcGivenCAndV_ComputesMoles()
    {
        var enricher = NewEnricher();
        var req = new EngineRequest { Known = new EngineKnown { C = 2.0, V = 0.25 } };
        var result = MakeResult("calculation", req);

        var structured = enricher.Enrich(result, "0.25 L 2.0 mol/L NaCl 溶液含多少 mol NaCl？");

        Assert.NotNull(structured?.Numeric);
        Assert.Equal(0.5, structured!.Numeric!.Value!.Value, 6);
        Assert.Equal("mol", structured.Numeric.Unit);
    }

    [Fact]
    public void Enrich_AqueousSalt_ProducesConservationPhAndOrdering()
    {
        var enricher = NewEnricher();
        var result = MakeResult("calculation");
        const string text = "0.1 mol/L CH3COONa 溶液中，写出电荷守恒、物料守恒与质子守恒表达式，并将 c(Na^+)、c(CH3COO^-)、c(OH^-)、c(H^+) 按由大到小排序。";

        var structured = enricher.Enrich(result, text);

        Assert.NotNull(structured);
        Assert.NotNull(structured!.Aqueous);
        Assert.True(structured.Aqueous!.ConservationEquations.Count >= 3,
            $"CH3COONa 应产出至少 3 条守恒式，实际 {structured.Aqueous.ConservationEquations.Count}");
        Assert.NotNull(structured.Aqueous.Ph);
        Assert.True(structured.Aqueous.Ph > 8, $"CH3COONa 水解应显碱性 pH>8，实际 {structured.Aqueous.Ph}");
        Assert.False(string.IsNullOrWhiteSpace(structured.Aqueous.AcidityVerdict));
    }

    [Fact]
    public void Enrich_UnknownSalt_DoesNotFabricate()
    {
        var enricher = NewEnricher();
        var result = MakeResult("calculation");
        const string text = "0.1 mol/L XYZNotInConstants 溶液的 pH 是多少？";

        var structured = enricher.Enrich(result, text);

        // 不蒙数：常数库中无该溶质 → 不产出确定数值/水相结果（可能仅留 warning）
        Assert.Null(structured?.Numeric);
        Assert.Null(structured?.Aqueous);
    }

    [Fact]
    public void Enrich_NoExtractableData_ReturnsNullOrEmpty()
    {
        var enricher = NewEnricher();
        var result = MakeResult("concept");
        var structured = enricher.Enrich(result, "什么是化学平衡？");
        // 无可抽取的数值/物种 → 不下发空壳 structured
        Assert.True(structured is null || structured.IsEmpty());
    }

    [Fact]
    public void Enrich_ReactionCheck_DoesNotThrow()
    {
        var enricher = NewEnricher();
        var req = new EngineRequest();
        req.Reaction.Left.Add("HCl");
        req.Reaction.Left.Add("NaOH");
        var result = MakeResult("reaction_check", req);

        var structured = enricher.Enrich(result, "盐酸和氢氧化钠能否反应？");

        // 不抛即通过；若产出轨迹则事件结构合法
        if (structured?.ReactionTrace is not null)
            Assert.All(structured.ReactionTrace.Events, e => Assert.NotNull(e.Equation));
    }

    [Fact]
    public void Enrich_EquilibriumTask_SkipsReactionTraceEvenWithParsedReactants()
    {
        // EQ-H01 类气相 Kp 题：即便解析出反应物，也不应产出偏离题意的反应轨迹
        var enricher = NewEnricher();
        var req = new EngineRequest();
        req.Reaction.Left.Add("N2");
        req.Reaction.Left.Add("H2");
        req.Reaction.Right.Add("NH3");
        var result = MakeResult("equilibrium", req);

        var structured = enricher.Enrich(result, "N2 + 3H2 ⇌ 2NH3，计算平衡常数 Kp");

        Assert.Null(structured?.ReactionTrace);
    }
}
