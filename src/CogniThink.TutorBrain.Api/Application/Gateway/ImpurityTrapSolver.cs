using System.Text.Json;
using CogniThink.Chem.Core.Data;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 网关层"除杂陷阱"求解器（阶段3E）。
/// <para>真值源：<c>data/knowledge/impurity-traps.json</c>（化学事实只在 JSON；本文件仅含触发识别与响应构造，无硬编码化学真值）。
/// 命中规则表后给出否定结论；本类不推导未知陷阱，只在规则命中时给出确定结论。</para>
/// <para>红线（不蒙数）：窄锚点（强氧化剂 + 被氧化物 + 除杂动作）齐全才命中；不命中返回 null（零回归，不蒙数）。</para>
/// </summary>
public static class ImpurityTrapSolver
{
    // ==================== KB（除杂陷阱在 JSON） ====================

    private sealed class ImpurityTrapEntry
    {
        public string Id { get; set; } = "";
        public string FinalResult { get; set; } = "";
        public List<string> DerivationSteps { get; set; } = new();
        public string ConstraintNote { get; set; } = "";
        public List<string> ErrorTags { get; set; } = new();
    }

    private sealed class ImpurityTrapsData
    {
        public string Version { get; set; } = "";
        public string Source { get; set; } = "";
        public List<ImpurityTrapEntry> Entries { get; set; } = new();
    }

    private static readonly Lazy<Dictionary<string, ImpurityTrapEntry>> Traps = new(LoadTraps);

    private const string TrapKmno4EthyleneId = "impurity-kmno4-ethylene-co2";

    private static Dictionary<string, ImpurityTrapEntry> LoadTraps()
    {
        try
        {
            var path = Path.Combine(DataRoot.Resolve(), "knowledge", "impurity-traps.json");
            if (!File.Exists(path)) return new Dictionary<string, ImpurityTrapEntry>();
            var json = File.ReadAllText(path);
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            var data = JsonSerializer.Deserialize<ImpurityTrapsData>(json, opts);
            return data?.Entries.ToDictionary(e => e.Id, StringComparer.Ordinal)
                   ?? new Dictionary<string, ImpurityTrapEntry>();
        }
        catch
        {
            return new Dictionary<string, ImpurityTrapEntry>();
        }
    }

    /// <summary>尝试求解；不命中返回 null（零回归）。</summary>
    public static EngineResponse? TrySolve(EngineRequest req)
    {
        var text = req.RawText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return null;

        // 强氧化剂 + 乙烯 + 除杂动作 → 命中除杂陷阱规则
        if ((text.Contains("高锰酸钾") || text.Contains("KMnO4")) &&
            text.Contains("乙烯") &&
            (text.Contains("除") || text.Contains("除去") || text.Contains("去除")))
        {
            if (!Traps.Value.TryGetValue(TrapKmno4EthyleneId, out var trap)) return null;

            return new EngineResponse
            {
                Ok = false,
                FinalResult = trap.FinalResult,
                IsStudentAnswerCorrect = false,
                ErrorTags = trap.ErrorTags,
                DerivationSteps = trap.DerivationSteps,
                ConstraintNote = trap.ConstraintNote,
                // 加性推理链（spec R4）：命中 JSON 规则即携带真实 knowledge_id；
                // 无命中返回 null（无响应）→ 节点天然缺席，不臆造。
                ReasoningNodes = new List<ReasoningNode>
                {
                    new()
                    {
                        Step = 1,
                        Description = "除杂陷阱判定（规则取自 impurity-traps.json）",
                        Premises = new List<string> { $"题干除杂锚点命中 data/knowledge/impurity-traps.json 条目：{trap.Id}" },
                        KnowledgeId = trap.Id,
                        Conclusion = trap.FinalResult,
                        EvidenceLevel = EvidenceLevel.KnowledgeFact
                    }
                },
                Confidence = 1.0
            };
        }

        return null;
    }
}
