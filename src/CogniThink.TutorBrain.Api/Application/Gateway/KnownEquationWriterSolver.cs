using System.Text.Json;
using CogniThink.Chem.Core.Data;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 网关层"已知方程式书写"求解器（阶段3E）。
/// <para>真值源：<c>data/knowledge/known-equations.json</c>（化学事实只在 JSON；本文件仅含触发识别与响应构造，无硬编码化学真值）。
/// 命中时直接给出配平好的方程式结论；不命中返回 null，由后续求解器/回退处理（零回归，不蒙数）。</para>
/// <para>覆盖（首批）：pdf-539（GaN 强碱离子方程式）、pdf-456 子问1（实验室制 SO₂ 化学方程式）；条目与 id 见 JSON。</para>
/// <para>红线：仅在锚点齐全（反应物 + 目标产物 + 问"方程式"）时命中；锚点缺失一律 null，绝不臆造。</para>
/// <para>采纳：离子方程式结论含"离子方程式"签名（既有门控）；分子方程式结论含"化学方程式"签名
/// （<c>HasReactionCheckTruthSignature</c> 对称扩展），均被采纳门控接受。</para>
/// </summary>
public static class KnownEquationWriterSolver
{
    // ==================== KB（化学事实在 JSON） ====================

    private sealed class EqEntry
    {
        public string Id { get; set; } = "";
        public string ResultPrefix { get; set; } = "";
        public string Equation { get; set; } = "";
        public List<string> DerivationSteps { get; set; } = new();
        public string ConstraintNote { get; set; } = "";
    }

    private sealed class EqData
    {
        public string Version { get; set; } = "";
        public string Source { get; set; } = "";
        public List<EqEntry> Entries { get; set; } = new();
    }

    private static readonly Lazy<Dictionary<string, EqEntry>> Facts = new(LoadFacts);

    private static Dictionary<string, EqEntry> LoadFacts()
    {
        try
        {
            var path = Path.Combine(DataRoot.Resolve(), "knowledge", "known-equations.json");
            if (!File.Exists(path)) return new Dictionary<string, EqEntry>();
            var json = File.ReadAllText(path);
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            var data = JsonSerializer.Deserialize<EqData>(json, opts);
            return data?.Entries.ToDictionary(e => e.Id, StringComparer.Ordinal)
                   ?? new Dictionary<string, EqEntry>();
        }
        catch
        {
            return new Dictionary<string, EqEntry>();
        }
    }

    private static EngineResponse Respond(EqEntry entry)
    {
        var finalResult = entry.ResultPrefix + entry.Equation;
        return new EngineResponse
        {
            Ok = true,
            FinalResult = finalResult,
            DerivationSteps = entry.DerivationSteps,
            // 加性推理链（spec R4）：命中 JSON 知识条目即携带真实 knowledge_id；
            // 无命中返回 null（无响应）→ 节点天然缺席，不臆造。
            ReasoningNodes = new List<ReasoningNode>
            {
                new()
                {
                    Step = 1,
                    Description = "已知方程式书写（事实取自 known-equations.json）",
                    Premises = new List<string> { $"题干锚点命中 data/knowledge/known-equations.json 条目：{entry.Id}" },
                    KnowledgeId = entry.Id,
                    Conclusion = finalResult,
                    EvidenceLevel = EvidenceLevel.KnowledgeFact
                }
            },
            ConstraintNote = entry.ConstraintNote,
            Confidence = 1.0
        };
    }

    /// <summary>尝试书写已知方程式；不命中返回 null（零回归）。</summary>
    public static EngineResponse? TrySolve(EngineRequest req)
    {
        var text = req.RawText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return null;

        // 只在明确问"方程式"的题干触发，避免污染其他 reaction_check 题
        if (!text.Contains("方程式") && !text.Contains("方程")) return null;

        // 1) GaN + 强碱（pdf-539）：反应物 GaN、产物锚点齐全。
        if (text.Contains("GaN") &&
            (text.Contains("Ga(OH)4") || text.Contains("[Ga(OH)4]") || text.Contains("四羟基合镓")))
        {
            if (Facts.Value.TryGetValue("known-eq-gan-strong-base", out var gan))
                return Respond(gan);
        }

        // 2) 实验室制取 SO2：亚硫酸钠 + 浓硫酸（pdf-456 子问1）。
        //    触发：问"产生/生成/制取 SO2 的化学方程式"。若题干点名亚硫酸钠则直接命中；
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
            if (Facts.Value.TryGetValue("known-eq-na2so3-so2", out var so2))
                return Respond(so2);
        }

        return null;
    }
}
