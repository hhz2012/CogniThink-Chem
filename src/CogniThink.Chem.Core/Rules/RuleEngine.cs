using CogniThink.Chem.Core.Diagnostics;

namespace CogniThink.Chem.Core.Rules;

/// <summary>被激活的规则及其依赖的默认假设。</summary>
public sealed class ActivatedRule
{
    public ActivatedRule(Rule rule, IReadOnlyList<string> defaultAssumptions)
    {
        Rule = rule;
        DefaultAssumptions = defaultAssumptions;
    }

    public Rule Rule { get; }

    public IReadOnlyList<string> DefaultAssumptions { get; }
}

/// <summary>
/// 规则仲裁引擎（M2）。按规划 3.2 的顺序执行：
/// 硬约束淘汰 → 待评审剔除 → 例外覆盖 → 学段合规 → 排序（层级/证据/学段/默认假设）。
/// 任何一步都不允许"因为讲得通"就选一个：并列时返回 <see cref="VerdictKind.Unresolved"/>。
/// </summary>
public sealed class RuleEngine
{
    private readonly Dictionary<string, Rule> _rules;

    private readonly IConservationValidator _conservation;

    public RuleEngine(IEnumerable<Rule> rules, IConservationValidator? conservationValidator = null)
    {
        _rules = new Dictionary<string, Rule>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules)
        {
            if (!_rules.TryAdd(rule.Id, rule))
            {
                throw new ArgumentException($"规则 id 重复：{rule.Id}");
            }
        }

        _conservation = conservationValidator ?? new PassThroughConservationValidator();
    }

    public IReadOnlyCollection<Rule> Rules => _rules.Values;

    public bool TryGetRule(string id, out Rule rule) => _rules.TryGetValue(id, out rule!);

    public Rule GetRequired(string id) =>
        _rules.TryGetValue(id, out var rule) ? rule : throw new KeyNotFoundException($"未登记的规则：{id}");

    /// <summary>激活规则：全部前提满足（默认假设也算满足，但会被记录）。</summary>
    public IReadOnlyList<ActivatedRule> Activate(FactContext context, IEnumerable<Rule>? scope = null)
    {
        var result = new List<ActivatedRule>();
        foreach (var rule in scope ?? _rules.Values)
        {
            if (TryActivate(rule, context, out var defaults))
            {
                result.Add(new ActivatedRule(rule, defaults));
            }
        }

        return result;
    }

    /// <summary>对给定上下文做一次完整裁决。ruleIds 非空时只在该子集内裁决（供推理层限定范围）。</summary>
    public Verdict Decide(FactContext context, IEnumerable<string>? ruleIds = null)
    {
        var trace = new List<DecisionTrace>();
        var rejected = new List<RejectedCandidate>();
        var scope = ruleIds is null
            ? _rules.Values.ToList()
            : ruleIds.Select(GetRequired).ToList();

        var activated = Activate(context, scope);
        trace.Add(new DecisionTrace("激活", activated.Count == 0
            ? $"上下文未激活任何规则：{context}"
            : $"激活 {activated.Count} 条：{string.Join(", ", activated.Select(a => a.Rule.Id))}"));

        if (activated.Count == 0)
        {
            return new Verdict { Kind = VerdictKind.NoMatch, Trace = trace };
        }

        // 步骤 1：L0 硬约束。守恒类规则作为约束生效，不参与竞争；结论违反守恒的候选直接淘汰。
        var constraints = new List<Rule>();
        var survivors = new List<ActivatedRule>();
        foreach (var candidate in activated)
        {
            if (candidate.Rule.Layer == RuleLayer.Conservation)
            {
                constraints.Add(candidate.Rule);
                continue;
            }

            var report = _conservation.Validate(candidate.Rule.Conclusion);
            if (!report.Ok)
            {
                rejected.Add(new RejectedCandidate(
                    candidate.Rule,
                    "L0 守恒",
                    string.Join("；", report.Errors)));
                continue;
            }

            survivors.Add(candidate);
        }

        if (constraints.Count > 0)
        {
            trace.Add(new DecisionTrace("L0 约束", $"生效：{string.Join(", ", constraints.Select(r => r.Id))}"));
        }

        // 步骤 2：教材一致性。待评审规则不得参与裁决。
        foreach (var candidate in survivors.Where(c => c.Rule.PendingReview).ToList())
        {
            rejected.Add(new RejectedCandidate(candidate.Rule, "教材一致性", "与教材表述冲突，待评审"));
            survivors.Remove(candidate);
        }

        // 步骤 3：例外覆盖。例外规则的触发条件已满足时，被它声明覆盖的一般规则退出竞争。
        var activeExceptionRules = survivors.Where(c => c.Rule.Layer == RuleLayer.Exception).Select(c => c.Rule).ToList();
        foreach (var exceptionRule in activeExceptionRules)
        {
            foreach (var overriddenId in exceptionRule.Overrides)
            {
                var victim = survivors.FirstOrDefault(c =>
                    string.Equals(c.Rule.Id, overriddenId, StringComparison.OrdinalIgnoreCase));
                if (victim is null)
                {
                    continue;
                }

                survivors.Remove(victim);
                rejected.Add(new RejectedCandidate(
                    victim.Rule,
                    "例外覆盖",
                    $"被例外规则 {exceptionRule.Id} 覆盖（其触发条件已满足）"));
            }
        }

        // 步骤 4：学段合规。存在必修候选时，延伸内容不作判定依据。
        if (survivors.Any(c => c.Rule.GradeScope == GradeScope.Required))
        {
            foreach (var extended in survivors.Where(c => c.Rule.GradeScope == GradeScope.Extended).ToList())
            {
                rejected.Add(new RejectedCandidate(extended.Rule, "学段合规", "延伸内容只作解释，不作判定依据"));
                survivors.Remove(extended);
            }
        }
        else if (survivors.Any(c => c.Rule.GradeScope == GradeScope.Elective))
        {
            foreach (var extended in survivors.Where(c => c.Rule.GradeScope == GradeScope.Extended).ToList())
            {
                rejected.Add(new RejectedCandidate(extended.Rule, "学段合规", "延伸内容只作解释，不作判定依据"));
                survivors.Remove(extended);
            }
        }

        if (survivors.Count == 0)
        {
            return new Verdict
            {
                Kind = VerdictKind.NoMatch,
                Rejected = rejected,
                AppliedConstraints = constraints,
                Trace = trace,
                Note = "所有候选均被淘汰"
            };
        }

        // 步骤 5：排序。层级 → 证据强度 → 学段 → 默认假设数量 → id（仅用于确定性，不用于裁决）。
        var ranked = survivors
            .OrderByDescending(c => (int)c.Rule.Layer)
            .ThenByDescending(c => EvidenceTypes.Strength(c.Rule.Evidence))
            .ThenBy(c => (int)c.Rule.GradeScope)
            .ThenBy(c => c.DefaultAssumptions.Count)
            .ThenBy(c => c.Rule.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var top = ranked[0];
        trace.Add(new DecisionTrace(
            "排序",
            $"首选 {top.Rule.Id}（层级 {top.Rule.Layer}，证据 {EvidenceTypes.Display(top.Rule.Evidence)}，" +
            $"学段 {top.Rule.GradeScope}，默认假设 {top.DefaultAssumptions.Count} 条）"));

        var tied = ranked.Where(c => SameRank(c, top)).Select(c => c.Rule).ToList();
        if (tied.Count > 1)
        {
            trace.Add(new DecisionTrace("并列", $"存在 {tied.Count} 条同等级候选，需补充条件或判别实验"));
            return new Verdict
            {
                Kind = VerdictKind.Unresolved,
                Selected = tied,
                Candidates = survivors.Select(c => c.Rule).ToList(),
                Rejected = rejected,
                AppliedConstraints = constraints,
                Trace = trace,
                Note = "规则优先级无法区分，禁止猜测，需判别信息"
            };
        }

        return new Verdict
        {
            Kind = VerdictKind.Selected,
            Selected = new[] { top.Rule },
            Candidates = survivors.Select(c => c.Rule).ToList(),
            Rejected = rejected,
            AppliedConstraints = constraints,
            Trace = trace
        };
    }

    private static bool SameRank(ActivatedRule a, ActivatedRule b) =>
        a.Rule.Layer == b.Rule.Layer &&
        a.Rule.Evidence == b.Rule.Evidence &&
        a.Rule.GradeScope == b.Rule.GradeScope &&
        a.DefaultAssumptions.Count == b.DefaultAssumptions.Count;

    private static bool TryActivate(Rule rule, FactContext context, out List<string> defaultAssumptions)
    {
        defaultAssumptions = new List<string>();
        foreach (var precondition in rule.Preconditions)
        {
            if (!IsSatisfied(precondition, context))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(precondition.DefaultAssumption))
            {
                defaultAssumptions.Add(precondition.DefaultAssumption!);
            }
        }

        return true;
    }

    private static bool IsSatisfied(RulePrecondition precondition, FactContext context)
    {
        var actual = context.Values(precondition.Key);
        if (precondition.Values.Count == 0)
        {
            return precondition.Negate ? !context.HasKey(precondition.Key) : context.HasKey(precondition.Key);
        }

        var hit = precondition.Values.Any(v => actual.Contains(v, StringComparer.OrdinalIgnoreCase));
        return precondition.Negate ? !hit : hit;
    }
}
