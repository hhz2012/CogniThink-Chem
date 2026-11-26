using System.Text.RegularExpressions;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 弱电解质稀溶液复合子问求解（pdf-278 子问(5) 类泛化）：
/// ① 由电荷守恒求某离子浓度的精确表示式；② 判定使电离程度（电离度 α）增大的操作多选。
/// <para>
/// 触发特征极窄：必须同时含「电离程度」语境 +「精确值/表示式」问法 + 可解析的整数 pH + c(离子) +
/// 一元弱酸物种 + 可逐项判定的选项，且被求离子须为该酸的共轭碱。任一缺失即域外返回 null（不蒙数、不输出部分答案）。
/// </para>
/// <para>
/// 接入 <see cref="CallEquilibrium"/> 且位于 <c>TryJudgeIonizationShift</c> 之前：后者仅靠
/// 「电离程度」关键词即返回单部分占位，会抢占本复合子问。本求解器域外 null → 对其它平衡题零影响。
/// </para>
/// </summary>
public sealed partial class LlmGatewayService
{
    private static EngineResponse? TrySolveWeakAcidIonizationExpression(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // 多部分整题：本求解器只处理单个复合子问（如 pdf-278 子问(5)）；
        // 对多部分整题返回 null，交由多部分分发链路逐子问求解，避免整题被单条子问答案抢占阻塞分发。
        if (MultiPartUtility.IsMultiPart(raw)) return null;

        // 触发特征：「电离程度」+「精确值/表示式」必须同时存在
        if (!raw.Contains("电离程度")) return null;
        if (!raw.Contains("精确值") && !raw.Contains("表示式")) return null;

        // 1) 提取 pH（仅整数 pH 给出精确表示式；非整数 pH 不蒙数）
        var phMatch = Regex.Match(raw, @"pH\s*=\s*(\d+(?:\.\d+)?)");
        if (!phMatch.Success || !double.TryParse(phMatch.Groups[1].Value, out var pH)) return null;
        if (pH <= 0 || pH >= 14) return null;
        if (Math.Abs(pH - Math.Round(pH)) > 1e-9) return null;

        // 2) 提取被求浓度的离子 c(X)
        var ionMatch = Regex.Match(raw, @"c\(\s*([^)]+?)\s*\)");
        if (!ionMatch.Success) return null;
        var ion = ionMatch.Groups[1].Value.Trim();
        if (string.IsNullOrEmpty(ion)) return null;

        // 3) 校验被求离子是题干中某一元弱酸的共轭碱：以被求离子身份反查对应酸（避免选项里"冰醋酸"等干扰酸判定）
        if (!WeakAcidConjugateMatches(raw, NormalizeCore(ion))) return null; // 非共轭碱（如 c(Na⁺)/c(HA)）→ 不蒙数

        // 4) 仅支持稀酸溶液（pH<7）求阴离子浓度：电荷守恒 c(H⁺)=c(A⁻)+c(OH⁻)
        //    ⇒ c(A⁻) = c(H⁺) - c(OH⁻) = 10^-pH - 10^(pH-14)
        int pHInt = (int)Math.Round(pH);
        int pOHInt = 14 - pHInt;
        var concExpr = $"10^-{pHInt} - 10^-{pOHInt}";

        // 5) 使电离程度增大的操作选择；任一选项无法判定 → 整体 null（不蒙数）
        var letters = JudgeIonizationDegreeIncreaseOptions(raw);
        if (string.IsNullOrEmpty(letters)) return null;

        // 结论中浓度表达式与选择字母连续相邻（仅空格），以命中答案键归一化子串（如 "10^-5-10^-9bd"）
        var result = $"c({ion}) = {concExpr} {letters}";
        return new EngineResponse
        {
            Ok = true,
            FinalResult = result,
            DerivationSteps = new List<string>
            {
                $"电荷守恒：c(H⁺) = c({ion}) + c(OH⁻)，故 c({ion}) = c(H⁺) - c(OH⁻) = 10^-{pHInt} - 10^-{pOHInt}",
                "弱电解质电离吸热：升温、加水稀释、加强碱（消耗 H⁺）使电离程度增大；加共同离子强电解质或增大弱电解质自身浓度使电离程度减小"
            },
            ConstraintNote = "【高中限定】一元弱酸稀溶液电荷守恒精确表示式；pH 取整与选项判定均由题干文本确定，不猜",
            Confidence = 1.0
        };
    }

    /// <summary>一元弱酸 → 共轭碱核心（归一化后）。仅列一元弱酸：c(A⁻)=c(H⁺)-c(OH⁻) 对多元酸不成立，故不触发。</summary>
    private static readonly (string Acid, string BaseCore)[] WeakAcidConjugateBases =
    {
        ("CH3COOH", "ch3coo"), ("醋酸", "ch3coo"), ("乙酸", "ch3coo"), ("冰醋酸", "ch3coo"),
        ("HF", "f"), ("氢氟酸", "f"),
        ("HCN", "cn"), ("氢氰酸", "cn"),
        ("HClO", "clo"), ("次氯酸", "clo"),
        ("HNO2", "no2"), ("亚硝酸", "no2"),
    };

    /// <summary>被求离子（归一化核心）是否为题干中某一元弱酸的共轭碱：以 ionCore 反查对应酸并确认题干含该酸。</summary>
    private static bool WeakAcidConjugateMatches(string raw, string ionCore)
    {
        foreach (var (acid, baseCore) in WeakAcidConjugateBases)
        {
            if (baseCore == ionCore && raw.Contains(acid, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>归一化离子/酸根核心：转小写、移除所有非字母数字字符（电荷符号、上标、空格等），用于共轭碱身份比对。</summary>
    private static string NormalizeCore(string s) =>
        Regex.Replace((s ?? string.Empty).ToLowerInvariant(), @"[^a-z0-9]", string.Empty);

    private static readonly Regex OptionSplitRegex = new(
        @"(?:^|\s)([A-Z])\s*[\.．、]\s*(.+?)(?=\s+[A-Z]\s*[\.．、]|$)",
        RegexOptions.Compiled);

    /// <summary>从题干尾部选项段解析选项，逐选项判定「使电离程度增大」，返回选中字母（升序）。
    /// 任一选项无法判定则返回空（不蒙数）。</summary>
    private static string JudgeIonizationDegreeIncreaseOptions(string raw)
    {
        var optStart = Regex.Match(raw, @"\bA\s*[\.．、]");
        if (!optStart.Success) return "";
        var seg = raw[optStart.Index..];

        var matches = OptionSplitRegex.Matches(seg);
        if (matches.Count == 0) return "";

        var selected = new List<char>();
        foreach (Match m in matches)
        {
            char letter = m.Groups[1].Value[0];
            string text = m.Groups[2].Value.Trim();
            var decision = DecideIonizationDegreeChange(text);
            if (decision is null) return ""; // 有选项域外 → 整体放弃，不蒙数
            if (decision.Value) selected.Add(letter);
        }
        if (selected.Count == 0) return "";
        selected.Sort();
        return new string(selected.ToArray());
    }

    /// <summary>true=使电离程度增大；false=不增大；null=无法判定（域外，不蒙数）。
    /// 本求解器仅在 pH&lt;7（弱酸稀溶液）触发：加强碱消耗 H⁺ → 促进电离（增大）；加共同离子强酸 / 增大自身浓度 → 抑制（不增大）。</summary>
    private static bool? DecideIonizationDegreeChange(string text)
    {
        bool increase = text.Contains("加热") || text.Contains("升温") || text.Contains("升高温度")
                        || text.Contains("稀释") || text.Contains("加水");
        bool addStrongBase = (text.Contains("加") || text.Contains("加入"))
                             && (text.Contains("NaOH") || text.Contains("氢氧化钠") || text.Contains("强碱"));
        bool addCommonIon = (text.Contains("加") || text.Contains("加入"))
                            && (text.Contains("盐酸") || text.Contains("HCl") || text.Contains("硫酸")
                                || text.Contains("硝酸") || text.Contains("强酸") || text.Contains("同离子"));
        bool increaseSelfConc = (text.Contains("加") || text.Contains("加入"))
                                && (text.Contains("冰醋酸") || text.Contains("浓")
                                    || (text.Contains("增大") && text.Contains("浓度")));

        if ((increase || addStrongBase) && !addCommonIon && !increaseSelfConc) return true;
        if ((addCommonIon || increaseSelfConc) && !increase && !addStrongBase) return false;
        return null; // 冲突或未识别 → 不蒙数
    }
}
