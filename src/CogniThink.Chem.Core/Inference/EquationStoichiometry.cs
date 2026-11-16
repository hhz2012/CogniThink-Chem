using CogniThink.Chem.Core.Balancing;
using CogniThink.Chem.Core.Chemistry;
using CogniThink.Chem.Core.Math;
using CogniThink.Chem.Core.Parsing;

namespace CogniThink.Chem.Core.Inference;

/// <summary>
/// 化学计量引擎：基于配平方程式的物质的量比例计算。
/// 流程：解析 → 配平（零空间法，绝不编造系数）→ 按化学式主体定位已知/目标物种的系数 →
/// n(目标) = n(已知) × coeff(目标) / coeff(已知)。
/// 纯公式换算（n=m/M、c=n/V、V=n·Vm、稀释、产率等）由 <see cref="Stoichiometry.MoleCalculator"/> 承担，
/// 本引擎只处理"方程式系数比"这一环。
/// </summary>
public static class EquationStoichiometry
{
    public static StoichiometryResult Solve(StoichiometryRequest request)
    {
        var result = new StoichiometryResult();

        if (!EquationParser.TryParse(request.Equation, out var parsed, out var error))
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = $"方程式无法解析：{error}";
            return result;
        }

        var balance = EquationBalancer.Balance(parsed!);
        if (!balance.Success)
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = $"方程式无法配平：{balance.FailReason}";
            return result;
        }

        result.BalancedEquation = balance.BalancedEquation!;
        var terms = parsed!.AllTerms.ToList();

        var knownIndex = FindSpeciesIndex(terms, request.KnownSpecies);
        var targetIndex = FindSpeciesIndex(terms, request.TargetSpecies);
        if (knownIndex < 0 || targetIndex < 0)
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = knownIndex < 0
                ? $"已知物种 {request.KnownSpecies} 不在方程式中。"
                : $"目标物种 {request.TargetSpecies} 不在方程式中。";
            return result;
        }

        var coeffKnown = balance.Coefficients[knownIndex];
        var coeffTarget = balance.Coefficients[targetIndex];
        if (coeffKnown.IsZero)
        {
            result.Status = InferenceStatus.NoSolution;
            result.Message = $"已知物种 {request.KnownSpecies} 的系数为 0，无法作为计量基准。";
            return result;
        }

        result.TargetMoles = request.KnownMoles * coeffTarget / coeffKnown;
        result.Status = InferenceStatus.Solved;
        result.Steps.Add(new InferenceStep
        {
            Description = $"配平方程式：{result.BalancedEquation}",
            Source = "EquationStoichiometry"
        });
        result.Steps.Add(new InferenceStep
        {
            Description = $"系数比：n({request.TargetSpecies}) : n({request.KnownSpecies}) = {coeffTarget} : {coeffKnown}",
            Source = "EquationStoichiometry"
        });
        result.Steps.Add(new InferenceStep
        {
            Description = $"n({request.TargetSpecies}) = {request.KnownMoles} × {coeffTarget} / {coeffKnown} = {result.TargetMoles} mol",
            Source = "EquationStoichiometry"
        });
        result.Message = $"由系数比得 n({request.TargetSpecies}) = {result.TargetMoles} mol。";
        return result;
    }

    /// <summary>按化学式主体（忽略电荷与状态符号）定位物种在方程式中的首个位置。</summary>
    private static int FindSpeciesIndex(List<ParsedTerm> terms, string formula)
    {
        string target;
        try
        {
            var species = Species.Parse(formula);
            target = species.Body;
        }
        catch (FormatException)
        {
            return -1;
        }

        for (var i = 0; i < terms.Count; i++)
        {
            if (terms[i].Species.Body == target)
            {
                return i;
            }
        }

        return -1;
    }
}
