using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Balancing;
using CogniThink.Chem.Core.Math;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 网关层"少量/过量产物书写"求解器（阶段3C / AC-13，P1）。
/// <para>真值源：高中反应优先级与量比规则表（少量/过量产物不同）→ 给出产物骨架 →
/// <see cref="EquationBalancer"/> 守恒配平。本类不推导未知产物，只在规则表命中时给出确定骨架。</para>
/// <para>覆盖：FeBr2+Cl2、NaAlO2+CO2、Ca(HCO3)2+NaOH、NaCN+CO2 的少量/过量离子方程式。</para>
/// <para>红线（不蒙数）：规则表外 / 量关键词缺失 / 配平失败（无解/欠定）→ 一律返回 null，绝不臆造产物。</para>
/// </summary>
public static class ProductWriterSolver
{
    /// <summary>规则命中后的配平骨架：Left/Right 为物种（电荷一律用 ^ 标注，如 Fe^2+、HCO3^-）；
    /// ExpandFeBr2 表示骨架中用中性 FeBr2 占位以固定 Fe:Br=1:2（消除欠定），输出时展开为离子。</summary>
    private sealed record Skeleton(List<string> Left, List<string> Right, bool ExpandFeBr2);

    /// <summary>尝试书写离子方程式；不命中返回 null（零回归）。</summary>
    public static EngineResponse? TrySolve(EngineRequest req)
    {
        var text = req.RawText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return null;
        // 只在明确问"方程式"的题干触发，避免污染其他 reaction_check 题
        if (!text.Contains("方程式") && !text.Contains("方程")) return null;

        var rule = MatchProductRule(text);
        if (rule is null) return null;

        var skeleton = rule;
        var skeletonStr = string.Join(" + ", skeleton.Left) + " = " + string.Join(" + ", skeleton.Right);

        BalanceResult result;
        try
        {
            result = EquationBalancer.Balance(skeletonStr);
        }
        catch
        {
            return null;
        }
        if (!result.Success || string.IsNullOrWhiteSpace(result.BalancedEquation)) return null;

        // 用返回的系数（与 Left+Right 顺序对齐）重建方程式，避免依赖 BalancedEquation 字符串格式。
        var coefs = result.Coefficients;
        if (coefs.Length != skeleton.Left.Count + skeleton.Right.Count) return null;

        var leftParts = new List<string>();
        var idx = 0;
        foreach (var sp in skeleton.Left)
        {
            if (skeleton.ExpandFeBr2 && sp == "FeBr2")
            {
                var n = coefs[idx].Num; // ToSmallestIntegers 后 Den==1
                leftParts.Add(FormatTerm(n, "Fe^2+"));
                leftParts.Add(FormatTerm(n * 2, "Br^-"));
            }
            else
            {
                leftParts.Add(FormatTerm(coefs[idx].Num, sp));
            }
            idx++;
        }

        var rightParts = new List<string>();
        foreach (var sp in skeleton.Right)
        {
            rightParts.Add(FormatTerm(coefs[idx].Num, sp));
            idx++;
        }

        // 输出规范化：去掉 ^ 电荷标注（Fe^2+ → Fe2+），分隔符用 →，
        // 以匹配评估侧 NormalizeChemicalNotation（不去 ^）与 PDF 整句答案关键词。
        var equation = (string.Join(" + ", leftParts) + " → " + string.Join(" + ", rightParts))
            .Replace("^", "");

        return new EngineResponse
        {
            Ok = true,
            FinalResult = $"离子方程式：{equation}",
            DerivationSteps = new List<string>
            {
                $"识别反应物与量条件，确定产物骨架：{skeletonStr.Replace("^", "")}",
                "守恒配平（原子/电荷守恒 → 最小正整数系数）",
                $"配平结果：{equation}"
            },
            Confidence = 1.0
        };
    }

    private static string FormatTerm(long coef, string species) =>
        coef == 1 ? species : $"{coef}{species}";

    private static Skeleton? MatchProductRule(string text)
    {
        // FeBr2 + Cl2：还原性 Fe2+ > Br-，少量 Cl2 只氧化 Fe2+，过量则全氧化。
        // 过量时原子+电荷守恒欠定（Fe2+ 与 Br- 可任意比例被氧化），故骨架用中性 FeBr2 占位
        // 固定 Fe:Br=1:2，配平后再展开为 Fe^2+ + 2Br^-。
        if (Regex.IsMatch(text, @"FeBr2") && text.Contains("Cl2"))
        {
            if (text.Contains("少量"))
                return new Skeleton(new List<string> { "Fe^2+", "Cl2" }, new List<string> { "Fe^3+", "Cl^-" }, false);
            if (text.Contains("过量"))
                return new Skeleton(new List<string> { "FeBr2", "Cl2" }, new List<string> { "Fe^3+", "Br2", "Cl^-" }, true);
        }

        // NaAlO2 + CO2：少量 → CO3^2-，过量 → HCO3^-（AlO2^- 碱性强于 HCO3^-）
        if ((text.Contains("NaAlO2") || text.Contains("AlO2")) && text.Contains("CO2"))
        {
            if (text.Contains("少量"))
                return new Skeleton(new List<string> { "AlO2^-", "CO2", "H2O" }, new List<string> { "Al(OH)3", "CO3^2-" }, false);
            if (text.Contains("过量"))
                return new Skeleton(new List<string> { "AlO2^-", "CO2", "H2O" }, new List<string> { "Al(OH)3", "HCO3^-" }, false);
        }

        // Ca(HCO3)2 + 少量 NaOH：1 mol OH^- 中和 1 mol HCO3^-，生成 CaCO3
        if (text.Contains("Ca(HCO3)2") && text.Contains("NaOH") && text.Contains("少量"))
            return new Skeleton(new List<string> { "Ca^2+", "HCO3^-", "OH^-" }, new List<string> { "CaCO3", "H2O" }, false);

        // NaCN + 少量 CO2：pKa 序 H2CO3 > HCN > HCO3^-，故生成 HCO3^-（而非 CO3^2-）
        if ((text.Contains("NaCN") || Regex.IsMatch(text, @"CN-")) && text.Contains("CO2") && text.Contains("少量"))
            return new Skeleton(new List<string> { "CN^-", "CO2", "H2O" }, new List<string> { "HCN", "HCO3^-" }, false);

        return null;
    }
}
