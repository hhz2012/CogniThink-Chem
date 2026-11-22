using System.Text.Json;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Data;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 网关层"概念正误判断"求解器（阶段3E）。
/// <para>真值源：<c>data/highschool/concept-verdicts.json</c>（断言模式与判决事实只在 JSON；
/// 本文件仅含判断空结构识别与响应构造，无硬编码断言/判决文本）。</para>
/// <para>覆盖（首批）：断言某物质为某类氧化物的判断题（pdf-467）→ 该断言错误 → 判错。</para>
/// <para>红线：仅在锚点齐全（明确断言 + 判断答题空）时命中；断言为否定形式或锚点缺失一律返回 null
/// （零回归，不蒙数）。输出纯判决符号串，经 <c>HasCorrectnessVerdictSignature</c> 采纳。</para>
/// </summary>
public static class ConceptVerdictSolver
{
    // ==================== KB（断言模式与判决在 JSON） ====================

    private sealed class VerdictEntry
    {
        public string Id { get; set; } = "";
        public string Pattern { get; set; } = "";
        public string Verdict { get; set; } = "";
        public List<string> DerivationSteps { get; set; } = new();
        public string ConstraintNote { get; set; } = "";
    }

    private sealed class VerdictData
    {
        public string Version { get; set; } = "";
        public string Source { get; set; } = "";
        public List<VerdictEntry> Entries { get; set; } = new();
    }

    private sealed class CompiledVerdict
    {
        public VerdictEntry Entry { get; init; } = new();
        public Regex Pattern { get; init; } = new("");
    }

    private static readonly Lazy<List<CompiledVerdict>> Rules = new(LoadRules);

    private static List<CompiledVerdict> LoadRules()
    {
        try
        {
            var path = Path.Combine(DataRoot.Resolve(), "highschool", "concept-verdicts.json");
            if (!File.Exists(path)) return new List<CompiledVerdict>();
            var json = File.ReadAllText(path);
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            var data = JsonSerializer.Deserialize<VerdictData>(json, opts);
            return data?.Entries
                       .Where(e => !string.IsNullOrEmpty(e.Pattern))
                       .Select(e => new CompiledVerdict { Entry = e, Pattern = new Regex(e.Pattern, RegexOptions.Compiled) })
                       .ToList()
                   ?? new List<CompiledVerdict>();
        }
        catch
        {
            return new List<CompiledVerdict>();
        }
    }

    // 判断答题空 ( ) / （ ）（任意位置），确认为正误判断题而非陈述句填空。
    private static readonly Regex JudgmentBlank = new(@"[（(]\s*[）)]", RegexOptions.Compiled);

    /// <summary>尝试求解；不命中返回 null（零回归）。</summary>
    public static EngineResponse? TrySolve(EngineRequest req)
    {
        var text = !string.IsNullOrWhiteSpace(req.RawText) ? req.RawText : (req.StudentAnswer ?? string.Empty);
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!JudgmentBlank.IsMatch(text)) return null;

        // 逐条匹配 JSON 中的断言模式（含否定式回顾后发，排除"不是/非为"等否定断言）；
        // 任一命中即给出对应判决，否则返回 null。
        foreach (var rule in Rules.Value)
        {
            if (!rule.Pattern.IsMatch(text)) continue;
            var entry = rule.Entry;
            return new EngineResponse
            {
                Ok = true,
                FinalResult = entry.Verdict,
                DerivationSteps = entry.DerivationSteps,
                ConstraintNote = entry.ConstraintNote,
                // 加性推理链（spec R4）：命中 JSON 断言即携带真实 knowledge_id；
                // 无命中返回 null（无响应）→ 节点天然缺席，不臆造。
                ReasoningNodes = new List<ReasoningNode>
                {
                    new()
                    {
                        Step = 1,
                        Description = "概念正误判决（事实取自 concept-verdicts.json）",
                        Premises = new List<string> { $"断言命中 data/highschool/concept-verdicts.json 条目：{entry.Id}" },
                        KnowledgeId = entry.Id,
                        Conclusion = entry.Verdict,
                        EvidenceLevel = EvidenceLevel.KnowledgeFact
                    }
                },
                Confidence = 1.0
            };
        }

        return null;
    }
}
