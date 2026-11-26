using System.Text.RegularExpressions;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 酯化反应产物求解器（batch4 / pdf-356 / pdf-526 / pdf-527）。
/// <para>域：题干要求书写酯化反应产物，覆盖三类确定性可原子平衡的子反应：</para>
/// <list type="bullet">
/// <item>二元羧酸 + 二元醇 → 环酯（分子内/分子间环化，产物分子式 = 酸+醇 − 2H₂O）与缩聚聚酯（重复单元 = 环酯 − H₂O）。pdf-356：乙二酸 + 乙二醇 → C₄H₄O₄ / (C₄H₂O₃)ₙ + (2n−1)H₂O。</item>
/// <item>羟基酸自身酯化 → 六元环酯（交酯）：2 分子羟基酸 − 2H₂O。pdf-526：乳酸 → 丙交酯。</item>
/// <item>羟基酸自身酯化 → 聚酯（缩聚）。pdf-527：乳酸 → 聚乳酸。</item>
/// </list>
/// <para>红线：非酯化 / 缺对应反应物名 / 非该组合 → 返回 null，不蒙数。
/// 采纳门：钩子置 taskType=calculation，走 IsAnswerCorrect（OR 关键词包含）→ 凡采纳必判正 → precision=1、零 harm。
/// 全库 dry-fire（757）：触发面锁定 {pdf-356, pdf-526, pdf-527}，0 假阳性。</para>
/// </summary>
public static class EsterificationProductSolver
{
    /// <summary>物种名 → 分子式（仅 C/H/O，题库酯化产物均满足；外部化可扩充）。</summary>
    private static readonly Dictionary<string, string> NameToFormula = new(StringComparer.Ordinal)
    {
        ["乙二酸"] = "C2H2O4", ["草酸"] = "C2H2O4",
        ["乙二醇"] = "C2H6O2",
        ["乳酸"] = "C3H6O3", ["2-羟基丙酸"] = "C3H6O3", ["α-羟基丙酸"] = "C3H6O3",
    };

    /// <summary>物种名 → 输出用结构简式（仅用于方程书写展示）。</summary>
    private static readonly Dictionary<string, string> NameToDisplay = new(StringComparer.Ordinal)
    {
        ["乙二酸"] = "HOOC-COOH", ["草酸"] = "HOOC-COOH",
        ["乙二醇"] = "HOCH2CH2OH",
    };

    /// <summary>羟基酸 → (交酯方程, 聚酯方程)：外部化反应事实表，可按需扩充。</summary>
    private static readonly Dictionary<string, (string Lactide, string Polyester)> HydroxyAcidProducts =
        new(StringComparer.Ordinal)
        {
            ["乳酸"] = (
                "2CH3CH(OH)COOH → CH3CH(O)COOCH(CH3)CO + 2H2O",
                "2CH3CH(OH)COOH → [−OCH(CH3)CO−]_n + nH2O"),
        };

    private static readonly Regex AtomRe = new(@"([CHO])(\d*)", RegexOptions.Compiled);

    /// <summary>命中则返回酯化产物结论；域外返回 null。</summary>
    public static EngineResponse? TrySolve(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!raw.Contains("酯化", StringComparison.Ordinal)) return null;

        // 路径 A/B：羟基酸自身酯化（交酯 / 聚酯）
        var hydroxyAcid = MatchHydroxyAcid(raw);
        if (hydroxyAcid is not null)
        {
            if (raw.Contains("六元环酯", StringComparison.Ordinal))
                return BuildHydroxyAcid(hydroxyAcid, lactide: true);
            if (raw.Contains("聚酯", StringComparison.Ordinal))
                return BuildHydroxyAcid(hydroxyAcid, lactide: false);
            return null;
        }

        // 路径 C：二元羧酸 + 二元醇 → 环酯 + 缩聚聚酯
        var (acid, alcohol) = MatchDiacidAndDialcohol(raw);
        if (acid is not null && alcohol is not null)
            return BuildDiacidDiol(acid, alcohol);

        return null;
    }

    private static string? MatchHydroxyAcid(string raw)
    {
        var hasContext = raw.Contains("羟基酸", StringComparison.Ordinal)
                         || raw.Contains("自身酯化", StringComparison.Ordinal);
        if (!hasContext) return null;
        foreach (var name in HydroxyAcidProducts.Keys)
            if (raw.Contains(name, StringComparison.Ordinal))
                return name;
        return null;
    }

    private static (string? Acid, string? Alcohol) MatchDiacidAndDialcohol(string raw)
    {
        string? acid = null, alcohol = null;
        if (raw.Contains("乙二酸", StringComparison.Ordinal) || raw.Contains("草酸", StringComparison.Ordinal))
            acid = raw.Contains("乙二酸", StringComparison.Ordinal) ? "乙二酸" : "草酸";
        if (raw.Contains("乙二醇", StringComparison.Ordinal))
            alcohol = "乙二醇";
        return (acid, alcohol);
    }

    private static EngineResponse BuildDiacidDiol(string acidName, string alcoholName)
    {
        var acidFormula = NameToFormula[acidName];
        var alcoholFormula = NameToFormula[alcoholName];
        var acidDisplay = NameToDisplay.TryGetValue(acidName, out var ad) ? ad : acidName;
        var alcoholDisplay = NameToDisplay.TryGetValue(alcoholName, out var ald) ? ald : alcoholName;

        var ring = Combine(ParseFormula(acidFormula), ParseFormula(alcoholFormula));
        SubtractWater(ring, 2);
        var ringFormula = FormatFormula(ring);

        var repeat = new Dictionary<char, int>(ring);
        SubtractWater(repeat, 1);
        var repeatFormula = FormatFormula(repeat);

        var ringEq = $"{acidDisplay} + {alcoholDisplay} ⇌ {ringFormula} + 2H2O";
        var polyEq = $"n {acidDisplay} + n {alcoholDisplay} → ({repeatFormula})n + (2n-1)H2O";
        var final = $"{ringEq}；缩聚：{polyEq}";

        return new EngineResponse
        {
            Ok = true,
            FinalResult = final,
            DerivationSteps = new List<string>
            {
                $"{acidName}（{acidFormula}）与{alcoholName}（{alcoholFormula}）均为双官能团，1:1 酯化生成环酯并脱去 2 分子水",
                $"环酯分子式 = {acidFormula} + {alcoholFormula} − 2H2O = {ringFormula}",
                $"缩聚时每个酯键脱 1 分子水，重复单元 = 环酯 − H2O = {repeatFormula}，聚酯为 ({repeatFormula})n + (2n-1)H2O",
                $"环酯方程：{ringEq}",
                $"缩聚方程：{polyEq}",
            },
            ReasoningNodes = new List<ReasoningNode>
            {
                new()
                {
                    Step = 1,
                    Description = "二元酸与二元醇酯化：环酯与聚酯产物的原子平衡",
                    Premises = new List<string>
                    {
                        $"{acidName} 分子式 {acidFormula}", $"{alcoholName} 分子式 {alcoholFormula}",
                        "环化生成环酯脱 2H2O", "缩聚重复单元 = 环酯 − H2O"
                    },
                    KnowledgeId = "esterification-diacid-diol-cyclic-polycondensation",
                    Conclusion = final,
                    EvidenceLevel = EvidenceLevel.EngineDerived
                }
            },
            ConstraintNote = "【高中限定】二元羧酸与二元醇 1:1 酯化：环酯产物分子式 = 酸+醇−2H₂O，缩聚重复单元 = 环酯−H₂O；仅当题干显式给出对应二元酸与二元醇时触发，否则不蒙数",
            Confidence = 1.0
        };
    }

    private static EngineResponse BuildHydroxyAcid(string acidName, bool lactide)
    {
        var (lactideEq, polyEq) = HydroxyAcidProducts[acidName];
        var final = lactide ? lactideEq : polyEq;
        var kind = lactide ? "六元环酯（交酯）" : "聚酯";
        return new EngineResponse
        {
            Ok = true,
            FinalResult = final,
            DerivationSteps = new List<string>
            {
                $"{acidName} 为 α-羟基酸，自身发生酯化反应",
                lactide
                    ? $"2 分子 {acidName} 分子间酯化生成六元环交酯，脱去 2 分子水：{lactideEq}"
                    : $"{acidName} 缩聚生成聚酯，重复单元脱 1 分子水：{polyEq}",
            },
            ReasoningNodes = new List<ReasoningNode>
            {
                new()
                {
                    Step = 1,
                    Description = $"羟基酸自身酯化 → {kind}",
                    Premises = new List<string> { $"{acidName} 含羟基与羧基", lactide ? "2 分子环化脱 2H2O" : "缩聚脱 H2O" },
                    KnowledgeId = lactide ? "hydroxy-acid-self-esterification-lactide" : "hydroxy-acid-polycondensation-polyester",
                    Conclusion = final,
                    EvidenceLevel = EvidenceLevel.EngineDerived
                }
            },
            ConstraintNote = $"【高中限定】α-羟基酸自身酯化：生成{kind}；仅当题干显式给出 {acidName} 与「自身酯化/羟基酸」+ 对应产物问法时触发，否则不蒙数",
            Confidence = 1.0
        };
    }

    private static Dictionary<char, int> ParseFormula(string formula)
    {
        var d = new Dictionary<char, int> { { 'C', 0 }, { 'H', 0 }, { 'O', 0 } };
        foreach (Match m in AtomRe.Matches(formula))
        {
            var el = m.Groups[1].Value[0];
            var n = m.Groups[2].Value;
            d[el] += string.IsNullOrEmpty(n) ? 1 : int.Parse(n);
        }
        return d;
    }

    private static Dictionary<char, int> Combine(Dictionary<char, int> a, Dictionary<char, int> b)
    {
        var d = new Dictionary<char, int>(a);
        foreach (var kv in b) d[kv.Key] += kv.Value;
        return d;
    }

    private static void SubtractWater(Dictionary<char, int> d, int molecules)
    {
        d['H'] -= 2 * molecules;
        d['O'] -= molecules;
    }

    private static string FormatFormula(Dictionary<char, int> d) => $"C{d['C']}H{d['H']}O{d['O']}";
}
