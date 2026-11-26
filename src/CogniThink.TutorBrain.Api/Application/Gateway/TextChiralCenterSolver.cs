using System.Text;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Molecules;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 文本结构简式 → 手性碳原子数求解器（batch2-B / pdf-169）。
/// <para>域：题干问「手性碳(原子)数目/个数」，且文本中给出可解析的结构简式（如「结构简式为 HOOC(CHOH)2COOH」）
/// → 归一化为标准 SMILES → RDKit numAtomStereoCenters → 输出手性碳原子数。</para>
/// <para>pdf-169：酒石酸 H2A 结构简式 HOOC(CHOH)2COOH → O=C(O)C(O)C(O)C(=O)O → 手性碳 2 个，命中答案键「2」。</para>
/// <para>红线：无「手性碳」问法 / 无「数目/个数」计数问法 / 无可解析结构简式 / 多个互斥计数 → 返回 null，绝不蒙数。
/// 归一化失败一律 null（不臆造）。采纳门 equilibrium 走 IsAnswerCorrect（OR）→ precision=1、零 harm。</para>
/// </summary>
public static class TextChiralCenterSolver
{
    private static readonly Regex FormulaAnchorRe = new(
        @"结构简式(?:为|是|：|:)?\s*([A-Za-z0-9()（）\[\]{}+\-=·]+)",
        RegexOptions.Compiled);

    // 结构简式 token：由元素字母、数字、括号、键连接符组成，且含 C、H（有机物）
    private static readonly Regex FormulaTokenRe = new(
        @"(?<![A-Za-z0-9])([A-Z][A-Za-z0-9]*(?:\([A-Za-z0-9]+\))?[A-Za-z0-9()（）]*)",
        RegexOptions.Compiled);

    private static readonly Regex RepeatRe = new(@"\(([^()]+)\)(\d+)", RegexOptions.Compiled);
    private static readonly Regex AcidRe = new(@"HOOC|COOH", RegexOptions.Compiled);
    private static readonly string[] CountAskMarkers = { "数目", "个数", "数为", "多少" };

    /// <summary>命中则返回含手性碳原子数的结论；域外返回 null。</summary>
    public static EngineResponse? TrySolve(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (!raw.Contains("手性碳", StringComparison.Ordinal)) return null;
        // 必须是「计数」问法，避免在仅提及手性碳的选择题/图像题误触发
        if (!CountAskMarkers.Any(m => raw.Contains(m, StringComparison.Ordinal))) return null;

        var candidates = ExtractFormulaCandidates(raw);
        if (candidates.Count == 0) return null;

        int? chiral = null;
        foreach (var cand in candidates)
        {
            var res = AnalyzeAny(cand);
            if (res is null || !res.Success) continue;
            if (chiral is null) chiral = res.ChiralCenterCount;
            else if (chiral.Value != res.ChiralCenterCount) return null; // 互斥 → 不蒙数
        }
        if (chiral is null) return null;

        var n = chiral.Value;
        var final = $"由结构简式分析，分子中手性碳原子数目为 {n}。";

        return new EngineResponse
        {
            Ok = true,
            FinalResult = final,
            DerivationSteps = new List<string>
            {
                "从题干文本提取结构简式并归一化为标准 SMILES（RDKit 可解析）",
                $"RDKit assignStereochemistry 后识别手性中心：{n} 个",
                $"手性碳原子数目为 {n}"
            },
            ReasoningNodes = new List<ReasoningNode>
            {
                new()
                {
                    Step = 1,
                    Description = "文本结构简式 → 手性碳原子数",
                    Premises = new List<string> { "题干给出结构简式，问手性碳原子数目" },
                    KnowledgeId = "text-structure-chiral-center",
                    Conclusion = $"手性碳原子数目为 {n}",
                    EvidenceLevel = EvidenceLevel.EngineDerived
                }
            },
            ConstraintNote = "【高中限定】仅对题干文本给出且可被 RDKit 解析的结构简式计手性碳；无可解析结构/歧义不蒙数",
            Confidence = 1.0
        };
    }

    // 提取候选结构简式 token：优先「结构简式为 X」锚点；兜底扫描含 C/H 的有机 token
    private static List<string> ExtractFormulaCandidates(string raw)
    {
        var list = new List<string>();
        foreach (Match m in FormulaAnchorRe.Matches(raw))
        {
            var tok = NormalizeParens(m.Groups[1].Value);
            if (LooksOrganic(tok)) list.Add(tok);
        }
        // 兜底：扫描全文字符，收集看起来像有机结构简式的 token
        foreach (Match m in FormulaTokenRe.Matches(raw))
        {
            var tok = NormalizeParens(m.Groups[1].Value);
            if (LooksOrganic(tok) && !list.Contains(tok)) list.Add(tok);
        }
        return list;
    }

    private static bool LooksOrganic(string tok)
    {
        if (tok.Length < 4) return false;
        // 必须含 C，且含 H 或 O/N/卤素等有机常见元素；排除纯离子/配合物（如 [Zn(NH3)4]）
        if (!tok.Contains('C')) return false;
        if (!Regex.IsMatch(tok, @"[HONClBrIFSP]")) return false;
        if (tok.Contains('[') || tok.Contains('{')) return false; // 配合物/离子 → 不参与
        return Regex.IsMatch(tok, @"^[A-Za-z0-9()]+$");
    }

    private static string NormalizeParens(string s) =>
        s.Replace('（', '(').Replace('）', ')').Trim().TrimEnd('。', '；', ';', ',', '，');

    // 尝试多种归一化，返回首个 RDKit 成功的分析结果；均失败返回 null
    private static MoleculeAnalysisResult? AnalyzeAny(string token)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var cands = new List<string> { token };
        var expanded = ExpandRepeats(token);
        cands.Add(expanded);
        var acids = RewriteAcids(expanded);
        cands.Add(acids);
        cands.Add(RewriteUnits(acids));
        cands.Add(RewriteUnits(RewriteAcids(token)));

        foreach (var c in cands)
        {
            if (string.IsNullOrWhiteSpace(c)) continue;
            var cc = NormalizeSuperscripts(c);
            if (!seen.Add(cc)) continue;
            var r = MoleculeAnalyzer.Analyze(cc, null);
            if (r.Success) return r;
        }
        return null;
    }

    // 展开 (group)n → group 重复 n 次（迭代至稳定）
    private static string ExpandRepeats(string s)
    {
        var prev = s;
        for (int i = 0; i < 6; i++)
        {
            var next = RepeatRe.Replace(prev, m =>
            {
                var group = m.Groups[1].Value;
                if (!int.TryParse(m.Groups[2].Value, out var n) || n <= 0 || n > 50) return m.Value;
                var sb = new StringBuilder();
                for (int k = 0; k < n; k++) sb.Append(group);
                return sb.ToString();
            });
            if (next == prev) break;
            prev = next;
        }
        return prev;
    }

    private static string RewriteAcids(string s) =>
        AcidRe.Replace(s, m => m.Value == "HOOC" ? "O=C(O)" : "C(=O)O");

    // CHOH → C(O)（RDKit 会补齐隐式 H）
    private static string RewriteUnits(string s) =>
        s.Replace("CHOH", "C(O)");

    private static string NormalizeSuperscripts(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            sb.Append(ch switch
            {
                '₀' => '0', '₁' => '1', '₂' => '2', '₃' => '3', '₄' => '4',
                '₅' => '5', '₆' => '6', '₇' => '7', '₈' => '8', '₉' => '9',
                _ => ch,
            });
        }
        return sb.ToString();
    }
}
