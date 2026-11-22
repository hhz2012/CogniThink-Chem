using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// Tier 2 / AC-18：NA（阿伏伽德罗常数）粒子计数选择题逐选项校验——最保守。
/// <para>真值源：<c>data/highschool/na-particle-facts.json</c>（每式 e/p/n/原子/σ/π/共价键/离子/氢键 + 标况物态 + 水解离子）。</para>
/// <para>仅当：① 题干锚定 NA；② 问法方向可判；③ <b>四个选项全部</b>能干净解析为
/// <c>[数量][单位][物质]…[粒子]…[k]·NA</c> 并唯一计算真伪；④ 满足方向的选项唯一 → 返回答案字母。否则返回 null。</para>
/// <para>红线（不蒙数、零假阳性）：任一选项涉及氧化还原电子转移、反应、同位素（不在库）、浓度缺体积等不可干净计算的情形 →
/// 该选项记 unknown → 整题不臆断。逻辑精确复刻 _evalscratch/verify_t2n.py（已在全题库扫描验证 0 假阳性）。</para>
/// </summary>
public static class NaChoiceValidator
{
    private const string ConstraintNote =
        "【高中限定】NA 粒子计数基于 data/highschool/na-particle-facts.json 事实库；" +
        "仅在四个选项均可干净计算且唯一时断言，任一选项不可判定则不臆断（氧化还原/水解/同位素等不强行计算）。";

    private static readonly Lazy<NaIndexes> _index = new(BuildIndexes);

    private sealed class NaIndexes
    {
        public readonly Dictionary<string, NaParticleFact> ByFormula = new(StringComparer.Ordinal);
        public int FormulaCount;
    }

    private sealed record ParticleInfo(string Field, bool IonSpecific, string IonTok);
    private sealed record QtyResult(double? N, bool TrapSolid, NaParticleFact? Sub, string Reason);

    // 显示名→分子式归一化（非化学事实）
    private static readonly Dictionary<string, string> SubAlias = new(StringComparer.Ordinal)
    {
        ["水"]="H2O",["双氧水"]="H2O2",["过氧化氢"]="H2O2",["氨气"]="NH3",["氨"]="NH3",
        ["甲烷"]="CH4",["乙烷"]="C2H6",["乙烯"]="C2H4",["乙炔"]="C2H2",["苯"]="C6H6",
        ["二氧化碳"]="CO2",["一氧化碳"]="CO",["二氧化硫"]="SO2",["三氧化硫"]="SO3",
        ["氯化氢"]="HCl",["硫化氢"]="H2S",["一氧化氮"]="NO",["二氧化氮"]="NO2",
        ["氯气"]="Cl2",["氧气"]="O2",["氮气"]="N2",["臭氧"]="O3",["氢气"]="H2",
        ["食盐"]="NaCl",["氯化钠"]="NaCl",["烧碱"]="NaOH",["氢氧化钠"]="NaOH",
        ["纯碱"]="Na2CO3",["苏打"]="Na2CO3",["小苏打"]="NaHCO3",["碳酸钙"]="CaCO3",
        ["生石灰"]="CaO",["熟石灰"]="Ca(OH)2",["消石灰"]="Ca(OH)2",["二氧化氯"]="ClO2",
    };

    private static readonly Regex NaRe = new(@"([\d.]+)\s*(?:N_A|NA|N A|Nₐ)", RegexOptions.Compiled);
    private static readonly Regex DisqualifyRe = new(
        @"转移电子|转移的电子|转移|充分反应|完全反应|电解|反应|生成|密度之比|质量之比|体积之比|分子数之比|物质的量之比",
        RegexOptions.Compiled);
    private static readonly Regex StpRe = new(@"标准状况|标况|0\s*℃|0℃|273\s*K|101\s*kPa|1\.01×10\^5", RegexOptions.Compiled);
    private static readonly Regex SolutionRe = new(@"([\d.]+)\s*L\s+([\d.]+)\s*mol", RegexOptions.Compiled);
    private static readonly Regex GasRe = new(@"([\d.]+)\s*L\s+([A-Za-z0-9()·]{2,12})", RegexOptions.Compiled);
    private static readonly Regex MolRe = new(@"([\d.]+)\s*mol\s+([A-Za-z0-9()·]{2,12})", RegexOptions.Compiled);
    private static readonly Regex MassRe = new(@"([\d.]+)\s*g\s+([A-Za-z0-9()·]{2,12})", RegexOptions.Compiled);
    private static readonly Regex IonRe = new(@"([A-Za-z0-9]{1,6}\^?[0-9]*[+-])", RegexOptions.Compiled);
    private static readonly Regex NaAnchorRe = new(@"N_A|NA|N A|阿伏伽德罗|阿伏加德罗|Nₐ", RegexOptions.Compiled);

    /// <summary>尝试对一道 NA 选择题唯一求解；不能唯一判定返回 null。</summary>
    public static ChoiceVerifier.Outcome? TryDecide(EngineRequest req)
    {
        var rawText = req.RawText ?? string.Empty;
        var (stem, opts) = ChoiceVerifier.ParseOptions(rawText);
        if (opts.Count < 2) return null;
        if (!NaAnchorRe.IsMatch(stem)) return null;   // 仅处理锚定 NA 的题干

        var askDir = AskDir(stem);
        if (askDir != "correct" && askDir != "wrong") return null;

        var letters = opts.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        var optionOk = new Dictionary<string, bool>();
        var reasons = new List<string>();
        // B-LlmMain T2b：收集每选项命中物质的 formula（na-particle-facts.json 稳定主键，类比 kaKey）。
        var optionHits = new List<ChoiceReasoningNodes.OptionHit>();
        foreach (var L in letters)
        {
            var (verdict, reason, formula) = ParseOption(opts[L]);
            if (verdict is null) return null;          // 任一选项不可干净计算 → 整题不臆断
            optionOk[L] = verdict.Value;
            reasons.Add($"{L}. {reason}");
            var hitIds = new List<string>();
            if (!string.IsNullOrWhiteSpace(formula)) hitIds.Add(formula);
            optionHits.Add(new ChoiceReasoningNodes.OptionHit(L, hitIds));
        }

        var candidates = askDir == "correct"
            ? letters.Where(l => optionOk[l]).ToList()
            : letters.Where(l => !optionOk[l]).ToList();
        if (candidates.Count != 1) return null;

        var letter = candidates[0];
        var dirWord = askDir == "correct" ? "正确" : "错误";
        reasons.Add($"依据 NA 粒子事实库，{dirWord}选项唯一为 {letter}。");
        var finalResult = $"{dirWord}选项为 {letter}";
        return new ChoiceVerifier.Outcome(
            AnswerLetter: letter,
            FinalResult: finalResult,
            Reasons: reasons,
            ConstraintNote: ConstraintNote)
        {
            ReasoningNodes = ChoiceReasoningNodes.Build(optionHits, finalResult)
        };
    }

    /// <summary>事实库分子式数量（供测试护栏断言 ≥40）。</summary>
    public static int FormulaCount => _index.Value.FormulaCount;

    // ---------- 问法 / 锚点 ----------

    private static string AskDir(string stem)
    {
        if (Regex.IsMatch(stem, @"错误|不正确")) return "wrong";   // 先判负向
        if (Regex.IsMatch(stem, @"正确")) return "correct";
        return "?";
    }

    // ---------- 选项解析 ----------

    private static (bool? verdict, string reason, string? formula) ParseOption(string text)
    {
        // k：NA 前的系数；无数字但有 NA 标记 → 1
        var mNa = NaRe.Match(text);
        double k;
        if (mNa.Success) k = double.Parse(mNa.Groups[1].Value);
        else if (NaAnchorRe.IsMatch(text)) k = 1.0;
        else return (null, "无 NA 计数断言", null);

        var particle = DetectParticle(text);
        if (particle is null) return (null, "未识别粒子类型", null);

        var qty = ParseQuantity(text);
        // B-LlmMain T2b：命中物质的 formula 作可溯源锚点（na-particle-facts.json 稳定主键，类比 kaKey）；
        // 未命中物质（qty.Sub 为 null）→ formula 为 null → 该选项 hit 留空（R4 不臆造）。
        var formula = qty.Sub?.Formula;
        if (qty.N is null)
        {
            if (qty.TrapSolid) return (false, $"{qty.Sub!.Formula} 在标况下为{qty.Sub.StpState}，22.4 L≠1 mol → 断言不成立", formula);
            return (null, qty.Reason, formula);
        }
        var n = qty.N.Value;
        var sub = qty.Sub!;
        var field = particle.Field;

        // 字段适用性守卫
        if (field is "sigmaBonds" or "piBonds" or "covalentBonds" && !sub.Molecular)
            return (null, $"非分子物质 {sub.Formula} 不查询键", formula);
        if (field == "ions" && sub.Ions <= 0)
            return (null, $"非离子物质 {sub.Formula} 不查询总离子数", formula);
        if (field == "hBonds" && sub.HBonds <= 0)
            return (null, $"{sub.Formula} 无氢键数据", formula);

        // 溶液水解陷阱：声称的水解离子名义浓度实际低于名义 → 断言为假
        if (qty is { TrapSolid: false } && particle.IonSpecific && !string.IsNullOrEmpty(particle.IonTok))
        {
            var hydrolyzable = sub.HydrolyzableIons.Select(CleanFormula).ToHashSet(StringComparer.Ordinal);
            if (hydrolyzable.Contains(particle.IonTok))
                return (false, $"{particle.IonTok} 发生水解，实际浓度低于名义 {k}NA → 断言不成立", formula);
        }
        // 溶液中的总离子数需物种分析 → 不臆断
        if (field == "ions" && qty.Reason.Contains("solution"))
            return (null, "溶液中总离子数需物种分析，不臆断", formula);

        if (particle.IonSpecific)
            return (null, $"特定离子 {particle.IonTok} 的每式计数未建模，不臆断", formula);

        var per = field == "molecules" ? 1.0 : FieldValue(sub, field);
        var expected = n * per;
        var ok = Math.Abs(expected - k) < 1e-6;
        return (ok, $"{sub.Formula} n={n:0.####} × {FieldLabel(field)}={per} = {expected:0.####}，声称 {k}NA → {(ok ? "成立" : "不成立")}", formula);
    }

    private static double FieldValue(NaParticleFact sub, string field) => field switch
    {
        "electrons" => sub.Electrons,
        "protons" => sub.Protons,
        "neutrons" => sub.Neutrons,
        "atoms" => sub.Atoms,
        "sigmaBonds" => sub.SigmaBonds,
        "piBonds" => sub.PiBonds,
        "covalentBonds" => sub.CovalentBonds,
        "ions" => sub.Ions,
        "hBonds" => sub.HBonds,
        _ => 0
    };

    private static string FieldLabel(string field) => field switch
    {
        "sigmaBonds" => "σ键", "piBonds" => "π键", "covalentBonds" => "共价键",
        "hBonds" => "氢键", "ions" => "离子", "molecules" => "分子",
        "electrons" => "电子", "protons" => "质子", "neutrons" => "中子", "atoms" => "原子",
        _ => field
    };

    private static ParticleInfo? DetectParticle(string t)
    {
        if (t.Contains('σ') || t.Contains("西格玛")) return new ParticleInfo("sigmaBonds", false, "");
        if (t.Contains('π') || t.Contains("派键")) return new ParticleInfo("piBonds", false, "");
        if (t.Contains("共价键")) return new ParticleInfo("covalentBonds", false, "");
        if (t.Contains("氢键")) return new ParticleInfo("hBonds", false, "");
        if (Regex.IsMatch(t, @"离子(?:的)?数目|离子(?:的)?数|离子总数")) return new ParticleInfo("ions", false, "");
        if (t.Contains("分子")) return new ParticleInfo("molecules", false, "");  // 1 mol 任何物质 = NA 个式单元
        if (t.Contains("电子")) return new ParticleInfo("electrons", false, "");
        if (t.Contains("质子")) return new ParticleInfo("protons", false, "");
        if (t.Contains("中子")) return new ParticleInfo("neutrons", false, "");
        if (t.Contains("原子")) return new ParticleInfo("atoms", false, "");
        var mi = IonRe.Match(t);
        if (mi.Success) return new ParticleInfo("ion_specific", true, CleanFormula(mi.Groups[1].Value));
        return null;
    }

    private static QtyResult ParseQuantity(string text)
    {
        if (DisqualifyRe.IsMatch(text))
            return new QtyResult(null, false, null, "氧化还原/反应/比值断言，不臆断");
        var stp = StpRe.IsMatch(text);

        // 溶液：V L × c mol/L
        var sol = SolutionRe.Match(text);
        if (sol.Success)
        {
            var V = double.Parse(sol.Groups[1].Value);
            var c = double.Parse(sol.Groups[2].Value);
            var rest = text.Substring(sol.Index + sol.Length);
            var mt = Regex.Match(rest, @"[A-Za-z0-9()·]{2,12}");
            if (!mt.Success) return new QtyResult(null, false, null, "溶液未识别溶质");
            var sub = Lookup(mt.Value);
            if (sub is null) return new QtyResult(null, false, null, "溶质不在事实库");
            return new QtyResult(V * c, false, sub, $"solution n={V * c} mol");
        }

        // 气体：V L 物质（仅标况可算；标况下非气态 → 陷阱，断言为假）
        var mg = GasRe.Match(text);
        if (mg.Success)
        {
            var V = double.Parse(mg.Groups[1].Value);
            var sub = Lookup(mg.Groups[2].Value);
            if (sub is null) return new QtyResult(null, false, null, "气体物质不在事实库");
            if (!stp) return new QtyResult(null, false, null, "气体体积无标况条件，不臆断");
            if (!string.Equals(sub.StpState, "gas", StringComparison.Ordinal))
                return new QtyResult(null, true, sub, $"{sub.Formula} 标况下为{sub.StpState}");
            return new QtyResult(V / 22.4, true, sub, $"gas n={V / 22.4} mol");
        }

        // 物质的量：n mol 物质
        var mm = MolRe.Match(text);
        if (mm.Success)
        {
            var n = double.Parse(mm.Groups[1].Value);
            var sub = Lookup(mm.Groups[2].Value);
            if (sub is null) return new QtyResult(null, false, null, "物质不在事实库");
            return new QtyResult(n, false, sub, $"n={n} mol");
        }

        // 质量：m g 物质
        var mgr = MassRe.Match(text);
        if (mgr.Success)
        {
            var m = double.Parse(mgr.Groups[1].Value);
            var sub = Lookup(mgr.Groups[2].Value);
            if (sub is null) return new QtyResult(null, false, null, "物质不在事实库");
            var n = m / sub.MolarMass;
            return new QtyResult(n, false, sub, $"mass n={n:0.###} mol");
        }
        return new QtyResult(null, false, null, "未解析出数量");
    }

    // ---------- 归一化 ----------

    private static string Norm(string s) => ClassificationValidator.Norm(s);

    private static string CleanFormula(string tok)
    {
        var t = Norm(tok).Trim();
        t = t.Replace("_", "");
        t = Regex.Replace(t, @"\^[0-9]*[+-]+$", "");   // 剥尾部电荷 ^2- / ^+
        t = Regex.Replace(t, @"[+-]+$", "");             // 剥尾部符号
        if (SubAlias.TryGetValue(t, out var a)) t = a;
        return t;
    }

    private static NaParticleFact? Lookup(string tok)
    {
        var f = CleanFormula(tok);
        return _index.Value.ByFormula.TryGetValue(f, out var s) ? s : null;
    }

    private static NaIndexes BuildIndexes()
    {
        var idx = new NaIndexes();
        var data = RuleStore.Load().Query<NaParticleFactData>("na-particle-facts");
        foreach (var r in data.Formulas)
        {
            var nf = Norm(r.Formula);
            if (nf.Length > 0 && !idx.ByFormula.ContainsKey(nf))
                idx.ByFormula[nf] = r;
            idx.FormulaCount++;
        }
        return idx;
    }
}
