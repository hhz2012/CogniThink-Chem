using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// Tier 2 / AC-16：物质分类选择题逐选项校验。
/// <para>真值源：<c>data/highschool/substance-classification.json</c>（物质→标签、同素异形体/同位素组）。</para>
/// <para>仅当：① 所有选项都能干净解析为分类断言；② 断言涉及的每个物质都在事实库命中；
/// ③ 问法方向（正确/错误）可判；④ 满足方向的选项恰好唯一 → 返回答案字母。否则返回 null。</para>
/// <para>红线：不蒙数、零假阳性。任一选项无法判定则整题不臆断。</para>
/// </summary>
public static class ClassificationValidator
{
    private const string ConstraintNote =
        "【高中限定】物质分类判定基于 data/highschool/substance-classification.json 事实库；" +
        "任一选项无法解析或物质不在库则不臆断。";

    private static readonly Lazy<Indexes> _index = new(BuildIndexes);

    private sealed class Indexes
    {
        public readonly Dictionary<string, ClassifiedSubstance> ByFormula = new(StringComparer.Ordinal);
        public readonly Dictionary<string, ClassifiedSubstance> ByName = new(StringComparer.Ordinal);
        public readonly HashSet<string> Tags = new(StringComparer.Ordinal);
        public readonly Dictionary<string, string> AllotropeMember = new(StringComparer.Ordinal); // token -> groupKey
        public readonly Dictionary<string, string> IsotopeMember = new(StringComparer.Ordinal);
        public List<ClassifiedSubstance> Substances = new();
    }

    /// <summary>尝试对一道物质分类选择题唯一求解；不能唯一判定返回 null。</summary>
    public static ChoiceVerifier.Outcome? TryDecide(EngineRequest req)
    {
        var rawText = req.RawText ?? string.Empty;
        var (stem, opts) = ChoiceVerifier.ParseOptions(rawText);
        if (opts.Count < 2) return null;

        var askDir = AskDir(stem);
        if (askDir != "correct" && askDir != "wrong") return null;

        var letters = opts.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        var idx = _index.Value;

        // stem 级别信息：
        //   P1（选项为「属于 Y」，物质在题干）→ stemMember；
        //   P4（选项为裸物质，类别在题干）→ stemCat；
        //   P5（关系类别在题干，选项为裸物质组，如「互为同素异形体的是」）→ stemRelation。
        var stemMember = ResolveUniqueStemMember(stem, idx);
        var stemCat = ExtractStemCategory(stem, idx);
        var stemRelation = ExtractStemRelation(stem);

        var optionCorrect = new Dictionary<string, bool>();
        var reasons = new List<string>();
        foreach (var L in letters)
        {
            var ok = EvalOption(opts[L], stemMember, stemCat, stemRelation, idx, out var reason);
            if (ok is null) return null;            // 任一选项无法干净判定 → 整题不臆断
            optionCorrect[L] = ok.Value;
            reasons.Add($"{L}. {reason}");
        }

        List<string> candidates = askDir == "correct"
            ? letters.Where(l => optionCorrect[l]).ToList()
            : letters.Where(l => !optionCorrect[l]).ToList();

        if (candidates.Count != 1) return null;
        var letter = candidates[0];
        var dirWord = askDir == "correct" ? "正确" : "错误";
        reasons.Add($"依据分类事实库，{dirWord}选项唯一为 {letter}。");
        return new ChoiceVerifier.Outcome(
            AnswerLetter: letter,
            FinalResult: $"{dirWord}选项为 {letter}",
            Reasons: reasons,
            ConstraintNote: ConstraintNote);
    }

    // ---------- 问法 / 题干抽取 ----------

    private static string AskDir(string stem)
    {
        if (Regex.IsMatch(stem, @"错误|不正确|不属于|不互为")) return "wrong";
        // 关系类问法「互为同素异形体/同位素的是」默认选正确项；无「正确/属于」字样也能判向。
        // 「均为 X 的是」「属于 X 的是」均为正向选正确项。
        if (Regex.IsMatch(stem, @"正确|分类正确|属于|均为|同素异形体|同位素|同分异构体")) return "correct";
        return "?";
    }

    private static string? ExtractStemCategory(string stem, Indexes idx)
    {
        // 允许单字类别（盐/酸/碱），{2,10} 会漏掉「属于盐的是」这类题干；
        // 兼容「属于 X」「均为 X」「为 X」三种题干写法（如 t2c-31「均为强酸」）。
        var m = Regex.Match(stem, @"(?:不)?(?:属于|均为|为)\s*(?<cat>[^的是。\s，]{1,10})");
        if (!m.Success) return null;
        var cat = Norm(m.Groups["cat"].Value);
        return idx.Tags.Contains(cat) ? cat : null;
    }

    private static string? ExtractStemRelation(string stem)
    {
        if (stem.Contains("同素异形体")) return "同素异形体";
        if (stem.Contains("同位素")) return "同位素";
        if (stem.Contains("同分异构体")) return "同分异构体";
        return null;
    }

    /// <summary>题干中唯一出现（最长匹配、去包含）的物质，用于「属于 Y」型选项；不唯一则 null。</summary>
    private static ClassifiedSubstance? ResolveUniqueStemMember(string stem, Indexes idx)
    {
        var normStem = Norm(stem);
        var hits = new List<(ClassifiedSubstance sub, int start, int len)>();
        foreach (var sub in idx.Substances)
        {
            CollectHits(normStem, Norm(sub.Formula), sub, hits);
            foreach (var name in sub.Names)
                CollectHits(normStem, Norm(name), sub, hits);
        }
        // 去掉被更长命中完全包含的短命中（如 FeSO4 中的 Fe / CO2 中的 O2）
        var filtered = hits.Where(h => !hits.Any(o =>
            !ReferenceEquals(o.sub, h.sub) && o.start <= h.start && o.start + o.len >= h.start + h.len && o.len > h.len)).ToList();
        var distinct = filtered.Select(h => h.sub).Distinct().ToList();
        return distinct.Count == 1 ? distinct[0] : null;
    }

    private static void CollectHits(string haystack, string needle, ClassifiedSubstance sub, List<(ClassifiedSubstance, int, int)> hits)
    {
        if (needle.Length < 2) return;
        var i = haystack.IndexOf(needle, StringComparison.Ordinal);
        while (i >= 0)
        {
            hits.Add((sub, i, needle.Length));
            i = haystack.IndexOf(needle, i + 1, StringComparison.Ordinal);
        }
    }

    // ---------- 选项断言解析 ----------

    private static bool? EvalOption(string text, ClassifiedSubstance? stemMember, string? stemCat, string? stemRelation, Indexes idx, out string reason)
    {
        reason = "";
        var t = text.Trim();
        if (t.Length == 0) return null;

        // P2: 「category：members」
        var m2 = Regex.Match(t, @"^(?<cat>[^：:]{1,10})[：:]\s*(?<rest>.+)$");
        if (m2.Success)
        {
            var cat = Norm(m2.Groups["cat"].Value);
            var members = SplitMembers(m2.Groups["rest"].Value);
            if (members.Count == 0) return null;
            if (IsRelation(cat)) return EvalRelation(cat, members, idx, out reason);
            if (!idx.Tags.Contains(cat)) return null;
            return EvalTagChecks(members, cat, idx, out reason);
        }

        // P1: 「属于/是/为 Y」，成员来自题干
        var m1 = Regex.Match(t, @"^(?:属于|是|为)\s*(?<cat>[^。；，]+)$");
        if (m1.Success && stemMember is not null)
        {
            var cat = Norm(m1.Groups["cat"].Value);
            if (idx.Tags.Contains(cat))
            {
                var hold = HasTag(stemMember, cat);
                reason = $"{SubstanceLabel(stemMember)} 属于「{cat}」：{(hold ? "是" : "否")}";
                return hold;
            }
            return null;
        }

        // P3: 「members 是/为 category」
        var m3 = Regex.Match(t, @"^(?<rest>.+?)[是为]\s*(?<cat>[^是为。；，]+?)\s*$");
        if (m3.Success)
        {
            var cat = Norm(m3.Groups["cat"].Value);
            var members = SplitMembers(m3.Groups["rest"].Value);
            if (members.Count == 0) return null;
            if (IsRelation(cat)) return EvalRelation(cat, members, idx, out reason);
            if (!idx.Tags.Contains(cat)) return null;
            return EvalTagChecks(members, cat, idx, out reason);
        }

        // P4: 裸物质，类别来自题干
        if (stemCat is not null)
        {
            var members = SplitMembers(t);
            if (members.Count > 0)
                return EvalTagChecks(members, stemCat, idx, out reason);
        }

        // P5: 关系类别来自题干，选项为裸物质组（如「互为同素异形体的是」）
        if (stemRelation is not null)
        {
            var members = SplitMembers(t);
            if (members.Count > 0)
                return EvalRelation(stemRelation, members, idx, out reason);
        }
        return null;
    }

    private static bool? EvalTagChecks(List<string> members, string cat, Indexes idx, out string reason)
    {
        var parts = new List<string>();
        var allOk = true;
        foreach (var tok in members)
        {
            var sub = FindSubstance(tok, idx);
            if (sub is null) { reason = ""; return null; }   // 物质不在库 → 不臆断
            var has = HasTag(sub, cat);
            if (!has) allOk = false;
            parts.Add($"{SubstanceLabel(sub)}:{(has ? "是" : "否")}{cat}");
        }
        reason = $"「{cat}」({string.Join("；", parts)}) → {(allOk ? "全部成立" : "存在错误")}";
        return allOk;
    }

    private static bool? EvalRelation(string relation, List<string> members, Indexes idx, out string reason)
    {
        var group = relation switch
        {
            "同素异形体" => idx.AllotropeMember,
            "同位素" => idx.IsotopeMember,
            _ => null
        };
        if (group is null) { reason = ""; return null; }

        var keys = new List<string>();
        foreach (var tok in members)
        {
            var known = FindSubstance(tok, idx) is not null || idx.AllotropeMember.ContainsKey(tok) || idx.IsotopeMember.ContainsKey(tok);
            if (!known) { reason = ""; return null; }   // 完全未知 → 不臆断
            keys.Add(group.TryGetValue(tok, out var k) ? k : "");
        }
        var allOk = keys.Count > 0 && keys.All(k => k.Length > 0) && keys.Distinct().Count() == 1;
        reason = $"「{relation}」({string.Join("、", members)}) → {(allOk ? "成立" : "不成立")}";
        return allOk;
    }

    // ---------- 工具 ----------

    private static bool IsRelation(string cat) => cat is "同素异形体" or "同位素" or "同分异构体";

    private static bool HasTag(ClassifiedSubstance sub, string cat) =>
        sub.Tags.Any(t => string.Equals(Norm(t), cat, StringComparison.Ordinal));

    private static ClassifiedSubstance? FindSubstance(string token, Indexes idx)
    {
        if (token.Length == 0) return null;
        if (idx.ByFormula.TryGetValue(token, out var s)) return s;
        if (idx.ByName.TryGetValue(token, out s)) return s;
        return null;
    }

    private static List<string> SplitMembers(string rest) =>
        // 分隔符涵盖顿号/逗号/斜杠/分号及连词「和、与、跟」（关系类问法如「金刚石和石墨」常见）；
        // 事实库物质名不含这些字（已核库），不会误伤。
        rest.Split(new[] { '、', '，', ',', '/', '／', '；', ';', '和', '与', '跟' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Norm)
            .Where(s => s.Length > 0)
            .ToList();

    private static string SubstanceLabel(ClassifiedSubstance sub) =>
        string.IsNullOrEmpty(sub.Formula) ? (sub.Names.FirstOrDefault() ?? "?") : sub.Formula;

    private static Indexes BuildIndexes()
    {
        var data = RuleStore.Load().Query<ClassificationData>("substance-classification");
        var idx = new Indexes { Substances = data.Substances };
        foreach (var s in data.Substances)
        {
            var nf = Norm(s.Formula);
            if (nf.Length > 0 && !idx.ByFormula.ContainsKey(nf)) idx.ByFormula[nf] = s;
            foreach (var name in s.Names)
            {
                var nn = Norm(name);
                if (nn.Length > 0 && !idx.ByName.ContainsKey(nn)) idx.ByName[nn] = s;
            }
            foreach (var tag in s.Tags) idx.Tags.Add(Norm(tag));
        }
        foreach (var kv in data.AllotropeGroups)
            foreach (var m in kv.Value) idx.AllotropeMember[Norm(m)] = kv.Key;
        foreach (var kv in data.IsotopeGroups)
            foreach (var m in kv.Value) idx.IsotopeMember[Norm(m)] = kv.Key;
        return idx;
    }

    /// <summary>归一化：Unicode 上下标数字→ASCII、去空白、全角→半角；不改大小写（Co≠CO）。</summary>
    internal static string Norm(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (char.IsWhiteSpace(ch)) continue;
            var c = ch;
            if (c >= '\u2080' && c <= '\u2089') { sb.Append((char)('0' + (c - '\u2080'))); continue; }
            if (c == '\u2070') { sb.Append('0'); continue; }
            if (c == '\u00B9') { sb.Append('1'); continue; }
            if (c == '\u00B2') { sb.Append('2'); continue; }
            if (c == '\u00B3') { sb.Append('3'); continue; }
            if (c >= '\u2074' && c <= '\u2079') { sb.Append((char)('4' + (c - '\u2074'))); continue; }
            if (c == '\u207A') { sb.Append('+'); continue; }
            if (c == '\u207B') { sb.Append('-'); continue; }
            if (c >= '\uFF01' && c <= '\uFF5E') { sb.Append((char)(c - 0xFEE0)); continue; }
            if (c == '\u3000') continue;
            sb.Append(c);
        }
        return sb.ToString();
    }
}
