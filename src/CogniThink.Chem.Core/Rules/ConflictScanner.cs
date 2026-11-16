namespace CogniThink.Chem.Core.Rules;

/// <summary>一对互相矛盾的规则。</summary>
public sealed class RuleConflict
{
    public RuleConflict(Rule a, Rule b, string reason)
    {
        RuleA = a;
        RuleB = b;
        Reason = reason;
    }

    public Rule RuleA { get; }

    public Rule RuleB { get; }

    public string Reason { get; }

    public override string ToString() => $"{RuleA.Id} ↔ {RuleB.Id}：{Reason}";
}

/// <summary>
/// 冲突扫描：在同一个 <see cref="Rule.ConflictGroup"/> 内查找"同谓词同主体、结论不同"的规则对。
/// 约定：结论取反用对象前缀 <c>not:</c> 表示，如 <c>not:soluble</c>。
/// 发布门槛：冲突数必须为 0。
/// </summary>
public static class ConflictScanner
{
    public static IReadOnlyList<RuleConflict> Scan(IEnumerable<Rule> rules)
    {
        var conflicts = new List<RuleConflict>();
        var groups = rules
            .Where(r => !string.IsNullOrWhiteSpace(r.ConflictGroup))
            .GroupBy(r => r.ConflictGroup, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var list = group.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                for (var j = i + 1; j < list.Count; j++)
                {
                    var reason = FindContradiction(list[i].Conclusion, list[j].Conclusion);
                    if (reason is not null)
                    {
                        conflicts.Add(new RuleConflict(list[i], list[j], reason));
                    }
                }
            }
        }

        return conflicts;
    }

    private static string? FindContradiction(Proposition a, Proposition b)
    {
        if (!string.Equals(a.Predicate, b.Predicate, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(a.Subject, b.Subject, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var objA = Normalize(a.Object);
        var objB = Normalize(b.Object);
        if (objA is null || objB is null)
        {
            return null;
        }

        if (string.Equals(objA, objB, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // 一个是另一个的取反形式，或两者取值互斥，视为矛盾。
        if (IsNegationOf(objA, objB))
        {
            return $"结论互斥：{objA} vs {objB}";
        }

        return null;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsNegationOf(string a, string b)
    {
        var negA = a.StartsWith("not:", StringComparison.OrdinalIgnoreCase);
        var negB = b.StartsWith("not:", StringComparison.OrdinalIgnoreCase);
        if (negA == negB)
        {
            return false;
        }

        var positive = negA ? b : a;
        var negative = (negA ? a : b)["not:".Length..];
        return string.Equals(positive.Trim(), negative.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
