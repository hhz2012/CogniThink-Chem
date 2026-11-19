namespace CogniThink.TutorBrain.Application.Gateway;

using System.Text.Json;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Chemistry;
using CogniThink.Chem.Core.Inference;
using CogniThink.Chem.Core.Reactions;
using CogniThink.TutorBrain.Application.Common;

/// <summary>graph 解题模式</summary>
public enum GraphSolveMode
{
    /// <summary>赋值：候选物种 + 转化拓扑 → 占位标签→物质</summary>
    Assign,
    /// <summary>选项验证：逐选项判断物质组是否满足图中每步一步转化</summary>
    VerifyOptions,
}

/// <summary>graph 结构化推理结果</summary>
public sealed class GraphSolveResult
{
    public InferenceStatus Status { get; init; }
    /// <summary>原标签 → 物种（Assign 模式）</summary>
    public Dictionary<string, string> Assignment { get; init; } = new();
    /// <summary>逐选项可行性（VerifyOptions 模式）：选项字母 → 是否满足全部转化</summary>
    public Dictionary<string, bool> OptionFeasibility { get; init; } = new();
    public List<InferenceStep> Steps { get; init; } = new();
    public string Message { get; init; } = "";
    /// <summary>给网关的结论文本</summary>
    public string FinalResult { get; init; } = "";
}

/// <summary>
/// graph 资产适配器：把 {nodes,edges} payload 转成 Chem.Core 引擎请求。
/// 红线：只做适配与调用，结论完全来自引擎；引擎无解时如实返回空，不蒙数。
/// </summary>
public static class GraphSolver
{
    // 反应条件令牌（非反应物）：命中即归入 conditions
    private static readonly HashSet<string> ConditionTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "△","加热","高温","点燃","燃烧","催化剂","通电","电解","光照","浓硫酸","稀硫酸","浓盐酸","稀盐酸",
        "浓硝酸","稀硝酸","NaOH溶液","BaCl2溶液","AgNO3溶液","KSCN","酚酞","石蕊","过量","适量","少量",
        "一定条件","催化剂加热","MnO2","酶","真空","高压","放电","研磨","水浴",
    };

    /// <summary>常见化学式模式：大写字母开头 + 数字/括号/上下标，用于从 cond 里识别反应物</summary>
    private static readonly Regex SpeciesPattern = new(@"^[A-Z][A-Za-z0-9()²³₀-₉^+\-]*$", RegexOptions.Compiled);

    /// <summary>化学式 + 用量后缀混写：CO2过量 / NaOH少量 / H2SO4足量 → 试剂(化学式) + 条件令牌(化学式+后缀)</summary>
    private static readonly Regex MixedSpeciesSuffix = new(@"^(?<sp>[A-Z][A-Za-z0-9()²³₀-₉^+\-]*)(?<suf>过量|少量|适量|足量|不足)$", RegexOptions.Compiled);

    /// <summary>浓/稀前缀 + 化学式：浓H2SO4 → 试剂 H2SO4 + 条件令牌 浓</summary>
    private static readonly Regex PrefixedSpecies = new(@"^(?<pre>浓|稀)(?<sp>[A-Z][A-Za-z0-9()²³₀-₉^+\-]*)$", RegexOptions.Compiled);

    private static readonly string[] CnStems = { "甲", "乙", "丙", "丁", "戊", "己", "庚", "辛", "壬", "癸" };

    /// <summary>把箭头标注 cond 拆成 试剂 + 条件</summary>
    private static (List<string> Reagents, List<string> Conditions) ParseCond(string cond)
    {
        var reagents = new List<string>();
        var conditions = new List<string>();
        if (string.IsNullOrWhiteSpace(cond)) return (reagents, conditions);

        var tokens = cond.Split(new[] { ',', '，', '、', '/', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in tokens)
        {
            var t = raw.Trim();
            if (string.IsNullOrEmpty(t)) continue;

            // ① 化学式+用量后缀混写（CO2过量）→ 拆为 试剂 + 条件令牌，与 NormalizeReagents 约定一致
            var mSuffix = MixedSpeciesSuffix.Match(t);
            if (mSuffix.Success)
            {
                reagents.Add(mSuffix.Groups["sp"].Value);
                conditions.Add(mSuffix.Groups["sp"].Value + mSuffix.Groups["suf"].Value);
                continue;
            }
            // ② 浓/稀前缀 + 化学式（浓H2SO4）→ 试剂 + 令牌
            var mPre = PrefixedSpecies.Match(t);
            if (mPre.Success)
            {
                reagents.Add(mPre.Groups["sp"].Value);
                conditions.Add(mPre.Groups["pre"].Value);
                continue;
            }
            // ③ 条件词：整体或包含
            if (ConditionTokens.Contains(t)) { conditions.Add(t); continue; }
            bool isCond = false;
            foreach (var c in ConditionTokens)
                if (t.Contains(c, StringComparison.OrdinalIgnoreCase)) { isCond = true; break; }
            if (isCond) { conditions.Add(t); continue; }
            // ④ 疑似化学式 → 试剂
            if (SpeciesPattern.IsMatch(t)) reagents.Add(t);
            else conditions.Add(t); // 无法识别的归入条件，不臆造为反应物
        }
        return (reagents, conditions);
    }

    /// <summary>判断 token 是否为已知物种（在候选列表里）</summary>
    private static bool IsKnown(string token, IReadOnlyList<string> candidates)
        => candidates.Contains(token, StringComparer.Ordinal);

    /// <summary>
    /// 收集占位标签并映射为 A/B/C…（单字母，满足引擎 IsLabelToken）。
    /// 已知物种（命中候选）不参与映射。
    /// </summary>
    private static Dictionary<string, string> BuildLabelMap(JsonElement edges, IReadOnlyList<string> candidates,
        out Dictionary<string, string> reverse)
    {
        var map = new Dictionary<string, string>();
        reverse = new Dictionary<string, string>();
        var order = new List<string>();
        foreach (var edge in edges.EnumerateArray())
        {
            foreach (var key in new[] { "from", "to" })
            {
                if (edge.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String)
                {
                    var token = v.GetString() ?? "";
                    if (!string.IsNullOrEmpty(token) && !order.Contains(token) && !IsKnown(token, candidates))
                        order.Add(token);
                }
            }
        }
        for (int i = 0; i < order.Count; i++)
        {
            if (i >= 26) break; // 高中题不超过 26 个占位标签
            var letter = ((char)('A' + i)).ToString();
            map[order[i]] = letter;
            reverse[letter] = order[i];
        }
        return map;
    }

    private static string GetString(JsonElement el, string key)
        => el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static GraphSolveResult NoSol(string message) => new()
    {
        Status = InferenceStatus.NoSolution,
        Message = message,
        FinalResult = message,
    };

    /// <summary>Assign 模式：候选物种 + 拓扑 → 标签赋值</summary>
    public static GraphSolveResult Solve(ImageAsset asset, IReadOnlyList<string> candidateSpecies,
        GraphSolveMode mode = GraphSolveMode.Assign, IReadOnlyDictionary<string, IReadOnlyList<string>>? options = null)
    {
        if (asset.Type != "graph" || asset.Payload is not JsonElement payload)
            return NoSol("非 graph 资产或无 payload");
        if (!payload.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array || nodes.GetArrayLength() == 0)
            return NoSol("graph payload 无节点");
        if (!payload.TryGetProperty("edges", out var edges) || edges.ValueKind != JsonValueKind.Array)
            return NoSol("graph payload 无 edges");

        if (mode == GraphSolveMode.VerifyOptions)
            return VerifyOptionsInternal(payload, candidateSpecies, options);

        return AssignInternal(payload, edges, candidateSpecies);
    }

    private static GraphSolveResult AssignInternal(JsonElement payload, JsonElement edges, IReadOnlyList<string> candidates)
    {
        var labelMap = BuildLabelMap(edges, candidates, out var reverse);
        if (labelMap.Count == 0)
            return NoSol("未识别到需赋值的占位标签（节点可能均为已知物质）");

        var steps = new List<ExperimentStep>();
        int i = 0;
        foreach (var edge in edges.EnumerateArray())
        {
            var from = GetString(edge, "from");
            var to = GetString(edge, "to");
            if (!labelMap.TryGetValue(from, out var f) || !labelMap.TryGetValue(to, out var t))
                continue; // from/to 含已知物质/操作节点的边不参与赋值
            var (reagents, conditions) = ParseCond(GetString(edge, "cond"));
            steps.Add(new ExperimentStep { Step = ++i, From = f, To = t, Reagents = reagents, Conditions = conditions });
        }
        if (steps.Count == 0)
            return NoSol("无可用于赋值的转化边");

        var req = new ProcessInferenceRequest { CandidateSpecies = candidates.ToList(), Steps = steps };
        var result = ProcessInferenceEngine.Infer(req);

        var assignment = new Dictionary<string, string>();
        foreach (var kv in result.Assignment)
            if (reverse.TryGetValue(kv.Key, out var orig)) assignment[orig] = kv.Value;

        return new GraphSolveResult
        {
            Status = result.Status,
            Assignment = assignment,
            Steps = result.Steps,
            Message = result.Message,
            FinalResult = BuildAssignFinalResult(result.Status, assignment, result.Message),
        };
    }

    private static string BuildAssignFinalResult(InferenceStatus status, Dictionary<string, string> assignment, string message)
    {
        if (status == InferenceStatus.Solved && assignment.Count > 0)
            return "转化图赋值：" + string.Join("，", assignment.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));
        if (status == InferenceStatus.Underdetermined)
            return "转化关系存在多组可能，无法唯一确定各物质。" + message;
        return message; // NoSolution 或空：如实说明，不蒙数
    }

    private static GraphSolveResult VerifyOptionsInternal(JsonElement payload, IReadOnlyList<string> candidates,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? options)
    {
        // 图中占位标签的出现顺序，与选项物质组按位置一一对应
        var labelOrder = new List<string>();
        IEnumerable<JsonElement> edgesEnum = payload.TryGetProperty("edges", out var e) && e.ValueKind == JsonValueKind.Array
            ? e.EnumerateArray() : Array.Empty<JsonElement>();
        foreach (var edge in edgesEnum)
            foreach (var key in new[] { "from", "to" })
                if (edge.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String)
                {
                    var t = v.GetString() ?? "";
                    if (!string.IsNullOrEmpty(t) && !labelOrder.Contains(t) && !IsKnown(t, candidates))
                        labelOrder.Add(t);
                }

        if (options == null || options.Count == 0 || labelOrder.Count == 0)
            return NoSol("选项验证需要提供选项物质组与占位标签顺序");

        var lib = ReactionLibrary.Default.Value;
        var feas = new Dictionary<string, bool>();
        var reasons = new Dictionary<string, string>();
        var allEdges = payload.GetProperty("edges").EnumerateArray().ToList();
        foreach (var opt in options)
        {
            if (opt.Value.Count < labelOrder.Count) { feas[opt.Key] = false; reasons[opt.Key] = "选项物质不足"; continue; }
            var assign = new Dictionary<string, string>();
            for (int i = 0; i < labelOrder.Count; i++) assign[labelOrder[i]] = opt.Value[i];

            bool ok = true; var failEdge = "";
            foreach (var edge in allEdges)
            {
                var from = GetString(edge, "from"); var to = GetString(edge, "to");
                if (!assign.TryGetValue(from, out var fromSp) || !assign.TryGetValue(to, out var toSp)) continue;
                // 转化关系图常略去反应条件：判据 = 知识库中是否存在 from→to 的一步反应（任一试剂/条件）。
                // 知识库未收录即视为当前引擎无法支撑（如实标记，不臆断为可行）。
                if (!CanConvertOneStep(lib, fromSp, toSp)) { ok = false; failEdge = $"{from}({fromSp})→{to}({toSp})"; break; }
            }
            feas[opt.Key] = ok;
            reasons[opt.Key] = ok ? "各步转化均有知识库反应支撑" : $"不满足：{failEdge} 无一步反应支撑";
        }

        var feasible = feas.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
        var infeasible = feas.Where(kv => !kv.Value).Select(kv => kv.Key).ToList();
        // 任一侧唯一（唯一可行 或 唯一不可行）即视为可解，由网关按问法极性选取答案。
        var status = (feasible.Count == 1 || infeasible.Count == 1)
            ? InferenceStatus.Solved
            : InferenceStatus.Underdetermined;

        var reasonText = string.Join("；", feas.Select(kv => $"{kv.Key}:{(kv.Value ? "可行" : "不可行")}（{reasons[kv.Key]}）"));
        return new GraphSolveResult
        {
            Status = status,
            OptionFeasibility = feas,
            Message = reasonText,
            FinalResult = reasonText,
        };
    }

    /// <summary>
    /// 一步转化可行性：知识库中是否存在 from 为反应物、to 为产物的反应条目（任一试剂/条件）。
    /// 先做分子精确匹配；失败时，在水溶液语境下把可溶性盐/强酸/强碱 from 电离为离子，
    /// 再匹配离子条目——例如 CaCl2→CaCO3：CaCl2 电离出 Ca^2+，命中 Ca^2+ + CO3^2- → CaCO3。
    /// 铁律：只检索知识库条目，不臆造反应；离子等价仅扫描反应物含离子的条目，避免分子条目重复命中。
    /// </summary>
    private static bool CanConvertOneStep(ReactionLibrary lib, string from, string to)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to)) return false;
        var fromKey = NormSpeciesKey(from);
        var toKey = NormSpeciesKey(to);
        foreach (var entry in lib.All)
        {
            if (entry.ReactionType == ReactionType.IonEquilibrium) continue;
            if (!entry.Reactants.Any(r => NormSpeciesKey(r) == fromKey)) continue;
            if (!entry.Products.Any(p => NormSpeciesKey(p) == toKey)) continue;
            return true;
        }

        // 离子等价匹配：仅当 from 能电离出离子（可溶性盐/强酸/强碱）时才尝试
        var fromIons = ReactionMatcher.BuildIonPool(new[] { from });
        fromIons.Remove("H2O"); // BuildIonPool 恒含溶剂 H2O，去掉以防对任意可溶性盐误匹配水相关条目
        if (!fromIons.Any(i => i.Contains('^'))) return false;

        var toKeys = new HashSet<string>(StringComparer.Ordinal) { toKey };
        var toIons = ReactionMatcher.BuildIonPool(new[] { to });
        toIons.Remove("H2O");
        foreach (var ti in toIons) toKeys.Add(NormSpeciesKey(ti));

        foreach (var entry in lib.All)
        {
            if (entry.ReactionType == ReactionType.IonEquilibrium) continue;
            if (!entry.Reactants.Any(r => r.Contains('^'))) continue; // 只考虑离子条目
            if (!entry.Reactants.Any(r => fromIons.Contains(r))) continue;
            if (!entry.Products.Any(p => toKeys.Contains(NormSpeciesKey(p)))) continue;
            return true;
        }
        return false;
    }

    private static string NormSpeciesKey(string s)
    {
        var t = (s ?? "").Trim();
        if (t.Length == 0) return t;
        try { return Species.Parse(t).Body; }
        catch { return t; }
    }
}
