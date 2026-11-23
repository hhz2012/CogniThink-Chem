using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Structure;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 18 电子规则金属羰基配合物计算器（calc 兜底）。
/// <para>域：题干含「18」+ 金属羰基通式 M(CO)n。由中心金属价电子数 v 得 n=(18-v)/2；
/// 若问 σ 键数则为 2n（n 个 M-C σ 配位键 + n 个 C-O σ 键，端基羰基）。</para>
/// <para>红线：价电子数取自 PeriodicTable.Configuration（不硬编码化学真值）；
/// 域外 / 非整数 n / 未知金属 → 返回 null（不蒙数）。采纳门必经答案键校验。</para>
/// </summary>
public static class Carbonyl18ElectronSolver
{
    // 匹配 M(CO)n 通式：金属 1~2 字母 + (CO) + n（大小写不敏感）
    private static readonly Regex CarbonylN = new(
        @"([A-Z][a-z]?)\s*\(\s*CO\s*\)\s*n", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static EngineResponse? TrySolve(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!raw.Contains("18")) return null;
        // 限定 18 电子规则语境，避免普通含「18」的计算题误触发
        if (!raw.Contains("电子") && !raw.Contains("e⁻") && !raw.Contains("e-") && !raw.Contains("EAN"))
            return null;

        var m = CarbonylN.Match(raw);
        if (!m.Success) return null;
        var metal = m.Groups[1].Value;
        var z = PeriodicTable.AtomicNumberOf(metal);
        if (z is not { } zz) return null;

        var v = ValenceElectrons(zz);
        var diff = 18 - v;
        if (diff <= 0 || diff % 2 != 0) return null; // CO 为 2e 配体，n 须为正整数
        var n = diff / 2;

        var asksSigma = raw.Contains("σ") || raw.Contains("sigma") || raw.Contains("Sigma") || raw.Contains("键");
        var sigma = 2 * n;

        string final;
        if (asksSigma)
            final = $"{metal}(CO)n 中心金属 {metal} 价层电子数为 {v}，每个 CO 提供 2 电子，由 18 电子规则 n=(18-{v})/2={n}；" +
                    $"端基羰基中每个 CO 含 1 个 σ 键、M 与 CO 间形成 {n} 个 σ 配位键，故 1 mol 含 {sigma} mol σ键。答案：{n} {sigma}";
        else
            final = $"{metal}(CO)n 中心金属 {metal} 价层电子数为 {v}，每个 CO 提供 2 电子，由 18 电子规则 n=(18-{v})/2={n}。答案：{n}";

        return new EngineResponse
        {
            Ok = true,
            FinalResult = final,
            DerivationSteps = new List<string>
            {
                $"18 电子规则：{metal} 价电子 v={v}，n=(18-v)/2={n}" + (asksSigma ? $"；σ键数 = 2n = {sigma}" : "")
            },
            ConstraintNote = "【高中限定】18 电子规则端基羰基配合物，价电子数取自元素周期表",
            Confidence = 1.0
        };
    }

    /// <summary>中性原子价电子数：最外层 ns+(n-1)d（过渡金属）或 ns+np（主族），即族数。</summary>
    private static int ValenceElectrons(int z)
    {
        var occ = PeriodicTable.Configuration(z);
        if (occ.Count == 0) return 0;
        int nmax = occ.Keys.Max(k => k[0] - '0');
        int valence = 0;
        foreach (var kv in occ)
        {
            int n = kv.Key[0] - '0';
            char l = kv.Key[1];
            if (n == nmax && (l == 's' || l == 'p')) valence += kv.Value;
            else if (n == nmax - 1 && l == 'd') valence += kv.Value;
        }
        return valence;
    }
}
