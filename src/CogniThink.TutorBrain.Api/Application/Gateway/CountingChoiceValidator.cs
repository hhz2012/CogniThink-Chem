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

    public static ChoiceVerifier.Outcome? TryDecide(EngineRequest req)
    {
        var raw = req.RawText ?? "";
        var (stem, opts) = ChoiceVerifier.ParseOptions(raw);
        if (opts.Count < 2) return null;

        var askDir = AskDir(stem);
        if (askDir == "?") return null;

        // 路径一：选项是"N 项 / 全部" → 题干列子项，问正确的有几项
        if (LooksLikeCountOfCorrect(opts))
            return TryDecideCountOfCorrect(stem, opts, askDir);

        // 路径二：逐选项判定计数断言
        var reasons = new List<string>();
        var verdicts = new List<(string letter, bool ok)>();
        foreach (var kv in opts.OrderBy(x => x.Key))
        {
            var v = JudgeCountOption(kv.Value);
            if (v is null) return null; // 任一不可判 → 整题 null
            verdicts.Add((kv.Key, v.Value));
            reasons.Add($"{kv.Key}：{kv.Value}（计算/查表得 {(v.Value ? "真" : "假")}）");
        }
        return ChooseUnique(verdicts, askDir, reasons);
    }

    // ===================== 路径二：逐选项计数断言 =====================

    /// <summary>判定一个计数断言选项的真伪；不可判返回 null。</summary>
    private static bool? JudgeCountOption(string text)
    {
        var claimed = ExtractClaimedCount(text);
        if (claimed is null) return null;

        // 1) 枚举：解析分子式 + 限定 → 计算真值
        var trueCount = ComputeCountByFormula(text);
        if (trueCount is not null) return claimed == trueCount;

        // 2) 事实表：精确子串匹配
        var fact = MatchFact(text);
        if (fact is not null) return claimed == fact.Count;

        return null;
    }

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
        var itemOpts = opts.Values.Count(v => Regex.IsMatch(v, @"^\s*\d+\s*项\s*$") || v.Contains("全部"));
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
            var v = JudgeCountOption(it);
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
        return "?";
    }

    private static ChoiceVerifier.Outcome? ChooseUnique(List<(string letter, bool ok)> verdicts, string askDir, List<string> reasons)
    {
        var wantTrue = askDir == "correct";
        var candidates = verdicts.Where(v => v.ok == wantTrue).Select(v => v.letter).ToList();
        if (candidates.Count != 1) return null;
        var letter = candidates[0];
        var dirWord = wantTrue ? "正确选项" : "错误选项";
        return new ChoiceVerifier.Outcome(letter, $"{dirWord}为 {letter}", reasons, ConstraintNote);
    }
}
