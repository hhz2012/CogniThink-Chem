using System.Text.RegularExpressions;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 多小题"判断方程式书写正误，填 √/×"聚合求解器（阶段3D / AC-14）。
/// <para>真值源：<see cref="EquationCorrectnessJudge"/>（符号引擎逐方程判定）；LLM 只提供题干文本。</para>
/// <para><b>聚合铁律（守 precision）</b>：仅当全部小题都有确定判决（无 null）才输出聚合串；
/// 任一小题不确定 → 整体返回 null（不输出部分答案——错一位会被采纳却判分不中 → precision 掉）。</para>
/// <para>N=1 输出单字符 √/×；N&gt;1 输出 "(1)√ (2)× ..."。</para>
/// <para>不命中（非正误判断题 / 取不到方程）返回 null → 零回归，由 <c>CallCompile</c> 后续分支接管。</para>
/// </summary>
public static class MultiSubCorrectnessSolver
{
    // 小题编号：(1) （2） ( 3 ) 等（半角/全角括号，数字与右括号间只允许空格）。
    // 注意：(2024·北京卷) 这类括号内数字后紧跟非空白非右括号字符 → 不匹配，避免误切出处分句。
    private static readonly Regex SubMarker = new(@"[（(]\s*(\d+)\s*[）)]", RegexOptions.Compiled);

    // 结尾答题空：( ) （ ） () （）(允许内部空白)，锚定结尾。
    private static readonly Regex TrailingBlank = new(@"[（(]\s*[）)]\s*$", RegexOptions.Compiled);

    // 任意位置的答题空 ( )/（ ）（不锚定结尾），用于枚举每个小题的终止位置。
    // (2024·北京卷) / (FeSO4·7H2O) / [Cu(NH3)2] 等括号内非空 → 不匹配，只命中答题空。
    private static readonly Regex JudgmentBlank = new(@"[（(]\s*[）)]", RegexOptions.Compiled);

    /// <summary>尝试求解；不命中返回 null（零回归）。</summary>
    public static EngineResponse? TrySolve(EngineRequest req)
    {
        var text = !string.IsNullOrWhiteSpace(req.RawText) ? req.RawText : (req.StudentAnswer ?? string.Empty);
        if (string.IsNullOrWhiteSpace(text)) return null;

        try
        {
            var subs = SplitSubQuestions(text);
            if (subs.Count == 0) return null;

            var symbols = new List<char>(subs.Count);
            var steps = new List<string>();
            foreach (var sub in subs)
            {
                var (equation, context) = ExtractEquationAndContext(sub.Text);
                if (string.IsNullOrWhiteSpace(equation))
                {
                    // 取不到方程（如概念小题）→ 不确定 → 整题 null
                    return null;
                }

                var verdict = EquationCorrectnessJudge.Judge(equation, context);
                if (!verdict.IsDetermined)
                {
                    // 任一小题不确定 → 整题 null（不蒙数，守 precision）
                    return null;
                }

                symbols.Add(verdict.Symbol!.Value);
                steps.Add($"第({sub.Index})小题：{verdict.Symbol} — {string.Join("；", verdict.Reasons)}");
            }

            return new EngineResponse
            {
                Ok = true,
                FinalResult = FormatVerdict(symbols),
                DerivationSteps = steps,
                ConstraintNote = "【高中限定】逐方程符号引擎判定；任一小题不确定则整题不输出（不蒙数）",
                Confidence = 1.0
            };
        }
        catch
        {
            // 任何意外异常都不拖垮网关链路；保守回退 null
            return null;
        }
    }

    private sealed record SubBlock(int Index, string Text);

    /// <summary>
    /// 是否为"判断方程式书写正误，填 √/×"题型。
    /// 仅此类题才进入拆分；其余题（含概念判断题、选择题）直接返回空 → 由后续分支接管，零回归。
    /// </summary>
    private static bool LooksLikeJudgmentQuestion(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        // 明确的正误判断信号（pdf-051 / pdf-134 / pdf-480 等题干都含）
        if (text.Contains("正误") || text.Contains("填√") || text.Contains("填“√") || text.Contains("填\"√"))
            return true;
        if (text.Contains("正确") && text.Contains("错误") && text.Contains("填"))
            return true;
        // (N) 编号 + 答题空：单小题判断（pdf-468/471/472 等）
        return SubMarker.IsMatch(text) && JudgmentBlank.IsMatch(text);
    }

    /// <summary>
    /// 拆小题：以答题空 ( )/（ ）锚定每个小题的结尾，逐段切分。
    /// <para>不依赖 (N) 编号——pdf-051 第 2-5 小题缺 (N) 前缀，仅以换行 + (年份·来源) 开头，
    /// 按编号切分会把 5 个小题并成 1 块；改按答题空切分可正确拆出 5 段。</para>
    /// 仅保留以答题空结尾的片段；编号取段内 (N)，缺省按顺序补未占用正整数。
    /// </summary>
    private static List<SubBlock> SplitSubQuestions(string text)
    {
        if (!LooksLikeJudgmentQuestion(text)) return new List<SubBlock>();

        var blanks = JudgmentBlank.Matches(text);
        if (blanks.Count == 0) return new List<SubBlock>();

        var raw = new List<(string seg, int? explicitIdx)>();
        var prevEnd = 0;
        foreach (Match b in blanks)
        {
            var end = b.Index + b.Length;
            var seg = text.Substring(prevEnd, end - prevEnd).Trim();
            prevEnd = end;
            if (!LooksLikeJudgmentBlank(seg)) continue;

            int? eidx = null;
            var m = SubMarker.Match(seg);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var n)) eidx = n;
            raw.Add((seg, eidx));
        }

        if (raw.Count == 0) return new List<SubBlock>();

        // 编号：有 (N) 用之；否则按顺序补未占用的正整数（pdf-051 仅 (1) 有编号 → 自动补 2..5）
        var used = new HashSet<int>(raw.Where(x => x.explicitIdx.HasValue).Select(x => x.explicitIdx!.Value));
        var next = 1;
        var blocks = new List<SubBlock>(raw.Count);
        foreach (var (seg, eidx) in raw)
        {
            int idx;
            if (eidx.HasValue) idx = eidx.Value;
            else { while (used.Contains(next)) next++; idx = next; used.Add(idx); }
            blocks.Add(new SubBlock(idx, seg));
        }

        return blocks;
    }

    /// <summary>片段是否像"判断正误填 √/×"小题：以答题空 ( )/（ ）结尾。</summary>
    private static bool LooksLikeJudgmentBlank(string seg)
    {
        if (string.IsNullOrWhiteSpace(seg)) return false;
        return TrailingBlank.IsMatch(seg.TrimEnd());
    }

    // 语境/方程分界冒号（半角/全角）
    private static readonly char[] ColonChars = { '：', ':' };

    /// <summary>
    /// 从小题文本中抽取（归一化方程, 语境）。
    /// √/× 小题格式恒为 "(N)语境描述:方程式( )"：去编号、去结尾答题空后，
    /// 以<b>最后一个冒号</b>为界切分（前=语境，后=方程）；无冒号时整段视为方程。
    /// 不沿用 <c>BalanceGatewaySolver.ExtractFirstEquation</c>，因其分隔符正则要求 ≥2 个等号，
    /// 无法识别单等号 "A = B"（pdf-051 等多用单等号）。
    /// 归一化后再校验（===/→/⇌/--条件--> 归一化为 =），排除 pH=7 之类非方程。
    /// </summary>
    private static (string? equation, string? context) ExtractEquationAndContext(string subText)
    {
        // 去首个编号 (N)
        var withoutIndex = SubMarker.Replace(subText, string.Empty, 1).Trim();
        // 去结尾答题空 ( )/（ ）
        var core = TrailingBlank.Replace(withoutIndex, string.Empty).TrimEnd().Trim();
        if (string.IsNullOrWhiteSpace(core)) return (null, null);

        string rawEquation;
        string? context;
        var colonIdx = core.LastIndexOfAny(ColonChars);
        if (colonIdx >= 0)
        {
            rawEquation = core[(colonIdx + 1)..].Trim();
            context = core[..colonIdx].Trim().TrimEnd('，', ',', '。', '；', ';', ' ');
            if (context.Length == 0) context = null;
        }
        else
        {
            rawEquation = core.Trim();
            context = null;
        }

        // 归一化（===/→/⇌/--条件--> → =；Unicode 上下标 → caret），再校验是否为方程
        var equation = EquationCorrectnessJudge.NormalizeEquation(rawEquation);
        if (!LooksLikeEquation(equation)) return (null, null);
        return (equation, context);
    }

    /// <summary>归一化后的串是否像化学方程式：含 = 且两侧均有元素字母，排除 pH=7 / x=5 / 概念句。</summary>
    private static bool LooksLikeEquation(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        var idx = s.IndexOf('=');
        if (idx < 0) return false;
        return ContainsElementLetter(s[..idx]) && ContainsElementLetter(s[(idx + 1)..]);
    }

    private static bool ContainsElementLetter(string s) => s.Any(char.IsLetter);

    /// <summary>N=1 输出单字符；N&gt;1 输出 "(1)√ (2)× ..."。</summary>
    private static string FormatVerdict(List<char> symbols)
    {
        if (symbols.Count == 1) return symbols[0].ToString();
        return string.Join(" ", symbols.Select((s, i) => $"({i + 1}){s}"));
    }
}
