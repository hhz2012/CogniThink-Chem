using System.Collections.Generic;
using System.IO;
using System.Linq;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>TEMPORARY — deleted after reading. Full-chain check + dump for extra candidates.</summary>
public class _TmpFullChainCheck
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
        var picks = new[] { "pdf-123","pdf-135","pdf-136","pdf-137","pdf-142","pdf-144","pdf-187","pdf-189","pdf-078","pdf-114" };
        var extra = new[] { "pdf-095","pdf-237","pdf-393","pdf-394" };
        var all = GatewayVsLlmEvaluationService.BuildTestCases().ToList();
        var lines = new List<string>();
        foreach (var id in picks.Concat(extra))
        {
            var c = all.FirstOrDefault(x => x.Id == id);
            if (c is null) { lines.Add($"{id}: NOT FOUND"); continue; }
            var req = new EngineRequest { RawText = c.Question };
            var concept = ConceptChoiceValidator.TryDecide(req);
            var counting = CountingChoiceValidator.TryDecide(req);
            var stoich = StoichiometryChoiceValidator.TryDecide(req);
            string Fmt(string name, ChoiceVerifier.Outcome? o) =>
                o is null ? $"{name}=null" : $"{name}={o.AnswerLetter}(graded={Correct(o, c)})";
            lines.Add($"{id} ans=[{c.Answer}] type={c.Type} | {Fmt("Concept",concept)} | {Fmt("Counting",counting)} | {Fmt("Stoich",stoich)}");
            if (extra.Contains(id))
            {
                lines.Add($"  NOTE: {c.Note}");
                lines.Add($"  Q: {c.Question}");
            }
        }
        File.WriteAllLines(Path.Combine(Root(), "_evalscratch", "_fullchain.txt"), lines);
    }
}
