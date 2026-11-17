using CogniThink.Chem.Core.Rules;

namespace CogniThink.Chem.Core.Knowledge;

/// <summary>知识问答检索结果中的一个知识点命中。</summary>
public sealed class KnowledgeHit
{
    public KnowledgeItem Item { get; set; } = null!;

    /// <summary>相关度分（name 命中权重最高）。</summary>
    public int Score { get; set; }

    /// <summary>命中的字段名，用于前端高亮。</summary>
    public string MatchedField { get; set; } = "";
}

/// <summary>知识问答检索结果中的一条规则命中。</summary>
public sealed class RuleHit
{
    public Rule Rule { get; set; } = null!;

    public int Score { get; set; }

    public string MatchedField { get; set; } = "";
}

/// <summary>知识问答检索结果。</summary>
public sealed class KnowledgeQueryResult
{
    public List<KnowledgeHit> KnowledgeHits { get; set; } = new();

    public List<RuleHit> RuleHits { get; set; } = new();

    public bool IsEmpty => KnowledgeHits.Count == 0 && RuleHits.Count == 0;
}

/// <summary>
/// 知识检索服务：基于关键词在知识库与规则库中做结构化检索，
/// 供前端问答面板与知识浏览页使用。不引入 LLM，纯本地匹配。
/// </summary>
public static class KnowledgeQueryService
{
    /// <summary>检索：同时匹配知识点与规则，按相关度降序返回。</summary>
    public static KnowledgeQueryResult Search(string query, KnowledgeBase kb, IReadOnlyList<Rule> rules)
    {
        var result = new KnowledgeQueryResult();
        if (string.IsNullOrWhiteSpace(query)) return result;

        var q = query.Trim();
        var tokens = Tokenize(q);

        // 知识点：name > summary > pitfall > textbookRef
        foreach (var item in kb.All)
        {
            var (score, field) = ScoreItem(item, tokens);
            if (score > 0)
            {
                result.KnowledgeHits.Add(new KnowledgeHit { Item = item, Score = score, MatchedField = field });
            }
        }

        // 规则：name > conclusion.display
        foreach (var rule in rules)
        {
            var (score, field) = ScoreRule(rule, tokens);
            if (score > 0)
            {
                result.RuleHits.Add(new RuleHit { Rule = rule, Score = score, MatchedField = field });
            }
        }

        result.KnowledgeHits.Sort((a, b) => b.Score.CompareTo(a.Score));
        result.RuleHits.Sort((a, b) => b.Score.CompareTo(a.Score));
        return result;
    }

    /// <summary>
    /// 中文简化分词：按 2-gram 切分，同时保留整串。
    /// 对短 query（<=4 字）直接用整串匹配，避免切太碎。
    /// </summary>
    private static List<string> Tokenize(string query)
    {
        var tokens = new List<string> { query };
        if (query.Length > 4)
        {
            for (int i = 0; i <= query.Length - 2; i++)
            {
                tokens.Add(query.Substring(i, 2));
            }
        }
        // 按空格拆分（用户可能输入多个关键词）
        var parts = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1) tokens.AddRange(parts);
        return tokens.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static (int Score, string Field) ScoreItem(KnowledgeItem item, List<string> tokens)
    {
        int nameScore = tokens.Sum(t => Contains(item.Name, t) ? 10 : 0);
        if (nameScore > 0) return (nameScore, "name");

        int summaryScore = tokens.Sum(t => Contains(item.Summary, t) ? 3 : 0);
        if (summaryScore > 0) return (summaryScore, "summary");

        int pitfallScore = tokens.Sum(t => Contains(item.Pitfall, t) ? 4 : 0);
        if (pitfallScore > 0) return (pitfallScore, "pitfall");

        int refScore = tokens.Sum(t => Contains(item.TextbookRef, t) ? 2 : 0);
        if (refScore > 0) return (refScore, "textbookRef");

        int idScore = tokens.Sum(t => Contains(item.Id, t) ? 5 : 0);
        if (idScore > 0) return (idScore, "id");

        return (0, "");
    }

    private static (int Score, string Field) ScoreRule(Rule rule, List<string> tokens)
    {
        int nameScore = tokens.Sum(t => Contains(rule.Name, t) ? 10 : 0);
        if (nameScore > 0) return (nameScore, "name");

        string display = rule.Conclusion?.Display ?? "";
        int displayScore = tokens.Sum(t => Contains(display, t) ? 3 : 0);
        if (displayScore > 0) return (displayScore, "display");

        int idScore = tokens.Sum(t => Contains(rule.Id, t) ? 5 : 0);
        if (idScore > 0) return (idScore, "id");

        return (0, "");
    }

    private static bool Contains(string text, string token) =>
        !string.IsNullOrEmpty(text) && text.Contains(token, StringComparison.OrdinalIgnoreCase);
}
