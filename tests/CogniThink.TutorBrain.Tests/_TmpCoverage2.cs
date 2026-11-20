using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>TEMPORARY probe — deleted after reading. Fresh coverage of choice questions.</summary>
public class _TmpCoverage2
{
    private static string Root()
    {
        var dir = System.AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "CogniThink.Chem.sln"))) return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return Directory.GetCurrentDirectory();
    }

    private static (string name, ChoiceVerifier.Outcome? o)[] Chain(EngineRequest req) => new (string, ChoiceVerifier.Outcome?)[]
    {
        ("Classification", ClassificationValidator.TryDecide(req)),
        ("Transformation", TransformationChainValidator.TryDecide(req)),
        ("NaChoice", NaChoiceValidator.TryDecide(req)),
        ("IonicEquation", IonicEquationValidator.TryDecide(req)),
        ("Redox", RedoxChoiceValidator.TryDecide(req)),
        ("Equilibrium", EquilibriumChoiceValidator.TryDecide(req)),
        ("Organic", OrganicChoiceValidator.TryDecide(req)),
        ("Structure", StructurePeriodicValidator.TryDecide(req)),
        ("Experiment", ExperimentChoiceValidator.TryDecide(req)),
        ("Concept", ConceptChoiceValidator.TryDecide(req)),
    };

    [Fact]
    public void Dump()
    {
        var cases = GatewayVsLlmEvaluationService.BuildTestCases()
            .Where(c => c.Type == "choice").ToList();
        var solved = new List<string>();
        var unsolved = new List<QaTestCase>();
        var byValidator = new Dictionary<string, int>();
        foreach (var c in cases)
        {
            var first = Chain(new EngineRequest { RawText = c.Question }).FirstOrDefault(x => x.o != null);
            if (first.o is null) { unsolved.Add(c); continue; }
            solved.Add($"{c.Id}->{first.name}({first.o.AnswerLetter})");
            byValidator[first.name] = byValidator.TryGetValue(first.name, out var n) ? n + 1 : 1;
        }

        var lines = new List<string>
        {
            $"total choice={cases.Count} solved={solved.Count} unsolved={unsolved.Count}",
            $"byValidator: {string.Join(", ", byValidator.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"))}",
            "",
            "===== UNSOLVED (text-safe filter: no images, single-letter ABCD answer) =====",
        };

        var wrongDir = new Regex(@"错误|不正确|不对|不合理|不可以|不能|不可|不符合|叙述错误|下列说法错误|没有关联|不涉及|不包括|不属于");
        foreach (var c in unsolved.OrderBy(x => x.Id))
        {
            bool hasImg = (c.Images?.Count ?? 0) > 0 || (c.ImageAssets?.Count ?? 0) > 0;
            if (hasImg) continue;
            var ans = (c.Answer ?? "").Trim();
            if (ans.Length != 1 || !"ABCD".Contains(ans)) continue;
            var ask = wrongDir.IsMatch(c.Question ?? "") ? "wrong" : "correct";
            lines.Add($"----- {c.Id} | answer={ans} | ask={ask} | nOpts={(c.Options?.Count ?? 0)} -----");
            lines.Add((c.Question ?? "").Replace("\r", ""));
            lines.Add("");
        }
        File.WriteAllLines(Path.Combine(Root(), "_evalscratch", "_coverage2.txt"), lines);
    }
}
