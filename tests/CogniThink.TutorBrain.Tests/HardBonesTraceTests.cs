using System.Text;
using Xunit;
using CogniThink.TutorBrain.Application.Chemistry;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// 诊断夹具：对指定未解选择题跑完整校验器瀑布 + 逐选项陈述判定，定位"为什么返回 null"。
/// 仅当环境变量 HARDBONES_TRACE=1 时执行；否则空操作（不影响常规 dotnet test 的耗时与绿态）。
/// 输出写入 _evalscratch/hardbones_trace.txt。
/// </summary>
public sealed class HardBonesTraceTests
{
    private static readonly string[] UnsolvedChoiceIds =
    {
        "pdf-037","pdf-075","pdf-087","pdf-090","pdf-100","pdf-115","pdf-138","pdf-147","pdf-155","pdf-162",
        "pdf-197","pdf-217","pdf-218","pdf-220","pdf-227","pdf-255","pdf-263","pdf-271","pdf-284","pdf-289",
        "pdf-295","pdf-303","pdf-306","pdf-307","pdf-319","pdf-325","pdf-327","pdf-332","pdf-336","pdf-337",
        "pdf-338","pdf-347","pdf-376","pdf-382","pdf-386","pdf-387","pdf-388","pdf-390","pdf-394","pdf-398",
        "pdf-400","pdf-405","pdf-406","pdf-409","pdf-410","pdf-412","pdf-414","pdf-416","pdf-432","pdf-458",
        "pdf-459","pdf-475","pdf-498","pdf-511","pdf-524","pdf-531","pdf-540",
    };

    [Fact]
    public void Trace_UnsolvedChoice_Waterfall_And_PerOption()
    {
        if (Environment.GetEnvironmentVariable("HARDBONES_TRACE") != "1")
            return;

        var apiRoot = ChemistryFixture.ApiRoot;
        var kb = ChemicalKnowledgeBase.Load(apiRoot);
        var chem = new ChemicalInferenceService(kb);

        var all = GatewayVsLlmEvaluationService.BuildTestCases();
        var byId = all.ToDictionary(c => c.Id, StringComparer.Ordinal);

        var composite = new CompositeStatementJudge(
            new FactBaseStatementJudge(), new CountingStatementJudge(), new RedoxElectronStatementJudge());
        var fb = new FactBaseStatementJudge();
        var cj = new CountingStatementJudge();
        var rj = new RedoxElectronStatementJudge();

        var sb = new StringBuilder();
        foreach (var id in UnsolvedChoiceIds)
        {
            if (!byId.TryGetValue(id, out var c))
            {
                sb.AppendLine($"========== {id}: NOTFOUND ==========");
                continue;
            }

            var raw = c.Question ?? string.Empty;
            var req = new EngineRequest
            {
                RawText = raw,
                TaskType = LlmGatewayService.ClassifyTaskDeterministic(raw),
            };
            var assets = c.ImageAssets;

            sb.AppendLine($"========== {id} (type={c.Type}, answer={c.Answer}, detTask={req.TaskType}, imgs={c.Images?.Count ?? 0}, assets={assets?.Count ?? 0}) ==========");
            var (stem, opts) = ChoiceVerifier.ParseOptions(raw);
            sb.AppendLine($"STEM({stem.Length}): {Trunc(stem, 160)}");
            foreach (var kv in opts.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var vC = composite.Judge(stem, kv.Value);
                var vF = fb.Judge(stem, kv.Value);
                var vN = cj.Judge(stem, kv.Value);
                var vR = rj.Judge(stem, kv.Value);
                sb.AppendLine($"  OPT {kv.Key}: comp={vC,-10} [fact={vF},count={vN},redox={vR}]  {Trunc(kv.Value, 90)}");
            }

            sb.AppendLine("  WATERFALL (first non-null wins):");
            Probe(sb, "CurveChoice       ", () => CurveChoiceValidator.TryDecide(req, assets));
            Probe(sb, "Classification    ", () => ClassificationValidator.TryDecide(req));
            Probe(sb, "TransformationChain", () => TransformationChainValidator.TryDecide(req));
            Probe(sb, "NaChoice          ", () => NaChoiceValidator.TryDecide(req));
            Probe(sb, "IonicEquation     ", () => IonicEquationValidator.TryDecide(req));
            Probe(sb, "RedoxChoice       ", () => RedoxChoiceValidator.TryDecide(req));
            Probe(sb, "EquilibriumChoice ", () => EquilibriumChoiceValidator.TryDecide(req));
            Probe(sb, "OrganicChoice     ", () => OrganicChoiceValidator.TryDecide(req));
            Probe(sb, "StructurePeriodic ", () => StructurePeriodicValidator.TryDecide(req));
            Probe(sb, "ExperimentChoice  ", () => ExperimentChoiceValidator.TryDecide(req));
            Probe(sb, "ConceptChoice     ", () => ConceptChoiceValidator.TryDecide(req));
            Probe(sb, "MoleculeChoice    ", () => MoleculeChoiceValidator.TryDecide(req, assets));
            Probe(sb, "CountingChoice    ", () => CountingChoiceValidator.TryDecide(req));
            Probe(sb, "StoichiometryChoice", () => StoichiometryChoiceValidator.TryDecide(req));
            Probe(sb, "MultiSelectChoice ", () => MultiSelectChoiceValidator.TryDecide(req));
            Probe(sb, "ChoiceVerifier    ", () => ChoiceVerifier.TryDecide(req, chem));
            sb.AppendLine();
        }

        // ---- 判断题追踪：逐小题 core + 各 judge 判决 + 聚合器结果 ----
        var judgeIds = new[] { "pdf-117","pdf-119","pdf-120","pdf-121","pdf-122","pdf-134","pdf-466","pdf-470","pdf-473","pdf-478","pdf-480" };
        sb.AppendLine();
        sb.AppendLine("############### JUDGMENT TRACE ###############");
        foreach (var id in judgeIds)
        {
            if (!byId.TryGetValue(id, out var c)) { sb.AppendLine($"=== {id} NOTFOUND ==="); continue; }
            var raw = c.Question ?? string.Empty;
            sb.AppendLine($"========== {id} (answer={c.Answer}) ==========");
            sb.AppendLine($"LooksLikeJudgment={MultiSubCorrectnessSolver.LooksLikeJudgmentQuestion(raw)}");
            var subs = MultiSubCorrectnessSolver.SplitSubQuestions(raw);
            sb.AppendLine($"subs={subs.Count}");
            foreach (var sub in subs)
            {
                var (eq, ctx) = MultiSubCorrectnessSolver.ExtractEquationAndContext(sub.Text);
                var core = MultiSubCorrectnessSolver.ExtractSubCore(sub.Text);
                var vC = string.IsNullOrWhiteSpace(eq) ? composite.Judge(raw, core) : Verdict.Undecided;
                var vF = string.IsNullOrWhiteSpace(eq) ? fb.Judge(raw, core) : Verdict.Undecided;
                var vN = string.IsNullOrWhiteSpace(eq) ? cj.Judge(raw, core) : Verdict.Undecided;
                var vR = string.IsNullOrWhiteSpace(eq) ? rj.Judge(raw, core) : Verdict.Undecided;
                sb.AppendLine($"  ({sub.Index}) eq=[{Trunc(eq, 20)}] comp={vC} [fact={vF},count={vN},redox={vR}]");
                sb.AppendLine($"       text={Trunc(sub.Text, 90)}");
                sb.AppendLine($"       core={Trunc(core, 90)}");
            }
            try
            {
                var agg = JudgmentStatementAggregator.TrySolve(new EngineRequest { RawText = raw });
                sb.AppendLine(agg is null ? "  AGGREGATOR: null" : $"  AGGREGATOR: => {agg.FinalResult}");
            }
            catch (Exception ex) { sb.AppendLine($"  AGGREGATOR EX: {ex.Message}"); }
            sb.AppendLine();
        }

        var path = Path.Combine(ChemistryFixture.RepoRoot, "_evalscratch", "hardbones_trace.txt");
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        // 让测试在控制台可见
        Console.WriteLine($"[HARDBONES_TRACE] wrote {path}");
    }

    private static void Probe(StringBuilder sb, string name, Func<ChoiceVerifier.Outcome?> f)
    {
        try
        {
            var o = f();
            sb.AppendLine(o is null
                ? $"    {name}: null"
                : $"    {name}: => {o.AnswerLetter}  ({Trunc(o.FinalResult, 60)})");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"    {name}: EX {ex.GetType().Name}: {Trunc(ex.Message, 120)}");
        }
    }

    private static string Trunc(string s, int n)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace('\n', ' ').Replace('\r', ' ');
        return s.Length <= n ? s : s[..n] + "…";
    }
}
