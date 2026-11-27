namespace CogniThink.TutorBrain.Tests;

using CogniThink.TutorBrain.Application.Chemistry;
using Xunit;

/// <summary>
/// 新主体类 ChemicalSimulator（统一契约 SimulationTrace）的单元测试。
/// 复用 ChemistryFixture 的知识库与方程解析器，验证富化字段（Basis/Priority/Extent）与边界（未知物质）。
/// </summary>
public class ChemicalSimulatorTests
{
    private static ChemicalSimulator NewSim() => new(ChemistryFixture.Knowledge, ChemistryFixture.Parser);

    [Fact]
    public void Simulate_FePlusCuSO4_ProducesRichReactionEvent()
    {
        var sim = NewSim();
        var input = new ChemicalSystemInput
        {
            Species = new()
            {
                new() { Substance = "Fe", Moles = 1 },
                new() { Substance = "CuSO4", Moles = 1 }
            },
            Conditions = new ChemicalConditions { Aqueous = true }
        };

        var trace = sim.Simulate(input);

        Assert.True(trace.Converged);
        var ev = Assert.Single(trace.Events, e => e.ReactionId == "R-FE-CUSO4");
        Assert.Equal(0, ev.StageIndex);

        // 富化字段：化学依据 / 优先级 / 反应进度
        Assert.NotNull(ev.Basis);
        Assert.Equal("R-FE-CUSO4", ev.Basis.ReactionId);
        Assert.NotNull(ev.Basis.KnowledgeIds);
        Assert.False(string.IsNullOrWhiteSpace(ev.Basis.SelectionReason));

        Assert.NotNull(ev.Priority);
        Assert.Equal(1, ev.Priority.Rank); // 实际发生的反应在本步候选中排第 1
        Assert.True(ev.Priority.Score >= 0);

        Assert.True(ev.Extent > 0); // 限量试剂允许的最大反应量
        Assert.NotEmpty(trace.FinalState.Species);
    }

    [Fact]
    public void Simulate_UnknownSubstance_AddsWarningInsteadOfCrashing()
    {
        var sim = NewSim();
        var input = new ChemicalSystemInput
        {
            Species = new() { new SpeciesInput { Substance = "XyzUnknownAcid123", Moles = 1 } }
        };

        var trace = sim.Simulate(input); // 不应抛异常

        Assert.Contains(trace.Warnings, w => w.Contains("XyzUnknownAcid123"));
        // 没有可反应的物质 → 事件为空但终态结构仍然齐全
        Assert.Empty(trace.Events);
        Assert.NotNull(trace.FinalState);
    }

    [Fact]
    public void Simulate_ReturnConsidered_PopulatesConsideredStructure()
    {
        var sim = NewSim();
        var input = new ChemicalSystemInput
        {
            Species = new()
            {
                new() { Substance = "Fe", Moles = 1 },
                new() { Substance = "CuSO4", Moles = 1 }
            },
            Conditions = new ChemicalConditions { Aqueous = true, ReturnConsidered = true }
        };

        var trace = sim.Simulate(input);

        Assert.True(trace.Converged);
        Assert.NotNull(trace.Considered); // 按步分组的候选列表结构必须存在
        Assert.Single(trace.Events, e => e.ReactionId == "R-FE-CUSO4");
    }
}
