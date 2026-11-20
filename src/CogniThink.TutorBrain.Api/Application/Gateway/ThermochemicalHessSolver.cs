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
        @"ΔH\s*(?<sub>[₀-₉0-9]?)\s*[=＝]\s*(?<sign>[+\-－−]?)\s*(?<num>[\d]+(?:\.[\d]+)?)",
        RegexOptions.Compiled);

    private static readonly Regex StateSymbol = new(@"\([gGlLsSqQaA]{1,2}\)", RegexOptions.Compiled);
    private static readonly Regex FractionalCoef = new(@"^(?<num>\d+)\s*/\s*(?<den>\d+)\s*(?<rest>.+)$", RegexOptions.Compiled);
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

        var known = new List<ParsedThermoEq>();
        ParsedThermoEq? target = null;
        foreach (var seg in segments)
        {
            var eq = ParseSegment(seg);
            if (eq is null) continue;
            // 目标段：含 ΔH 未知（"的ΔH是多少"/"计算…ΔH"）或没有 ΔH 数值且出现在问句中
            var hasKnownDh = DeltaH.IsMatch(seg);
            if (hasKnownDh && !LooksLikeTargetQuestion(seg))
                known.Add(eq);
            else
                target ??= eq;
        }

        if (target is null || known.Count == 0) return null;

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
                if (piece.Contains('⇌') || piece.Contains('=') || piece.Contains("→") || piece.Contains("->"))
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
            else if (ch == '＝') sb.Append('=');
            else if (ch == '　') sb.Append(' ');
            else sb.Append(ch);
        }
        var t = sb.ToString();
        t = StateSymbol.Replace(t, ""); // 去 (g)(l)(s)(aq)
        return t;
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
