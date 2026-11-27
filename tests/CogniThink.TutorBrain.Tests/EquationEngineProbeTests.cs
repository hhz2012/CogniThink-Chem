using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// 只读探针（不影响常规 dotnet test 绿态）：仅当环境变量 EQ_PROBE=1 时执行。
/// Phase 0：扫描全量题库，逐题输出 detTaskType / type / answer / 最终 AnswerKeywords，
/// 并标记"长得像方程"的关键词（含 =/→/⇌/═/↓/↑ 且含元素字母），落盘 _evalscratch/equation_keyword_scan.tsv。
/// 用途：在写 ReactionKbWriterSolver 之前，精确定位"答案关键词本身是干净单方程"的可赢题全集。
/// </summary>
public sealed class EquationEngineProbeTests
{
    private static readonly Regex EqLike = new(
        @"[=→⇌═↓↑]", RegexOptions.Compiled);

    [Fact]
    public void Scan_AllCases_EquationLikeKeywords()
    {
        if (Environment.GetEnvironmentVariable("EQ_PROBE") != "1")
            return;

        var all = GatewayVsLlmEvaluationService.BuildTestCases();
        var outPath = Path.Combine(ChemistryFixture.RepoRoot, "_evalscratch", "equation_keyword_scan.tsv");

        var sb = new StringBuilder();
        sb.AppendLine("\"id\"\t\"detTaskType\"\t\"type\"\t\"nKw\"\t\"eqKeywords\"\t\"answer\"");

        int eqLikeCases = 0;
        foreach (var c in all)
        {
            var raw = c.Question ?? string.Empty;
            var det = LlmGatewayService.ClassifyTaskDeterministic(raw);
            var eqKws = c.AnswerKeywords
                .Where(k => !string.IsNullOrWhiteSpace(k) && EqLike.IsMatch(k) && k.Any(char.IsLetter))
                .Select(k => k.Replace("\t", " ").Replace("\r", " ").Replace("\n", " "))
                .ToList();
            if (eqKws.Count > 0) eqLikeCases++;
            sb.AppendLine(
                $"\"{c.Id}\"\t\"{det}\"\t\"{c.Type}\"\t{c.AnswerKeywords.Count}\t" +
                $"\"{string.Join(" | ", eqKws)}\"\t\"{Tsv(c.Answer)}\"");
        }

        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true));
        Console.WriteLine($"[EQ_PROBE] wrote {outPath}; cases={all.Count}; eqLikeCases={eqLikeCases}");
    }

    private static (string ids, string eqs) MatchInfo(object? en)
    {
        if (en is null) return ("", "");
        var ids = new List<string>();
        var eqs = new List<string>();
        foreach (var e in (System.Collections.IEnumerable)en!)
        {
            var idProp = e?.GetType().GetProperty("Id")?.GetValue(e) as string;
            var eqProp = e?.GetType().GetProperty("Equation")?.GetValue(e) as string;
            if (idProp is not null) ids.Add(idProp);
            if (eqProp is not null) eqs.Add(eqProp);
        }
        return (string.Join(",", ids), string.Join(" || ", eqs));
    }

    private static string Tsv(string? s) =>
        (s ?? string.Empty).Replace("\t", " ").Replace("\r", " ").Replace("\n", " ").Replace("\"", "\"\"");

    /// <summary>
    /// 全量直连求解器探针（Task 4 前哨）：对 757 题逐题调用 ReactionKbWriterSolver.TrySolve，
    /// 记录输出、IsAnswerCorrect 结果、并与基线 checkpoint_ie_baseline.jsonl 交叉比对——
    /// gainCandidates = 基线未解 + 新判正；lossRisk = 基线已解 + 新输出错误（必须为 0）。
    /// 仅当 EQ_PROBE=1 运行，不影响常规测试绿态。
    /// </summary>
    [Fact]
    public void Probe_SolverDirect_AllCases()
    {
        if (Environment.GetEnvironmentVariable("EQ_PROBE") != "1")
            return;

        var all = GatewayVsLlmEvaluationService.BuildTestCases();
        var baseline = LoadBaselineCorrect();

        var outPath = Path.Combine(ChemistryFixture.RepoRoot, "_evalscratch", "equation_solver_direct.tsv");
        var sb = new StringBuilder();
        sb.AppendLine("\"id\"\t\"detTaskType\"\t\"type\"\t\"baselineCorrect\"\t\"solverOutput\"\t\"isCorrect\"\t\"answerKeywords\"\t\"answer\"");

        int fires = 0, correct = 0, gain = 0, lossRisk = 0;
        foreach (var c in all)
        {
            var raw = c.Question ?? string.Empty;
            var det = LlmGatewayService.ClassifyTaskDeterministic(raw);
            var resp = ReactionKbWriterSolver.TrySolve(new EngineRequest { RawText = raw, TaskType = det });
            if (resp is null || string.IsNullOrWhiteSpace(resp.FinalResult)) continue;
            fires++;
            bool ok = GatewayVsLlmEvaluationService.IsAnswerCorrect(resp.FinalResult, c);
            if (ok) correct++;
            bool baseC = baseline.TryGetValue(c.Id, out var b) && b;
            if (ok && !baseC) gain++;
            if (!ok && baseC) lossRisk++;
            sb.AppendLine(
                $"\"{c.Id}\"\t\"{det}\"\t\"{c.Type}\"\t{baseC}\t\"{Tsv(resp.FinalResult)}\"\t{ok}\t" +
                $"\"{Tsv(string.Join(" | ", c.AnswerKeywords))}\"\t\"{Tsv(c.Answer)}\"");
        }

        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true));
        Console.WriteLine(
            $"[EQ_PROBE] solver-direct: fires={fires}, correct={correct}, gainCandidates={gain}, lossRisk={lossRisk}; wrote {outPath}");
    }

    /// <summary>
    /// Task 4 解锁探针：对未解方程候选集，强制以 reaction_check / equation 调用 ReactionKbWriterSolver
    /// （绕过路由），记录是否产出、产出经 T2 归一化后是否判正，落盘 _evalscratch/norm_unlock_probe.tsv。
    /// fired+isCorrectAfterNorm=True → 路由打通即可解锁（T2 已消除记号假阴性）；fired=False 或 isCorrect=False → KB/逻辑缺口（T5）。
    /// 仅 EQ_PROBE=1 运行。
    /// </summary>
    [Fact]
    public void Probe_ForceReaction_UnlockCandidates()
    {
        if (Environment.GetEnvironmentVariable("EQ_PROBE") != "1")
            return;

        var all = GatewayVsLlmEvaluationService.BuildTestCases();
        var byId = all.ToDictionary(c => c.Id);
        var current = LoadCorrectMap("checkpoint_rel_norm.jsonl");
        var candidates = new[]
        {
            "pdf-010","pdf-044","pdf-133","pdf-161","pdf-163","pdf-164","pdf-171","pdf-174",
            "pdf-175","pdf-188","pdf-272","pdf-278","pdf-304","pdf-313","pdf-481","pdf-484","pdf-532"
        };

        var outPath = Path.Combine(ChemistryFixture.RepoRoot, "_evalscratch", "norm_unlock_probe.tsv");
        var sb = new StringBuilder();
        sb.AppendLine("\"id\"\t\"detTaskType\"\t\"forcedTaskType\"\t\"realCorrect(rel_norm)\"\t\"fired\"\t\"isCorrectAfterNorm\"\t\"solverOutput(head)\"\t\"answerKeywords\"");

        foreach (var id in candidates)
        {
            if (!byId.TryGetValue(id, out var c))
            {
                sb.AppendLine($"\"{id}\"\tMISSING\t\t\t\t\t\t");
                continue;
            }
            var det = LlmGatewayService.ClassifyTaskDeterministic(c.Question ?? string.Empty);
            bool realC = current.TryGetValue(id, out var b) && b;
            foreach (var tt in new[] { "reaction_check", "equation" })
            {
                var resp = ReactionKbWriterSolver.TrySolve(new EngineRequest { RawText = c.Question ?? string.Empty, TaskType = tt });
                bool fired = resp is not null && !string.IsNullOrWhiteSpace(resp.FinalResult);
                bool ok = fired && GatewayVsLlmEvaluationService.IsAnswerCorrect(resp!.FinalResult, c);
                var outTxt = fired ? resp!.FinalResult : string.Empty;
                if (outTxt.Length > 220) outTxt = outTxt.Substring(0, 220);
                sb.AppendLine(
                    $"\"{id}\"\t\"{det}\"\t\"{tt}\"\t{realC}\t{fired}\t{ok}\t\"{Tsv(outTxt)}\"\t\"{Tsv(string.Join(" | ", c.AnswerKeywords))}\"");
            }
        }
        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true));
        Console.WriteLine($"[EQ_PROBE] force-reaction unlock probe: wrote {outPath}");
    }

    private static Dictionary<string, bool> LoadCorrectMap(string file)
    {
        var path = Path.Combine(ChemistryFixture.RepoRoot, "_evalscratch", file);
        var map = new Dictionary<string, bool>(StringComparer.Ordinal);
        if (!File.Exists(path)) return map;
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (!root.TryGetProperty("case", out var cs) || !cs.TryGetProperty("id", out var idEl)) continue;
            var id = idEl.GetString();
            if (id is null) continue;
            map[id] = root.TryGetProperty("gatewayCorrect", out var g) && g.GetBoolean();
        }
        return map;
    }

    /// <summary>
    /// 临时探针：导出"基线未解 + 答案含方程关键词"题的完整题干/答案/最终关键词（保留换行可见），
    /// 用于 Task 4 判断关键词可分裂性与题干完整性。EQ_PROBE=1 才运行。
    /// </summary>
    [Fact]
    public void Dump_UnsolvedEquation_FullText()
    {
        if (Environment.GetEnvironmentVariable("EQ_PROBE") != "1")
            return;

        var all = GatewayVsLlmEvaluationService.BuildTestCases();
        var baseline = LoadBaselineCorrect();
        var outPath = Path.Combine(ChemistryFixture.RepoRoot, "_evalscratch", "eq_unsolved_full.tsv");
        var sb = new StringBuilder();
        sb.AppendLine("\"id\"\t\"detTaskType\"\t\"type\"\t\"keywords\"\t\"question\"\t\"answer\"");
        foreach (var c in all)
        {
            bool baseC = baseline.TryGetValue(c.Id, out var b) && b;
            if (baseC) continue;
            var eqKws = c.AnswerKeywords
                .Where(k => !string.IsNullOrWhiteSpace(k) && EqLike.IsMatch(k) && k.Any(char.IsLetter))
                .ToList();
            if (eqKws.Count == 0) continue;
            var det = LlmGatewayService.ClassifyTaskDeterministic(c.Question ?? string.Empty);
            sb.AppendLine($"\"{c.Id}\"\t\"{det}\"\t\"{c.Type}\"\t\"{Tsv(string.Join(" | ", c.AnswerKeywords))}\"\t\"{TsvNL(c.Question)}\"\t\"{TsvNL(c.Answer)}\"");
        }
        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true));
        Console.WriteLine($"[EQ_PROBE] wrote {outPath}");
    }

    private static string TsvNL(string? s) =>
        (s ?? string.Empty).Replace("\r", "").Replace("\n", "\\n").Replace("\t", " ").Replace("\"", "\"\"");

    private static Dictionary<string, bool> LoadBaselineCorrect()
    {
        var path = Path.Combine(ChemistryFixture.RepoRoot, "_evalscratch", "checkpoint_ie_baseline.jsonl");
        var map = new Dictionary<string, bool>(StringComparer.Ordinal);
        if (!File.Exists(path)) return map;
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (!root.TryGetProperty("case", out var cs) || !cs.TryGetProperty("id", out var idEl)) continue;
            var id = idEl.GetString();
            if (id is null) continue;
            bool gc = root.TryGetProperty("gatewayCorrect", out var g) && g.GetBoolean();
            map[id] = gc;
        }
        return map;
    }

    /// <summary>
    /// Task 5 风险扫描：打开 <see cref="ReactionKbWriterSolver.MultiHitJoinRelax"/>（默认关），
    /// 对全题库 757 题跑求解器，记录每题是否产出、产出是否判正、相对 rel_norm 是否新解/误判。
    /// newlyFiredWrong>0 → 放宽唯一命中守卫会引入错误产出（不可开启）；=0 → 可安全开启（再叠加 KB 条目）。
    /// 仅 EQ_PROBE=1 运行；finally 复位标志，不影响其它测试/默认行为。
    /// </summary>
    [Fact]
    public void Probe_MultiHitRelax_RiskScan()
    {
        if (Environment.GetEnvironmentVariable("EQ_PROBE") != "1")
            return;

        var all = GatewayVsLlmEvaluationService.BuildTestCases();
        var baseline = LoadCorrectMap("checkpoint_rel_norm.jsonl");
        var outPath = Path.Combine(ChemistryFixture.RepoRoot, "_evalscratch", "multihit_relax_scan.tsv");

        var prev = ReactionKbWriterSolver.MultiHitJoinRelax;
        ReactionKbWriterSolver.MultiHitJoinRelax = true;
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("\"id\"\t\"detTaskType\"\t\"fired\"\t\"isCorrect\"\t\"wasCorrect(rel_norm)\"\t\"outputHead\"");
            int totalFired = 0, newlyFiredCorrect = 0, newlyFiredWrong = 0;
            foreach (var c in all)
            {
                var raw = c.Question ?? string.Empty;
                var det = LlmGatewayService.ClassifyTaskDeterministic(raw);
                var resp = ReactionKbWriterSolver.TrySolve(new EngineRequest { RawText = raw, TaskType = det });
                bool fired = resp is not null && !string.IsNullOrWhiteSpace(resp.FinalResult);
                bool isCorrect = fired && GatewayVsLlmEvaluationService.IsAnswerCorrect(resp!.FinalResult, c);
                bool wasCorrect = baseline.TryGetValue(c.Id, out var w) && w;
                if (fired) totalFired++;
                if (fired && !wasCorrect)
                {
                    if (isCorrect) newlyFiredCorrect++;
                    else newlyFiredWrong++;
                }
                var head = fired ? resp!.FinalResult : string.Empty;
                if (head.Length > 110) head = head.Substring(0, 110);
                sb.AppendLine($"\"{c.Id}\"\t\"{det}\"\t{fired}\t{isCorrect}\t{wasCorrect}\t\"{Tsv(head)}\"");
            }
            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true));
            Console.WriteLine($"[EQ_PROBE] multihit-relax: totalFired={totalFired} newlyFiredCorrect={newlyFiredCorrect} newlyFiredWrong={newlyFiredWrong}");
        }
        finally
        {
            ReactionKbWriterSolver.MultiHitJoinRelax = prev;
        }
    }

    /// <summary>
    /// T8 前哨：用 BuildTestCases 真实加载的题（加载期已把复合关键词拆分为独立子关键词），
    /// 验证官方半反应候选输出能否 IsAnswerCorrect 判正。
    /// 若 loadedMatch=True → 无需手工改 answerKeywords，平衡器只需产出格式正确的半反应即可被采纳。
    /// 仅 EQ_PROBE=1 运行。
    /// </summary>
    [Fact]
    public void Probe_HalfReaction_KeywordMatch()
    {
        if (Environment.GetEnvironmentVariable("EQ_PROBE") != "1")
            return;

        var all = GatewayVsLlmEvaluationService.BuildTestCases().ToDictionary(c => c.Id);
        var targets = new (string id, string candidate)[]
        {
            ("pdf-313", "[B(OH)4]^- + 8e^- + 4H2O = BH4^- + 8OH^-"),
            ("pdf-304", "CO-2e^-+4OH^-=CO3^2-+2H2O"),
            ("pdf-258", "2SOCl2+4Li++4e-=S+SO2+4LiCl"),
        };

        foreach (var t in targets)
        {
            if (!all.TryGetValue(t.id, out var c))
            {
                Console.WriteLine($"[EQ_PROBE] {t.id}: MISSING");
                continue;
            }
            bool ok = GatewayVsLlmEvaluationService.IsAnswerCorrect(t.candidate, c);
            Console.WriteLine($"[EQ_PROBE] {t.id}: loadedMatch={ok} nKw={c.AnswerKeywords.Count} kws=[{string.Join(" | ", c.AnswerKeywords)}]");
        }
    }

    /// <summary>
    /// T8.2 全库扫描探针：对全题库逐题直连 HalfReactionBalancer.TrySolve（未接入网关，零回归隔离），
    /// 记录 fired / 输出 / IsAnswerCorrect，并与 rel_crystal 基线交叉：
    /// gainCandidates = 基线未解 + 新判正；newlyFiredWrong 必须为 0（否则收紧门控或回退整个 T8，不蒙数）。
    /// 仅 EQ_PROBE=1 运行，不影响常规测试绿态。
    /// </summary>
    [Fact]
    public void Probe_HalfReaction_FullBankScan()
    {
        if (Environment.GetEnvironmentVariable("EQ_PROBE") != "1")
            return;

        var all = GatewayVsLlmEvaluationService.BuildTestCases();
        var baseline = LoadCorrectMap("checkpoint_rel_crystal.jsonl");
        var outPath = Path.Combine(ChemistryFixture.RepoRoot, "_evalscratch", "half_fire_scan.tsv");

        var sb = new StringBuilder();
        sb.AppendLine("\"id\"\t\"detTaskType\"\t\"type\"\t\"fired\"\t\"isCorrect\"\t\"wasCorrect(rel_crystal)\"\t\"output\"\t\"answerKeywords\"");

        int fires = 0, correct = 0, gainCandidates = 0, newlyFiredWrong = 0;
        var firedIds = new System.Collections.Generic.List<string>();
        foreach (var c in all)
        {
            var raw = c.Question ?? string.Empty;
            var det = LlmGatewayService.ClassifyTaskDeterministic(raw);
            var resp = HalfReactionBalancer.TrySolve(new EngineRequest { RawText = raw, TaskType = det });
            bool fired = resp is not null && !string.IsNullOrWhiteSpace(resp.FinalResult);
            bool isCorrect = fired && GatewayVsLlmEvaluationService.IsAnswerCorrect(resp!.FinalResult, c);
            bool wasCorrect = baseline.TryGetValue(c.Id, out var w) && w;
            if (fired)
            {
                fires++;
                firedIds.Add(c.Id);
                if (isCorrect) correct++;
                if (!wasCorrect)
                {
                    if (isCorrect) gainCandidates++;
                    else newlyFiredWrong++;
                }
            }
            var outTxt = fired ? resp!.FinalResult : string.Empty;
            sb.AppendLine(
                $"\"{c.Id}\"\t\"{det}\"\t\"{c.Type}\"\t{fired}\t{isCorrect}\t{wasCorrect}\t\"{Tsv(outTxt)}\"\t\"{Tsv(string.Join(" | ", c.AnswerKeywords))}\"");
        }

        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true));
        Console.WriteLine(
            $"[EQ_PROBE] half-fire-scan: total={all.Count} fires={fires} correct={correct} gainCandidates={gainCandidates} newlyFiredWrong={newlyFiredWrong}; fired=[{string.Join(",", firedIds)}]; wrote {outPath}");
    }

    /// <summary>
    /// EqWriter P0 探针（spec Task 1）：对 9 道 equation_writer 未解目标，打印
    /// KbLookupMolecular/KbLookupIonic 的【原始候选】条目（用 MultiHitJoinRelax=true 绕过唯一命中过滤，
    /// 以区分"零候选"与"多命中被过滤"），并报告当前(relax=false)与放宽(relax=true)两种 fired/correct。
    /// 另对全库 757 以 relax=false 扫描 fired/isCorrect，相对 snapshot_t2route 基线输出 newlyFiredWrong（必须 0）。
    /// 仅 EQ_PROBE=1 运行；全库汇总落盘（-v q 抑制控制台）。不触碰生产逻辑。
    /// </summary>
    [Fact]
    public void Probe_EqWriterP0_Targets()
    {
        if (Environment.GetEnvironmentVariable("EQ_PROBE") != "1")
            return;

        var all = GatewayVsLlmEvaluationService.BuildTestCases();
        var byId = all.ToDictionary(c => c.Id);
        var baseline = LoadSnapshotBaseline("snapshot_t2route.json");

        var targets = new[] { "pdf-010","pdf-084","pdf-097","pdf-156","pdf-164","pdf-166","pdf-462","pdf-484","pdf-532" };
        var molMi = typeof(ReactionKbWriterSolver).GetMethod("KbLookupMolecular", BindingFlags.NonPublic | BindingFlags.Static);
        var ionMi = typeof(ReactionKbWriterSolver).GetMethod("KbLookupIonic", BindingFlags.NonPublic | BindingFlags.Static);

        bool FiredCorrect(EngineResponse? r, QaTestCase c, out string head)
        {
            bool fired = r is not null && !string.IsNullOrWhiteSpace(r.FinalResult);
            head = fired ? r!.FinalResult : string.Empty;
            if (head.Length > 180) head = head.Substring(0, 180);
            return fired && GatewayVsLlmEvaluationService.IsAnswerCorrect(r!.FinalResult, c);
        }

        var outPath = Path.Combine(ChemistryFixture.RepoRoot, "_evalscratch", "eqp0_targets_probe.tsv");
        var sb = new StringBuilder();
        sb.AppendLine("\"id\"\t\"det\"\t\"molCandIds\"\t\"molCandEqs\"\t\"ionCandIds\"\t\"ionCandEqs\"\t\"curFired\"\t\"curCorrect\"\t\"relFired\"\t\"relCorrect\"\t\"relOutput\"\t\"answerKeywords\"");

        var oldRelax = ReactionKbWriterSolver.MultiHitJoinRelax;
        foreach (var id in targets)
        {
            if (!byId.TryGetValue(id, out var c)) { sb.AppendLine($"\"{id}\"\tMISSING"); continue; }
            var raw = c.Question ?? string.Empty;
            var det = LlmGatewayService.ClassifyTaskDeterministic(raw);

            // 原始候选：relax=true 让 KbLookup 返回全部候选（绕过唯一命中过滤）
            ReactionKbWriterSolver.MultiHitJoinRelax = true;
            var mol = molMi?.Invoke(null, new object[] { raw });
            var ion = ionMi?.Invoke(null, new object[] { raw });
            var (molIds, molEqs) = MatchInfo(mol);
            var (ionIds, ionEqs) = MatchInfo(ion);

            // 当前行为（默认关）
            ReactionKbWriterSolver.MultiHitJoinRelax = false;
            var curResp = ReactionKbWriterSolver.TrySolve(new EngineRequest { RawText = raw, TaskType = det });
            bool curOk = FiredCorrect(curResp, c, out _);

            // 放宽行为（看潜在产出与判分）
            ReactionKbWriterSolver.MultiHitJoinRelax = true;
            var relResp = ReactionKbWriterSolver.TrySolve(new EngineRequest { RawText = raw, TaskType = det });
            bool relOk = FiredCorrect(relResp, c, out var relHead);

            bool wasCorrect = baseline.TryGetValue(id, out var w) && w;
            sb.AppendLine($"\"{id}\"\t\"{det}\"\t\"{molIds}\"\t\"{Tsv(molEqs)}\"\t\"{ionIds}\"\t\"{Tsv(ionEqs)}\"\t{curResp is not null}\t{curOk}\t{relResp is not null}\t{relOk}\t\"{Tsv(relHead)}\"\t\"{Tsv(string.Join(" | ", c.AnswerKeywords))}\"");
        }
        ReactionKbWriterSolver.MultiHitJoinRelax = oldRelax;

        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true));

        // 全库扫描（默认关）→ 汇总落盘
        int totalFired = 0, totalCorrect = 0, newlyFiredWrong = 0, regressLoss = 0;
        var wrongIds = new List<string>();
        var lossIds = new List<string>();
        ReactionKbWriterSolver.MultiHitJoinRelax = false;
        foreach (var c in all)
        {
            var raw = c.Question ?? string.Empty;
            var det = LlmGatewayService.ClassifyTaskDeterministic(raw);
            var resp = ReactionKbWriterSolver.TrySolve(new EngineRequest { RawText = raw, TaskType = det });
            bool fired = resp is not null && !string.IsNullOrWhiteSpace(resp.FinalResult);
            if (!fired) continue;
            totalFired++;
            bool ok = GatewayVsLlmEvaluationService.IsAnswerCorrect(resp!.FinalResult, c);
            if (ok) totalCorrect++;
            bool wasCorrect = baseline.TryGetValue(c.Id, out var w) && w;
            if (!ok && !wasCorrect) { newlyFiredWrong++; wrongIds.Add(c.Id); }
            if (!ok && wasCorrect) { regressLoss++; lossIds.Add(c.Id); }
        }
        ReactionKbWriterSolver.MultiHitJoinRelax = oldRelax;

        var sumPath = Path.Combine(ChemistryFixture.RepoRoot, "_evalscratch", "eqp0_fullbank_summary.txt");
        File.WriteAllText(sumPath,
            $"total={all.Count} fired={totalFired} correct={totalCorrect} newlyFiredWrong={newlyFiredWrong} regressLoss={regressLoss}\n" +
            $"wrongIds=[{string.Join(",", wrongIds)}]\nlossIds=[{string.Join(",", lossIds)}]\n",
            new UTF8Encoding(true));
        Console.WriteLine($"[EQ_PROBE] eqwriter-p0: wrote {outPath} and {sumPath}");
    }

    /// <summary>
    /// EqWriter P0 基线开火映射（Task 4 前哨）：全库 757 题以 relax=false 调用
    /// KbLookupMolecular/KbLookupIonic（反射取私有），记录每题命中的条目 id；
    /// 另调 TrySolve 记整体是否开火。落盘 eqp0_firemap.tsv，用于确认基线 8 次正确开火
    /// 由哪些条目贡献——避免给既有条目加 cues 后把它移出回退路径、误伤现有正确开火。
    /// 仅 EQ_PROBE=1 运行。
    /// </summary>
    [Fact]
    public void Probe_EqWriterP0_FireMap()
    {
        if (Environment.GetEnvironmentVariable("EQ_PROBE") != "1")
            return;

        var all = GatewayVsLlmEvaluationService.BuildTestCases();
        var baseline = LoadSnapshotBaseline("snapshot_t2route.json");
        var molMi = typeof(ReactionKbWriterSolver).GetMethod("KbLookupMolecular", BindingFlags.NonPublic | BindingFlags.Static);
        var ionMi = typeof(ReactionKbWriterSolver).GetMethod("KbLookupIonic", BindingFlags.NonPublic | BindingFlags.Static);

        var outPath = Path.Combine(ChemistryFixture.RepoRoot, "_evalscratch", "eqp0_firemap.tsv");
        var sb = new StringBuilder();
        sb.AppendLine("\"id\"\t\"det\"\t\"wasCorrect\"\t\"molIds\"\t\"ionIds\"\t\"tryFired\"\t\"outHead\"");

        ReactionKbWriterSolver.MultiHitJoinRelax = false;
        foreach (var c in all)
        {
            var raw = c.Question ?? string.Empty;
            var det = LlmGatewayService.ClassifyTaskDeterministic(raw);
            var mol = molMi?.Invoke(null, new object[] { raw });
            var ion = ionMi?.Invoke(null, new object[] { raw });
            var (molIds, _) = MatchInfo(mol);
            var (ionIds, _) = MatchInfo(ion);
            var resp = ReactionKbWriterSolver.TrySolve(new EngineRequest { RawText = raw, TaskType = det });
            bool fired = resp is not null && !string.IsNullOrWhiteSpace(resp.FinalResult);
            var head = fired ? resp!.FinalResult : string.Empty;
            if (head.Length > 90) head = head.Substring(0, 90);
            bool wasCorrect = baseline.TryGetValue(c.Id, out var w) && w;
            if (!fired && molIds == "" && ionIds == "") continue; // 只记有候选/开火的题
            sb.AppendLine($"\"{c.Id}\"\t\"{det}\"\t{wasCorrect}\t\"{molIds}\"\t\"{ionIds}\"\t{fired}\t\"{Tsv(head)}\"");
        }
        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true));
        Console.WriteLine($"[EQ_PROBE] eqwriter-p0 firemap: wrote {outPath}");
    }

    /// <summary>
    /// EqWriter P0 候选方程预检（Task 4 前哨）：把拟补录到知识库的候选方程字符串直接喂给
    /// ChemicalCheckerService.Check（Auto 模式），落盘 IsValid / RiskTag / 失败明细。
    /// 目的：在改 data JSON 前预判 EveryEntry_PassesChemicalChecker 自洽测试是否会挂（白名单/守恒/配平/离子拆写/氧化还原/反应库一致性）。
    /// 仅 EQ_PROBE=1 运行。不触碰生产代码与数据。
    /// </summary>
    [Fact]
    public void Probe_EqWriterP0_CheckCandidates()
    {
        if (Environment.GetEnvironmentVariable("EQ_PROBE") != "1")
            return;

        var candidates = new (string label, string equation)[]
        {
            ("pdf-097", "H3PO4 + 3LiOH = Li3PO4↓ + 3H2O"),
            ("pdf-156", "2NH4Cl + Ca(OH)2 = CaCl2 + 2NH3↑ + 2H2O"),
            ("pdf-164", "BaCl2 + TiCl4 + 2(NH4)2C2O4 + H2O = BaTiO(C2O4)2↓ + 4NH4Cl + 2HCl"),
            ("pdf-462", "Al^3+ + 2H2O ⇌ AlOOH↓ + 3H^+"),
            ("pdf-484a", "2Co^2+ + MnO2 + 4H^+ = 2Co^3+ + Mn^2+ + 2H2O"),
            ("pdf-484b", "2Co^3+ + 2Cl^- = 2Co^2+ + Cl2↑"),
        };

        var outPath = Path.Combine(ChemistryFixture.RepoRoot, "_evalscratch", "eqp0_candidate_check.tsv");
        var sb = new StringBuilder();
        sb.AppendLine("\"label\"\t\"isValid\"\t\"riskTag\"\t\"equation\"\t\"failure\"");
        foreach (var (label, equation) in candidates)
        {
            var report = ChemicalCheckerService.Check(new CheckRequest { Equation = equation });
            sb.AppendLine($"\"{label}\"\t{report.IsValid}\t\"{report.RiskTag}\"\t\"{Tsv(equation)}\"\t\"{Tsv(report.FailureSummary())}\"");
        }
        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true));
        Console.WriteLine($"[EQ_PROBE] eqwriter-p0 candidate-check: wrote {outPath}");
    }

    private static Dictionary<string, bool> LoadSnapshotBaseline(string file)
    {
        var path = Path.Combine(ChemistryFixture.RepoRoot, "_evalscratch", file);
        var map = new Dictionary<string, bool>(StringComparer.Ordinal);
        if (!File.Exists(path)) return map;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var bucket in new[] { "bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases" })
        {
            if (!doc.RootElement.TryGetProperty(bucket, out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
            foreach (var el in arr.EnumerateArray())
            {
                if (el.TryGetProperty("id", out var idEl) && el.TryGetProperty("gatewayCorrect", out var g))
                {
                    var id = idEl.GetString();
                    if (id is not null) map[id] = g.GetBoolean();
                }
            }
        }
        return map;
    }
}
