using System.Text.Json;
using CogniThink.Chem.Core.Data;

namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>
/// 标况气体摩尔体积陷阱求解器（unknown 路由拦截）：
/// 「标准状况下，22.4L X 含有的分子数是 NA 吗？」——若 X 在标况下不是气体，
/// 则 22.4 L 不为 1 mol，不能用 22.4 L/mol，答案否定。
/// <para>红线：仅当题干精确匹配「标况 + 22.4 + 分子数/NA + 物种在非气态事实表中」时判定；否则 null。
/// 化学事实（标况下物质状态）集中在 <c>data/knowledge/standard-state-phases.json</c>；
/// 本文件仅含触发识别与响应构造，无硬编码状态事实（B-LlmMain T4 外置）。匹配语义保持
/// <c>raw.Contains</c>（大小写敏感 ordinal），迭代顺序 = JSON 数组顺序（首命中语义不变）。</para>
/// </summary>
public static class StoichiometryStateTrapSolver
{
    private sealed class StandardStatePhaseEntry
    {
        public string Id { get; set; } = "";
        public string State { get; set; } = "";
        public List<string> Match { get; set; } = new();
    }

    private sealed class StandardStatePhasesData
    {
        public string Version { get; set; } = "";
        public string Source { get; set; } = "";
        public List<StandardStatePhaseEntry> Entries { get; set; } = new();
    }

    // 扁平化有序列表 (matchKey, state, id)：保持 JSON 数组顺序 → 与原 dict 插入序一致，首命中语义字节级不变。
    private static readonly Lazy<List<(string key, string state, string id)>> Phases = new(LoadPhases);

    private static List<(string key, string state, string id)> LoadPhases()
    {
        try
        {
            var path = Path.Combine(DataRoot.Resolve(), "knowledge", "standard-state-phases.json");
            if (!File.Exists(path)) return new List<(string, string, string)>();
            var json = File.ReadAllText(path);
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            var data = JsonSerializer.Deserialize<StandardStatePhasesData>(json, opts);
            var list = new List<(string, string, string)>();
            if (data?.Entries is not null)
            {
                foreach (var e in data.Entries)
                {
                    foreach (var m in e.Match)
                    {
                        list.Add((m, e.State, e.Id));
                    }
                }
            }
            return list;
        }
        catch
        {
            return new List<(string, string, string)>();
        }
    }

    /// <summary>命中返回 (FinalResult, Note, 命中条目的 id)；不命中返回 null（HitId 为 null 时不发节点，R4）。</summary>
    public static (string FinalResult, string Note, string? HitId)? TrySolve(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // 护栏：必须是「标况 + 22.4 + 分子数/NA + 疑问」类陷阱问法
        bool isStpTrap = (raw.Contains("标准状况") || raw.Contains("标况")) &&
                         raw.Contains("22.4") &&
                         (raw.Contains("分子数") || raw.Contains("NA") || raw.Contains("Nₐ")) &&
                         raw.Contains("吗");
        if (!isStpTrap) return null;

        foreach (var p in Phases.Value)
        {
            if (raw.Contains(p.key))
            {
                return ($"不是。标准状况下 {p.key} 是{p.state}，22.4 L 不是 1 mol 气体，不能用 22.4 L/mol 计算，故分子数不是 NA。",
                        "【高中限定】标况下非气态物质不适用气体摩尔体积",
                        string.IsNullOrWhiteSpace(p.id) ? null : p.id);
            }
        }
        return null;
    }
}
