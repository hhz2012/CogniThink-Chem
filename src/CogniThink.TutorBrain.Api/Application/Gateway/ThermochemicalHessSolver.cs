using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 热化学方程式解析 + 盖斯定律（Hess）线性组合求解器。
/// <para>
/// 把题干中编号的热化学方程式（含 ΔH）解析为「物质 → 净计量系数」向量（反应物为正、生成物为负），
/// 对目标方程式求线性组合系数 k，使 Σ kᵢ·eqᵢ = target；唯一可解时 ΔH = Σ kᵢ·ΔHᵢ。
/// </para>
/// <para>红线：任一方程无法解析、线性组合无解/不唯一 → 返回 null，绝不蒙数。</para>
/// <para>支持：全角 ＋／数字、下标 ₂→2、状态符号 (g)(l)(s)(aq) 去除、分数系数 1/2、ΔH 全角等号与负号。</para>
/// </summary>
public static class ThermochemicalHessSolver
{
    // 下标数字 → 普通数字
    private static readonly Dictionary<char, char> SubscriptDigits = new()
    {
        ['₀'] = '0', ['₁'] = '1', ['₂'] = '2', ['₃'] = '3', ['₄'] = '4',
        ['₅'] = '5', ['₆'] = '6', ['₇'] = '7', ['₈'] = '8', ['₉'] = '9',
    };

    // 方程编号标记：①②…⑩ / ⅰⅱⅲ / 反应 i / 反应 ii / i: 等
    private static readonly Regex EnumerationMarker = new(
        @"(?<=[\s。；;:：]|^)\s*(?:[①②③④⑤⑥⑦⑧⑨⑩⑪⑫⑬⑭⑮]|反应\s*[iIvVxX]{1,3}\s*[:：]?|[iIvVxX]{1,3}\s*[:：])\s*",
        RegexOptions.Compiled);

    private static readonly Regex DeltaH = new(
        @"ΔH\s*(?<sub>[₀-₉0-9]?)\s*[=＝═]\s*(?<sign>[+\-－−]?)\s*(?<num>[\d]+(?:\.[\d]+)?)",
        RegexOptions.Compiled);

    private static readonly Regex FractionalCoef = new(@"^(?<num>\d+)\s*/\s*(?<den>\d+)\s*(?<rest>.+)$", RegexOptions.Compiled);
    // 物种 key 尾部的相态后缀（用于 StripPhase 剥离，以对齐已知方程相态）
    private static readonly Regex StateSuffix = new(@"\([gGlLsSqQaA]{1,2}\)$", RegexOptions.Compiled);
    private static readonly Regex IntegerCoef = new(@"^(?<coef>\d+)\s*(?<rest>.+)$", RegexOptions.Compiled);

    /// <summary>一条解析后的热化学方程式：净计量（反应物正 / 生成物负）+ ΔH。</summary>
    public sealed class ParsedThermoEq
    {
        public Dictionary<string, double> Net { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public double DeltaH { get; init; }
        public string Raw { get; init; } = "";
    }

    /// <summary>
    /// 尝试从题干解出目标反应的 ΔH（kJ/mol，保留一位小数）。
    /// </summary>
    /// <param name="question">完整题干。</param>
    /// <returns>(ΔH 数值, 推导文本)；不可解返回 null。</returns>
    public static (double Value, string Derivation)? TrySolve(string question)
    {
        if (string.IsNullOrWhiteSpace(question)) return null;

        // 1. 切出所有编号方程段 + 目标方程
        var segments = SplitEquationSegments(question);
        if (segments.Count < 2) return null; // 至少需要 1 个已知 + 1 个目标

        // 两遍扫描：先收集已知方程（含 ΔH 且非问句），再从剩余段中选目标，
        // 避免 T3=____℃ / K=____ 等无 ΔH 垃圾段抢占 target（pdf-261）。
        var known = new List<ParsedThermoEq>();
        var targetCandidates = new List<ParsedThermoEq>();
        foreach (var seg in segments)
        {
            var eq = ParseSegment(seg);
            if (eq is null) continue;
            var hasKnownDh = DeltaH.IsMatch(seg);
            if (hasKnownDh && !LooksLikeTargetQuestion(seg))
                known.Add(eq);
            else
                targetCandidates.Add(eq);
        }

        if (known.Count == 0) return null;

        var target = SelectTarget(targetCandidates, known);

        // 无显式目标段时，若题干问「X 的燃烧热」，推断燃烧目标方程（H₂→H₂O(l) 等，pdf-261）。
        target ??= TryInferCombustionTarget(question, known);

        if (target is null) return null;

        // 2. 线性组合求解
        var k = SolveLinearCombination(known, target);
        if (k is null) return null;

        double dh = 0;
        for (int i = 0; i < known.Count; i++) dh += k[i] * known[i].DeltaH;

        // 3. 校验：组合后的净计量必须与目标一致（浮点容差）
        if (!CombinationMatchesTarget(known, k, target)) return null;

        double rounded = Math.Round(dh, 1);
        var der = new StringBuilder();
        der.Append("盖斯定律：目标反应 = ");
        for (int i = 0; i < known.Count; i++)
        {
            if (i > 0) der.Append(" + ");
            der.Append(k[i].ToString("0.###", CultureInfo.InvariantCulture)).Append("×方程").Append((char)('①' + i));
        }
        der.Append($"；ΔH = {rounded.ToString("0.0", CultureInfo.InvariantCulture)} kJ/mol");
        return (rounded, der.ToString());
    }

    private static bool LooksLikeTargetQuestion(string seg) =>
        seg.Contains("的ΔH") || seg.Contains("ΔH是多少") || seg.Contains("ΔH＝____") ||
        seg.Contains("计算") || seg.Contains("求") || seg.Contains("目标");

    private static string StripPhase(string key) => StateSuffix.Replace(key, "");

    /// <summary>
    /// 从候选段中选目标方程：优先「像问句 且 与已知方程有物种重叠」的段，
    /// 排除 T3=__℃ / K=__ 等无物种重叠的垃圾段；否则取最后一个有重叠的段。
    /// </summary>
    private static ParsedThermoEq? SelectTarget(List<ParsedThermoEq> candidates, List<ParsedThermoEq> known)
    {
        var knownSpecies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in known) foreach (var k in e.Net.Keys) knownSpecies.Add(k);

        bool Overlaps(ParsedThermoEq eq)
        {
            foreach (var k in eq.Net.Keys) if (knownSpecies.Contains(k)) return true;
            return false;
        }

        foreach (var c in candidates)
            if (LooksLikeTargetQuestion(c.Raw) && Overlaps(c)) return c;
        for (int i = candidates.Count - 1; i >= 0; i--)
            if (Overlaps(candidates[i])) return candidates[i];
        return null;
    }

    /// <summary>
    /// 题干问「X 的燃烧热」且无显式目标方程时，推断 X 燃烧的热化学目标方程：
    /// X + O₂(g) → CO₂(g) + H₂O(l) [+ SO₂(g) + N₂(g)]（高中燃烧热约定：H 的终态为液态水）。
    /// 推断后仍须由已知方程线性组合且守恒校验通过才会被 TrySolve 采纳，否则返回 null（不蒙数）。
    /// </summary>
    private static ParsedThermoEq? TryInferCombustionTarget(string question, List<ParsedThermoEq> known)
    {
        if (!question.Contains("燃烧热")) return null;
        var fuelMatch = Regex.Match(question, @"([A-Za-z][A-Za-z0-9()（）]{0,14})\s*(?:的)?\s*燃烧热");
        if (!fuelMatch.Success) return null;
        var fuel = StripPhase(fuelMatch.Groups[1].Value.Trim());
        if (fuel.Length == 0) return null;

        var comp = ParseFormulaComposition(fuel);
        if (comp is null || comp.Count == 0) return null;

        double nC = comp.TryGetValue("C", out var c) ? c : 0;
        double nH = comp.TryGetValue("H", out var h) ? h : 0;
        double nS = comp.TryGetValue("S", out var s) ? s : 0;
        double nN = comp.TryGetValue("N", out var n) ? n : 0;
        double nO = comp.TryGetValue("O", out var o) ? o : 0;
        if (nC == 0 && nH == 0 && nS == 0) return null; // 无可燃烧元素

        double co2 = nC, h2o = nH / 2.0, so2 = nS, n2 = nN / 2.0;
        double o2 = nC + nH / 4.0 + nS - nO / 2.0;
        if (o2 < 0) return null; // 燃料自身含氧过多，保守不作燃烧推断

        // 燃料相态：对齐到已知方程中该燃料的相态（如已知用 H2(g)，目标燃料也用 (g)）
        string fuelKey = Regex.Replace(fuel, @"\s+", "").ToLowerInvariant();
        string fuelStripped = StripPhase(fuelKey);
        string fuelPhase = "";
        foreach (var e in known)
            foreach (var k in e.Net.Keys)
                if (string.Equals(StripPhase(k), fuelStripped, StringComparison.OrdinalIgnoreCase) && k.Length > fuelStripped.Length)
                { fuelPhase = k.Substring(fuelStripped.Length); break; }

        var sb = new StringBuilder();
        sb.Append(fuel).Append(fuelPhase).Append(" + ").Append(TermWithCoef(o2, "O2(g)")).Append(" = ");
        var products = new List<string>();
        if (co2 > 0) products.Add(TermWithCoef(co2, "CO2(g)"));
        if (h2o > 0) products.Add(TermWithCoef(h2o, "H2O(l)"));
        if (so2 > 0) products.Add(TermWithCoef(so2, "SO2(g)"));
        if (n2 > 0) products.Add(TermWithCoef(n2, "N2(g)"));
        sb.Append(string.Join(" + ", products));
        var eqText = sb.ToString();

        var net = ParseNetStoichiometry(eqText);
        if (net is null || net.Count == 0) return null;
        return new ParsedThermoEq { Net = AlignTargetToKnown(net, known), DeltaH = 0, Raw = eqText };
    }

    private static string TermWithCoef(double coef, string species)
    {
        if (Math.Abs(coef - 1.0) < 1e-9) return species;
        return $"{FormatCoef(coef)} {species}";
    }

    private static string FormatCoef(double v)
    {
        if (Math.Abs(v % 1) < 1e-9) return ((long)Math.Round(v)).ToString();
        for (int den = 2; den <= 4; den++)
        {
            double num = v * den;
            if (Math.Abs(num - Math.Round(num)) < 1e-9)
                return $"{(long)Math.Round(num)}/{den}";
        }
        return v.ToString("0.###", CultureInfo.InvariantCulture);
    }

    /// <summary>把目标物种的相态对齐到已知方程：同公式已知中仅出现一种相态时跟随它；多种相态时保留目标相态。</summary>
    private static Dictionary<string, double> AlignTargetToKnown(Dictionary<string, double> target, List<ParsedThermoEq> known)
    {
        var phasesByFormula = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in known)
            foreach (var k in e.Net.Keys)
            {
                var f = StripPhase(k);
                var ph = k.Substring(f.Length);
                if (!phasesByFormula.TryGetValue(f, out var set))
                { set = new HashSet<string>(StringComparer.OrdinalIgnoreCase); phasesByFormula[f] = set; }
                set.Add(ph);
            }
        var aligned = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in target)
        {
            var f = StripPhase(kv.Key);
            var ph = kv.Key.Substring(f.Length);
            if (phasesByFormula.TryGetValue(f, out var set) && set.Count > 0)
            {
                if (set.Contains(ph)) aligned[kv.Key] = kv.Value;
                else if (set.Count == 1) aligned[f + set.First()] = kv.Value;
                else aligned[kv.Key] = kv.Value;
            }
            else aligned[f] = kv.Value;
        }
        return aligned;
    }

    private static Dictionary<string, double>? ParseFormulaComposition(string formula)
    {
        if (string.IsNullOrWhiteSpace(formula)) return null;
        var comp = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        int i = 0;
        ParseGroup(formula, ref i, comp, 1.0);
        return comp.Count == 0 ? null : comp;
    }

    private static void ParseGroup(string s, ref int i, Dictionary<string, double> comp, double mult)
    {
        while (i < s.Length)
        {
            char c = s[i];
            if (c == '(' || c == '（')
            {
                i++;
                var inner = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                ParseGroup(s, ref i, inner, 1.0);
                double m = ReadNumber(s, ref i);
                foreach (var kv in inner) AddTo(comp, kv.Key, kv.Value * m * mult);
            }
            else if (c == ')' || c == '）')
            {
                i++;
                return;
            }
            else if (char.IsUpper(c))
            {
                string elem = c.ToString();
                i++;
                if (i < s.Length && char.IsLower(s[i])) { elem += s[i]; i++; }
                double cnt = ReadNumber(s, ref i);
                AddTo(comp, elem, cnt * mult);
            }
            else { i++; }
        }
    }

    private static double ReadNumber(string s, ref int i)
    {
        int start = i;
        while (i < s.Length && char.IsDigit(s[i])) i++;
        if (i > start && double.TryParse(s.AsSpan(start, i - start), out var v)) return v;
        return 1.0;
    }

    private static void AddTo(Dictionary<string, double> d, string k, double v)
    {
        if (d.TryGetValue(k, out var ex)) d[k] = ex + v; else d[k] = v;
    }

    /// <summary>把题干切成方程段：按编号标记 + 句号切分，只保留含等号/可逆号的段。</summary>
    private static List<string> SplitEquationSegments(string question)
    {
        var result = new List<string>();
        // 先在编号标记前插入换行，便于切分
        var marked = EnumerationMarker.Replace(question, "\n$0");
        var raw = marked.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in raw)
        {
            // 进一步按句号/分号切，但保留含 ⇌/=/→ 的完整方程
            foreach (var piece in part.Split(new char[] { '。', '；', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                // 含 ═（U+2550 框线等号，pdf-261 用）的段也要保留
                if (piece.Contains('⇌') || piece.Contains('=') || piece.Contains('═') || piece.Contains("→") || piece.Contains("->"))
                    result.Add(piece);
            }
        }
        return result;
    }

    private static ParsedThermoEq? ParseSegment(string seg)
    {
        // 提取 ΔH 数值
        var dhMatch = DeltaH.Match(seg);
        double deltaH = 0;
        bool dhFound = false;
        if (dhMatch.Success)
        {
            var sign = dhMatch.Groups["sign"].Value;
            var num = double.Parse(dhMatch.Groups["num"].Value, CultureInfo.InvariantCulture);
            deltaH = (sign == "-" || sign == "－" || sign == "−") ? -num : num;
            dhFound = true;
        }

        // 取 ΔH 之前的子串作为纯方程（同时去掉末尾 kJ/mol 等单位，避免污染物种 key）；
        // 无 ΔH 的目标段则保留整段，由 ParseNetStoichiometry 裁剪到方程核心。
        var eqText = (dhMatch.Success ? seg.Substring(0, dhMatch.Index) : seg).Trim();

        var net = ParseNetStoichiometry(eqText);
        if (net is null || net.Count == 0) return null;

        return new ParsedThermoEq { Net = net, DeltaH = dhFound ? deltaH : 0, Raw = seg };
    }

    /// <summary>归一化并解析方程为净计量（左正右负）。</summary>
    private static Dictionary<string, double>? ParseNetStoichiometry(string eqText)
    {
        var text = Normalize(eqText);

        string? sep = null;
        int idx = -1;
        foreach (var s in new[] { "⇌", "=", "→", "->" })
        {
            var j = text.IndexOf(s, StringComparison.Ordinal);
            if (j >= 0 && (idx < 0 || j < idx)) { idx = j; sep = s; }
        }
        if (sep is null || idx < 0) return null;

        // 裁剪到纯方程核心：从分隔符向左/向右，只保留方程字符，去掉"则/的ΔH是多少/反应 i:"等自然语言
        int leftStart = idx;
        while (leftStart > 0 && IsEquationChar(text[leftStart - 1])) leftStart--;
        int rightEnd = idx + sep.Length;
        while (rightEnd < text.Length && IsEquationChar(text[rightEnd])) rightEnd++;

        var left = text.Substring(leftStart, idx - leftStart);
        var right = text.Substring(idx + sep.Length, rightEnd - (idx + sep.Length));

        var net = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var term in SplitTerms(left))
            AddTerm(net, term, +1.0);
        foreach (var term in SplitTerms(right))
            AddTerm(net, term, -1.0);

        return net.Count == 0 ? null : net;
    }

    private static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (SubscriptDigits.TryGetValue(ch, out var d)) sb.Append(d);
            else if (ch == '＋') sb.Append('+');
            else if (ch == '－' || ch == '−') sb.Append('-'); // 全角/负号减号统一
            else if (ch == '＝' || ch == '═') sb.Append('='); // 全角等号 / 框线等号（U+2550，pdf-261）统一为 =
            else if (ch == '　') sb.Append(' ');
            else sb.Append(ch);
        }
        // 保留相态后缀到物种 key：H2O(l) 与 H2O(g) 是不同物种（pdf-261 需区分液态/气态水，
        // 否则 H2O(l)→H2O(g) 净计量抵消为 0）。同物质同相仍匹配 → 不破坏既有单测。
        return sb.ToString();
    }

    /// <summary>
    /// 归一化后，属于方程式的字符（仅 ASCII 字母/数字，避免中文（Unicode Letter 类）被吞进方程核心；
    /// 以及括号/加号/空格/斜杠/点/减号/箭头/可逆号）。
    /// </summary>
    private static bool IsEquationChar(char c) =>
        (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') ||
        c is '(' or ')' or '[' or ']' or '+' or ' ' or '/' or '.' or '-' or '⇌' or '→';

    private static void AddTerm(Dictionary<string, double> net, string rawTerm, double sign)
    {
        var term = rawTerm.Trim();
        if (term.Length == 0) return;

        double coef = 1.0;
        var frac = FractionalCoef.Match(term);
        if (frac.Success)
        {
            coef = double.Parse(frac.Groups["num"].Value, CultureInfo.InvariantCulture)
                  / double.Parse(frac.Groups["den"].Value, CultureInfo.InvariantCulture);
            term = frac.Groups["rest"].Value.Trim();
        }
        else
        {
            var integer = IntegerCoef.Match(term);
            if (integer.Success)
            {
                coef = int.Parse(integer.Groups["coef"].Value, CultureInfo.InvariantCulture);
                term = integer.Groups["rest"].Value.Trim();
            }
        }

        // 去掉首尾空白和可能的电荷标注残留；物种 key 归一化到小写去空白
        var key = Regex.Replace(term, @"\s+", "").ToLowerInvariant();
        if (key.Length == 0) return;
        if (net.TryGetValue(key, out var existing)) net[key] = existing + sign * coef;
        else net[key] = sign * coef;
    }

    /// <summary>按顶层 "+" 拆分（括号内不拆）。</summary>
    private static List<string> SplitTerms(string side)
    {
        var terms = new List<string>();
        var depth = 0;
        var buf = new StringBuilder();
        foreach (var c in side)
        {
            switch (c)
            {
                case '(' or '[': depth++; buf.Append(c); break;
                case ')' or ']': depth = Math.Max(0, depth - 1); buf.Append(c); break;
                case '+' when depth == 0: terms.Add(buf.ToString()); buf.Clear(); break;
                default: buf.Append(c); break;
            }
        }
        terms.Add(buf.ToString());
        return terms.Where(t => t.Trim().Length > 0).ToList();
    }

    /// <summary>高斯消元解 Σ kᵢ·netᵢ[m] = netTarget[m]（对所有物质 m）。</summary>
    private static double[]? SolveLinearCombination(List<ParsedThermoEq> known, ParsedThermoEq target)
    {
        var substances = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in known) foreach (var k in e.Net.Keys) substances.Add(k);
        foreach (var k in target.Net.Keys) substances.Add(k);
        var subs = substances.ToList();

        int n = known.Count;
        // 增广矩阵：每行一个物质约束，n 列 k + 1 列 rhs
        var rows = new List<double[]>();
        foreach (var m in subs)
        {
            var row = new double[n + 1];
            for (int i = 0; i < n; i++)
                row[i] = known[i].Net.TryGetValue(m, out var v) ? v : 0.0;
            row[n] = target.Net.TryGetValue(m, out var tv) ? tv : 0.0;
            rows.Add(row);
        }

        return GaussianEliminate(rows, n);
    }

    private static bool CombinationMatchesTarget(List<ParsedThermoEq> known, double[] k, ParsedThermoEq target)
    {
        var subs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in known) foreach (var key in e.Net.Keys) subs.Add(key);
        foreach (var key in target.Net.Keys) subs.Add(key);

        foreach (var m in subs)
        {
            double sum = 0;
            for (int i = 0; i < known.Count; i++)
                sum += k[i] * (known[i].Net.TryGetValue(m, out var v) ? v : 0.0);
            double tv = target.Net.TryGetValue(m, out var t) ? t : 0.0;
            if (Math.Abs(sum - tv) > 1e-6) return false;
        }
        return true;
    }

    private static double[]? GaussianEliminate(List<double[]> rows, int n)
    {
        // 取前 n 个线性独立行求解（行可能多于 n）
        var a = rows.ToArray();
        int rowCount = a.Length;
        int pivotRow = 0;
        for (int col = 0; col < n && pivotRow < rowCount; col++)
        {
            // 选主元
            int best = pivotRow;
            for (int r = pivotRow + 1; r < rowCount; r++)
                if (Math.Abs(a[r][col]) > Math.Abs(a[best][col])) best = r;
            if (Math.Abs(a[best][col]) < 1e-9) continue;
            (a[pivotRow], a[best]) = (a[best], a[pivotRow]);

            double div = a[pivotRow][col];
            for (int j = col; j <= n; j++) a[pivotRow][j] /= div;
            for (int r = 0; r < rowCount; r++)
            {
                if (r == pivotRow) continue;
                double factor = a[r][col];
                if (Math.Abs(factor) < 1e-12) continue;
                for (int j = col; j <= n; j++) a[r][j] -= factor * a[pivotRow][j];
            }
            pivotRow++;
        }

        // 校验所有行：若某行系数全 0 但 rhs 非 0 → 无解
        foreach (var row in a)
        {
            bool allZero = true;
            for (int j = 0; j < n; j++) if (Math.Abs(row[j]) > 1e-9) { allZero = false; break; }
            if (allZero && Math.Abs(row[n]) > 1e-6) return null;
        }

        var sol = new double[n];
        for (int i = 0; i < n; i++) sol[i] = 0;
        // 回代：每个主元列对应一个 k
        for (int r = 0; r < rowCount; r++)
        {
            int col = -1;
            for (int j = 0; j < n; j++) if (Math.Abs(a[r][j]) > 1e-9) { col = j; break; }
            if (col >= 0) sol[col] = a[r][n];
        }
        return sol;
    }
}
