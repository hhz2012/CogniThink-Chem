using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Balancing;
using CogniThink.Chem.Core.Chemistry;
using CogniThink.Chem.Core.Math;
using CogniThink.Chem.Core.Parsing;
using CogniThink.Chem.Core.Stoichiometry;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 氧化还原电子转移计数陈述判定原语（spec FR-4 / Task 4）：判定
/// "每生成 1 mol X 转移 N mol 电子" / "每消耗 1 mol X 转移 N mol 电子" /
/// "每生成 1 mol X 转移电子数为 N NA" 类陈述的真伪。
/// <para>真值源：<see cref="RedoxBalancer.SplitHalfReactions"/>（配平 + 氧化数变化 + 电子守恒）
/// 给出配平方程式的总转移电子数，再除以目标物种的计量系数得到"每 1 mol 目标物种转移电子数"，
/// 与陈述声称值比较。引擎计算是唯一真值源。</para>
/// <para>红线（不蒙数、零假阳性）：仅当<b>方程式可配平、是氧化还原反应、电子守恒成立、
/// 目标物种在方程式中唯一出现在方向对应的一侧且含变价元素、声称值与计算值在容差内一致</b>
/// 时返回 True/False；任一环节不满足 → <see cref="Verdict.Undecided"/>。不蒙数。</para>
/// <para>本类为新增校验器（MultiSelectChoiceValidator / CountingChoiceValidator 扩展）的依赖，
/// 接入 <see cref="CompositeStatementJudge"/>；不替换既有单选瀑布链的判定路径，既有行为字节级不变。</para>
/// <para>范围说明（刻意保守）：本版本只判定电子转移数陈述；"X 是氧化剂/还原剂""氧化产物/还原产物"
/// "被氧化与被还原物质的量之比"等角色/比例陈述暂不判定（返回 Undecided），留待后续任务在零假阳性
/// 预算内扩展。</para>
/// </summary>
internal sealed class RedoxElectronStatementJudge : IStatementJudge
{
    public Verdict Judge(string stem, string statement)
    {
        if (string.IsNullOrEmpty(stem) || string.IsNullOrEmpty(statement))
            return Verdict.Undecided;

        var claim = ParseElectronClaim(statement);
        if (claim is null) return Verdict.Undecided;

        var equation = ResolveEquation(stem, statement);
        if (equation is null) return Verdict.Undecided;

        var truePerMol = ComputeElectronsPerMol(equation, claim.Species, claim.Generated);
        if (truePerMol is null) return Verdict.Undecided;

        return ApproxEqual(claim.PerMolElectrons, truePerMol.Value)
            ? Verdict.True
            : Verdict.False;
    }

    // ===================== 声称值解析 =====================

    private sealed record Claim(string Species, bool Generated, double PerMolElectrons);

    // 方向 + 数量 + 物种：每生成/消耗/脱去 <num> <mol|g|L> <species>
    private static readonly Regex FrameRe = new(
        @"(每)?(生成|消耗|脱去)\s*([0-9]+(?:\.[0-9]+)?)\s*(mol|g|L)\s*([A-Za-z0-9()^+\-]+)",
        RegexOptions.Compiled);

    // "N mol 电子" 中的 N（"mol 电子"/"mol e" 紧邻，避免误抓 "1 mol O2"）
    private static readonly Regex MolElectronRe = new(
        @"([0-9]+(?:\.[0-9]+)?)\s*mol\s*(?:电子|e)", RegexOptions.Compiled);

    // "N NA" / "N N_A" 中的 N
    private static readonly Regex NaRe = new(
        @"([0-9]+(?:\.[0-9]+)?)\s*N_?A", RegexOptions.Compiled);

    //  bare "NA"（无数值）→ 1
    private static readonly Regex BareNaRe = new(
        @"电子(?:的)?(?:数|数目)?(?:为|是)\s*N_?A", RegexOptions.Compiled);

    private static Claim? ParseElectronClaim(string statement)
    {
        var s = RedoxStatementPrimitives.NormalizeText(statement);

        // 必须是"转移电子"类陈述
        if (!s.Contains("转移")) return null;
        if (!s.Contains("电子") && !Regex.IsMatch(s, @"N_?A")) return null;

        // 1) 提取声称转移电子数 N
        double n;
        var mMol = MolElectronRe.Match(s);
        var mNa = NaRe.Match(s);
        if (mMol.Success)
            n = double.Parse(mMol.Groups[1].Value, CultureInfo.InvariantCulture);
        else if (mNa.Success)
            n = double.Parse(mNa.Groups[1].Value, CultureInfo.InvariantCulture);
        else if (BareNaRe.IsMatch(s))
            n = 1.0;
        else
            return null;

        // 2) 提取方向 + 数量 + 目标物种
        var mFrame = FrameRe.Match(s);
        if (!mFrame.Success) return null;

        var dir = mFrame.Groups[2].Value;
        var amount = double.Parse(mFrame.Groups[3].Value, CultureInfo.InvariantCulture);
        var unit = mFrame.Groups[4].Value;
        var token = mFrame.Groups[5].Value;

        // 解析物种主体（去电荷），与方程式物种 Body 对齐；解析失败 → 不判
        Species species;
        try { species = Species.Parse(token); }
        catch (FormulaException) { return null; }
        if (string.IsNullOrEmpty(species.Body)) return null;

        var generated = dir == "生成"; // 消耗 / 脱去 → 反应物侧

        // 3) 把声称数量换算为目标物种的物质的量（用于归一化到"每 1 mol"）
        double moles;
        if (unit == "mol")
        {
            moles = amount;
        }
        else if (unit == "g")
        {
            if (!MoleCalculator.TryMolarMass(species.Body, out var M, out _)) return null;
            if (M.IsZero) return null;
            moles = amount / ToDouble(M);
        }
        else // L
        {
            if (!(s.Contains("标况") || s.Contains("标准状况"))) return null;
            var vm = MoleCalculator.StandardMolarVolume;
            if (vm.IsZero) return null;
            moles = amount / ToDouble(vm);
        }
        if (moles <= 0) return null;

        return new Claim(species.Body, generated, n / moles);
    }

    // ===================== 方程式定位（共享原语，见 RedoxStatementPrimitives） =====================

    private static string? ResolveEquation(string stem, string statement)
    {
        var s = RedoxStatementPrimitives.NormalizeText(stem);

        var labelValue = RedoxStatementPrimitives.FindLabelRef(statement);
        if (labelValue is not null)
        {
            var labeled = RedoxStatementPrimitives.ResolveLabeled(s, labelValue.Value);
            if (labeled is not null) return labeled;
            // 标签未命中时退化为"唯一方程式"，避免漏判
        }
        return RedoxStatementPrimitives.ResolveUnique(s);
    }

    // ===================== 真值计算 =====================

    /// <summary>
    /// 计算配平方程式中每 1 mol <paramref name="species"/> 转移的电子数（double）。
    /// 不可判（方程未配平 / 非氧化还原 / 电子不守恒 / 目标物种歧义或不含变价元素）返回 null。
    /// </summary>
    private static double? ComputeElectronsPerMol(string equation, string species, bool generated)
    {
        var rx = RedoxBalancer.SplitHalfReactions(equation);
        if (!rx.Success || !rx.IsRedox || !rx.ElectronConservation || rx.ElectronsTransferred.IsZero)
            return null;

        if (!EquationParser.TryParse(equation, out var parsed, out _) || parsed is null)
            return null;
        var terms = parsed.AllTerms.ToList();
        var leftCount = parsed.Left.Count;

        // 目标物种在方向对应侧的出现位置；另一侧不得出现（歧化等歧义 → 不判）
        var sideIndices = new List<int>();
        for (var i = 0; i < terms.Count; i++)
        {
            if (!string.Equals(terms[i].Species.Body, species, StringComparison.Ordinal))
                continue;
            var onLeft = i < leftCount;
            if (generated == onLeft) return null; // 出现在错误一侧
            sideIndices.Add(i);
        }
        if (sideIndices.Count != 1) return null; // 未出现 / 同侧多次出现 → 歧义

        // 目标物种必须含变价元素（否则电子数比值无化学意义，虽算术上可能成立，仍保守不判）
        var changing = new HashSet<string>(rx.Changes.Select(c => c.Element), StringComparer.Ordinal);
        if (changing.Count == 0) return null;
        Species target;
        try { target = Species.Parse(species); }
        catch (FormulaException) { return null; }
        if (!target.Elements.Keys.Any(e => changing.Contains(e))) return null;

        // 取配平后的计量系数（与 terms 顺序对齐，同 StoichiometryChoiceValidator 口径）
        var b = EquationBalancer.Balance(equation);
        if (!b.Success) return null;
        var idx = sideIndices[0];
        if (idx >= b.Coefficients.Length) return null;
        var coeff = b.Coefficients[idx];
        if (coeff.IsZero) return null;

        return ToDouble(rx.ElectronsTransferred) / ToDouble(coeff);
    }

    // ===================== 工具 =====================

    private static double ToDouble(Rational r) => (double)r.Num / r.Den;

    private static bool ApproxEqual(double a, double b)
    {
        var tol = 1e-6 * Math.Max(1.0, Math.Max(Math.Abs(a), Math.Abs(b)));
        return Math.Abs(a - b) <= tol;
    }
}
