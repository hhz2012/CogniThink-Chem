using System.IO;
using System.Linq;
using CogniThink.TutorBrain.Application.Chemistry;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// 回归护栏：ExtractIonsFromText 必须正确提取硫氰根 SCN-（不能把 SCN- 错切成 CN-），
/// 否则 Fe3+ + SCN- 的共存题会被误判为可共存（ion-6 精度回归）。
/// </summary>
public class ExtractIonsFromTextTests
{
    private static string RepoRoot()
    {
        var dir = System.AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "CogniThink.Chem.sln"))) return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return Directory.GetCurrentDirectory();
    }

    [Fact]
    public void Extracts_SCN_Thiocyanate_Not_CN()
    {
        var ions = LlmGatewayService.ExtractIonsFromText("Fe3+ 和 SCN- 能大量共存吗？");
        Assert.Contains("SCN-", ions);
        Assert.DoesNotContain("CN-", ions);
    }

    [Fact]
    public void Fe3_And_SCN_Cannot_Coexist()
    {
        var root = RepoRoot();
        var kb = ChemicalKnowledgeBase.Load(Path.Combine(root, "src", "CogniThink.TutorBrain.Api"));
        var chem = new ChemicalInferenceService(kb);
        var ions = LlmGatewayService.ExtractIonsFromText("Fe3+ 和 SCN- 能大量共存吗？");
        var result = chem.CheckCoexistence(ions);
        Assert.False(result.CanCoexist);
    }
}
