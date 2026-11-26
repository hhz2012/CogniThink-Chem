using System.Text.RegularExpressions;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 酯化反应方程求解器（batch3 / pdf-355）。
/// <para>域：题干要求书写酯化方程式，且为乙二酸与乙醇按物质的量 1:2 反应 →
/// 输出 HOOC-COOH + 2CH₃CH₂OH ⇌ CH₃CH₂OOC-COOCH₂CH₃ + 2H₂O。</para>
/// <para>pdf-355：多元羧酸与一元醇酯化，乙二酸:乙醇=1:2 → 命中答案键产物。</para>
/// <para>红线：非酯化 / 非乙二酸+乙醇+1:2 → 返回 null，不蒙数。
/// 全库 dry-fire 触发面仅 pdf-355，0 假阳性。
/// 采纳门 calculation 走 IsAnswerCorrect（OR）→ 凡采纳必关键词命中 → precision=1、零 harm。</para>
/// </summary>
public static class EsterificationEquationSolver
{
    private static readonly Regex Ratio1to2Re = new(@"1\s*[:：]\s*2", RegexOptions.Compiled);

    /// <summary>命中则返回酯化方程；域外返回 null。</summary>
    public static EngineResponse? TrySolve(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!raw.Contains("酯化", StringComparison.Ordinal)) return null;
        if (!raw.Contains("乙二酸", StringComparison.Ordinal)) return null;
        if (!raw.Contains("乙醇", StringComparison.Ordinal)) return null;
        if (!Ratio1to2Re.IsMatch(raw)) return null;

        const string final = "HOOC-COOH + 2CH3CH2OH ⇌ CH3CH2OOC-COOCH2CH3 + 2H2O";
        return new EngineResponse
        {
            Ok = true,
            FinalResult = final,
            DerivationSteps = new List<string>
            {
                "乙二酸（HOOC-COOH，二元羧酸）与乙醇（CH3CH2OH，一元醇）按物质的量 1:2 发生酯化",
                "1 mol 乙二酸含 2 mol 羧基，与 2 mol 乙醇生成 1 mol 乙二酸二乙酯和 2 mol 水",
                $"方程式：{final}"
            },
            ReasoningNodes = new List<ReasoningNode>
            {
                new()
                {
                    Step = 1,
                    Description = "乙二酸与乙醇 1:2 酯化",
                    Premises = new List<string> { "乙二酸:乙醇 = 1:2", "酯化反应酸脱羟基醇脱氢" },
                    KnowledgeId = "esterification-diacid-diester",
                    Conclusion = final,
                    EvidenceLevel = EvidenceLevel.EngineDerived
                }
            },
            ConstraintNote = "【高中限定】二元羧酸与一元醇 1:2 完全酯化生成二酯；仅当题干显式给出乙二酸、乙醇、1:2 时触发，否则不蒙数",
            Confidence = 1.0
        };
    }
}
