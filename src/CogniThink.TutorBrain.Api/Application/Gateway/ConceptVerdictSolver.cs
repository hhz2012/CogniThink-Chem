using System.Text.RegularExpressions;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 网关层"概念正误判断"求解器（阶段3E）。
/// <para>真值源：高中化学概念事实表 → 对明确断言某概念属性的判断题给出 √/× 判决。</para>
/// <para>覆盖（首批）：断言"SiO₂ 为碱性氧化物"——SiO₂ 实为酸性氧化物（与 HF 反应是特性，
/// 且能与强碱反应生成盐和水），故该断言错误 → ×（pdf-467）。</para>
/// <para>红线：仅在锚点齐全（明确断言 + 判断答题空）时命中；断言为否定形式（如"不是碱性氧化物"）
/// 或锚点缺失一律返回 null（零回归，不蒙数）。输出纯 √/× 串，经 <c>HasCorrectnessVerdictSignature</c> 采纳。</para>
/// </summary>
public static class ConceptVerdictSolver
{
    // 判断答题空 ( ) / （ ）（任意位置），确认为正误判断题而非陈述句填空。
    private static readonly Regex JudgmentBlank = new(@"[（(]\s*[）)]", RegexOptions.Compiled);

    /// <summary>尝试求解；不命中返回 null（零回归）。</summary>
    public static EngineResponse? TrySolve(EngineRequest req)
    {
        var text = !string.IsNullOrWhiteSpace(req.RawText) ? req.RawText : (req.StudentAnswer ?? string.Empty);
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!JudgmentBlank.IsMatch(text)) return null;

        // 断言"SiO2 为/是 碱性氧化物"（肯定式）：SiO2 与断言间不跨句；否定式（不是/并非）不命中。
        // .NET 支持定长负向后行 (?<![非不])：排除"不是""非为"等否定断言。
        if (Regex.IsMatch(text, @"(?:SiO2|二氧化硅)[^。；\n]{0,20}(?<![非不])(?:是|为)\s*碱性氧化物"))
        {
            return new EngineResponse
            {
                Ok = true,
                FinalResult = "×",
                DerivationSteps = new List<string>
                {
                    "SiO2 能与强碱反应生成盐和水，属于酸性氧化物；与 HF 反应是其特性，不能据此判定为碱性氧化物",
                    "故\"SiO2 为碱性氧化物\"的说法错误"
                },
                ConstraintNote = "【高中限定】SiO2 为酸性氧化物（仅与 HF 反应是特性）",
                Confidence = 1.0
            };
        }

        return null;
    }
}
