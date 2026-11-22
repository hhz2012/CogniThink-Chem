namespace CogniThink.Chem.Core.WorldModel;

using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Data;
using CogniThink.Chem.Core.Reactions;
using CogniThink.Chem.Core.Rules;

// ============================================================
// P2-2：世界模型与机理层最小可用版（趋势级解释链）
// 纯函数，只读已有 JSON（data/highschool + data/rules + data/knowledge/reactions），
// 不硬编码化学事实；无命中返回空（不蒙数）。所有解释定性、不给绝对值，
// 标注「帮助理解，不作判定依据」。网关映射到 Api ReasoningNode（EvidenceLevel=Trend），
// 永不进入采纳门（spec CQ-5）。
// ============================================================

/// <summary>
/// 趋势级/机理级解释节点（Core 侧）。与 Api <c>ReasoningNode</c> 字段对齐但不含
/// <c>EvidenceLevel</c>（Core 解释恒为 Trend，由网关标注）。仅承载定性内容。
/// </summary>
public sealed class TrendExplanationNode
{
    public int Step { get; set; }

    public string Description { get; set; } = "";

    public List<string> Premises { get; set; } = new();

    /// <summary>命中的规则 id（data/rules/rules.json 条目）；无则 null。</summary>
    public string? RuleId { get; set; }

    /// <summary>命中的知识点/反应 id；无则 null。</summary>
    public string? KnowledgeId { get; set; }

    /// <summary>本步定性结论（机理/趋势方向），不是判定答案。</summary>
    public string? Conclusion { get; set; }
}

/// <summary>
/// 世界模型最小可用版：三条趋势级/机理级解释链
/// （M1 金属性/还原性递变 · M2 沉淀-配位溶解 · M3 盐类水解酸碱性）。
/// </summary>
public static class WorldModel
{
    /// <summary>统一免责声明：解释为帮助理解，不作判定依据。</summary>
    public const string Disclaimer = "帮助理解，不作判定依据";

    private sealed class Snapshot
    {
        public ActivitySeriesData Activity { get; init; } = null!;
        public Dictionary<string, Rule> RulesById { get; init; } = null!;
        public ReactionLibrary Reactions { get; init; } = null!;
    }

    // 进程内一次加载（与 ReactionLibrary.Default / RuleStore 缓存同构）；
    // 数据根由 DataRoot.Resolve 定位（环境变量 COGNITHINK_DATA 或向上查找仓库根）。
    private static readonly Lazy<Snapshot> _snapshot = new(LoadSnapshot);

    private static Snapshot LoadSnapshot()
    {
        var root = DataRoot.Resolve();
        var store = RuleStore.Load(root);
        var rules = ChemDataLoader.LoadRules(root)
            .GroupBy(r => r.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var reactions = ReactionLibrary.Load(root);
        return new Snapshot
        {
            Activity = store.Query<ActivitySeriesData>("activity-series"),
            RulesById = rules,
            Reactions = reactions,
        };
    }

    /// <summary>
    /// 对题干生成趋势级/机理级解释节点（定性，不给绝对值）。
    /// 不读、不改判定字段；无命中返回空列表。
    /// </summary>
    /// <param name="questionText">题干文本（含选项）。</param>
    /// <param name="finalResult">引擎已解结论（仅供上下文，不参与判定）。</param>
    public static IReadOnlyList<TrendExplanationNode> Explain(string questionText, string? finalResult = null)
    {
        var text = questionText ?? string.Empty;
        var snap = _snapshot.Value;
        var nodes = new List<TrendExplanationNode>();

        nodes.AddRange(ExplainMetallicTrend(text, snap));
        nodes.AddRange(ExplainAmmoniaComplexation(text, snap));
        nodes.AddRange(ExplainHydrolysisAlkalinity(text, snap));

        // 重新连续编号（跨链），保证 Step 从 1 连续无跳步
        for (var i = 0; i < nodes.Count; i++)
        {
            nodes[i].Step = i + 1;
        }

        return nodes;
    }

    // ──────────────────────────────────────────────────────────
    // M1 金属性 / 还原性 / 活动性递变
    // ──────────────────────────────────────────────────────────
    private static readonly Regex MetallicTrendTrigger = new(
        @"(金属性|非金属性|还原性|氧化性|活动性|活泼性).{0,12}(递变|强弱|比较|顺序|增强|减弱|由.+到)|" +
        @"(同周期|同主族).{0,12}(金属性|非金属性|还原性|氧化性)",
        RegexOptions.Compiled);

    private static IEnumerable<TrendExplanationNode> ExplainMetallicTrend(string text, Snapshot snap)
    {
        if (!MetallicTrendTrigger.IsMatch(text))
        {
            yield break;
        }

        // 同周期递变（STR-001）
        if (text.Contains("同周期") && snap.RulesById.TryGetValue("STR-001", out var str001))
        {
            yield return NodeFromRule(str001, "由元素周期律：" + str001.Conclusion.Display);
        }

        // 同主族递变（STR-002）
        if (text.Contains("同主族") && snap.RulesById.TryGetValue("STR-002", out var str002))
        {
            yield return NodeFromRule(str002, "由元素周期律：" + str002.Conclusion.Display);
        }

        // 活动性顺序置换（RED-001）+ 题干中出现的活动性金属相对位置（来自 activity-series.json，确定序）
        var present = PresentActivityMetals(text, snap.Activity.Series);
        if (present.Count >= 2 && snap.RulesById.TryGetValue("RED-001", out var red001))
        {
            var ordered = present.OrderBy(i => i).Select(i => snap.Activity.Series[i]).ToList();
            yield return NodeFromRule(red001,
                $"按金属活动性顺序（{string.Join("、", snap.Activity.Series)}），{string.Join("、", ordered)} 的位置依次靠后，故活动性/还原性依次减弱：{red001.Conclusion.Display}");
        }
    }

    // 找出题干中独立出现（非 H2O/H+ 等内部子串）的活动性序列金属下标
    private static List<int> PresentActivityMetals(string text, List<string> series)
    {
        var found = new List<int>();
        for (var i = 0; i < series.Count; i++)
        {
            var sym = series[i];
            if (sym == "H") continue; // H 为参照位，易与 H2O/H+ 混淆，不计入金属比较
            if (Regex.IsMatch(text, $@"(?<![A-Za-z0-9]){Regex.Escape(sym)}(?![A-Za-z0-9])"))
            {
                if (!found.Contains(i)) found.Add(i);
            }
        }

        return found;
    }

    // ──────────────────────────────────────────────────────────
    // M2 沉淀-配位溶解机理（AgCl + 过量氨水）
    // ──────────────────────────────────────────────────────────
    private static IEnumerable<TrendExplanationNode> ExplainAmmoniaComplexation(string text, Snapshot snap)
    {
        var mentionsAgCl = text.Contains("AgCl") || text.Contains("氯化银");
        var mentionsAmmonia = text.Contains("氨水") || text.Contains("氨") || text.Contains("银氨");
        if (!mentionsAgCl || !mentionsAmmonia)
        {
            yield break;
        }

        if (snap.RulesById.TryGetValue("SOL-EXC-001", out var rule))
        {
            yield return NodeFromRule(rule, "沉淀-配位耦合机理：" + rule.Conclusion.Display);
        }

        if (snap.Reactions.TryGet("RX-PL-PPT-001", out var dissoc))
        {
            yield return NodeFromReaction(dissoc, $"AgCl 存在沉淀溶解平衡：{dissoc.Equation}；加入过量氨水后 Ag^+ 被配位消耗，平衡右移而溶解。");
        }
    }

    // ──────────────────────────────────────────────────────────
    // M3 盐类水解酸碱性（Na2CO3 / 碳酸根水解显碱性）
    // ──────────────────────────────────────────────────────────
    private static IEnumerable<TrendExplanationNode> ExplainHydrolysisAlkalinity(string text, Snapshot snap)
    {
        var mentionsCarbonate = text.Contains("Na2CO3") || text.Contains("碳酸钠")
                                || text.Contains("CO3") || text.Contains("碳酸根");
        var mentionsHydrolysisOrAlkaline = text.Contains("水解") || text.Contains("碱性")
                                           || text.Contains("显碱") || Regex.IsMatch(text, @"溶液.{0,4}碱");
        if (!mentionsCarbonate || !mentionsHydrolysisOrAlkaline)
        {
            yield break;
        }

        if (snap.Reactions.TryGet("RX-PL-HYD-003", out var hydrolysis))
        {
            yield return NodeFromReaction(hydrolysis,
                $"Na2CO3 是强碱弱酸盐，弱酸根 CO3^2- 发生水解：{hydrolysis.Equation}，产生 OH^-，故溶液显碱性（定性，不涉及 pH 数值）。");
        }
    }

    // ──────────────────────────────────────────────────────────
    // 节点构造：内容必须来自 JSON 事实（rule display / reaction equation），不硬编码化学真值
    // ──────────────────────────────────────────────────────────
    private static TrendExplanationNode NodeFromRule(Rule rule, string description) => new()
    {
        Description = description + "（" + Disclaimer + "）",
        Premises = new List<string> { rule.Conclusion.Display ?? string.Empty },
        RuleId = rule.Id,
        KnowledgeId = string.IsNullOrEmpty(rule.KnowledgeId) ? null : rule.KnowledgeId,
        Conclusion = rule.Conclusion.Display,
    };

    private static TrendExplanationNode NodeFromReaction(ReactionEntry rxn, string description) => new()
    {
        Description = description + "（" + Disclaimer + "）",
        Premises = new List<string> { rxn.Equation },
        RuleId = null,
        KnowledgeId = string.IsNullOrEmpty(rxn.KnowledgeId) ? null : rxn.KnowledgeId,
        Conclusion = rxn.Name,
    };
}
