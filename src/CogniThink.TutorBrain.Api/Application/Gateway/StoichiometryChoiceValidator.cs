using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Balancing;
using CogniThink.Chem.Core.Chemistry;
using CogniThink.Chem.Core.Inference;
using CogniThink.Chem.Core.Math;
using CogniThink.Chem.Core.Parsing;
using CogniThink.Chem.Core.Stoichiometry;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 化学计量选择题逐选项校验——最保守。
/// <para>覆盖：题干给出（可配平的）反应方程式 + 一个已知投料量；选项断言某物质的
/// 物质的量 / 质量 / 标况体积 / 转移电子数。</para>
/// <para>真值源：<see cref="EquationStoichiometry"/>（精确有理系数比）+ <see cref="MoleCalculator"/>
/// （n=m/M、V=n·Vm）+ <see cref="RedoxBalancer"/>（电子转移数）。引擎计算是唯一真值源。</para>
/// <para>红线（不蒙数、零假阳性）：仅当方程式可配平、已知量可干净换算、<b>四个选项全部</b>能干净
/// 解析为可计算断言且满足方向的候选唯一 → 返回答案字母；任一环节失败 → 整题 null，不臆断。</para>
/// </summary>
public static class StoichiometryChoiceValidator
{
    private const string ConstraintNote =
        "【高中限定】化学计量基于方程式精确配平与系数比（Rational 精确运算）；" +
        "方程式必须可配平、已知量与选项断言必须可干净解析；多反应耦合/极值/缺数据的不臆断。";

    private enum ClaimType { Moles, Mass, VolumeStp, Electrons }

    private sealed record ParsedClaim(ClaimType Type, string? TargetFormula, double Claimed);

    public static ChoiceVerifier.Outcome? TryDecide(EngineRequest req)
    {
        var raw = req.RawText ?? "";
        var (stem, opts) = ChoiceVerifier.ParseOptions(raw);
        if (opts.Count < 2) return null;

        var askDir = AskDir(stem);
        if (askDir == "?") return null;

        // 1) 从题干抽取并配平方程式
        var equation = ExtractEquation(stem);
        if (equation is null) return null;
        var balance = EquationBalancer.Balance(equation);
        if (!balance.Success || string.IsNullOrEmpty(balance.BalancedEquation)) return null;
        var balanced = balance.BalancedEquation!;

        // 2) 方程式物种主体列表（用于在选项中定位目标物种）
        if (!EquationParser.TryParse(balanced, out var parsedEq, out _) || parsedEq is null) return null;
        var speciesBodies = parsedEq.AllTerms.Select(t => t.Species.Body).ToList();

        // 3) 从题干抽取已知量（物种 + 物质的量）
        var known = ExtractKnown(stem, speciesBodies);
        if (known is null) return null;
        var (knownSpecies, knownMoles) = known.Value;

        var reasons = new List<string>();
        var verdicts = new List<(string letter, bool ok)>();
        foreach (var kv in opts.OrderBy(x => x.Key))
        {
            var claim = ParseOptionClaim(kv.Value, speciesBodies, knownSpecies);
            if (claim is null) return null; // 任一选项不可判 → 整题 null

            var trueValue = ComputeTrueValue(claim, balanced, knownSpecies, knownMoles);
            if (trueValue is null) return null;

            var ok = Math.Abs(claim.Claimed - trueValue.Value) < 1e-4;
            verdicts.Add((kv.Key, ok));
            reasons.Add($"{kv.Key}：{kv.Value}（计算得 {FormatDouble(trueValue.Value)} → {(ok ? "真" : "假")}）");
        }

        return ChooseUnique(verdicts, askDir, reasons);
    }

    // ===================== 题干解析 =====================

    private static readonly Regex EquationRe = new(
        @"[A-Za-z0-9()^\s+·]+(?:=|→)[A-Za-z0-9()^\s+↑↓·]+", RegexOptions.Compiled);

    private static string? ExtractEquation(string stem)
    {
        foreach (Match m in EquationRe.Matches(stem))
        {
            var cand = m.Value.Trim();
            if (EquationParser.TryParse(cand, out _, out _))
                return cand;
        }
        return null;
    }

    private static readonly Regex KnownRe = new(
        @"(\d+(?:\.\d+)?)\s*(mol|g|L|mL)\s*([A-Za-z0-9()^]+)", RegexOptions.Compiled);

    private static (string species, Rational moles)? ExtractKnown(string stem, List<string> speciesBodies)
    {
        var hasStp = stem.Contains("标况") || stem.Contains("标准状况");
        foreach (Match m in KnownRe.Matches(stem))
        {
            var num = double.Parse(m.Groups[1].Value);
            var unit = m.Groups[2].Value;
            var token = m.Groups[3].Value;
            // 该 token 必须是方程式中的某个物种主体
            var body = speciesBodies.FirstOrDefault(b => b.Equals(token, StringComparison.Ordinal));
            if (body is null) continue;

            Rational moles;
            switch (unit)
            {
                case "mol":
                    moles = Rational.Parse(m.Groups[1].Value);
                    break;
                case "g":
                    if (!MoleCalculator.TryMolarMass(body, out var M, out _)) return null;
                    moles = MoleCalculator.Moles(Rational.Parse(m.Groups[1].Value), body);
                    break;
                case "L":
                    if (!hasStp) return null;
                    moles = MoleCalculator.MolesFromGasVolume(Rational.Parse(m.Groups[1].Value));
                    break;
                case "mL":
                    if (!hasStp) return null;
                    moles = MoleCalculator.MolesFromGasVolume(Rational.Parse(m.Groups[1].Value) / new Rational(1000));
                    break;
                default:
                    return null;
            }
            if (moles.IsZero || moles.IsNegative) return null;
            return (body, moles);
        }
        return null;
    }

    // ===================== 选项解析 =====================

    private static readonly Regex NumUnitRe = new(
        @"(\d+(?:\.\d+)?)\s*(mol|g|L|mL)", RegexOptions.Compiled);

    private static ParsedClaim? ParseOptionClaim(string text, List<string> speciesBodies, string knownSpecies)
    {
        var m = NumUnitRe.Match(text);
        if (!m.Success) return null;
        var claimed = double.Parse(m.Groups[1].Value);
        var unit = m.Groups[2].Value;
        if (unit == "mL") claimed /= 1000.0;

        ClaimType type;
        string? target = null;
        if (unit == "L" || unit == "mL")
        {
            if (!(text.Contains("标况") || text.Contains("标准状况"))) return null;
            type = ClaimType.VolumeStp;
        }
        else if (unit == "g")
        {
            type = ClaimType.Mass;
        }
        else // mol
        {
            type = text.Contains("电子") ? ClaimType.Electrons : ClaimType.Moles;
        }

        if (type != ClaimType.Electrons)
        {
            target = FindTargetSpecies(text, speciesBodies, knownSpecies);
            if (target is null) return null;
        }
        return new ParsedClaim(type, target, claimed);
    }

    /// <summary>在选项中定位目标物种（方程式中、非已知物种、以完整 token 出现）；歧义返回 null。</summary>
    private static string? FindTargetSpecies(string text, List<string> speciesBodies, string knownSpecies)
    {
        string? hit = null;
        foreach (var body in speciesBodies.Where(b => b != knownSpecies).OrderByDescending(b => b.Length))
        {
            if (!IsTokenPresent(text, body)) continue;
            if (hit is not null) return null; // 多个物种出现 → 歧义，不判
            hit = body;
        }
        return hit;
    }

    private static bool IsTokenPresent(string text, string body)
    {
        var pattern = @"(?<![A-Za-z0-9])" + Regex.Escape(body) + @"(?![A-Za-z0-9])";
        return Regex.IsMatch(text, pattern);
    }

    // ===================== 真值计算 =====================

    private static double? ComputeTrueValue(ParsedClaim claim, string balanced, string knownSpecies, Rational knownMoles)
    {
        switch (claim.Type)
        {
            case ClaimType.Electrons:
                return ComputeElectrons(balanced, knownSpecies, knownMoles);
            case ClaimType.Moles:
            {
                var r = EquationStoichiometry.Solve(new StoichiometryRequest
                {
                    Equation = balanced, KnownSpecies = knownSpecies, KnownMoles = knownMoles, TargetSpecies = claim.TargetFormula!
                });
                if (r.Status != InferenceStatus.Solved) return null;
                return ToDouble(r.TargetMoles);
            }
            case ClaimType.Mass:
            {
                var r = EquationStoichiometry.Solve(new StoichiometryRequest
                {
                    Equation = balanced, KnownSpecies = knownSpecies, KnownMoles = knownMoles, TargetSpecies = claim.TargetFormula!
                });
                if (r.Status != InferenceStatus.Solved) return null;
                if (!MoleCalculator.TryMolarMass(claim.TargetFormula!, out var M, out _)) return null;
                return ToDouble(r.TargetMoles * M);
            }
            case ClaimType.VolumeStp:
            {
                var r = EquationStoichiometry.Solve(new StoichiometryRequest
                {
                    Equation = balanced, KnownSpecies = knownSpecies, KnownMoles = knownMoles, TargetSpecies = claim.TargetFormula!
                });
                if (r.Status != InferenceStatus.Solved) return null;
                return ToDouble(r.TargetMoles * MoleCalculator.StandardMolarVolume);
            }
            default:
                return null;
        }
    }

    private static double? ComputeElectrons(string balanced, string knownSpecies, Rational knownMoles)
    {
        var rx = RedoxBalancer.SplitHalfReactions(balanced);
        if (!rx.Success || !rx.IsRedox || rx.ElectronConservation == false) return null;
        if (rx.ElectronsTransferred.IsZero) return null;

        // 已知物种在配平方程式中的系数
        if (!EquationParser.TryParse(balanced, out var parsed, out _) || parsed is null) return null;
        var terms = parsed.AllTerms.ToList();
        var idx = terms.FindIndex(t => t.Species.Body == knownSpecies);
        if (idx < 0) return null;
        // 需重新配平拿系数（EquationParser 不带系数；用 EquationBalancer）
        var b = EquationBalancer.Balance(balanced);
        if (!b.Success || idx >= b.Coefficients.Length) return null;
        var coeffKnown = b.Coefficients[idx];
        if (coeffKnown.IsZero) return null;

        // 每 mol 已知物种转移电子数 = 总转移电子数 / 已知物种系数
        var perMol = rx.ElectronsTransferred / coeffKnown;
        return ToDouble(knownMoles * perMol);
    }

    private static double ToDouble(Rational r) => (double)r.Num / r.Den;

    private static string FormatDouble(double v)
    {
        if (Math.Abs(v - Math.Round(v)) < 1e-9) return ((long)Math.Round(v)).ToString();
        return v.ToString("0.###");
    }

    // ===================== 公共工具 =====================

    private static string AskDir(string stem)
    {
        if (Regex.IsMatch(stem, @"错误|不正确|不相符|有误")) return "wrong";
        if (Regex.IsMatch(stem, @"正确|符合|准确")) return "correct";
        return "?";
    }

    private static ChoiceVerifier.Outcome? ChooseUnique(List<(string letter, bool ok)> verdicts, string askDir, List<string> reasons)
    {
        var wantTrue = askDir == "correct";
        var candidates = verdicts.Where(v => v.ok == wantTrue).Select(v => v.letter).ToList();
        if (candidates.Count != 1) return null;
        var letter = candidates[0];
        var dirWord = wantTrue ? "正确选项" : "错误选项";
        return new ChoiceVerifier.Outcome(letter, $"{dirWord}为 {letter}", reasons, ConstraintNote);
    }
}
