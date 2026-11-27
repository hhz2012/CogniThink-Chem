using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Structure;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 选择项电子排布真伪判定（FR-1）。
/// 判定形如「基态/离子 &lt;元素&gt; 电子排布式 &lt;X&gt;」「简化电子排布 &lt;X&gt;」「价层电子排布 &lt;X&gt;」的陈述：
/// 从 PeriodicTable 算真值排布，与陈述声称排布（归一化、展开稀有气体简写后）逐亚层比对。
/// 红线（不蒙数）：元素不可识别 / 声称排布不可解析 / 真值不可算 / 多元素歧义 / 比较型陈述 → Undecided。
/// 仅当恰好识别出 1 个元素且声称排布可解析时，才返回 True/False。
/// </summary>
internal sealed class ElectronConfigStatementJudge : IStatementJudge
{
    // 排布锚点：陈述必须明确在说电子排布
    private static readonly Regex Anchor = new(
        @"电子排布|电子排布式|核外电子排布|轨道表示式|外围电子排布|价层电子|价电子排布",
        RegexOptions.Compiled);
    // 比较/开放型陈述不判（如「Ga 与 Ge 排布不同」）
    private static readonly Regex Reject = new(@"不同|比较|分别|依次|与.*电子排布", RegexOptions.Compiled);
    // 元素符号（带边界，避免命中 CO 中的 C/O 等）——在剥离 [稀有气体] 后扫描。
    // 允许符号后紧跟离子电荷（如 Fe2+、O2-），否则 Fe2+ 中的 Fe 会因后面是数字被判越界而漏检。
    private static readonly Regex Sym = new(@"(?<![A-Za-z0-9])([A-Z][a-z]?)(?:\d+[+\-])?(?![A-Za-z0-9])", RegexOptions.Compiled);
    // 亚层 token：如 1s2 / 3d10 / 4p1（已归一化为 ASCII 数字）。
    // 排布串常无分隔符（1s22s22p6…），必须非贪婪取电子数 + 前瞻「下一亚层/非数字/结尾」才能正确切分；
    // 壳层用 [1-7]，避免把 3d10 中计数的末位误当成下一亚层壳号。
    private static readonly Regex Subshell =
        new(@"([1-7])([spdf])(\d+?)(?=[1-7][spdf]|[^0-9]|$)", RegexOptions.Compiled);
    private static readonly Regex NobleBracket = new(@"\[([A-Z][a-z]?)\]", RegexOptions.Compiled);

    public Verdict Judge(string stem, string statement)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(statement)) return Verdict.Undecided;
            var n = StatementNorm.Norm(statement);              // 去空白
            if (!Anchor.IsMatch(n)) return Verdict.Undecided;
            if (Reject.IsMatch(n)) return Verdict.Undecided;

            // 剥离 [稀有气体] 简写后再扫元素，避免把 Ar/Ne 当成题述元素
            var stripped = NobleBracket.Replace(n, "");

            // 收集识别到的元素（符号 + 中文名），去重
            var zs = new HashSet<int>();
            foreach (Match m in Sym.Matches(stripped))
            {
                var z = PeriodicTable.AtomicNumberOf(m.Groups[1].Value);
                if (z is int zz && zz >= 1) zs.Add(zz);
            }
            for (var z = 1; z <= 92; z++)
            {
                var name = PeriodicTable.NameOf(z);
                if (!string.IsNullOrEmpty(name) && stripped.Contains(name)) zs.Add(z);
            }
            // 多元素歧义 → 不蒙数
            if (zs.Count != 1) return Verdict.Undecided;
            var z0 = zs.First();

            var charge = ParseCharge(stripped, z0);

            // 解析声称排布：展开 [稀有气体] → 全排布，再按亚层 token 化
            var expanded = ExpandNobleGas(n);
            var claimed = new Dictionary<string, int>();
            foreach (Match m in Subshell.Matches(expanded))
            {
                var sh = m.Groups[1].Value + m.Groups[2].Value;
                var cnt = int.Parse(m.Groups[3].Value);
                claimed[sh] = claimed.GetValueOrDefault(sh) + cnt;
            }
            if (claimed.Count == 0) return Verdict.Undecided;

            // 真值
            var valence = Regex.IsMatch(n, @"价层|价电子|最外层电子|外围电子");
            IReadOnlyDictionary<string, int> truth = valence
                ? PeriodicTable.ValenceConfiguration(z0, charge)
                : (charge != 0 ? PeriodicTable.IonConfiguration(z0, charge) : PeriodicTable.Configuration(z0));
            if (truth.Count == 0) return Verdict.Undecided;

            var equal = truth.Count == claimed.Count
                        && truth.All(kv => claimed.TryGetValue(kv.Key, out var v) && v == kv.Value);
            return equal ? Verdict.True : Verdict.False;
        }
        catch
        {
            return Verdict.Undecided;
        }
    }

    // 在元素符号/名称之后的紧邻片段中解析电荷，如 Fe2+ → +2，O2- → -2
    private static int ParseCharge(string text, int z)
    {
        var sym = PeriodicTable.SymbolOf(z);
        var name = PeriodicTable.NameOf(z);
        var idx = -1;
        if (!string.IsNullOrEmpty(sym)) idx = text.IndexOf(sym, System.StringComparison.Ordinal);
        if (idx < 0 && !string.IsNullOrEmpty(name)) idx = text.IndexOf(name, System.StringComparison.Ordinal);
        if (idx < 0) return 0;
        var start = idx + (sym != null && text.IndexOf(sym, System.StringComparison.Ordinal) == idx ? sym!.Length : name!.Length);
        var len = System.Math.Min(8, text.Length - start);
        if (len <= 0) return 0;
        var frag = text.Substring(start, len);
        var m = Regex.Match(frag, @"^(\d+)([+\-])");
        if (!m.Success) return 0;
        var mag = int.Parse(m.Groups[1].Value);
        return m.Groups[2].Value == "+" ? mag : -mag;
    }

    // 把 [Ar] 展开为 Ar 的全排布（ASCII、无空格）
    private static string ExpandNobleGas(string text)
    {
        return NobleBracket.Replace(text, m =>
        {
            var z = PeriodicTable.AtomicNumberOf(m.Groups[1].Value);
            if (z is null or < 1) return "";
            return PeriodicTable.FormatConfiguration(PeriodicTable.Configuration(z.Value), superscript: false, space: false);
        });
    }
}
