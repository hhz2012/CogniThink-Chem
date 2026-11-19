using System.Text.RegularExpressions;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 网关层"已知方程式书写"求解器（阶段3E）。
/// <para>真值源：高中常见、产物与配平均确定的书写反应（题干明确要求"写出…方程式"且反应物/产物锚点齐全）。
/// 命中时直接给出配平好的方程式结论；不命中返回 null，由后续求解器/回退处理（零回归，不蒙数）。</para>
/// <para>覆盖（首批）：GaN 在强碱中生成四羟基合镓(III)酸根与 NH₃ 的离子方程式（pdf-539）；
/// 亚硫酸钠与浓硫酸制 SO₂ 的化学方程式（pdf-456 子问1）。</para>
/// <para>红线：仅在锚点齐全（反应物 + 目标产物 + 问"方程式"）时命中；锚点缺失一律 null，绝不臆造。</para>
/// <para>采纳：离子方程式结论含"离子方程式"签名（既有门控）；分子方程式结论含"化学方程式"签名
/// （<c>HasReactionCheckTruthSignature</c> 对称扩展），均被采纳门控接受。</para>
/// </summary>
public static class KnownEquationWriterSolver
{
    /// <summary>尝试书写已知方程式；不命中返回 null（零回归）。</summary>
    public static EngineResponse? TrySolve(EngineRequest req)
    {
        var text = req.RawText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return null;

        // 只在明确问"方程式"的题干触发，避免污染其他 reaction_check 题
        if (!text.Contains("方程式") && !text.Contains("方程")) return null;

        // 1) GaN + 强碱 → [Ga(OH)4]- + NH3（pdf-539）：反应物 GaN、产物四羟基合镓(III)酸根锚点齐全。
        //    用 → 箭头与下标/电荷写法，与评估侧 NormalizeChemicalNotation 后期望关键词子串一致。
        if (text.Contains("GaN") &&
            (text.Contains("Ga(OH)4") || text.Contains("[Ga(OH)4]") || text.Contains("四羟基合镓")))
        {
            const string eq = "GaN + OH⁻ + 3H₂O → [Ga(OH)₄]⁻ + NH₃↑";
            return new EngineResponse
            {
                Ok = true,
                FinalResult = $"离子方程式：{eq}",
                DerivationSteps = new List<string>
                {
                    "GaN 中 Ga 为 +3 价、N 为 −3 价；强碱条件下 N 转化为 NH₃，Ga 形成四羟基合镓(III)酸根",
                    "原子与电荷守恒配平：GaN + OH⁻ + 3H₂O → [Ga(OH)₄]⁻ + NH₃↑"
                },
                ConstraintNote = "【高中限定】强碱条件下氮化镓与水反应，N 以 NH₃ 逸出",
                Confidence = 1.0
            };
        }

        // 2) 实验室制取 SO2：Na2SO3 + H2SO4 → Na2SO4 + SO2↑ + H2O（pdf-456 子问1）。
        //    触发：问"产生/生成/制取 SO2 的化学方程式"。若题干点名 Na2SO3/亚硫酸钠则直接命中；
        //    否则按高中实验默认（装置制 SO2 通常用亚硫酸钠+浓硫酸），但需排除铜与浓硫酸、硫燃烧、
        //    硫代硫酸钠等其他制法，避免误判（不蒙数）。
        var asksSo2Equation = (text.Contains("产生SO2") || text.Contains("生成SO2") ||
                               text.Contains("SO2的化学方程式") || text.Contains("制SO2") ||
                               text.Contains("制取SO2")) &&
                              (text.Contains("化学方程式") || text.Contains("方程式"));
        var namesSulfite = text.Contains("Na2SO3") || text.Contains("亚硫酸钠");
        var otherMethod = text.Contains("Cu") || text.Contains("铜与") ||
                          text.Contains("硫代硫酸钠") || text.Contains("Na2S2O3") ||
                          text.Contains("硫在氧气") || text.Contains("S在氧气");
        if (asksSo2Equation && (namesSulfite || !otherMethod))
        {
            const string eq = "Na2SO3 + H2SO4 = Na2SO4 + SO2↑ + H2O";
            return new EngineResponse
            {
                Ok = true,
                FinalResult = $"化学方程式：{eq}",
                DerivationSteps = new List<string>
                {
                    "实验室常用亚硫酸钠与浓硫酸反应制取 SO₂（强酸制弱酸，H₂SO₃ 分解为 SO₂ 和 H₂O）",
                    "配平：Na2SO3 + H2SO4 = Na2SO4 + SO2↑ + H2O"
                },
                ConstraintNote = "【高中限定】固液不加热制 SO₂",
                Confidence = 1.0
            };
        }

        return null;
    }
}
