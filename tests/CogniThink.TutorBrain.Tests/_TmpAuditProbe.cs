using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Chemistry;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// TEMPORARY audit probe (deleted after use). Runs the engine choice chain over
/// ALL BuildTestCases and writes a JSON report of which questions are solved by
/// which validator, plus the list of unsolved pure-text single-ABCD candidates
/// eligible for Concept fact curation. Does NOT call the LLM.
/// </summary>
public class _TmpAuditProbe
{
    private static readonly List<QaTestCase> AllCases = GatewayVsLlmEvaluationService.BuildTestCases();

    [Fact]
    public void Dump_Audit_Report()
    {
        var chem = new ChemicalInferenceService(ChemistryFixture.Knowledge);
        var byTag = new Dictionary<string, int> { ["none"] = 0 };
        var unsolved = new List<QaTestCase>();
        var solvedIds = new HashSet<string>();

        foreach (var c in AllCases)
        {
            var req = new EngineRequest { RawText = c.Question };
            string? tag = null;
            ChoiceVerifier.Outcome? o = null;
            if ((o = ClassificationValidator.TryDecide(req)) != null) tag = "Classification";
            else if ((o = TransformationChainValidator.TryDecide(req)) != null) tag = "Transformation";
            else if ((o = NaChoiceValidator.TryDecide(req)) != null) tag = "Na";
            else if ((o = IonicEquationValidator.TryDecide(req)) != null) tag = "Ionic";
            else if ((o = RedoxChoiceValidator.TryDecide(req)) != null) tag = "Redox";
            else if ((o = EquilibriumChoiceValidator.TryDecide(req)) != null) tag = "Equilibrium";
            else if ((o = OrganicChoiceValidator.TryDecide(req)) != null) tag = "Organic";
            else if ((o = StructurePeriodicValidator.TryDecide(req)) != null) tag = "Structure";
            else if ((o = ExperimentChoiceValidator.TryDecide(req)) != null) tag = "Experiment";
            else if ((o = ConceptChoiceValidator.TryDecide(req)) != null) tag = "Concept";
            else if ((o = MoleculeChoiceValidator.TryDecide(req, null)) != null) tag = "Molecule";
            else if ((o = CountingChoiceValidator.TryDecide(req)) != null) tag = "Counting";
            else if ((o = StoichiometryChoiceValidator.TryDecide(req)) != null) tag = "Stoichiometry";
            else if ((o = ChoiceVerifier.TryDecide(req, chem)) != null) tag = "ChoiceVerifier";

            if (tag == null) { byTag["none"]++; unsolved.Add(c); }
            else { byTag[tag] = byTag.GetValueOrDefault(tag) + 1; solvedIds.Add(c.Id); }
        }

        // Candidates for Concept curation: unsolved, pure text (no images),
        // single-letter ABCD answer, not t3s-/t3count- (handled by downstream solvers
        // which run AFTER Concept in the chain -> adding Concept facts would shadow them).
        var candidates = unsolved
            .Where(c =>
            {
                var noImg = (c.Images == null || c.Images.Count == 0)
                            && (c.ImageAssets == null || c.ImageAssets.Count == 0);
                var ans = (c.Answer ?? "").Trim();
                var single = ans.Length == 1 && "ABCD".Contains(ans);
                var notDownstream = !c.Id.StartsWith("t3s-") && !c.Id.StartsWith("t3count-");
                return noImg && single && notDownstream;
            })
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .Select(c => new
            {
                id = c.Id,
                type = c.Type,
                answer = c.Answer,
                note = c.Note ?? "",
                question = c.Question ?? ""
            })
            .ToList();

        var typeDist = unsolved.GroupBy(c => c.Type ?? "(null)")
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key, g => g.Count());

        var prefixDist = unsolved.GroupBy(c =>
            {
                if (c.Id.StartsWith("t3s-")) return "t3s";
                if (c.Id.StartsWith("t3count-")) return "t3count";
                if (c.Id.StartsWith("pdf-")) return "pdf";
                return "other";
            })
            .ToDictionary(g => g.Key, g => g.Count());

        // unsolved that ARE t3s/t3count (covered by dedicated downstream solvers)
        var downstreamUnsolved = unsolved
            .Where(c => c.Id.StartsWith("t3s-") || c.Id.StartsWith("t3count-"))
            .Select(c => new { id = c.Id, type = c.Type, answer = c.Answer })
            .ToList();

        var allUnsolved = unsolved
            .Select(c => new
            {
                id = c.Id,
                type = c.Type,
                answer = c.Answer,
                images = c.Images?.Count ?? 0,
                imageAssets = c.ImageAssets?.Count ?? 0,
                note = c.Note ?? "",
                question = c.Question ?? ""
            })
            .ToList();

        var report = new
        {
            generatedAt = DateTime.Now.ToString("o"),
            totalCases = AllCases.Count,
            solved = solvedIds.Count,
            unsolved = unsolved.Count,
            byValidator = byTag.OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key, kv => kv.Value),
            unsolvedByType = typeDist,
            unsolvedByPrefix = prefixDist,
            candidateCount = candidates.Count,
            candidates,
            allUnsolvedCount = allUnsolved.Count,
            allUnsolved,
            downstreamUnsolvedCount = downstreamUnsolved.Count,
            downstreamUnsolved
        };

        // resolve repo root (test runner cwd is bin/Debug/net8.0)
        var baseDir = AppContext.BaseDirectory;
        var repoRoot = baseDir;
        while (!string.IsNullOrEmpty(repoRoot) && !File.Exists(Path.Combine(repoRoot, "CogniThink.Chem.sln")))
            repoRoot = Path.GetDirectoryName(repoRoot)!;
        if (string.IsNullOrEmpty(repoRoot)) repoRoot = baseDir;
        var scratch = Path.Combine(repoRoot, "_evalscratch");
        Directory.CreateDirectory(scratch);
        var reportPath = Path.Combine(scratch, "_audit_report.json");
        var summaryPath = Path.Combine(scratch, "_audit_summary.txt");

        var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var json = JsonSerializer.Serialize(report, opts);
        File.WriteAllText(reportPath, json);

        // human summary
        var lines = new List<string>
        {
            $"total={AllCases.Count} solved={solvedIds.Count} unsolved={unsolved.Count} candidates={candidates.Count}",
            "byValidator: " + string.Join(", ", byTag.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}")),
            "unsolvedByType: " + string.Join(", ", typeDist.Select(kv => $"{kv.Key}={kv.Value}")),
            "unsolvedByPrefix: " + string.Join(", ", prefixDist.Select(kv => $"{kv.Key}={kv.Value}")),
            "",
            $"=== {candidates.Count} Concept-curation candidates (unsolved, pure-text, single ABCD, not t3s/t3count) ==="
        };
        foreach (var c in candidates)
            lines.Add($"--- {c.id}  type={c.type}  answer={c.answer}  note={Truncate(c.note, 80)}");
        File.WriteAllLines(summaryPath, lines);

        Assert.True(true);
    }

    private static string Truncate(string s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n) + "…");
}
