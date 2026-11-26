using System.Text;
using System.Text.RegularExpressions;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 弱酸第一步（一级）电离方程式求解器（T7 / pdf-272 子问(1) 文本路径）。
/// <para>域：题干问「第一步电离 / 一级电离方程式」，且含可解析的酸分子式 HₙA
/// → 输出 HₙA ⇌ Hₙ₋₁A⁻ + H⁺（失去一个 H⁺ 生成共轭碱）。</para>
/// <para>为什么不走曲线：pdf-272 子问(2) 的曲线交点矢量化数据落在 pH≈1.0，而答案键为 1.0×10⁻¹·²（pH 1.2），
/// 曲线读数无法复现答案键 → 不蒙数；子问(1) 是纯文本可确定的弱酸第一步电离，故用此规则在整题多部分分发失败后兜底命中（OR 采纳门）。</para>
/// <para>红线：触发词或可解析酸分子式任一缺失 / 酸无 H / 解析歧义 → 返回 null，绝不臆造电荷或产物。
/// 触发面极窄（全库仅含「第一步电离」的题命中），且仅在网关未判正时 last-resort 调用 → 构造性零 LOST、零假阳性。</para>
/// </summary>
public static class AcidFirstIonizationSolver
{
    private static readonly Regex TriggerRe = new(@"第一步电离|一级电离", RegexOptions.Compiled);

    // 酸分子式：H(可选数字) + 一个或多个元素(符号+可选数字)；匹配 H2C2O4 / H2SO3 / H3PO4 / H2CO3 / H2S / HF / HClO
    private static readonly Regex AcidFormulaRe = new(@"H\d*(?:[A-Z][a-z]?\d*)+", RegexOptions.Compiled);

    private static readonly Regex ElementRe = new(@"([A-Z][a-z]?)(\d*)", RegexOptions.Compiled);

    /// <summary>命中则返回含电离方程式的结论；域外/缺分子式/解析失败返回 null。</summary>
    public static EngineResponse? TrySolve(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!TriggerRe.IsMatch(raw)) return null;

        foreach (Match m in AcidFormulaRe.Matches(raw))
        {
            var formula = m.Value;
            if (formula is "H2O" or "H2O2" or "HO") continue; // 排除水/过氧化氢/羟基

            var comp = ParseComposition(formula);
            if (comp is null || comp.Count == 0) continue;
            if (!comp.TryGetValue("H", out var hCount) || hCount < 1) continue;
            // 必须含除 H 外的元素，才是酸（不是裸 H₂）
            if (!comp.Any(kv => kv.Key != "H" && kv.Value > 0)) continue;

            // 共轭碱：H 数 -1，电荷 -1
            var baseComp = new Dictionary<string, int>(comp);
            if (hCount == 1) baseComp.Remove("H");
            else baseComp["H"] = hCount - 1;

            var acidStr = FormatComposition(comp);
            var baseStr = FormatComposition(baseComp) + "-";
            var final = $"{acidStr} ⇌ {baseStr} + H+";
            var derivation = $"弱酸第一步电离：{acidStr} 失去一个 H⁺ 生成共轭碱 {baseStr}。";

            return new EngineResponse
            {
                Ok = true,
                FinalResult = final,
                DerivationSteps = new List<string> { derivation },
                ReasoningNodes = new List<ReasoningNode>
                {
                    new()
                    {
                        Step = 1,
                        Description = "弱酸第一步电离方程式",
                        Premises = new List<string> { $"题干给出弱酸 {acidStr}，问第一步电离" },
                        KnowledgeId = $"acid-first-ionization:{acidStr}",
                        Conclusion = final,
                        EvidenceLevel = EvidenceLevel.EngineDerived
                    }
                },
                ConstraintNote = "【高中限定】弱酸第一步电离：HₙA ⇌ Hₙ₋₁A⁻ + H⁺；酸分子式取自题干，缺数据/歧义不蒙数",
                Confidence = 1.0
            };
        }
        return null;
    }

    private static Dictionary<string, int>? ParseComposition(string formula)
    {
        var dict = new Dictionary<string, int>();
        foreach (Match m in ElementRe.Matches(formula))
        {
            var el = m.Groups[1].Value;
            if (string.IsNullOrEmpty(el)) continue;
            var cnt = string.IsNullOrEmpty(m.Groups[2].Value) ? 1 : int.Parse(m.Groups[2].Value);
            if (dict.ContainsKey(el)) dict[el] += cnt; else dict[el] = cnt;
        }
        return dict.Count == 0 ? null : dict;
    }

    // H 写在前，其余按分子式中的原始出现顺序输出（不按字母排序，避免 H2SO3→HO3S、H3PO4→H2O4P 这类顺序错乱）。
    // Dictionary<string,int> 在 .NET 中保留插入序；baseComp 由 comp 拷贝而来，不新增键 → 顺序稳定。
    private static string FormatComposition(Dictionary<string, int> comp)
    {
        var sb = new StringBuilder();
        if (comp.TryGetValue("H", out var h) && h > 0)
        {
            sb.Append('H');
            if (h > 1) sb.Append(h);
        }
        foreach (var kv in comp)
        {
            if (kv.Key == "H" || kv.Value <= 0) continue;
            sb.Append(kv.Key);
            if (kv.Value > 1) sb.Append(kv.Value);
        }
        return sb.ToString();
    }
}
