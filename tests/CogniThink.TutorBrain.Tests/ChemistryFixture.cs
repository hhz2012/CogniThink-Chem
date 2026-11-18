namespace CogniThink.TutorBrain.Tests;

using CogniThink.TutorBrain.Application.Chemistry;

/// <summary>测试共享的知识库与引擎实例（只读，构建一次）</summary>
public static class ChemistryFixture
{
    public static readonly string RepoRoot = FindRepoRoot();
    public static readonly string ApiRoot = Path.Combine(RepoRoot, "src", "CogniThink.TutorBrain.Api");
    public static readonly ChemicalKnowledgeBase Knowledge = ChemicalKnowledgeBase.Load(ApiRoot);
    public static readonly ReactionEquationParser Parser = new(Knowledge);
    public static readonly ChemicalSimulationEngine Simulator = new(Knowledge, Parser);
    public static readonly ChemicalReverseInferenceEngine Reverse = new(Knowledge, Simulator);

    public static SimulationResult Simulate(
        (string Substance, double Moles)[] species,
        Action<SimulationRequest>? configure = null)
    {
        var request = new SimulationRequest
        {
            Species = species
                .Select(s => new SimulationSpeciesInput { Substance = s.Substance, Moles = s.Moles })
                .ToList()
        };

        configure?.Invoke(request);
        return Simulator.Simulate(request);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(
                    dir.FullName, "src", "CogniThink.TutorBrain.Api", "data", "chemistry")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("未能定位仓库根目录（缺少 src/CogniThink.TutorBrain.Api/data/chemistry）。");
    }
}
