using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Molecules;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 计数 / 组合推理选择题逐选项校验——最保守。
/// <para>覆盖两类：① 选项断言"某分子式的同分异构体/取代物有 N 种"；② 题干列出子项、选项问"正确的有几项"。</para>
/// <para>真值源：烷烃/烷基构造异构用 <see cref="ConstitutionalIsomerCounter"/> 枚举计算（引擎计算是真值源）；
/// 笼状/苯环/萘等不可枚举的固定结构计数查 <c>data/highschool/counting-facts.json</c>（精确子串匹配 + 数目比对）。</para>
/// <para>红线（不蒙数、零假阳性）：仅当四个选项<b>全部</b>能干净判定真伪且满足方向的候选唯一 → 返回答案字母；
/// 任一选项不可判（分子式解析不出、官能团不在支持列表、事实表歧义、子项需图像/氧还判定等）→ 整题返回 null，不臆断。</para>
/// </summary>
public static class CountingChoiceValidator
{
    private const string ConstraintNote =
        "【高中限定】计数基于构造异构枚举（不考虑立体异构）与 counting-facts.json 事实表；" +
        "分子式必须能干净解析、官能团在支持列表内；需结构图或复杂推理的计数不臆断。";

    private static readonly Lazy<List<CountingFact>> _facts = new(() =>
        RuleStore.Load().Query<CountingFactData>("counting-facts").Facts);

    public static int FactCount => _facts.Value.Count; // 供测试护栏

    // Task 3/4：通用陈述子项判定用的聚合真值源（事实库 + 计数 + 氧化还原电子计数），与 MultiSelectChoiceValidator 同口径。
    // FR-5：追加氧还反应计数（FR-2）/ 氧还角色比值（FR-3），保持三处判定口径一致。
    private static readonly CompositeStatementJudge SubItemJudge =
        new(new FactBaseStatementJudge(), new CountingStatementJudge(), new RedoxElectronStatementJudge(),
            new RedoxCountStatementJudge(), new RedoxRoleRatioStatementJudge());

    public static ChoiceVerifier.Outcome? TryDecide(EngineRequest req)
    {
        var raw = req.RawText ?? "";
        var (stem, opts) = ChoiceVerifier.ParseOptions(raw);
        if (opts.Count < 2) return null;

        var askDir = AskDir(stem);
        if (askDir == "?") return null;

        // 路径一：选项是"N 项 / 全部" → 题干列子项，问正确的有几项
        if (LooksLikeCountOfCorrect(opts))
        {
            // 1a：仅 "N 种" 计数字项的既有路径（字节级不变，TR-3.2）。
            var byCount = TryDecideCountOfCorrect(stem, opts, askDir);
            if (byCount is not null) return byCount;
            // 1b：Task 3 扩展——一般化学陈述子项（经 IStatementJudge 逐子项判定）。
            //     仅当 1a 未求解时触发，不影响既有计数题。
            return TryDecideGeneralSubItems(stem, opts, askDir);
        }

        // 路径一c：组合型——选项是 ①②③ 等子项编号组合（Task 3 / FR-3）。
        if (LooksLikeCombination(opts))
            return TryDecideCombination(stem, opts, askDir);

        // 路径二：逐选项判定计数断言
        var reasons = new List<string>();
        var verdicts = new List<(string letter, bool ok)>();
        // B-LlmMain T2b：收集每选项命中的计数事实 Id（仅事实表路径；枚举路径无锚点留空，R4）。
        var optionHits = new List<ChoiceReasoningNodes.OptionHit>();
        foreach (var kv in opts.OrderBy(x => x.Key))
        {
            var (v, factId) = JudgeCountOption(kv.Value);
            if (v is null) return null; // 任一不可判 → 整题 null
            verdicts.Add((kv.Key, v.Value));
            reasons.Add($"{kv.Key}：{kv.Value}（计算/查表得 {(v.Value ? "真" : "假")}）");
            var hitIds = new List<string>();
            if (!string.IsNullOrWhiteSpace(factId)) hitIds.Add(factId);
            optionHits.Add(new ChoiceReasoningNodes.OptionHit(kv.Key, hitIds));
        }
        return ChooseUnique(verdicts, askDir, reasons, optionHits);
    }

    // ===================== 路径二：逐选项计数断言 =====================

    /// <summary>判定一个计数断言选项的真伪；不可判返回 null。factId 仅在事实表命中时非空（R4 锚点）。</summary>
    private static (bool? verdict, string? factId) JudgeCountOption(string text)
    {
        var claimed = ExtractClaimedCount(text);
        if (claimed is null) return (null, null);

        // 1) 枚举：解析分子式 + 限定 → 计算真值（纯引擎，无 JSON 锚点 → factId=null，R4 留空）
        var trueCount = ComputeCountByFormula(text);
        if (trueCount is not null) return (claimed == trueCount, null);

        // 2) 事实表：精确子串匹配（命中 → factId=CountingFact.Id，真实锚点）
        var fact = MatchFact(text);
        if (fact is not null) return (claimed == fact.Count, string.IsNullOrWhiteSpace(fact.Id) ? null : fact.Id);

        return (null, null);
    }

    /// <summary>
    /// 判定一条计数断言陈述的真伪；不可判返回 null。供 <see cref="CountingStatementJudge"/>
    /// （IStatementJudge 链路）复用——仅做薄包装，不改 <see cref="JudgeCountOption"/> 判定语义，
    /// 既有计数/组合路径字节级不变。
    /// </summary>
    internal static bool? TryJudgeCountStatement(string text) => JudgeCountOption(text).verdict;

    /// <summary>从选项提取声称的数目：取 "…N 种" 中的 N。</summary>
    private static int? ExtractClaimedCount(string text)
    {
        var m = Regex.Match(text, @"(\d+)\s*种");
        return m.Success ? int.Parse(m.Groups[1].Value) : null;
    }

    private static readonly Regex FormulaRe = new(@"C(\d*)H(\d+)([A-Za-z0-9]*)", RegexOptions.Compiled);

    private sealed record FormulaCounts(int C, int H, int O, int Cl, int N);

    private static FormulaCounts? ParseFormula(string text)
    {
        var m = FormulaRe.Match(text);
        if (!m.Success) return null;
        var c = m.Groups[1].Value == "" ? 1 : int.Parse(m.Groups[1].Value);
        var h = int.Parse(m.Groups[2].Value);
        var rest = m.Groups[3].Value;
        int o = 0, cl = 0, n = 0;
        // 解析 rest 中的 O/Cl/N 计数（rest 形如 O2、Cl、N 等）
        var restMatches = Regex.Matches(rest, @"([A-Z][a-z]?)(\d*)");
        foreach (Match rm in restMatches)
        {
            var sym = rm.Groups[1].Value;
            var num = rm.Groups[2].Value == "" ? 1 : int.Parse(rm.Groups[2].Value);
            switch (sym)
            {
                case "O": o += num; break;
                case "Cl": cl += num; break;
                case "N": n += num; break;
            }
        }
        return new FormulaCounts(c, h, o, cl, n);
    }

    /// <summary>由分子式 + 选项限定词计算异构体真值；不可算返回 null。</summary>
    private static int? ComputeCountByFormula(string text)
    {
        var f = ParseFormula(text);
        if (f is null) return null;

        var hasMethylFilter = Regex.Match(text, @"含\s*(\d+)\s*个甲基");
        if (hasMethylFilter.Success && f.H == 2 * f.C + 2 && f.O == 0 && f.Cl == 0 && f.N == 0)
        {
            // 烷烃 + "含 k 个甲基" 限定
            var k = int.Parse(hasMethylFilter.Groups[1].Value);
            return ConstitutionalIsomerCounter.CountAlkaneIsomersWithMethylCount(f.C, k);
        }

        // 纯烷烃 CnH2n+2（无氧/氯/氮，且无官能团限定词）
        if (f.H == 2 * f.C + 2 && f.O == 0 && f.Cl == 0 && f.N == 0
            && !text.Contains("羧酸") && !text.Contains("醇") && !text.Contains("醚")
            && !text.Contains("醛") && !text.Contains("酮") && !text.Contains("酯"))
        {
            return ConstitutionalIsomerCounter.CountAlkaneIsomers(f.C);
        }

        // 羧酸 CnH2nO2 → 烷基 C(n-1)H(2n-1)-COOH → 烷基异构体数
        if (text.Contains("羧酸") && f.H == 2 * f.C && f.O == 2)
            return ConstitutionalIsomerCounter.CountAlkylIsomers(f.C - 1);

        // 醇 CnH2n+2O → 烷基 CnH2n+1-OH（排除"醚"歧义）
        if (text.Contains("醇") && f.H == 2 * f.C + 2 && f.O == 1 && !text.Contains("醚"))
            return ConstitutionalIsomerCounter.CountAlkylIsomers(f.C);

        // 一氯代烷 CnH2n+1Cl → 烷基异构体数
        if (f.Cl == 1 && f.H == 2 * f.C + 1 && f.O == 0 && f.N == 0)
            return ConstitutionalIsomerCounter.CountAlkylIsomers(f.C);

        // 醛 CnH2nO → 烷基 C(n-1)H(2n-1)-CHO
        if (text.Contains("醛") && f.H == 2 * f.C && f.O == 1)
            return ConstitutionalIsomerCounter.CountAlkylIsomers(f.C - 1);

        return null;
    }

    /// <summary>事实表匹配：选项含全部 MatchAllOf 且不含任一 Exclude；命中多条（歧义）→ null。</summary>
    private static CountingFact? MatchFact(string text)
    {
        CountingFact? hit = null;
        foreach (var fact in _facts.Value)
        {
            if (fact.MatchAllOf.Count == 0) continue;
            if (fact.MatchAllOf.Any(s => !text.Contains(s))) continue;
            if (fact.Exclude.Any(s => text.Contains(s))) continue;
            if (hit is not null) return null; // 多条命中 → 歧义，不判
            hit = fact;
        }
        return hit;
    }

    // ===================== 路径一：有几项正确 =====================

    private static bool LooksLikeCountOfCorrect(Dictionary<string, string> opts)
    {
        // 多数选项是 "N 项" / "全部" 形式
        var itemOpts = opts.Values.Count(v => Regex.IsMatch(v, @"^\s*\d+\s*(?:项|个)\s*$") || v.Contains("全部"));
        return itemOpts >= 2;
    }

    private static ChoiceVerifier.Outcome? TryDecideCountOfCorrect(string stem, Dictionary<string, string> opts, string askDir)
    {
        var items = SplitSubItems(stem);
        // 过滤掉前导语 / 非计数字项（如"下列说法正确的有几项？"不含"N 种"断言），只保留真正的计数子项
        items = items.Where(it => ExtractClaimedCount(it) is not null).ToList();
        if (items.Count == 0) return null;

        var reasons = new List<string>();
        var trueCount = 0;
        foreach (var it in items)
        {
            // 子项路径留空 ReasoningNodes（选项是"N 项"聚合，非事实直查；R4 不臆造映射），仅取 verdict。
            var (v, _) = JudgeCountOption(it);
            if (v is null) return null; // 任一子项不可判 → 整题 null
            if (v.Value) trueCount++;
            reasons.Add($"子项：{it}（{(v.Value ? "正确" : "错误")}）");
        }

        // 匹配声称"N 项"的选项
        var verdicts = new List<(string letter, bool ok)>();
        foreach (var kv in opts.OrderBy(x => x.Key))
        {
            bool ok;
            var m = Regex.Match(kv.Value, @"(\d+)\s*项");
            if (m.Success)
            {
                var claimed = int.Parse(m.Groups[1].Value);
                ok = claimed == trueCount;
            }
            else if (kv.Value.Contains("全部"))
            {
                ok = trueCount == items.Count;
            }
            else
            {
                return null; // 选项不是 N 项/全部 → 不可判
            }
            verdicts.Add((kv.Key, ok));
            reasons.Add($"{kv.Key}：{kv.Value}（实际正确 {trueCount} 项 → {(ok ? "真" : "假")}）");
        }
        return ChooseUnique(verdicts, askDir, reasons);
    }

    // ===== Task 3：通用陈述子项（count-of-correct）与组合型 =====

    private const string CircledDigits = "①②③④⑤⑥⑦⑧⑨⑩";

    /// <summary>选项是否为子项编号组合（多数选项含 ≥2 个圆圈数字）。</summary>
    private static bool LooksLikeCombination(Dictionary<string, string> opts)
    {
        var comboOpts = opts.Values.Count(v => Regex.Matches(v, @"[①②③④⑤⑥⑦⑧⑨⑩]").Count >= 2);
        return comboOpts >= 2;
    }

    /// <summary>
    /// 路径 1b：题干列一般化学陈述子项、选项是 "N 项 / 全部"，问正确的有几项。
    /// 逐子项经 <see cref="SubItemJudge"/>（事实库 + 计数）判定；任一子项 Undecided → 整题 null。
    /// 与 1a 共用 "N 项 / 全部" 选项匹配与 ChooseUnique 口径，保持判分一致。
    /// </summary>
    private static ChoiceVerifier.Outcome? TryDecideGeneralSubItems(string stem, Dictionary<string, string> opts, string askDir)
    {
        var items = SplitSubItemsIndexed(stem);
        if (items.Count < 2) return null;

        var reasons = new List<string>();
        var trueCount = 0;
        foreach (var (idx, text) in items)
        {
            var v = SubItemJudge.Judge(stem, text);
            if (v == Verdict.Undecided) return null;   // 任一子项不可判 → 整题 null
            if (v == Verdict.True) trueCount++;
            reasons.Add($"子项 {idx}：{text}（{(v == Verdict.True ? "正确" : "错误")}）");
        }

        var verdicts = new List<(string letter, bool ok)>();
        foreach (var kv in opts.OrderBy(x => x.Key))
        {
            bool ok;
            var m = Regex.Match(kv.Value, @"(\d+)\s*(?:项|个)");
            if (m.Success)
                ok = int.Parse(m.Groups[1].Value) == trueCount;
            else if (kv.Value.Contains("全部"))
                ok = trueCount == items.Count;
            else
                return null;
            verdicts.Add((kv.Key, ok));
            reasons.Add($"{kv.Key}：{kv.Value}（实际正确 {trueCount} 项 → {(ok ? "真" : "假")}）");
        }
        return ChooseUnique(verdicts, askDir, reasons);
    }

    /// <summary>
    /// 路径 1c：组合型——选项是 ①②③ 等子项编号的组合。仅处理正确方向：正确选项的编号集合
    /// 必须恰好等于真子项集合；任一子项 Undecided 或无唯一匹配 → null。不蒙数、零假阳性。
    /// </summary>
    private static ChoiceVerifier.Outcome? TryDecideCombination(string stem, Dictionary<string, string> opts, string askDir)
    {
        if (askDir != "correct") return null;   // 仅稳妥支持"正确组合"型；反向组合语义歧义 → 不判

        var items = SplitSubItemsIndexed(stem);
        if (items.Count < 2) return null;

        var trueSet = new HashSet<int>();
        var reasons = new List<string>();
        foreach (var (idx, text) in items)
        {
            var v = SubItemJudge.Judge(stem, text);
            if (v == Verdict.Undecided) return null;
            if (v == Verdict.True) trueSet.Add(idx);
            reasons.Add($"子项 {idx}：{text}（{(v == Verdict.True ? "正确" : "错误")}）");
        }

        var trueLabel = "{" + string.Join(",", trueSet.OrderBy(x => x)) + "}";
        var verdicts = new List<(string letter, bool ok)>();
        foreach (var kv in opts.OrderBy(x => x.Key))
        {
            var combo = ParseCombo(kv.Value);
            if (combo is null) return null;   // 选项无法解析为编号组合 → 整题不判
            var ok = combo.SetEquals(trueSet);
            verdicts.Add((kv.Key, ok));
            reasons.Add($"{kv.Key}：{kv.Value}（真子项集合 {trueLabel} → {(ok ? "真" : "假")}）");
        }
        return ChooseUnique(verdicts, askDir, reasons);
    }

    /// <summary>把选项文本解析为圆圈数字编号集合；无圆圈数字返回 null。</summary>
    private static HashSet<int>? ParseCombo(string opt)
    {
        var matches = Regex.Matches(opt, @"[①②③④⑤⑥⑦⑧⑨⑩]");
        if (matches.Count == 0) return null;
        var set = new HashSet<int>();
        foreach (Match m in matches)
        {
            var idx = CircledDigits.IndexOf(m.Value, StringComparison.Ordinal);
            if (idx < 0) return null;
            set.Add(idx + 1);
        }
        return set;
    }

    /// <summary>
    /// 把题干拆成带编号的子项（编号 1-based）。丢弃首个标记之前的引导语（题干问法部分），
    /// 因此每一项都是真正的子项陈述——与 <see cref="SplitSubItems"/> 不同：后者可能包含引导语，
    /// 原靠 <see cref="ExtractClaimedCount"/> 过滤掉，对一般陈述子项不适用。
    /// 供 Task 3 通用陈述子项 / 组合路径使用；不修改 SplitSubItems 既有口径（TR-3.2）。
    /// </summary>
    private static List<(int index, string text)> SplitSubItemsIndexed(string stem)
    {
        // 1) 圆圈数字 ①-⑩
        var circled = Regex.Matches(stem, @"[①②③④⑤⑥⑦⑧⑨⑩]");
        if (circled.Count >= 2)
        {
            var list = new List<(int, string)>();
            for (var i = 0; i < circled.Count; i++)
            {
                var start = circled[i].Index + circled[i].Length;
                var end = i + 1 < circled.Count ? circled[i + 1].Index : stem.Length;
                var idx = CircledDigits.IndexOf(circled[i].Value, StringComparison.Ordinal) + 1;
                var text = stem[start..end].Trim();
                if (text.Length > 0) list.Add((idx, text));
            }
            return list;
        }
        // 2) (1)(2) 或 ⑴⑵⑶（\u2474-\u2479）
        var paren = Regex.Matches(stem, @"\((\d+)\)|[\u2474\u2475\u2476\u2477\u2478\u2479]");
        if (paren.Count >= 2)
        {
            var list = new List<(int, string)>();
            for (var i = 0; i < paren.Count; i++)
            {
                var m = paren[i];
                var start = m.Index + m.Length;
                var end = i + 1 < paren.Count ? paren[i + 1].Index : stem.Length;
                var num = m.Groups[1].Value;
                var idx = !string.IsNullOrEmpty(num)
                    ? int.Parse(num)
                    : "\u2474\u2475\u2476\u2477\u2478\u2479".IndexOf(m.Value, StringComparison.Ordinal) + 1;
                var text = stem[start..end].Trim();
                if (text.Length > 0) list.Add((idx, text));
            }
            return list;
        }
        // 3) "1." "2." 行首编号
        var numbered = Regex.Matches(stem, @"(?<!\d)(\d+)[\.．、]");
        if (numbered.Count >= 2)
        {
            var list = new List<(int, string)>();
            for (var i = 0; i < numbered.Count; i++)
            {
                var m = numbered[i];
                var start = m.Index + m.Length;
                var end = i + 1 < numbered.Count ? numbered[i + 1].Index : stem.Length;
                var idx = int.Parse(m.Groups[1].Value);
                var text = stem[start..end].Trim();
                if (text.Length > 0) list.Add((idx, text));
            }
            return list;
        }
        return new List<(int, string)>();
    }

    /// <summary>按 ①②③ / (1)(2) / 1. 2. 等标记拆分子项。</summary>
    private static List<string> SplitSubItems(string stem)
    {
        var result = new List<string>();
        // 先尝试圆圈数字 ①-⑩
        var circled = Regex.Split(stem, @"[①②③④⑤⑥⑦⑧⑨⑩]")
            .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        if (circled.Count >= 2) return circled;

        // 再尝试 (1)(2)(3) 或 ⑴⑵⑶
        var paren = Regex.Split(stem, @"\(\d+\)|[⑴⑵⑶⑷⑸⑹]")
            .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        if (paren.Count >= 2) return paren;

        // 再尝试 "1." "2." 行首编号（仅当确实有多条）
        var numbered = Regex.Split(stem, @"(?<!\d)(\d+)[\.．、]")
            .Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        // Split 会把分隔符也作为元素返回，过滤掉纯数字
        var cleaned = numbered.Where(s => !Regex.IsMatch(s, @"^\d+$")).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        if (cleaned.Count >= 2) return cleaned;

        return result;
    }

    // ===================== 公共工具 =====================

    private static string AskDir(string stem)
    {
        if (Regex.IsMatch(stem, @"错误|不正确|不相符|有误")) return "wrong";
        if (Regex.IsMatch(stem, @"正确|符合|准确")) return "correct";
        // “下列过程涉及氧化还原反应的有”：计数满足某性质的项，等价于选真（correct）。
        // 负向回顾排除“不涉及氧化还原…”（保持 ?，不误判为 correct）。
        if (Regex.IsMatch(stem, @"(?<!不)涉及氧化还原")) return "correct";
        return "?";
    }

    // 子项/组合路径未线程化（选项为聚合编号，无逐选项事实锚点 → R4 留空），传空 hit 列表。
    private static ChoiceVerifier.Outcome? ChooseUnique(List<(string letter, bool ok)> verdicts, string askDir, List<string> reasons)
        => ChooseUnique(verdicts, askDir, reasons, new List<ChoiceReasoningNodes.OptionHit>());

    private static ChoiceVerifier.Outcome? ChooseUnique(List<(string letter, bool ok)> verdicts, string askDir, List<string> reasons, List<ChoiceReasoningNodes.OptionHit> optionHits)
    {
        var wantTrue = askDir == "correct";
        var candidates = verdicts.Where(v => v.ok == wantTrue).Select(v => v.letter).ToList();
        if (candidates.Count != 1) return null;
        var letter = candidates[0];
        var dirWord = wantTrue ? "正确选项" : "错误选项";
        var finalResult = $"{dirWord}为 {letter}";
        return new ChoiceVerifier.Outcome(letter, finalResult, reasons, ConstraintNote)
        {
            ReasoningNodes = ChoiceReasoningNodes.Build(optionHits, finalResult)
        };
    }
}
