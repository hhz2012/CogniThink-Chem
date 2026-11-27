namespace CogniThink.TutorBrain.Tests;

using CogniThink.TutorBrain.Application.Chemistry;
using Xunit;

/// <summary>
/// 多步推理模拟器（第二主体类）的单元测试：状态轨迹、Numerical 挂载、WhatIf 纯函数不可变性、目标搜索。
/// 平衡数值用注入的 EquilibriumStateSimulator（EquilibriumTestConstants），避免依赖文件系统。
/// </summary>
public class ChemicalMultiStepReasoningSimulatorTests
{
    private static (ChemicalSimulator sim, ChemicalMultiStepReasoningSimulator multi) New()
    {
        var sim = new ChemicalSimulator(ChemistryFixture.Knowledge, ChemistryFixture.Parser);
        var eq = new EquilibriumStateSimulator(EquilibriumTestConstants.Make());
        var multi = new ChemicalMultiStepReasoningSimulator(sim, ChemistryFixture.Knowledge, eq);
        return (sim, multi);
    }

    [Fact]
    public void Reason_BaselinePlusAssertNumerical_MountsEquilibriumPh()
    {
        var (_, multi) = New();
        var initial = new ChemicalSystemInput
        {
            Species = new() { new SpeciesInput { Substance = "CH3COOH", Moles = 0.1, Concentration = 0.1, Volume = 1 } },
            Conditions = new ChemicalConditions { Aqueous = true, Air = false }
        };
        var plan = new ReasoningPlan
        {
            Steps =
            {
                new ReasoningStep
                {
                    Kind = ReasoningStepKind.Assert,
                    Name = "测醋酸 pH",
                    Numerical = new EquilibriumQuery { Quantities = { EquilibriumQuantity.Ph } }
                }
            }
        };

        var result = multi.Reason(plan, initial);

        Assert.Equal(2, result.Trajectory.Count); // 基线 + 1 步
        var stepSnap = result.Trajectory[1];
        Assert.NotNull(stepSnap.Numerical);
        Assert.Equal(EquilibriumDetermination.Determined, stepSnap.Numerical!.Status);
        Assert.NotNull(stepSnap.Numerical.Ph);
        Assert.True(Math.Abs(stepSnap.Numerical.Ph!.Value - 2.88) < 0.02,
            $"Assert 步挂载的平衡 pH 期望≈2.88，实际 {stepSnap.Numerical.Ph}");
        Assert.NotNull(result.FinalState);
    }

    [Fact]
    public void Reason_AddSpecies_ThenWhatIf_IsPureAndDoesNotMutateOriginal()
    {
        var (_, multi) = New();
        var initial = new ChemicalSystemInput
        {
            Species = new() { new SpeciesInput { Substance = "Fe", Moles = 1 } },
            Conditions = new ChemicalConditions { Aqueous = true, Air = false } // 基线不氧化，保证确定
        };
        var plan = new ReasoningPlan
        {
            Steps =
            {
                new ReasoningStep
                {
                    Kind = ReasoningStepKind.AddSpecies,
                    Name = "加硫酸铜",
                    Species = new() { new SpeciesInput { Substance = "CuSO4", Moles = 1 } }
                }
            }
        };

        var result = multi.Reason(plan, initial);
        Assert.Equal(2, result.Trajectory.Count);

        var baseline = result.Trajectory[0];
        var feBefore = baseline.State.Species
            .FirstOrDefault(s => s.Id == "Fe" || s.Formula == "Fe");
        Assert.NotNull(feBefore);
        var molesBefore = feBefore!.FinalMoles;

        // WhatIf 是纯函数：原轨迹物种量不得被修改
        var branch = multi.WhatIf(baseline, new ChemicalConditions { Aqueous = true, Air = true, Heating = true });
        Assert.NotNull(branch);

        var feAfter = baseline.State.Species.First(s => s.Id == feBefore.Id);
        Assert.Equal(molesBefore, feAfter.FinalMoles); // 不可变性断言

        // QueryState 能按谓词定位轨迹中的快照
        var located = multi.QueryState(result.Trajectory, s => s.StepIndex == 1);
        Assert.NotNull(located);
        Assert.Equal("加硫酸铜", located!.StageName);
    }

    [Fact]
    public void Search_KscnToFeScn3_ReturnsCandidatesWithVerification()
    {
        var (_, multi) = New();
        var initial = new ChemicalSystemInput
        {
            Species = new() { new SpeciesInput { Substance = "KSCN", Moles = 1 } }
        };
        var goal = new ReasoningGoal
        {
            SolutionColor = "血红色",
            TargetProducts = { "Fe(SCN)3" }
        };

        var candidates = multi.Search(initial, goal);

        Assert.NotNull(candidates);
        Assert.NotEmpty(candidates); // 委托反向引擎：KSCN → Fe(SCN)3 命中 R-077
        Assert.All(candidates, c =>
        {
            Assert.NotNull(c.Trace);
            Assert.NotNull(c.Verification);
        });
        Assert.Contains(candidates, c => c.Verification!.Matched);
    }
}
