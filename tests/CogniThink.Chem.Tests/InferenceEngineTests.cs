using CogniThink.Chem.Core.Benchmark;
using CogniThink.Chem.Core.Inference;
using CogniThink.Chem.Core.Math;
using Xunit;

namespace CogniThink.Chem.Tests;

// ===========================================================================
// 产物推断引擎
// ===========================================================================

public class ProductInferenceEngineTests
{
    [Fact]
    public void SodiumInChlorine_Ignite_SolvesToSodiumChloride()
    {
        var result = ProductInferenceEngine.Infer(new ProductInferenceRequest
        {
            Reactants = new List<string> { "Na", "Cl2" },
            Conditions = new List<string> { "点燃" },
        });

        Assert.Equal(InferenceStatus.Solved, result.Status);
        var match = Assert.Single(result.Matches);
        Assert.Equal("2Na + Cl2 =点燃= 2NaCl", match.Equation);
        Assert.False(string.IsNullOrWhiteSpace(match.KnowledgeId));
        Assert.False(string.IsNullOrWhiteSpace(match.Source));
    }

    [Fact]
    public void SodiumInOxygen_Ignite_PrefersPeroxideOverOxide()
    {
        var result = ProductInferenceEngine.Infer(new ProductInferenceRequest
        {
            Reactants = new List<string> { "Na", "O2" },
            Conditions = new List<string> { "点燃" },
        });

        Assert.Equal(InferenceStatus.Solved, result.Status);
        var match = Assert.Single(result.Matches);
        Assert.Equal("2Na + O2 =点燃= Na2O2", match.Equation);
    }

    [Fact]
    public void SodiumHydroxide_ExcessCarbonDioxide_Bicarbonate()
    {
        var result = ProductInferenceEngine.Infer(new ProductInferenceRequest
        {
            Reactants = new List<string> { "NaOH", "CO2" },
            Conditions = new List<string> { "CO2过量" },
        });

        Assert.Equal(InferenceStatus.Solved, result.Status);
        Assert.Equal("NaOH + CO2 = NaHCO3", Assert.Single(result.Matches).Equation);
    }

    [Fact]
    public void SodiumHydroxide_CarbonDioxide_NoCondition_Carbonate()
    {
        var result = ProductInferenceEngine.Infer(new ProductInferenceRequest
        {
            Reactants = new List<string> { "NaOH", "CO2" },
        });

        Assert.Equal(InferenceStatus.Solved, result.Status);
        Assert.Equal("2NaOH + CO2 = Na2CO3 + H2O", Assert.Single(result.Matches).Equation);
    }

    [Fact]
    public void IronInHydrochloricAcid_FerrousChloride()
    {
        var result = ProductInferenceEngine.Infer(new ProductInferenceRequest
        {
            Reactants = new List<string> { "Fe", "HCl" },
        });

        Assert.Equal(InferenceStatus.Solved, result.Status);
        Assert.Equal("Fe + 2HCl = FeCl2 + H2↑", Assert.Single(result.Matches).Equation);
    }

    [Fact]
    public void UnknownReactant_NoSolution_NotFabricated()
    {
        var result = ProductInferenceEngine.Infer(new ProductInferenceRequest
        {
            Reactants = new List<string> { "Xy", "O2" },
            Conditions = new List<string> { "点燃" },
        });

        Assert.Equal(InferenceStatus.NoSolution, result.Status);
        Assert.Empty(result.Matches);
    }
}

// ===========================================================================
// 离子共存判定引擎
// ===========================================================================

public class CoexistenceEngineTests
{
    private static CoexistenceResult Judge(List<string> ions, string medium = "") =>
        CoexistenceEngine.Judge(new CoexistenceRequest { Species = ions, Medium = medium });

    [Fact]
    public void BariumAndSulfate_NotCoexist_Precipitate()
    {
        var result = Judge(new List<string> { "Ba^2+", "SO4^2-" });

        Assert.Equal(CoexistenceVerdict.NotCoexist, result.Verdict);
        var conflict = Assert.Single(result.Conflicts);
        Assert.Equal("RX-IN-ION-002", conflict.Reaction!.Id);
        Assert.Equal("生成沉淀", conflict.Reason);
    }

    [Fact]
    public void CarbonateAndAcid_NotCoexist_Gas()
    {
        var result = Judge(new List<string> { "CO3^2-", "H^+" });

        Assert.Equal(CoexistenceVerdict.NotCoexist, result.Verdict);
        var conflict = Assert.Single(result.Conflicts);
        Assert.Equal("RX-IN-ION-005", conflict.Reaction!.Id);
        Assert.Equal("生成气体", conflict.Reason);
    }

    [Fact]
    public void AmmoniumAndHydroxide_NotCoexist_WeakElectrolyte()
    {
        var result = Judge(new List<string> { "NH4^+", "OH^-" });

        Assert.Equal(CoexistenceVerdict.NotCoexist, result.Verdict);
        var conflict = Assert.Single(result.Conflicts);
        Assert.Equal("RX-IN-ION-008", conflict.Reaction!.Id);
    }

    [Fact]
    public void AluminumAndBicarbonate_NotCoexist_DoubleHydrolysis()
    {
        var result = Judge(new List<string> { "Al^3+", "HCO3^-" });

        Assert.Equal(CoexistenceVerdict.NotCoexist, result.Verdict);
        var conflict = Assert.Single(result.Conflicts);
        Assert.Equal("RX-PL-HYD-009", conflict.Reaction!.Id);
    }

    [Fact]
    public void FerrousAndPermanganate_AcidicMedium_NotCoexist_Redox()
    {
        var result = Judge(new List<string> { "Fe^2+", "MnO4^-" }, medium: "acidic");

        Assert.Equal(CoexistenceVerdict.NotCoexist, result.Verdict);
        var conflict = Assert.Single(result.Conflicts);
        Assert.Equal("RX-IN-ION-012", conflict.Reaction!.Id);
        Assert.Equal("发生氧化还原反应", conflict.Reason);
    }

    [Fact]
    public void SodiumPotassiumChlorideNitrate_Coexist()
    {
        var result = Judge(new List<string> { "Na^+", "K^+", "Cl^-", "NO3^-" });

        Assert.Equal(CoexistenceVerdict.Coexist, result.Verdict);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public void UnknownIon_Underdetermined_NotGuessed()
    {
        var result = Judge(new List<string> { "Xy^+", "Cl^-" });

        Assert.Equal(CoexistenceVerdict.Underdetermined, result.Verdict);
    }
}

// ===========================================================================
// 过程推断引擎（基准驱动：8 题全解）
// ===========================================================================

public class ProcessInferenceEngineTests
{
    private static readonly IReadOnlyList<ProblemSpec> Problems =
        BenchmarkLoader.LoadCategory(ProblemCategory.ProcessInference);

    public static IEnumerable<object[]> ProblemIds() => Problems.Select(p => new object[] { p.Id });

    private static ProblemSpec ById(string id) => Problems.Single(p => p.Id == id);

    [Theory]
    [MemberData(nameof(ProblemIds))]
    public void Benchmark_SolvesWithExpectedAssignmentAndEquations(string caseId)
    {
        var problem = ById(caseId);
        var request = new ProcessInferenceRequest
        {
            CandidateSpecies = problem.Given.CandidateSpecies.ToList(),
            Medium = problem.Given.Medium ?? "",
            Steps = problem.Given.Steps.Select(ToExperimentStep).ToList(),
        };

        var result = ProcessInferenceEngine.Infer(request);

        Assert.True(
            result.Status == InferenceStatus.Solved,
            $"{caseId}: 期望 Solved，实际 {result.Status}（{result.Message}）");

        foreach (var kv in problem.Expected.SubstanceMap!)
        {
            Assert.True(
                result.Assignment.TryGetValue(kv.Key, out var actual),
                $"{caseId}: 缺少标签 {kv.Key} 的赋值");
            Assert.True(
                actual == kv.Value,
                $"{caseId}: {kv.Key} 期望 {kv.Value}，实际 {actual}");
        }

        var equations = result.StepResults.OrderBy(r => r.Step).Select(r => r.Equation).ToList();
        Assert.True(
            problem.Expected.Equations.SequenceEqual(equations),
            $"{caseId}: 方程式序列不符。期望 [{string.Join(" | ", problem.Expected.Equations)}]，" +
            $"实际 [{string.Join(" | ", equations)}]");
    }

    internal static ExperimentStep ToExperimentStep(StepSpec s) => new()
    {
        Step = s.Step,
        From = s.From,
        To = s.To,
        Reagents = s.Reagents.ToList(),
        Conditions = s.Conditions.ToList(),
        Phenomenon = s.Phenomenon,
    };
}

// ===========================================================================
// 成分推断引擎（基准驱动：8 题全解）
// ===========================================================================

public class IdentificationEngineTests
{
    private static readonly IReadOnlyList<ProblemSpec> Problems =
        BenchmarkLoader.LoadCategory(ProblemCategory.SubstanceIdentification);

    public static IEnumerable<object[]> ProblemIds() => Problems.Select(p => new object[] { p.Id });

    private static ProblemSpec ById(string id) => Problems.Single(p => p.Id == id);

    [Theory]
    [MemberData(nameof(ProblemIds))]
    public void Benchmark_SolvesWithExpectedCompositionAndEquations(string caseId)
    {
        var problem = ById(caseId);
        var request = new IdentificationRequest
        {
            CandidateSpecies = problem.Given.CandidateSpecies.ToList(),
            Medium = problem.Given.Medium ?? "",
            SingleEntity = problem.Given.SingleEntity,
            Steps = problem.Given.Steps.Select(ProcessInferenceEngineTests.ToExperimentStep).ToList(),
        };

        var result = IdentificationEngine.Identify(request);

        Assert.True(
            result.Status == InferenceStatus.Solved,
            $"{caseId}: 期望 Solved，实际 {result.Status}（{result.Message}）");

        foreach (var kv in problem.Expected.Composition!)
        {
            var expected = (CompositionVerdict)Enum.Parse(typeof(CompositionVerdict), kv.Value.ToString());
            Assert.True(
                result.Composition.TryGetValue(kv.Key, out var actual),
                $"{caseId}: 缺少 {kv.Key} 的判定");
            Assert.True(
                actual == expected,
                $"{caseId}: {kv.Key} 期望 {expected}，实际 {actual}");
        }

        var actualEquations = result.Equations
            .Select(e => e.Equation)
            .OrderBy(e => e, StringComparer.Ordinal)
            .ToList();
        var expectedEquations = problem.Expected.Equations
            .OrderBy(e => e, StringComparer.Ordinal)
            .ToList();
        Assert.True(
            expectedEquations.SequenceEqual(actualEquations),
            $"{caseId}: 方程式集合不符。期望 [{string.Join(" | ", expectedEquations)}]，" +
            $"实际 [{string.Join(" | ", actualEquations)}]");
    }
}

// ===========================================================================
// 化学计量引擎（方程式系数比）
// ===========================================================================

public class EquationStoichiometryTests
{
    [Fact]
    public void SodiumCarbonate_HydrochloricAcid_CarbonDioxideAmount()
    {
        var result = EquationStoichiometry.Solve(new StoichiometryRequest
        {
            Equation = "Na2CO3 + 2HCl = 2NaCl + H2O + CO2↑",
            KnownSpecies = "Na2CO3",
            KnownMoles = new Rational(1, 10),
            TargetSpecies = "CO2",
        });

        Assert.Equal(InferenceStatus.Solved, result.Status);
        Assert.Equal(new Rational(1, 10), result.TargetMoles);
    }

    [Fact]
    public void UnbalancedInput_RebalancedBeforeStoichiometry()
    {
        var result = EquationStoichiometry.Solve(new StoichiometryRequest
        {
            Equation = "Na2CO3 + HCl = NaCl + H2O + CO2↑",
            KnownSpecies = "Na2CO3",
            KnownMoles = new Rational(1, 10),
            TargetSpecies = "CO2",
        });

        Assert.Equal(InferenceStatus.Solved, result.Status);
        Assert.Equal(new Rational(1, 10), result.TargetMoles);
        Assert.Equal("Na2CO3 + 2HCl = 2NaCl + H2O + CO2↑", result.BalancedEquation);
    }

    [Fact]
    public void SodiumPeroxide_CarbonDioxide_OxygenAmount()
    {
        var result = EquationStoichiometry.Solve(new StoichiometryRequest
        {
            Equation = "2Na2O2 + 2CO2 = 2Na2CO3 + O2",
            KnownSpecies = "Na2O2",
            KnownMoles = new Rational(1, 5),
            TargetSpecies = "O2",
        });

        Assert.Equal(InferenceStatus.Solved, result.Status);
        Assert.Equal(new Rational(1, 10), result.TargetMoles);
    }

    [Fact]
    public void SpeciesNotInEquation_NoSolution()
    {
        var result = EquationStoichiometry.Solve(new StoichiometryRequest
        {
            Equation = "Na2CO3 + 2HCl = 2NaCl + H2O + CO2↑",
            KnownSpecies = "CH4",
            KnownMoles = new Rational(1, 10),
            TargetSpecies = "CO2",
        });

        Assert.Equal(InferenceStatus.NoSolution, result.Status);
    }

    [Fact]
    public void UnparseableEquation_NoSolution()
    {
        var result = EquationStoichiometry.Solve(new StoichiometryRequest
        {
            Equation = "这不是一个方程式",
            KnownSpecies = "Na2CO3",
            KnownMoles = new Rational(1, 10),
            TargetSpecies = "CO2",
        });

        Assert.Equal(InferenceStatus.NoSolution, result.Status);
    }
}
