using System.Text;
using System.Text.RegularExpressions;
using CogniThink.TutorBrain.Application.Chemistry;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 网关层选择题逐选项校验器（阶段3B / AC-12）。
/// <para>
/// 真值源：<strong>独立符号化学规则</strong>（F1–F18 反应可行性判定）+
/// <see cref="ChemicalInferenceService"/> 离子共存引擎。LLM 不参与任何化学判定，
/// 只负责把自然语言转成结构化请求（题干+选项渲染文本）以及基于引擎结果生成教学话术。
/// </para>
/// <para>
/// 本类只做：① 从题干渲染文本解析 A/B/C/D 选项；② 逐选项归一化 → 方程走可行性规则、
/// 离子组走共存引擎；③ 横向唯一求解（ask=correct 需恰好 3 项不可行 / ask=wrong 需恰好 1 项不可行，
/// 或共存题恰好 1 项共存/冲突）。
/// </para>
/// <para>
/// 红线（不蒙数）：无法唯一判定一律返回 null，绝不臆测选项字母。
/// <strong>守恒（原子/电荷）校验不接入</strong>——已证明在「正确选项 parseFail、干扰项恰好守恒」
/// 时会产生假唯一、把对的选错（harm），故生产完全剔除，只保留可行性 + 共存两条可靠路径。
/// </para>
/// </summary>
public static class ChoiceVerifier
{
    /// <summary>唯一判定成功的产物。</summary>
    public sealed record Outcome(
        string AnswerLetter,
        string FinalResult,
        List<string> Reasons,
        string ConstraintNote);

    private sealed record OptionVerdict(string Type, string Coexist, string Infeasible);

    private const string ConstraintNote =
        "【高中限定】选择题仅在符号引擎能唯一判定答案时断言；无法唯一判定则不臆断。" +
        "守恒校验未纳入（选项解析不对称会导致假唯一）。";

    /// <summary>
    /// 对一道选择题逐选项校验并横向唯一求解。能唯一判定返回答案字母；否则返回 null。
    /// </summary>
    public static Outcome? TryDecide(EngineRequest req, ChemicalInferenceService chem)
    {
        var rawText = req.RawText ?? string.Empty;

        // 1. 从渲染文本解析选项（stem 用于问法方向判定，避免选项文本中的「正确/错误」干扰）
        var (stem, opts) = ParseOptions(rawText);
        if (opts.Count < 2) return null;
        var letters = opts.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

        // 2. 问法方向：correct（选正确项）/ wrong（选错误项）；判不出则不臆断
        var askDir = AskDir(stem);
        if (askDir != "correct" && askDir != "wrong") return null;

        // 3. 逐选项判定
        var verdict = new Dictionary<string, OptionVerdict>(letters.Count);
        foreach (var L in letters)
            verdict[L] = JudgeOption(opts[L], stem, chem);

        // 4a. 离子共存路径：仅当全部选项都是离子组时才能横向比较
        if (letters.All(L => verdict[L].Type == "ion"))
        {
            var coexistList = letters.Where(L => verdict[L].Coexist == "coexist").ToList();
            var conflictList = letters.Where(L => verdict[L].Coexist == "conflict").ToList();
            if (askDir == "correct" && coexistList.Count == 1)
                return BuildOutcome(coexistList[0], askDir, verdict, "离子共存引擎唯一判定");
            if (askDir == "wrong" && conflictList.Count == 1)
                return BuildOutcome(conflictList[0], askDir, verdict, "离子共存引擎唯一判定");
        }

        // 4b. 反应可行性路径：仅当全部选项都是方程时才能横向比较
        if (letters.All(L => verdict[L].Type == "eq"))
        {
            var infList = letters.Where(L => !string.IsNullOrEmpty(verdict[L].Infeasible)).ToList();
            if (askDir == "wrong" && infList.Count == 1)
                return BuildOutcome(infList[0], askDir, verdict, "反应可行性规则唯一判定");
            if (askDir == "correct" && infList.Count == 3 && letters.Count == 4)
            {
                var rest = letters.Except(infList).ToList();
                if (rest.Count == 1)
                    return BuildOutcome(rest[0], askDir, verdict, "反应可行性规则唯一判定");
            }
        }

        // 不满足唯一性 → 不判定（红线：不蒙数）
        return null;
    }

    // ===================== 选项解析 =====================

    // 行首选项标记：A. / B．/ C、（与确定性分类器 IsChoiceQuestion 保持一致）
    private static readonly Regex OptionMarker =
        new(@"(^|\n)\s*(?<letter>[A-D])[\.．、]\s", RegexOptions.Compiled);

    // 设为 internal 供 Tier 2 各分类/转化链/NA 校验器复用同一套选项拆分（行首 A. / B．/ C、），
    // 保证跨校验器选项解析口径一致；不改动判定逻辑。
    internal static (string stem, Dictionary<string, string> options) ParseOptions(string rawText)
    {
        var matches = OptionMarker.Matches(rawText);
        var opts = new Dictionary<string, string>();
        if (matches.Count == 0) return (rawText.Trim(), opts);

        for (var i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            var letter = m.Groups["letter"].Value;
            var start = m.Index + m.Length;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : rawText.Length;
            opts[letter] = rawText[start..end].Trim();
        }
        var stem = rawText[..matches[0].Index].Trim();
        return (stem, opts);
    }

    private static string AskDir(string? q)
    {
        q ??= "";
        if (q.Contains("不能大量共存") || q.Contains("不能共存") || q.Contains("无法大量共存")) return "wrong";
        if (q.Contains("能大量共存") || q.Contains("可以大量共存") || q.Contains("大量共存") || q.Contains("能共存")) return "correct";
        if (q.Contains("错误的是") || q.Contains("不正确") || q.Contains("错误的有")) return "wrong";
        if (q.Contains("正确的是") || q.Contains("正确的有")) return "correct";
        if (q.Contains("错误")) return "wrong";
        if (q.Contains("正确")) return "correct";
        return "?";
    }

    // ===================== 逐选项判定 =====================

    private static OptionVerdict JudgeOption(string text, string stem, ChemicalInferenceService chem)
    {
        var eq = NormalizeOptionEquation(text);
        var isEq = eq.Length > 0;                 // 含方程分隔符 → 视为方程选项
        var ions = LlmGatewayService.ExtractIonsFromText(text);
        var type = isEq ? "eq" : (ions.Count >= 2 ? "ion" : "oth");

        var coexist = "no-ion";
        if (type == "ion" && ions.Count >= 2)
        {
            var ctx = stem + " " + text;
            var acidic = ions.Any(i => i.Contains("H+") && !i.Contains("OH")) || ctx.Contains("酸性");
            var alkaline = ions.Any(i => i.Contains("OH-")) || ctx.Contains("碱性");
            var r = chem.CheckCoexistence(ions, acidic, alkaline);
            coexist = r.CanCoexist ? "coexist" : "conflict";
        }

        var infeasible = type == "eq" ? JudgeInfeasible(text, eq) : "";
        return new OptionVerdict(type, coexist, infeasible);
    }

    private static Outcome BuildOutcome(string letter, string askDir,
        Dictionary<string, OptionVerdict> verdict, string note)
    {
        var reasons = new List<string>();
        foreach (var kv in verdict)
        {
            if (!string.IsNullOrEmpty(kv.Value.Infeasible))
                reasons.Add($"{kv.Key} 不可行：{kv.Value.Infeasible}");
            else if (kv.Value.Coexist == "conflict")
                reasons.Add($"{kv.Key} 离子不共存");
            else if (kv.Value.Coexist == "coexist")
                reasons.Add($"{kv.Key} 离子可共存");
        }
        var dirWord = askDir == "correct" ? "正确" : "错误";
        return new Outcome(
            AnswerLetter: letter,
            FinalResult: $"{dirWord}选项为 {letter}",
            Reasons: reasons,
            ConstraintNote: $"{ConstraintNote}（{note}）");
    }

    // ===================== 方程归一化（真值判定输入，不做化学推断） =====================

    private static readonly Regex SepRegex = new(
        @"={2,}|→|⇌|═|≡|⟶|⇒|[-—–]{1,}[^-—–>]*[-—–]{1,}>", RegexOptions.Compiled);

    // 丢失箭头时夹在两侧化学式之间的条件词（前后必须是公式字符，避免误命中题干前缀里的词）。
    private static readonly Regex LostArrowCond = new(
        @"(?<=[A-Za-z0-9\)\]\}])\s*(高温高压|高温|点燃|加热|催化剂|通电|光照|放电|沸腾|酶)\s*(?=[A-Za-z0-9\(\[\{])",
        RegexOptions.Compiled);

    /// <summary>把首个夹在化学式之间的条件词替换为 =，恢复丢失箭头的方程边界；无匹配则原样返回。</summary>
    private static string ReplaceConditionSeparator(string text)
    {
        var m = LostArrowCond.Match(text);
        if (!m.Success) return text;
        return text[..m.Index] + "=" + text[(m.Index + m.Length)..];
    }

    private static int FindSeparatorIndex(string s)
    {
        if (string.IsNullOrEmpty(s)) return -1;
        var m = SepRegex.Match(s);
        if (m.Success) return m.Index;
        var i = s.IndexOf('=');
        if (i < 0) return -1;
        bool Letter(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        return s[..i].Any(Letter) && s[(i + 1)..].Any(Letter) ? i : -1;
    }

    /// <summary>
    /// 从选项文本中提取并归一化方程（供可行性规则共用的真值判定输入）：
    /// ① 去分隔符前的中文前缀（取最后一个 :/： 之后）
    /// ② 统一分隔符/箭头：═ ≡ ⟶ ⇒ → ⇌ ─(cond)→ → =（--> / --cond--> 由解析器原生支持，不动）
    /// ③ Unicode 下标/上标 → ASCII
    /// ④ 去 ↓↑、去状态/浓度括号 (g)(s)(l)(aq)(浓)(稀)
    /// ⑤ 项级电荷归一化：正确补 ^（不把系数/下标误当电荷）
    /// 红线：只做字符串归一化，不做任何化学推断。
    /// </summary>
    public static string NormalizeOptionEquation(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        // 渲染丢失箭头时（无 = / → 等任何分隔符），反应物与产物之间常夹着条件词
        // （高温/点燃/加热/催化剂/通电/光照/放电…）。仅在此情形把首个条件词替换为 =，
        // 恢复方程边界——只做字符串归一化，不做化学推断，不破坏已有带分隔符的方程。
        if (FindSeparatorIndex(text) < 0)
            text = ReplaceConditionSeparator(text);
        var sep = FindSeparatorIndex(text);
        if (sep < 0) return "";
        var left = text[..sep];
        var colon = -1;
        for (var i = left.Length - 1; i >= 0; i--)
            if (left[i] == ':' || left[i] == '：') { colon = i; break; }
        var start = colon >= 0 ? colon + 1 : 0;
        var eq = text[start..].Trim();
        eq = NormalizeSeparators(eq);
        eq = UnicodeToAscii(eq);
        eq = eq.Replace("↓", "").Replace("↑", "");
        // 状态/浓度括号：只剥离 (g)(s)(l)(aq)(浓)(稀)，不碰 Fe(OH)3 与箭头条件 (高温) 等
        eq = Regex.Replace(eq, @"\((g|s|l|aq|浓|稀)\)", "");
        eq = NormalizeCharges(eq);
        eq = Regex.Replace(eq, @"\s+", " ").Trim();
        return eq;
    }

    // 只处理解析器不支持的箭头：单破折号 + 可选条件 + Unicode 箭头（→ ⟶ ⇒）。
    // --> / --cond--> / -> / ⇌ / =cond= 均由 EquationParser 原生处理，不动。
    private static readonly Regex CondArrow = new(
        @"[-—–═]{1,}\s*(?:\([^)]*\))?\s*(?:→|⟶|⇒)", RegexOptions.Compiled);

    private static string NormalizeSeparators(string eq)
    {
        eq = CondArrow.Replace(eq, "=");
        eq = eq.Replace("→", "=").Replace("⟶", "=").Replace("⇒", "=")
               .Replace("═", "=").Replace("≡", "=").Replace("⇌", "=");
        eq = Regex.Replace(eq, @"={2,}", "=");
        return eq;
    }

    private static string UnicodeToAscii(string eq)
    {
        eq = eq.Replace('−', '-').Replace('—', '-').Replace('–', '-');
        const string sub = "₀₁₂₃₄₅₆₇₈₉", sup = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        for (var i = 0; i < 10; i++)
        {
            eq = eq.Replace(sub[i], (char)('0' + i));
            eq = eq.Replace(sup[i], (char)('0' + i));
        }
        eq = eq.Replace("⁺", "+").Replace("⁻", "-");
        return eq;
    }

    private static string NormalizeCharges(string eq)
    {
        var sides = eq.Split('=');
        var sb = new StringBuilder();
        for (var s = 0; s < sides.Length; s++)
        {
            if (s > 0) sb.Append('=');
            sb.Append(NormalizeSideCharges(sides[s]));
        }
        return sb.ToString();
    }

    private static string NormalizeSideCharges(string side)
    {
        var terms = SplitTerms(side);
        return string.Join(" + ", terms.Select(NormalizeTermCharge).Where(t => t.Trim().Length > 0));
    }

    // 复刻 EquationParser.SplitTerms：区分 项分隔符 '+' 与 电荷 '+'（紧跟 ^ 或不在项首）。
    private static List<string> SplitTerms(string side)
    {
        var trimmed = side.Trim();
        if (Regex.IsMatch(trimmed, @"\s\+\s"))
            return Regex.Split(trimmed, @"\s+\+\s+").Where(s => s.Trim().Length > 0).Select(s => s.Trim()).ToList();
        var terms = new List<string>();
        var buffer = new StringBuilder();
        for (var i = 0; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            if (c == '+')
            {
                var next = i + 1 < trimmed.Length ? trimmed[i + 1] : '\0';
                var previous = buffer.Length > 0 ? buffer[^1] : '\0';
                var isSeparator = previous != '^' &&
                                  (char.IsUpper(next) || next == '[' || next == '(' || char.IsDigit(next));
                if (isSeparator) { terms.Add(buffer.ToString()); buffer.Clear(); continue; }
            }
            buffer.Append(c);
        }
        terms.Add(buffer.ToString());
        return terms.Where(s => s.Trim().Length > 0).Select(s => s.Trim()).ToList();
    }

    private static readonly Regex TrailingCharge = new(@"(?<digits>\d*)(?<sign>[+-])$", RegexOptions.Compiled);
    private static readonly Regex SingleElementHead = new(@"^[A-Z][a-z]?$", RegexOptions.Compiled);

    private static string NormalizeTermCharge(string term)
    {
        var t = term.Trim();
        if (t.Length == 0 || t.Contains('^')) return t;
        var i = 0;
        while (i < t.Length && char.IsDigit(t[i])) i++;
        var coef = t[..i];
        var body = t[i..].Trim();
        if (body.Length == 0) return t;
        var m = TrailingCharge.Match(body);
        if (!m.Success) return t;
        var spaceSeparated = m.Index > 0 && char.IsWhiteSpace(body[m.Index - 1]);
        var head = body[..m.Index].Trim();
        var digits = m.Groups["digits"].Value;
        var sign = m.Groups["sign"].Value[0];
        if (head.Length == 0 || digits.Length == 0) return t; // 裸 ±1（Cl-/OH-/ClO-），解析器原生支持
        if (SingleElementHead.IsMatch(head))
            return coef + head + "^" + digits + sign;               // Fe3+ / Fe 3+ → Fe^3+
        if (spaceSeparated)
            return coef + head + "^" + digits + sign;               // SiO3 2- → SiO3^2-（空格分隔=电荷量）
        if (digits.Length == 1)
            return coef + head + digits + "^" + sign;              // NO3- → NO3^-（相邻 digit 是下标，电荷 ±1）
        var sub = digits[..^1];
        var chg = digits[^1..];
        return coef + head + sub + "^" + chg + sign;               // SO42- → SO4^2-
    }

    // ===================== 不可行判定（F1–F18，高精度，只在有把握时返回原因） =====================

    /// <summary>
    /// 高精度不可行判定：只在有把握时返回原因；否则空串（不臆断）。
    /// 覆盖评估集中高频「守恒成立但化学错」模式：产物错、反应不能发生、氧化还原方向错。
    /// </summary>
    private static string JudgeInfeasible(string raw, string eq)
    {
        if (string.IsNullOrWhiteSpace(eq) && string.IsNullOrWhiteSpace(raw)) return "";
        var t = raw ?? "";   // 上下文词（稀硝酸/过量氨水/氢碘酸/生锈/水蒸气…）
        var e = eq ?? "";    // 归一化后的方程（ASCII），用于物种/产物匹配

        // 只对「归一化后的单一方程」生效：分隔符总数恰好为 1（= 或 -->，含 --(cond)-->）。
        // 转化路径（Fe --(Cl2)--> FeCl3 --(Cu)--> FeCl2）有 ≥2 个分隔符；离子组/文字描述无分隔符
        // → 均回退空串，避免把路径中的反应物/产物误当方程产物匹配而误命中。
        var sepCount = e.Count(c => c == '=') + Regex.Matches(e, @"-->").Count;
        if (sepCount != 1) return "";

        // F1：Fe3+ 被 Cu 还原到 Fe 单质（Cu 只能 Fe3+→Fe2+，不能置换出 Fe）
        if (Regex.IsMatch(e, @"Fe\^?3\+|FeCl3|Fe2\(SO4\)3|Fe\(NO3\)3")
            && e.Contains("Cu")
            && Regex.IsMatch(e, @"(?<![A-Za-z])Fe(?![A-Za-z0-9^])"))
            return "F1:Fe3+Cu→Fe(应→Fe2+)";

        // F2：氢后金属（Cu/Hg/Ag/Pt/Au）与非氧化性酸放出 H2
        if ((Regex.IsMatch(t, @"\b(Cu|Hg|Ag|Pt|Au)\b") || t.Contains("铜") || t.Contains("银"))
            && (t.Contains("稀盐酸") || t.Contains("稀硫酸") || Regex.IsMatch(t, @"稀\s*H2SO4|稀\s*HCl") || (t.Contains("HCl") && !t.Contains("浓")))
            && !t.Contains("硝酸")
            && Regex.IsMatch(e, @"\bH2\b"))
            return "F2:氢后金属+稀酸→H2(不可能)";

        // F3：Fe 与非氧化性酸生成 Fe3+（稀酸只能到 +2）
        if (Regex.IsMatch(e, @"\bFe\b")
            && (t.Contains("稀盐酸") || t.Contains("稀硫酸") || Regex.IsMatch(t, @"稀\s*H2SO4|稀\s*HCl") || (t.Contains("H2SO4") && !t.Contains("浓") && !t.Contains("硝酸")))
            && !t.Contains("硝酸") && !e.Contains("HNO3")
            && Regex.IsMatch(e, @"Fe\^?3\+|FeCl3|Fe2\(SO4\)3"))
            return "F3:Fe+稀酸→Fe3+(应→Fe2+)";

        // F4：FeO/Fe(OH)2/Fe2+ 与硝酸反应产物仍为 Fe2+（硝酸应氧化到 Fe3+）
        if ((t.Contains("硝酸") || e.Contains("HNO3"))
            && Regex.IsMatch(e, @"FeO|Fe\(OH\)2|FeCl2|FeSO4|Fe\^?2\+")
            && Regex.IsMatch(e, @"Fe\^?2\+|FeCl2|FeSO4"))
            return "F4:Fe2++硝酸→Fe2+(应→Fe3+ +NOx)";

        // F5：Pt/Au 与硝酸/浓硫酸（需王水）
        if (Regex.IsMatch(e, @"\b(Pt|Au)\b")
            && (t.Contains("硝酸") || t.Contains("浓硫酸") || e.Contains("HNO3") || (t.Contains("H2SO4") && t.Contains("浓"))))
            return "F5:Pt/Au+氧化性酸(需王水)";

        // F6：弱酸不能制强酸：SO2/CO2 通入 BaCl2/CaCl2 生成沉淀
        if ((e.Contains("SO2") || e.Contains("CO2"))
            && (e.Contains("BaCl2") || e.Contains("CaCl2"))
            && (e.Contains("BaSO3") || e.Contains("CaSO3") || e.Contains("BaCO3") || e.Contains("CaCO3")))
            return "F6:弱酸不能制强酸";

        // F7：煅烧硫铁矿 FeS2 + O2 → SO3（应为 SO2）
        if (e.Contains("FeS2") && e.Contains("SO3"))
            return "F7:FeS2+O2→SO3(应→SO2)";

        // F8：过量氨水吸收 SO2 → 酸式盐 NH4HSO3（过量氨水应生成正盐 (NH4)2SO3）
        if (t.Contains("过量氨水") && e.Contains("SO2") && (e.Contains("NH4HSO3") || e.Contains("HSO3")))
            return "F8:过量氨水+SO2→酸式盐(应→正盐)";

        // F9：稀硝酸与金属反应生成 NO2（稀硝酸应生成 NO）
        if (t.Contains("稀硝酸") && e.Contains("NO2"))
            return "F9:稀硝酸→NO2(应→NO)";

        // F10：S2- + SO2 → H2S（足量 SO2 应归中生成 S↓）
        if (Regex.IsMatch(e, @"S\^?2-") && e.Contains("SO2") && e.Contains("H2S"))
            return "F10:S2-+SO2→H2S(应→S↓)";

        // F11：氢碘酸(HI)溶解 Fe(OH)3/Fe2O3 生成 Fe3+（I- 还原 Fe3+，应得 Fe2+ + I2）
        if ((t.Contains("氢碘酸") || e.Contains("HI"))
            && (e.Contains("Fe(OH)3") || e.Contains("Fe2O3"))
            && Regex.IsMatch(e, @"Fe\^?3\+"))
            return "F11:HI+Fe(OH)3→Fe3+(应→Fe2+)";

        // F12：Fe 与水蒸气反应生成 Fe2O3（应生成 Fe3O4）
        if ((t.Contains("水蒸气") || e.Contains("H2O(g)"))
            && e.Contains("Fe") && e.Contains("Fe2O3"))
            return "F12:Fe+H2O(g)→Fe2O3(应→Fe3O4)";

        // F13：铁生锈生成 Fe3O4（铁锈应为 Fe2O3·xH2O）
        if ((t.Contains("生锈") || t.Contains("铁锈")) && e.Contains("Fe3O4"))
            return "F13:铁锈→Fe3O4(应→Fe2O3)";

        // F14：NaH + H2O → Na2O（应生成 NaOH）
        if (e.Contains("NaH") && e.Contains("H2O") && e.Contains("Na2O"))
            return "F14:NaH+H2O→Na2O(应→NaOH)";

        // F15：S2O3^2- + Cl2 → SO3^2-（足量 Cl2 应氧化到 SO4^2-）
        // 守卫：若同时含 SO4^2-（可能是部分氧化的正确方程）则不命中，避免误判。
        if (Regex.IsMatch(e, @"S2O3") && e.Contains("Cl2")
            && Regex.IsMatch(e, @"SO3\^?2-") && !Regex.IsMatch(e, @"SO4\^?2-"))
            return "F15:S2O3+Cl2→SO3^2-(应→SO4^2-)";

        // F16：过量氨水与 Ag+/Cu2+ 生成氢氧化物沉淀（应生成可溶氨络合物）
        if (t.Contains("过量氨水")
            && (e.Contains("Ag") || e.Contains("Cu"))
            && (e.Contains("AgOH") || e.Contains("Cu(OH)2") || e.Contains("↓")))
            return "F16:过量氨水→氢氧化物沉淀(应→络合物)";

        // F17：还原半反应（e- 在反应物侧被消耗）却生成 O2（O 被氧化）→ 不可能。
        // 不变量：同一只半反应里，消耗 e-（还原）与生成 O2（氧化 -2→0）互斥。
        // 正确正极/阴极耗氧反应为 O2 + 2H2O + 4e- = 4OH-（O2 在左），不会命中。
        if (e.Contains("="))
        {
            var sides = e.Split('=');
            if (sides.Length >= 2)
            {
                var lhs = sides[0];
                var rhs = sides[1];
                var eOnLeft = Regex.IsMatch(lhs, @"(?<![A-Za-z])e-");
                var o2OnRight = Regex.IsMatch(rhs, @"\bO2\b");
                var o2OnLeft = Regex.IsMatch(lhs, @"\bO2\b");
                if (eOnLeft && o2OnRight && !o2OnLeft)
                    return "F17:还原半反应生成O2(不可能,应消耗O2)";
            }
        }

        // F18：氢氟酸刻蚀玻璃生成 SiF4，但反应物是硅酸盐 SiO3^2- 而非 SiO2（玻璃成分）。
        // 正确：SiO2 + 4HF = SiF4 + 2H2O（或 SiO2 + 6F- + 4H+ = SiF6^2- + 2H2O）。
        // 守卫：反应物侧必须含 SiO3 且不含 SiO2，才判定错。
        if ((t.Contains("氢氟酸") || t.Contains("HF"))
            && (t.Contains("玻璃") || t.Contains("刻蚀"))
            && e.Contains("=") && e.Contains("SiF4"))
        {
            var lhs = e.Split('=')[0];
            if (Regex.IsMatch(lhs, @"SiO3") && !Regex.IsMatch(lhs, @"SiO2"))
                return "F18:HF刻蚀玻璃反应物应为SiO2(非SiO3^2-)";
        }

        return "";
    }
}
