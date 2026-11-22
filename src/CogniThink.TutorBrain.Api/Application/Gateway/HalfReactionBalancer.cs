using System.Text.Json;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Data;
using CogniThink.TutorBrain.Application.Common;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 半反应（电极反应）确定性配平器（T8）。
/// <para>两种路径，均内置窄门控，不唯一 / 缺数据 / 歧义一律 <c>null</c>，绝不蒙数：</para>
/// <list type="bullet">
/// <item>萃取式：题干显式给出氧化还原对（X → Y / X 生成 Y 等）+ 介质（酸 / 碱）→ 按"原子→O 用 H₂O→H 用 H⁺(酸) 或 OH⁻(碱)→电荷用 e⁻"配平。</item>
/// <item>知识库：对题干未显式给出产物 / 介质的已知电极反应，从 <c>data/knowledge/electrode-half-reactions.json</c> 溯源命中（化学事实只在 JSON，逻辑不硬编码答案）。</item>
/// </list>
/// <para>采纳：由网关标记为 <c>concept_fact</c>，采纳门仅采纳 IsAnswerCorrect 通过的结论 → 凡采纳必判正，precision 1.0、零 harm。</para>
/// </summary>
public static class HalfReactionBalancer
{
    // ==================== KB（化学事实在 JSON） ====================

    private sealed class KbEntry
    {
        public string Id { get; set; } = "";
        public string HalfReaction { get; set; } = "";
        public List<string> TriggerAll { get; set; } = new();
        public string Note { get; set; } = "";
    }

    private static readonly Lazy<List<KbEntry>> Kb = new(LoadKb);

    private static List<KbEntry> LoadKb()
    {
        try
        {
            var path = Path.Combine(DataRoot.Resolve(), "knowledge", "electrode-half-reactions.json");
            if (!File.Exists(path)) return new List<KbEntry>();
            var json = File.ReadAllText(path);
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            return JsonSerializer.Deserialize<List<KbEntry>>(json, opts) ?? new List<KbEntry>();
        }
        catch
        {
            return new List<KbEntry>();
        }
    }

    private static KbEntry? KbLookup(string text)
    {
        foreach (var entry in Kb.Value)
        {
            if (entry.TriggerAll.Count == 0) continue;
            if (entry.TriggerAll.All(tok => TriggerPresent(text, tok)))
                return entry;
        }
        return null;
    }

    /// <summary>触发词命中：化学式用边界匹配（避免 CO 命中 CO₂），普通词用子串。</summary>
    private static bool TriggerPresent(string text, string token)
    {
        if (LooksLikeFormula(token))
            return WholeFormulaContains(text, token);
        return text.Contains(token, StringComparison.Ordinal);
    }

    // 化学式必有大写元素符号（如 CO、Fe）；纯中文触发词（燃料电池/电极反应）不含大写 → 走子串匹配，
    // 否则会被 WholeFormulaContains 的中文相邻字边界判定漏匹配。
    private static bool LooksLikeFormula(string s) =>
        !string.IsNullOrEmpty(s) &&
        s.Any(c => c >= 'A' && c <= 'Z') &&
        s.All(c => char.IsLetterOrDigit(c) || "[]()^+-".Contains(c));

    /// <summary>整体化学式匹配：formula 作为独立物种出现（前/后为非化学式字符），避免 Fe 命中 FeSO₄。</summary>
    private static bool WholeFormulaContains(string text, string formula)
    {
        var compact = formula.Replace(" ", "");
        if (compact.Length == 0) return false;
        int idx = 0;
        while (true)
        {
            int found = text.IndexOf(compact, idx, StringComparison.Ordinal);
            if (found < 0) return false;
            char before = found > 0 ? text[found - 1] : ' ';
            char after = found + compact.Length < text.Length ? text[found + compact.Length] : ' ';
            // 边界：前后不得为字母 / 数字 / ^ （即不是更大化学式的一部分）
            if (!IsFormulaChar(before) && !IsFormulaChar(after)) return true;
            idx = found + 1;
        }
    }

    // 边界字符：仅 ASCII 字母/数字、^、unicode 上下标数字视为化学式延续；
    // 中文字（char.IsLetter 也为 true）不算 → 「是CO-空气」中 CO 可作为独立物种命中，
    // 同时 CO₂ / CO₃²⁻ 中的下标仍阻断 "CO" 误命中。
    private static bool IsFormulaChar(char c) =>
        (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
        (c >= '0' && c <= '9') || c == '^' ||
        (c >= '₀' && c <= '₉') || (c >= '⁰' && c <= '⁹');

    // ==================== 公共入口 ====================

    public static EngineResponse? TrySolve(EngineRequest req)
    {
        // 选择题不介入（保留既有选项校验路径，零回归）
        if (string.Equals(req.TaskType, "choice", StringComparison.OrdinalIgnoreCase)) return null;

        var text = req.RawText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return null;

        // 窄门控：必须是"写电极反应"意图
        if (!text.Contains("电极反应", StringComparison.Ordinal)) return null;

        // 1) 萃取式配平（显式氧化还原对 + 介质）
        var ex = TryBalanceExtractive(text);
        if (ex is not null) return Respond(ex, "萃取配平");

        // 2) 知识库溯源命中（化学事实在 JSON；命中条目 id 用于推理链溯源）
        var kbEntry = KbLookup(text);
        if (kbEntry is not null)
        {
            var knowledgeId = string.IsNullOrEmpty(kbEntry.Id) ? null : kbEntry.Id;
            return Respond(kbEntry.HalfReaction, "知识库溯源", knowledgeId);
        }

        return null;
    }

    private static EngineResponse Respond(string result, string kind, string? knowledgeId = null) => new()
    {
        Ok = true,
        FinalResult = result,
        DerivationSteps = new List<string> { result },
        // 加性推理链（spec R4）：仅知识库溯源命中携带真实 knowledge_id 时填充；
        // 萃取式配平为纯计算、无知识锚点 → 留空，避免假可溯源。
        ReasoningNodes = knowledgeId is null
            ? new List<ReasoningNode>()
            : new List<ReasoningNode>
            {
                new()
                {
                    Step = 1,
                    Description = "知识库溯源命中",
                    Premises = new List<string> { "题干触发词全命中 data/knowledge/electrode-half-reactions.json 条目" },
                    KnowledgeId = knowledgeId,
                    Conclusion = result,
                    EvidenceLevel = EvidenceLevel.KnowledgeFact
                }
            },
        ConstraintNote = $"【half-reaction 引擎】{kind}：仅当题干显式给出氧化还原对 + 介质，或命中 JSON 溯源条目时配平；不唯一 / 缺数据一律 null 不蒙数",
        Confidence = 1.0,
        AssetKind = "half_reaction"
    };

    // ==================== 萃取式配平 ====================

    private static string? TryBalanceExtractive(string text)
    {
        var medium = DetectMedium(text);
        if (medium is null) return null;

        var pair = ExtractPair(text);
        if (pair is null) return null;

        return BalanceHalfReaction(pair.Value.reactant, pair.Value.product, medium);
    }

    /// <summary>介质判定：酸 / 碱必须唯一，歧义 → null。</summary>
    private static string? DetectMedium(string text)
    {
        bool isBase = text.Contains("碱性") || text.Contains("OH-") || text.Contains("OH⁻") || text.Contains("氢氧根");
        bool isAcid = text.Contains("酸性") || text.Contains("H+") || text.Contains("H⁺") || text.Contains("氢离子");
        if (isBase && !isAcid) return "base";
        if (isAcid && !isBase) return "acid";
        return null;
    }

    private static readonly Regex PairRegex = new(
        @"([A-Za-z0-9\[\]\(\)\^\+\-]{2,})\s*(?:电解)?\s*(?:→|→|→|生成|转化为)\s*([A-Za-z0-9\[\]\(\)\^\+\-]{2,})",
        RegexOptions.Compiled);

    private static (Formula reactant, Formula product)? ExtractPair(string text)
    {
        foreach (Match m in PairRegex.Matches(text))
        {
            var r = ParseFormula(m.Groups[1].Value);
            var p = ParseFormula(m.Groups[2].Value);
            if (r is null || p is null) continue;
            if (SameFormula(r, p)) continue;
            return (r, p);
        }
        return null;
    }

    // ==================== 化学式解析 ====================

    private sealed class Formula
    {
        public Dictionary<string, int> Atoms { get; set; } = new();
        public int Charge;
        public string Core = "";   // 原子骨架（保留括号/下标数字），用于输出
    }

    private static Formula? ParseFormula(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var s = NormalizeUnicode(input).Trim();
        if (s.Length == 0) return null;

        int charge = 0;
        int caret = s.IndexOf('^');
        if (caret >= 0)
        {
            charge = ParseCharge(s.Substring(caret + 1).Trim());
            s = s.Substring(0, caret).Trim();
        }
        else if (s.Length > 1 && (s[^1] == '+' || s[^1] == '-'))
        {
            char prev = s[^2];
            if (char.IsLetterOrDigit(prev) || prev == ']' || prev == ')')
            {
                charge = s[^1] == '+' ? 1 : -1;
                s = s.Substring(0, s.Length - 1).TrimEnd();
            }
        }

        // Core 保留原始括号骨架（如 [B(OH)4]），用于输出还原；仅原子计数时剥离外层括号。
        var coreRaw = s;
        s = StripOuterBrackets(s);
        if (s.Length == 0) return null;

        var atoms = new Dictionary<string, int>();
        int i = 0;
        if (!ParseGroup(s, ref i, 1, atoms) || i != s.Length) return null;
        if (atoms.Count == 0) return null;

        return new Formula { Atoms = atoms, Charge = charge, Core = coreRaw };
    }

    private static int ParseCharge(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        int sign = 1;
        var t = s.Trim();
        if (t[0] == '-') { sign = -1; t = t.Substring(1); }
        else if (t[0] == '+') { t = t.Substring(1); }
        int mag = 1;
        if (t.Length > 0 && char.IsDigit(t[0]))
        {
            int end = 0;
            while (end < t.Length && char.IsDigit(t[end])) end++;
            mag = int.Parse(t.AsSpan(0, end));
        }
        return sign * mag;
    }

    private static bool ParseGroup(string s, ref int i, int mult, Dictionary<string, int> dict)
    {
        while (i < s.Length)
        {
            char c = s[i];
            if (c == '(' || c == '[')
            {
                char close = c == '(' ? ')' : ']';
                i++;
                var inner = new Dictionary<string, int>();
                if (!ParseGroup(s, ref i, 1, inner)) return false;
                if (i >= s.Length || s[i] != close) return false;
                i++;
                int m = ReadNumber(s, ref i);
                foreach (var kv in inner)
                    dict[kv.Key] = dict.GetValueOrDefault(kv.Key) + kv.Value * m * mult;
            }
            else if (c == ')' || c == ']')
            {
                return true;
            }
            else if (char.IsUpper(c))
            {
                string sym = c.ToString();
                i++;
                if (i < s.Length && char.IsLower(s[i])) { sym += s[i]; i++; }
                int m = ReadNumber(s, ref i);
                dict[sym] = dict.GetValueOrDefault(sym) + m * mult;
            }
            else
            {
                return false;
            }
        }
        return true;
    }

    private static int ReadNumber(string s, ref int i)
    {
        int start = i;
        while (i < s.Length && char.IsDigit(s[i])) i++;
        return i > start ? int.Parse(s.AsSpan(start, i - start)) : 1;
    }

    private static string StripOuterBrackets(string s)
    {
        while (s.Length >= 2 &&
               ((s[0] == '[' && s[^1] == ']') || (s[0] == '(' && s[^1] == ')')))
        {
            s = s.Substring(1, s.Length - 2);
        }
        return s;
    }

    private static bool SameFormula(Formula a, Formula b)
    {
        if (a.Atoms.Count != b.Atoms.Count || a.Charge != b.Charge) return false;
        foreach (var kv in a.Atoms)
            if (b.Atoms.GetValueOrDefault(kv.Key) != kv.Value) return false;
        return true;
    }

    private static string NormalizeUnicode(string s)
    {
        var chars = s.Select(c => c switch
        {
            '₀' => '0', '₁' => '1', '₂' => '2', '₃' => '3', '₄' => '4',
            '₅' => '5', '₆' => '6', '₇' => '7', '₈' => '8', '₉' => '9',
            '⁰' => '0', '¹' => '1', '²' => '2', '³' => '3', '⁴' => '4',
            '⁵' => '5', '⁶' => '6', '⁷' => '7', '⁸' => '8', '⁹' => '9',
            '⁺' => '+', '⁻' => '-',
            _ => c
        }).ToArray();
        return new string(chars);
    }

    // ==================== 半反应配平（核心） ====================

    /// <summary>
    /// 配平单个氧化还原对：reactant → product，介质 acid/base。
    /// 步骤：配平非 H/O 原子（系数 a/b）→ O 用 H₂O → H 用 H⁺(酸) 或 H₂O/OH⁻(碱) → 电荷用 e⁻。
    /// 输出约定：电子一律写在左侧（还原 +ne⁻、氧化 -ne⁻），与高中答案键惯例一致。
    /// 无法唯一配平 → null。
    /// </summary>
    private static string? BalanceHalfReaction(Formula reactant, Formula product, string medium)
    {
        var elems = reactant.Atoms.Keys
            .Concat(product.Atoms.Keys)
            .Where(e => e != "H" && e != "O")
            .Distinct()
            .ToList();
        if (elems.Count == 0) return null;

        // 每个非 H/O 元素必须两侧都有（氧化还原中心原子）
        foreach (var e in elems)
            if (!reactant.Atoms.ContainsKey(e) || !product.Atoms.ContainsKey(e)) return null;

        // 系数 a:b = p:r（取第一个元素），并校验所有元素比例一致
        var e0 = elems[0];
        int r0 = reactant.Atoms[e0], p0 = product.Atoms[e0];
        int g0 = Gcd(r0, p0);
        int a = p0 / g0, b = r0 / g0;
        foreach (var e in elems)
        {
            if (a * reactant.Atoms[e] != b * product.Atoms[e]) return null; // 比例不一致 → 非合法单一电对
        }

        // 两侧基准原子（含 H/O）与电荷，按系数 a/b 缩放
        int baseLeftO = a * reactant.Atoms.GetValueOrDefault("O");
        int baseRightO = b * product.Atoms.GetValueOrDefault("O");
        int baseLeftH = a * reactant.Atoms.GetValueOrDefault("H");
        int baseRightH = b * product.Atoms.GetValueOrDefault("H");
        int baseLeftQ = a * reactant.Charge;
        int baseRightQ = b * product.Charge;

        // 添加物计数（H₂O / OH⁻ / H⁺）
        int addLeftH2O = 0, addRightH2O = 0, addLeftOH = 0, addRightOH = 0, addLeftH = 0, addRightH = 0;

        // 1) 配 O：少的一侧加 H₂O
        int dO = baseRightO - baseLeftO;
        if (dO > 0) addLeftH2O += dO;
        else if (dO < 0) addRightH2O += -dO;

        // 2) 配 H（计入已加 H₂O 带来的 H）
        int totalLeftH = baseLeftH + 2 * addLeftH2O + addLeftH;
        int totalRightH = baseRightH + 2 * addRightH2O + addRightH;
        int dH = totalRightH - totalLeftH;
        if (medium == "acid")
        {
            if (dH > 0) addLeftH += dH;
            else addRightH += -dH;
        }
        else // base：缺 H 侧加 H₂O、另一侧加等当量 OH⁻
        {
            if (dH > 0) { addLeftH2O += dH; addRightOH += dH; }
            else { addRightH2O += -dH; addLeftOH += -dH; }
        }

        // 抵消两侧 H₂O
        int common = Math.Min(addLeftH2O, addRightH2O);
        addLeftH2O -= common; addRightH2O -= common;

        // 3) 配电荷（电子带 -1）：电子加到电荷较高（更正）的一侧
        int totalLeftQ = baseLeftQ - addLeftOH + addLeftH;
        int totalRightQ = baseRightQ - addRightOH + addRightH;
        int dQ = totalRightQ - totalLeftQ;
        int leftE = 0, rightE = 0;
        if (dQ > 0) rightE = dQ;        // 右侧更正 → 右侧加电子
        else if (dQ < 0) leftE = -dQ;   // 左侧更正 → 左侧加电子

        if (addLeftH < 0 || addRightH < 0 || addLeftOH < 0 || addRightOH < 0 || addLeftH2O < 0 || addRightH2O < 0) return null;

        // 电子约定：统一写到左侧（还原 +ne⁻、氧化 -ne⁻），与高中答案键惯例一致
        if (rightE > 0) { leftE -= rightE; rightE = 0; }

        return FormatResult(a, reactant, b, product, leftE, addLeftH2O, addRightH2O, addLeftOH, addRightOH, addLeftH, addRightH);
    }

    private static string FormatResult(
        int a, Formula reactant, int b, Formula product,
        int leftE, int leftH2O, int rightH2O, int leftOH, int rightOH, int leftH, int rightH)
    {
        var left = new List<string>();
        left.Add(Term(a, reactant.Core, reactant.Charge));
        if (leftE > 0) left.Add($"+ {leftE}e^-");
        else if (leftE < 0) left.Add($"- {-leftE}e^-");
        if (leftH2O > 0) left.Add($"+ {leftH2O}H2O");
        if (leftOH > 0) left.Add($"+ {leftOH}OH^-");
        if (leftH > 0) left.Add($"+ {leftH}H^+");

        var right = new List<string>();
        right.Add(Term(b, product.Core, product.Charge));
        if (rightH2O > 0) right.Add($"+ {rightH2O}H2O");
        if (rightOH > 0) right.Add($"+ {rightOH}OH^-");
        if (rightH > 0) right.Add($"+ {rightH}H^+");

        string Render(List<string> terms)
        {
            // 首项无符号；其余带 " + " 或衔接负号（电子为负时 "- ne^-" 直接衔接）
            var sb = new System.Text.StringBuilder(terms[0]);
            for (int i = 1; i < terms.Count; i++)
            {
                var t = terms[i];
                if (t.StartsWith('-')) sb.Append(t);          // "- 2e^-" → 紧贴，去空格
                else sb.Append(' ').Append(t);                  // "+ 8e^-" / "+ 4H2O"
            }
            return sb.ToString();
        }

        return $"{Render(left)} = {Render(right)}";
    }

    private static string Term(int coeff, string core, int charge)
    {
        var head = coeff > 1 ? $"{coeff}·{core}" : core;
        return charge != 0 ? $"{head}^{ChargeSuffix(charge)}" : head;
    }

    private static string ChargeSuffix(int charge)
    {
        int mag = Math.Abs(charge);
        string s = mag > 1 ? mag.ToString() : "";
        return s + (charge > 0 ? "+" : "-");
    }

    private static int Gcd(int a, int b)
    {
        a = Math.Abs(a); b = Math.Abs(b);
        while (b != 0) { (a, b) = (b, a % b); }
        return a == 0 ? 1 : a;
    }
}
