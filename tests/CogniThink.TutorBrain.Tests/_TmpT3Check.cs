using System.Collections.Generic;
using System.IO;
using System.Linq;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>TEMPORARY — deleted after reading. Check if t3* already solved by Concept/Counting/Stoichiometry.</summary>
public class _TmpT3Check
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

    private static bool Correct(ChoiceVerifier.Outcome o, QaTestCase c)
    {
        foreach (var kw in c.AnswerKeywords ?? new List<string>())
            if (o.AnswerLetter.Contains(kw)) return true;
        return (c.Answer ?? "").Trim() == (o.AnswerLetter ?? "").Trim();
    }

    [Fact]
    public void Dump()
    {
        var ids = new[] { "t3s-01","t3s-02","t3s-03","t3s-04","t3s-05","t3s-06","t3s-07","t3s-08",
                          "t3count-01","t3count-02","t3count-03" };
        var all = GatewayVsLlmEvaluationService.BuildTestCases().ToList();
        var lines = new List<string>();
        foreach (var id in ids)
        {
            var c = all.FirstOrDefault(x => x.Id == id);
            if (c is null) { lines.Add($"{id}: NOT FOUND"); continue; }
            var req = new EngineRequest { RawText = c.Question };
            var concept = ConceptChoiceValidator.TryDecide(req);
            var counting = CountingChoiceValidator.TryDecide(req);
            var stoich = StoichiometryChoiceValidator.TryDecide(req);
            string Fmt(string name, ChoiceVerifier.Outcome? o) =>
                o is null ? $"{name}=null" : $"{name}={o.AnswerLetter}(graded={Correct(o, c)})";
            lines.Add($"{id} ans=[{c.Answer}] | {Fmt("Concept",concept)} | {Fmt("Counting",counting)} | {Fmt("Stoich",stoich)}");
        }
        File.WriteAllLines(Path.Combine(Root(), "_evalscratch", "_t3check.txt"), lines);
    }
}
