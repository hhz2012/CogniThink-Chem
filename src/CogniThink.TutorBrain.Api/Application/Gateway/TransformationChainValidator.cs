using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// Tier 2 / AC-17：物质转化链选择题逐选项校验。
/// <para>真值源：<c>data/highschool/transformation-rules.json</c>（可行转化规则）+
/// <c>data/highschool/substance-classification.json</c>（名称→分子式归一化）。</para>
/// <para>仅当：① 所有选项都能干净解析为转化链；② 问法方向（能实现/不能实现）可判；
/// ③ 满足方向的选项恰好唯一 → 返回答案字母。否则返回 null。</para>
/// <para>红线：不蒙数、零假阳性。任一选项无法解析为链、或无唯一解，则整题不臆断。
/// 逻辑精确复刻 _evalscratch/verify_t2t.py（已在全题库扫描验证 0 假阳性）。</para>
/// <para>本类内的 NAME_ALIAS / REAGENT_ALIAS / ACID_CONC / COND_SET 是<b>显示名归一化</b>映射
/// （题干写法→标准分子式），<b>不是</b>化学事实；「某步转化是否可行」的真值完全来自规则库 JSON。</para>
/// </summary>
public static class TransformationChainValidator
{
    private const string ConstraintNote =
        "【高中限定】转化链判定基于 data/highschool/transformation-rules.json 规则库；" +
        "任一选项无法解析为链、或满足问法的选项不唯一，则不臆断。";

    private static readonly Lazy<ChainIndexes> _index = new(BuildIndexes);

    private sealed class ChainIndexes
    {
        public readonly Dictionary<(string from, string to), List<TransformationStep>> ByPair =
            new();
        public readonly Dictionary<string, string> ByFormula = new(StringComparer.Ordinal); // 归一化分子式 → 标准分子式
        public readonly Dictionary<string, string> ByName = new(StringComparer.Ordinal);    // 归一化名称 → 标准分子式
        public int RuleCount;
    }

    private sealed record Step(string From, HashSet<string> Reag, HashSet<string> Cond, string To);

    // ---------- 显示名归一化映射（非化学事实，仅用于把题干写法归一成可查库的分子式） ----------

    private static readonly Dictionary<string, string> NameAlias = new(StringComparer.Ordinal)
    {
        ["食盐水"]="NaCl",["饱和食盐水"]="NaCl",["盐水"]="NaCl",["漂白粉"]="漂白粉",
        ["小苏打"]="NaHCO3",["苏打"]="Na2CO3",["纯碱"]="Na2CO3",
        ["熟石灰"]="Ca(OH)2",["消石灰"]="Ca(OH)2",["澄清石灰水"]="Ca(OH)2",["石灰水"]="Ca(OH)2",["石灰乳"]="Ca(OH)2",
        ["生石灰"]="CaO",["石灰石"]="CaCO3",["大理石"]="CaCO3",
        ["烧碱"]="NaOH",["火碱"]="NaOH",["苛性钠"]="NaOH",["苛性钾"]="KOH",
        ["干冰"]="CO2",["冰"]="H2O",["水银"]="Hg",["硫磺"]="S",["硫"]="S",
        ["液氯"]="Cl2",["氯水"]="Cl2",["液氨"]="NH3",["氨水"]="NH3·H2O",
        ["双氧水"]="H2O2",["过氧化氢"]="H2O2",
        ["沼气"]="CH4",["天然气"]="CH4",["甲烷"]="CH4",
        ["酒精"]="C2H5OH",["乙醇"]="C2H5OH",["醋酸"]="CH3COOH",["乙酸"]="CH3COOH",
        ["葡萄糖"]="C6H12O6",["蔗糖"]="C12H22O11",["水玻璃"]="Na2SiO3",["金刚砂"]="SiC",
        ["胆矾"]="CuSO4",["蓝矾"]="CuSO4",["绿矾"]="FeSO4",["明矾"]="KAl(SO4)2",
        ["芒硝"]="Na2SO4",["石膏"]="CaSO4",["重晶石"]="BaSO4",
        ["硅石"]="SiO2",["石英"]="SiO2",["水晶"]="SiO2",["光导纤维"]="SiO2",
        ["刚玉"]="Al2O3",["铝土矿"]="Al2O3",["赤铁矿"]="Fe2O3",["磁铁矿"]="Fe3O4",
        ["食盐"]="NaCl",["粗盐"]="NaCl",
    };

    private static readonly string[] PrefixStrip = { "饱和", "无水", "粗", "高纯", "新制", "稀", "浓" };
    private static readonly string[] StateSuffix = { "溶液", "熔融", "固体", "液体", "气体", "水溶液", "悬浊液", "乳浊液", "胶体" };

    private static readonly HashSet<string> CondSet = new(StringComparer.Ordinal)
    {
        "高温","加热","△","点燃","催化剂","放电","光照","电解","通电","蒸干","灼烧","煮沸","微生物","酶","高压","常温","熔融电解","熔融"
    };

    private static readonly Dictionary<string, string> ReagentAlias = new(StringComparer.Ordinal)
    {
        ["氯气"]="Cl2",["液氯"]="Cl2",["氯水"]="Cl2",
        ["盐酸"]="HCl",["稀盐酸"]="HCl",["浓盐酸"]="浓HCl",
        ["硫酸"]="H2SO4",["稀硫酸"]="H2SO4",["浓硫酸"]="浓H2SO4",
        ["硝酸"]="HNO3",["稀硝酸"]="HNO3",["浓硝酸"]="浓HNO3",
        ["水"]="H2O",["蒸馏水"]="H2O",
        ["氧气"]="O2",["空气"]="O2",["过量空气"]="O2",["过量氧气"]="O2",
        ["二氧化碳"]="CO2",["过量CO2"]="CO2",["过量二氧化碳"]="CO2",
        ["一氧化碳"]="CO",["氢气"]="H2",["氮气"]="N2",["氨气"]="NH3",
        ["氯化氢"]="HCl",["硫化氢"]="H2S",["二氧化硫"]="SO2",["三氧化硫"]="SO3",
        ["氢氧化钠"]="NaOH",["氢氧化钾"]="KOH",
        ["氢氧化钙"]="Ca(OH)2",["澄清石灰水"]="Ca(OH)2",["石灰水"]="Ca(OH)2",["石灰乳"]="Ca(OH)2",
        ["氢氧化钡"]="Ba(OH)2",["氨水"]="NH3·H2O",["一水合氨"]="NH3·H2O",
        ["氯化钠"]="NaCl",["氯化钙"]="CaCl2",["氯化钡"]="BaCl2",
        ["氯化铁"]="FeCl3",["氯化亚铁"]="FeCl2",["氯化铜"]="CuCl2",["氯化铵"]="NH4Cl",
        ["硝酸银"]="AgNO3",["硝酸钡"]="Ba(NO3)2",
        ["碳酸钠"]="Na2CO3",["碳酸氢钠"]="NaHCO3",["碳酸钾"]="K2CO3",
        ["次氯酸钠"]="NaClO",["次氯酸钙"]="Ca(ClO)2",["氯酸钾"]="KClO3",
        ["高锰酸钾"]="KMnO4",["二氧化锰"]="MnO2",["过氧化氢"]="H2O2",["双氧水"]="H2O2",
        ["铁粉"]="Fe",["铁"]="Fe",["铜粉"]="Cu",["铜"]="Cu",["铝粉"]="Al",["铝"]="Al",
        ["锌"]="Zn",["锌粉"]="Zn",["镁"]="Mg",["碳"]="C",["木炭"]="C",["焦炭"]="C",
        ["硫"]="S",["硫磺"]="S",["磷"]="P",
        ["KSCN"]="KSCN",["硫氰化钾"]="KSCN",["硫氰酸钾"]="KSCN",
        ["KI"]="KI",["碘化钾"]="KI",["淀粉"]="淀粉",
        ["乙醇"]="C2H5OH",["酒精"]="C2H5OH",["醋酸"]="CH3COOH",["乙酸"]="CH3COOH",
        ["NaAlO2"]="NaAlO2",["偏铝酸钠"]="NaAlO2",["硅酸钠"]="Na2SiO3",
        ["Na2O2"]="Na2O2",["过氧化钠"]="Na2O2",["Na2O"]="Na2O",["氧化钠"]="Na2O",
    };

    // 酸的浓/稀前缀必须保留（Cu+稀硫酸 不应可行）；规则指定浓度时选项须同浓度。
    private static readonly Dictionary<string, string> AcidConc = new(StringComparer.Ordinal)
    {
        ["稀硫酸"]="稀H2SO4",["浓硫酸"]="浓H2SO4",
        ["稀盐酸"]="稀HCl",["浓盐酸"]="浓HCl",
        ["稀硝酸"]="稀HNO3",["浓硝酸"]="浓HNO3",
        ["稀H2SO4"]="稀H2SO4",["浓H2SO4"]="浓H2SO4",
        ["稀HCl"]="稀HCl",["浓HCl"]="浓HCl",
        ["稀HNO3"]="稀HNO3",["浓HNO3"]="浓HNO3",
    };

    // 连接器：可选注解（平衡括号/方括号，支持 Ca(OH)2 内嵌括号）+ 箭头
    private static readonly Regex ConnRe = new Regex(
        @"--?\s*(\((?:[^()]|\([^()]*\))*\)|\[(?:[^\[\]]|\[[^\[\]]*\])*\])\s*--?>|-{1,2}>|->|→",
        RegexOptions.Compiled);

    private static readonly Regex StateSuffixRe = new Regex(
        @"[\(（](?:s|l|g|aq|S|L|G|AQ|固体|液体|气体|溶液|熔融)[\)）]\s*$", RegexOptions.Compiled);
    private static readonly Regex PrefixParenRe = new Regex(
        @"^\s*[\(（](?:粗|纯|高纯|无水|新制|稀|浓)[\)）]\s*", RegexOptions.Compiled);
    private static readonly Regex TempRe = new Regex(@"^(\d+)℃$", RegexOptions.Compiled);

    /// <summary>尝试对一道物质转化链选择题唯一求解；不能唯一判定返回 null。</summary>
    public static ChoiceVerifier.Outcome? TryDecide(EngineRequest req)
    {
        var rawText = req.RawText ?? string.Empty;
        var (stem, opts) = ChoiceVerifier.ParseOptions(rawText);
        if (opts.Count < 2) return null;

        var askDir = AskDirT(stem);
        if (askDir != "correct" && askDir != "wrong") return null;

        var letters = opts.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        var idx = _index.Value;

        var optionOk = new Dictionary<string, bool>();
        var reasons = new List<string>();
        foreach (var L in letters)
        {
            var steps = ParseChain(opts[L]);
            if (steps == null || steps.Count == 0) return null;   // 任一选项无法解析为链 → 整题不臆断
            var ok = ChainOk(steps, idx, out var reason);
            if (ok is null) return null;
            optionOk[L] = ok.Value;
            reasons.Add($"{L}. {reason}");
        }

        List<string> candidates = askDir == "correct"
            ? letters.Where(l => optionOk[l]).ToList()
            : letters.Where(l => !optionOk[l]).ToList();

        if (candidates.Count != 1) return null;   // 不唯一（如 pdf-017 A/D 均可行）→ 诚实返回 null
        var letter = candidates[0];
        var dirWord = askDir == "correct" ? "正确" : "错误";
        reasons.Add($"依据转化链规则库，{dirWord}选项唯一为 {letter}。");
        return new ChoiceVerifier.Outcome(
            AnswerLetter: letter,
            FinalResult: $"{dirWord}选项为 {letter}",
            Reasons: reasons,
            ConstraintNote: ConstraintNote);
    }

    /// <summary>规则库步数（供测试护栏断言 ≥50）。</summary>
    public static int RuleCount => _index.Value.RuleCount;

    // ---------- 问法方向 ----------

    private static string AskDirT(string stem)
    {
        // 先判负向，避免「不正确」被「正确」吃掉
        if (Regex.IsMatch(stem, @"不能实现|无法实现|不能一步|不符合|错误|不正确")) return "wrong";
        if (Regex.IsMatch(stem, @"能实现|可以实现|能够实现|一步实现|符合|正确")) return "correct";
        return "?";
    }

    // ---------- 链解析 ----------

    private static List<Step>? ParseChain(string text)
    {
        var conns = ConnRe.Matches(text);
        if (conns.Count > 0)
        {
            var subs = new List<string>();
            var anns = new List<string>();
            var prev = 0;
            foreach (Match m in conns)
            {
                subs.Add(text.Substring(prev, m.Index - prev));
                anns.Add(StripAnnParens(m.Groups[1].Success ? m.Groups[1].Value : ""));
                prev = m.Index + m.Length;
            }
            subs.Add(text.Substring(prev));

            var steps = new List<Step>();
            for (var i = 0; i < conns.Count; i++)
            {
                // 剥前导「工业制取漂白粉：」标签，取冒号后物质段
                var s0 = subs[i].Split(new[] { '：', ':' }).LastOrDefault() ?? "";
                var s1 = subs[i + 1].Split(new[] { '：', ':' }).LastOrDefault() ?? "";
                var frm = NormSub(s0);
                var to = NormSub(s1);
                var (reag, cond) = SplitAnn(anns[i]);
                if (frm.Length > 0 && to.Length > 0)
                    steps.Add(new Step(frm, reag, cond, to));
            }
            return steps.Count >= 1 ? steps : null;
        }

        // 裸三物质格式（pdf-015 式）："Na、NaOH、NaCl" → 两两成步，无试剂无条件
        if (text.Contains('、') || text.Contains(','))
        {
            var toks = text.Split(new[] { '、', '，', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => NormSub(t.Trim()))
                .Where(t => t.Length > 0)
                .ToList();
            if (toks.Count >= 2)
            {
                var steps = new List<Step>();
                for (var i = 0; i < toks.Count - 1; i++)
                    steps.Add(new Step(toks[i], new HashSet<string>(StringComparer.Ordinal),
                        new HashSet<string>(StringComparer.Ordinal), toks[i + 1]));
                return steps;
            }
        }
        return null;
    }

    private static string StripAnnParens(string ann)
    {
        if (ann.Length == 0) return "";
        var a = ann.Trim();
        if (a.Length >= 2 &&
            ((a[0] == '(' && a[^1] == ')') || (a[0] == '[' && a[^1] == ']') || (a[0] == '（' && a[^1] == '）')))
            a = a.Substring(1, a.Length - 2);
        return a.Trim();
    }

    private static (HashSet<string> reag, HashSet<string> cond) SplitAnn(string ann)
    {
        var reag = new HashSet<string>(StringComparer.Ordinal);
        var cond = new HashSet<string>(StringComparer.Ordinal);
        if (ann.Length == 0) return (reag, cond);
        var toks = ann.Split(new[] { '、', '，', ',', ';', '；', '/', '／', ' ', '\t', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries);
        foreach (var t in toks)
        {
            if (IsCond(t)) cond.Add(NormCond(t));
            else reag.Add(NormReagent(t));
        }
        return (reag, cond);
    }

    private static bool IsCond(string tok)
    {
        var t = Norm(tok).Trim();
        if (CondSet.Contains(t)) return true;
        if (TempRe.IsMatch(t)) return true;
        return false;
    }

    /// <summary>条件词归一：同义词「加热」→△（规则库统一用 △）；数字温度 N℃→高温(N≥500)/△。</summary>
    private static string NormCond(string c)
    {
        var t = Norm(c).Trim();
        if (t == "加热") return "△";
        var m = TempRe.Match(t);
        if (m.Success)
            return int.Parse(m.Groups[1].Value) >= 500 ? "高温" : "△";
        return t;
    }

    /// <summary>规则要求的每个条件须由选项提供（归一化后取子集）。</summary>
    private static bool CondSatisfies(List<string> ruleConds, HashSet<string> optCond)
    {
        foreach (var rc in ruleConds)
            if (!optCond.Contains(NormCond(rc))) return false;
        return true;
    }

    // ---------- 单步可行性判定（核心三分支，与 verify_t2t.py 完全一致） ----------

    private static bool? StepFeasible(string frm, HashSet<string> reag, HashSet<string> cond, string to, ChainIndexes idx)
    {
        if (!idx.ByPair.TryGetValue((frm, to), out var matches) || matches.Count == 0)
            return false;   // 无此对规则 → 断链（裸三物质格式中未收录的转化视为不可一步实现）

        if (reag.Count == 0 && cond.Count == 0)
            // 真裸箭头（三物质格式，试剂/条件略去）：成对存在即可判可行，
            // 但若该对仅收录了 feasible:false 规则（明确标注不可行），仍判不可行。
            return matches.Any(r => r.Feasible);

        // 试剂轮廓是否匹配选项分支：纯条件箭头要求规则不消耗试剂；带试剂箭头要求规则消耗试剂且全部满足。
        bool ReagFits(TransformationStep r)
        {
            var req = NormReaList(r.Reagents);
            if (reag.Count == 0) return req.Count == 0;
            return req.Count > 0 && ReagSatisfies(req, reag);
        }

        // 尊重 feasible 标志 + 条件校验（规则要求的条件须由选项提供）：
        // 匹配到可行规则→可行；仅匹配到不可行规则→不可行；都不匹配→断链。
        foreach (var r in matches)
            if (r.Feasible && ReagFits(r) && CondSatisfies(r.Conditions, cond))
                return true;
        foreach (var r in matches)
            if (!r.Feasible && ReagFits(r) && CondSatisfies(r.Conditions, cond))
                return false;
        return false;
    }

    private static bool? ChainOk(List<Step> steps, ChainIndexes idx, out string reason)
    {
        var parts = new List<string>();
        foreach (var s in steps)
        {
            var f = StepFeasible(s.From, s.Reag, s.Cond, s.To, idx);
            if (f is null) { reason = ""; return null; }
            parts.Add(f.Value ? $"{s.From}→{s.To}可行" : $"{s.From}→{s.To}断链");
            if (!f.Value)
            {
                reason = string.Join("；", parts) + " → 全链不成立";
                return false;
            }
        }
        reason = string.Join("；", parts) + " → 全链可行";
        return true;
    }

    // ---------- 归一化 ----------

    /// <summary>复用 ClassificationValidator.Norm（上下标/全角归一，保留大小写）。</summary>
    private static string Norm(string s) => ClassificationValidator.Norm(s);

    private static string NormSub(string tok)
    {
        var t = Norm(tok).Trim();
        t = StateSuffixRe.Replace(t, "");
        t = PrefixParenRe.Replace(t, "");
        bool changed;
        do
        {
            changed = false;
            foreach (var p in PrefixStrip)
                if (t.StartsWith(p, StringComparison.Ordinal) && t.Length > p.Length)
                { t = t.Substring(p.Length); changed = true; break; }
        } while (changed);
        do
        {
            changed = false;
            foreach (var s in StateSuffix)
                if (t.EndsWith(s, StringComparison.Ordinal) && t.Length > s.Length)
                { t = t.Substring(0, t.Length - s.Length); changed = true; break; }
        } while (changed);
        t = t.Trim();
        if (NameAlias.TryGetValue(t, out var a)) t = a;
        if (_index.Value.ByFormula.TryGetValue(t, out var f)) return f;
        if (_index.Value.ByName.TryGetValue(t, out f)) return f;
        return t;
    }

    private static string NormReagent(string tok)
    {
        var t = Norm(tok).Trim();
        if (AcidConc.TryGetValue(t, out var c)) return c;
        foreach (var p in new[] { "过量", "足量", "适量", "少量" })
            if (t.StartsWith(p, StringComparison.Ordinal) && t.Length > p.Length)
            { t = t.Substring(p.Length); break; }
        if (ReagentAlias.TryGetValue(t, out var a)) t = a;
        var idx = _index.Value;
        if (idx.ByFormula.TryGetValue(t, out var f)) t = f;
        else if (idx.ByName.TryGetValue(t, out f)) t = f;
        return t;
    }

    private static string Conc(string r)
    {
        if (r.StartsWith("浓", StringComparison.Ordinal)) return "浓";
        if (r.StartsWith("稀", StringComparison.Ordinal)) return "稀";
        return "";
    }
    private static string Base(string r) => Regex.Replace(r, @"^(浓|稀)", "");

    /// <summary>规则要求的每种试剂须由选项提供；规则指定浓度时须同浓度，未指定则接受任意浓度。</summary>
    private static bool ReagSatisfies(HashSet<string> reqSet, HashSet<string> optSet)
    {
        foreach (var r in reqSet)
        {
            var br = Base(r);
            var cr = Conc(r);
            var found = false;
            foreach (var o in optSet)
            {
                if (string.Equals(Base(o), br, StringComparison.Ordinal) &&
                    (cr.Length == 0 || string.Equals(Conc(o), cr, StringComparison.Ordinal)))
                { found = true; break; }
            }
            if (!found) return false;
        }
        return true;
    }

    private static HashSet<string> NormReaList(List<string> lst) =>
        new(lst.Select(NormReagent), StringComparer.Ordinal);

    // ---------- 索引构建 ----------

    private static ChainIndexes BuildIndexes()
    {
        var idx = new ChainIndexes();

        // 转化规则库
        var rules = RuleStore.Load().Query<TransformationRuleData>("transformation-rules");
        foreach (var s in rules.Steps)
        {
            var key = (Norm(s.From), Norm(s.To));
            if (!idx.ByPair.TryGetValue(key, out var list))
            {
                list = new List<TransformationStep>();
                idx.ByPair[key] = list;
            }
            list.Add(s);
            idx.RuleCount++;
        }

        // 分类事实库（名称→分子式，供归一化）
        var cls = RuleStore.Load().Query<ClassificationData>("substance-classification");
        foreach (var s in cls.Substances)
        {
            var nf = Norm(s.Formula);
            if (nf.Length > 0 && !idx.ByFormula.ContainsKey(nf)) idx.ByFormula[nf] = s.Formula;
            foreach (var name in s.Names)
            {
                var nn = Norm(name);
                if (nn.Length > 0 && !idx.ByName.ContainsKey(nn)) idx.ByName[nn] = s.Formula;
            }
        }
        return idx;
    }
}
