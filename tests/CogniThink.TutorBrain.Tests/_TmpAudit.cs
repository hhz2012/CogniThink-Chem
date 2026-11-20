using System.Collections.Generic;
using System.IO;
using System.Linq;
using CogniThink.TutorBrain.Application.Evaluation;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>TEMPORARY probe — deleted after reading. Dump full records for audit shortlist.</summary>
public class _TmpAudit
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

    [Fact]
    public void Dump()
    {
        var ids = new[] { "pdf-123","pdf-135","pdf-136","pdf-137","pdf-142","pdf-144","pdf-187","pdf-189",
                          "pdf-078","pdf-114","pdf-158","pdf-399","pdf-403","pdf-525",
                          "t3s-01","t3s-02","t3s-03","t3s-04","t3s-05","t3s-06","t3s-07","t3s-08",
                          "t3count-01","t3count-02","t3count-03" };
        var all = GatewayVsLlmEvaluationService.BuildTestCases();
        var lines = new List<string>();
        foreach (var id in ids)
        {
            var c = all.FirstOrDefault(x => x.Id == id);
            if (c is null) { lines.Add($"===== {id} NOT FOUND ====="); continue; }
            lines.Add($"===== {id} | type={c.Type} | answer=[{c.Answer}] =====");
            lines.Add($"NOTE: {c.Note}");
            lines.Add($"Q: {c.Question}");
            lines.Add("");
        }
        File.WriteAllLines(Path.Combine(Root(), "_evalscratch", "_audit.txt"), lines);
    }
}
