using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Balancing;
using CogniThink.Chem.Core.Ionic;
using CogniThink.Chem.Core.Parsing;
using CogniThink.Chem.Core.Rules;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 离子反应类选择题确定性判定器（T1：离子方程式正误 + 离子共存）。
/// <list type="bullet">
/// <item>离子共存：解析每个选项的离子组（含「在 X 溶液中」语境与酸碱性），用 ion-conflicts.json 判定冲突，唯一解才返回。</item>
/// <item>离子方程式正误：复用 IonicEquationChecker + 守恒/不可行规则判定每个方程式的对错，唯一解才返回。</item>
/// </list>
/// 红线：无唯一解返回 null，零假阳性；事实/规则在 data/highschool/*.json。
/// </summary>
public static class IonicEquationValidator
{
    private static readonly Lazy<Indexes> _index = new(BuildIndexes);

    private static readonly Lazy<InfeasiblePatternData> _infeasible = new(() =>
    {
        try { return RuleStore.Load().Query<InfeasiblePatternData>("infeasible-equation-patterns") ?? new InfeasiblePatternData(); }
        catch { return new InfeasiblePatternData(); }
    });

    private sealed class Indexes
    {
        public required IonConflictRule[] Rules { get; init; }
        public required IonSolutionContext[] Contexts { get; init; } // 按 keyword 长度降序
    }

    private static Indexes BuildIndexes()
    {
        var store = RuleStore.Load();
        var rules = store.Query<IonConflictData>("ion-conflicts").Rules.ToArray();
        var ctx = store.Query<IonSolutionContextData>("ion-solution-context").Contexts
            .OrderByDescending(c => c.Keyword?.Length ?? 0).ToArray();
        foreach (var r in rules)
            r.Ions = r.Ions.Select(NormIon).Where(s => s.Length > 0).ToArray();
        foreach (var c in ctx)
            c.Species = c.Species.Select(NormIon).Where(s => s.Length > 0).ToArray();
        return new Indexes { Rules = rules, Contexts = ctx };
    }

    public static ChoiceVerifier.Outcome? TryDecide(EngineRequest req)
    {
        if (req?.RawText is null) return null;
        var (stem, opts) = ChoiceVerifier.ParseOptions(req.RawText);
        if (opts.Count < 2) return null;

        var coexist = TryDecideCoexist(stem, opts);
        if (coexist != null) return coexist;

        var eq = TryDecideEquation(stem, opts);
        if (eq != null) return eq;

        return null;
    }

    // ==================== 离子共存 ====================

    private static ChoiceVerifier.Outcome? TryDecideCoexist(string stem, Dictionary<string, string> opts)
    {
        var groups = new Dictionary<string, List<string>>();
        foreach (var kv in opts)
        {
            var ions = ParseIonGroup(kv.Value, stem);
            if (ions.Count < 2) return null;
            groups[kv.Key] = ions;
        }

        var askCorrect = !Regex.IsMatch(stem, @"不能|无法|不可以");
        var verdicts = new Dictionary<string, (bool coexist, string? reason)>();
        foreach (var kv in groups)
            verdicts[kv.Key] = FindConflict(kv.Value) is { } conflict
                ? (false, conflict)
                : (true, null);

        var target = (askCorrect
            ? verdicts.Where(v => v.Value.coexist)
            : verdicts.Where(v => !v.Value.coexist)).Select(v => v.Key).ToList();
        if (target.Count != 1) return null;

        var letter = target[0];
        var reasons = new List<string>();
        foreach (var kv in verdicts)
            reasons.Add(kv.Value.coexist ? $"{kv.Key} 可大量共存" : $"{kv.Key} 不能大量共存：{kv.Value.reason}");
        var dirWord = askCorrect ? "正确" : "错误";
        return new ChoiceVerifier.Outcome(
            AnswerLetter: letter,
            FinalResult: $"{dirWord}选项为 {letter}",
            Reasons: reasons,
            ConstraintNote: "离子共存事实库唯一判定（高中限定，无法唯一判定则不臆断）。");
    }

    /// <summary>解析一个选项的有效离子组（含溶液语境引入物种 + 酸碱性 H+/OH-）。</summary>
    private static List<string> ParseIonGroup(string optionText, string stem)
    {
        var idx = _index.Value;
        var prefix = optionText;
        var ionPart = optionText;
        var colonIdx = optionText.LastIndexOfAny(new[] { ':', '：' });
        if (colonIdx >= 0)
        {
            prefix = optionText[..colonIdx];
            ionPart = optionText[(colonIdx + 1)..];
        }

        var ions = ExtractIons(ionPart);

        // 语境关键词匹配：两侧都做 Unicode→ASCII + 去 ^ 归一，保证 Fe³⁺/CO₂ 等能命中
        var hay = KeyNorm(prefix + " " + stem);
        string? ctxCond = null;
        var freeProtons = true;
        foreach (var c in idx.Contexts)
        {
            if (c.Keyword is null) continue;
            var key = KeyNorm(c.Keyword);
            if (key.Length == 0 || !hay.Contains(key)) continue;
            foreach (var sp in c.Species) ions.Add(sp);
            if (!string.IsNullOrEmpty(c.Condition)) ctxCond = c.Condition;
            if (c.FreeProtons == false) freeProtons = false;
            break;
        }

        var hasH = ions.Contains("H^1+");
        var hasOH = ions.Contains("OH^1-");
        var condition = ctxCond ?? DetermineCondition(stem, hasH, hasOH);

        if (condition is "alkaline" or "alkaline_possible") ions.Add("OH^1-");
        if (condition == "acidic" && freeProtons) ions.Add("H^1+");

        return ions.Distinct().ToList();
    }

    private static string DetermineCondition(string stem, bool hasH, bool hasOH)
    {
        if (hasOH || Regex.IsMatch(stem, @"碱性|碱溶液|pH\s*[>＞]\s*7|c\s*\(\s*OH")) return "alkaline";
        if (hasH || Regex.IsMatch(stem, @"酸性|酸溶液|pH\s*[<＜]\s*7")) return "acidic";
        return "neutral";
    }

    private static string? FindConflict(List<string> ions)
    {
        var idx = _index.Value;
        var set = new HashSet<string>(ions);
        var effective = set.Contains("OH^1-") ? "alkaline"
            : set.Contains("H^1+") ? "acidic" : "neutral";
        foreach (var r in idx.Rules)
        {
            if (r.Ions.Length == 0) continue;
            if (!string.IsNullOrEmpty(r.Condition))
            {
                if (r.Condition == "acidic" && effective != "acidic") continue;
                if (r.Condition == "alkaline" && effective is not ("alkaline" or "alkaline_possible")) continue;
            }
            if (r.Ions.All(i => set.Contains(i)))
                return r.Result ?? $"{string.Join("、", r.Ions)} 不能共存（{r.Type}）";
        }
        return null;
    }

    // ==================== 离子方程式正误 ====================

    private static ChoiceVerifier.Outcome? TryDecideEquation(string stem, Dictionary<string, string> opts)
    {
        var eqs = new Dictionary<string, string>();
        foreach (var kv in opts)
        {
            var neq = ChoiceVerifier.NormalizeOptionEquation(kv.Value);
            if (neq.Length == 0) return null;
            eqs[kv.Key] = neq;
        }

        var askCorrect = !Regex.IsMatch(stem, @"错误|不正确|不相符|不符合|有误|不对");
        var verdicts = new Dictionary<string, (bool correct, string? reason)>();
        foreach (var kv in eqs)
        {
            var (ok, reason) = JudgeEquation(kv.Value);
            verdicts[kv.Key] = (ok, reason);
        }

        // 零假阳性护栏：若任一选项因「不可靠理由」被判错，则无法保证其余「正确」选项是唯一真值。
        // 选择题选项常省略 ↓/↑ 状态符号、或带 (高温)/△ 等反应条件标注，IonicEquationChecker / 解析器
        // 会据此误判化学上正确的选项为错，造成唯一性错位（把配平但化学错的选项捧为唯一解）。
        // 此类情形保守返回 null，不蒙数、不帮倒忙。
        foreach (var v in verdicts)
        {
            if (!v.Value.correct && IsUnreliableReason(v.Value.reason))
                return null;
        }

        var target = (askCorrect
            ? verdicts.Where(v => v.Value.correct)
            : verdicts.Where(v => !v.Value.correct)).Select(v => v.Key).ToList();
        if (target.Count != 1) return null;

        var letter = target[0];
        var reasons = new List<string>();
        foreach (var kv in verdicts)
            reasons.Add(kv.Value.correct ? $"{kv.Key} 方程式正确" : $"{kv.Key} 方程式错误：{kv.Value.reason}");
        var dirWord = askCorrect ? "正确" : "错误";
        return new ChoiceVerifier.Outcome(
            AnswerLetter: letter,
            FinalResult: $"{dirWord}选项为 {letter}",
            Reasons: reasons,
            ConstraintNote: "离子方程式事实库唯一判定（高中限定，无法唯一判定则不臆断）。");
    }

    /// <summary>
    /// 判错理由是否「不可靠」：状态符号缺失 / 方程式无法解析 / 检查异常。
    /// 这些理由会误伤化学正确的选择题选项（选项常省略 ↓/↑ 或带反应条件），不能作为唯一真值判据。
    /// </summary>
    private static bool IsUnreliableReason(string? reason) =>
        !string.IsNullOrEmpty(reason) && (
            reason.StartsWith("状态符号", StringComparison.Ordinal) ||
            reason.StartsWith("方程式无法解析", StringComparison.Ordinal) ||
            reason.StartsWith("离子方程式检查异常", StringComparison.Ordinal));

    private static (bool correct, string? reason) JudgeEquation(string eq)
    {
        if (!EquationParser.TryParse(eq, out var parsed, out var parseErr) || parsed is null)
            return (false, "方程式无法解析：" + parseErr);

        // 守恒：原子/电荷不守恒 -> 必错（充分条件，零假阳性）
        var violations = EquationBalancer.ConservationViolations(parsed);
        if (violations.Count > 0)
            return (false, "不守恒：" + string.Join("；", violations));

        try
        {
            var r = IonicEquationChecker.Check(eq);
            if (!r.IsValid)
                return (false, r.FailureSummary());
        }
        catch (Exception ex)
        {
            return (false, "离子方程式检查异常：" + ex.Message);
        }

        var infeasible = DetectInfeasible(eq);
        if (infeasible != null)
            return (false, infeasible);

        return (true, null);
    }

    /// <summary>
    /// 常见「守恒但化学错」的高置信不可行模式。规则全在 data/highschool/infeasible-equation-patterns.json，
    /// 某条规则的 AllOf 正则全部命中即判定化学不可行（零假阳性，仅可追加不可放宽）。
    /// </summary>
    private static string? DetectInfeasible(string eq)
    {
        foreach (var r in _infeasible.Value.Rules)
        {
            if (r.AllOf.Count > 0 && r.AllOf.All(p => Regex.IsMatch(eq, p)))
                return r.Reason;
        }
        return null;
    }

    // ==================== 离子归一化 ====================

    private static readonly Dictionary<char, char> Super = new()
    {
        {'⁰','0'},{'¹','1'},{'²','2'},{'³','3'},{'⁴','4'},{'⁵','5'},{'⁶','6'},{'⁷','7'},{'⁸','8'},{'⁹','9'},
        {'⁺','+'},{'⁻','-'}
    };
    private static readonly Dictionary<char, char> Sub = new()
    {
        {'₀','0'},{'₁','1'},{'₂','2'},{'₃','3'},{'₄','4'},{'₅','5'},{'₆','6'},{'₇','7'},{'₈','8'},{'₉','9'}
    };

    /// <summary>常见多原子离子（本体→电荷数），用于无角标 ASCII 写法的消歧。</summary>
    private static readonly Dictionary<string, int> KnownIonCharge = new()
    {
        {"NH4",1},{"NO3",1},{"SO4",2},{"CO3",2},{"PO4",3},{"ClO3",1},{"ClO4",1},{"ClO",1},
        {"MnO4",1},{"Cr2O7",2},{"CrO4",2},{"HCO3",1},{"HSO4",1},{"H2PO4",1},{"HPO4",2},
        {"S2O3",2},{"SO3",2},{"SiO3",2},{"CN",1},{"SCN",1},{"OH",1},{"CH3COO",1},{"AlO2",1},
        {"NO2",1},{"BrO3",1},{"IO3",1},{"C2O4",2},{"HS",1},{"H2PO3",1},{"HSO3",1}
    };

    /// <summary>全文预处理：Unicode 下标→ASCII 数字、上标→^+数字、全角→半角。</summary>
    public static string PreNormText(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw ?? "";
        var sb = new StringBuilder();
        foreach (var ch in raw) sb.Append(Sub.TryGetValue(ch, out var d) ? d : ch);
        var s = sb.ToString();
        sb.Clear();
        var inSuper = false;
        foreach (var ch in s)
        {
            if (Super.TryGetValue(ch, out var c))
            {
                if (!inSuper) { sb.Append('^'); inSuper = true; }
                sb.Append(c);
            }
            else { inSuper = false; sb.Append(ch); }
        }
        return sb.ToString().Normalize(NormalizationForm.FormKC);
    }

    /// <summary>语境关键词归一：Unicode→ASCII 并去掉 ^，便于子串匹配（Fe³⁺→Fe3+、CO₂→CO2）。</summary>
    public static string KeyNorm(string s) => PreNormText(s ?? "").Replace("^", "");

    /// <summary>把任意写法归一为 本体^N± 形式（与事实库离子口径一致）。</summary>
    public static string NormIon(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var s = PreNormText(raw.Trim()).Trim();
        s = s.TrimEnd('。', '.', '，', ',', ';', '；', '、');
        s = s.Replace("AlO2^-", "[Al(OH)4]^1-").Replace("AlO2^1-", "[Al(OH)4]^1-");

        var ci = s.IndexOf('^');
        string body, charge;
        if (ci >= 0)
        {
            body = s[..ci];
            charge = s[(ci + 1)..];
        }
        else
        {
            // 无 ^ 写法：先把结尾的 ± 剥离，用「去掉符号的整体」查已知多原子离子电荷表
            // （避免把下标数字误当电荷：NO3- 应是 NO3^1- 而非 NO^3-；Fe3+ 则是 Fe^3+）。
            if (s.Length == 0) return s;
            var last = s[^1];
            if (last != '+' && last != '-') return s;
            var sign = last.ToString();
            var bodyNoSign = s[..^1];
            if (KnownIonCharge.TryGetValue(bodyNoSign, out var kc))
            {
                body = bodyNoSign;
                charge = kc + sign;
            }
            else
            {
                var m = Regex.Match(bodyNoSign, @"^(.*?)(\d*)$");
                body = m.Groups[1].Value;
                var digits = m.Groups[2].Value;
                charge = (digits.Length > 0 ? digits : "1") + sign;
            }
        }
        if (charge == "+") charge = "1+";
        else if (charge == "-") charge = "1-";
        else if (!char.IsDigit(charge[0])) charge = "1" + charge;
        return body + "^" + charge;
    }

    private static readonly Regex IonToken = new(@"[A-Za-z0-9\(\)\[\]\^\+\-]+", RegexOptions.Compiled);

    private static List<string> ExtractIons(string text)
    {
        var norm = PreNormText(text);
        var list = new List<string>();
        foreach (Match m in IonToken.Matches(norm))
        {
            var t = m.Value;
            if (t.Length < 2) continue;
            if (!t.Contains('+') && !t.Contains('-')) continue;
            if (!Regex.IsMatch(t, @"[A-Za-z]")) continue;
            var n = NormIon(t);
            if (n.Length > 1 && !n.EndsWith("^")) list.Add(n);
        }
        return list.Distinct().ToList();
    }
}
