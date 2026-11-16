using CogniThink.Chem.Core.Rules;

namespace CogniThink.Chem.Core.Evaluation;

/// <summary>单条用例结果。</summary>
public sealed class RuleCaseResult
{
    public RuleCaseResult(string id, bool passed, string expected, string actual, string? message = null)
    {
        Id = id;
        Passed = passed;
        Expected = expected;
        Actual = actual;
        Message = message;
    }

    public string Id { get; }

    public bool Passed { get; }

    public string Expected { get; }

    public string Actual { get; }

    public string? Message { get; }

    public override string ToString() =>
        $"{(Passed ? "PASS" : "FAIL")} {Id} 期望[{Expected}] 实际[{Actual}]" +
        (Message is null ? "" : $" {Message}");
}

/// <summary>规则回归报告。发布时要求全部通过。</summary>
public sealed class RuleRegressionReport
{
    public RuleRegressionReport(IReadOnlyList<RuleCaseResult> cases)
    {
        Cases = cases;
    }

    public IReadOnlyList<RuleCaseResult> Cases { get; }

    public int Total => Cases.Count;

    public int Passed => Cases.Count(c => c.Passed);

    public int Failed => Total - Passed;

    public bool Ok => Failed == 0;

    public IReadOnlyList<RuleCaseResult> Failures => Cases.Where(c => !c.Passed).ToList();

    public override string ToString() =>
        $"规则回归：{Passed}/{Total} 通过" + (Ok ? "" : $"，失败 {Failed} 条");
}

/// <summary>
/// 规则回归器。除了显式用例，还会为每条规则生成"激活冒烟用例"：
/// 构造满足其全部前提的上下文，要求该规则能被激活。这样"每条规则都必须被执行到"。
/// </summary>
public sealed class RuleRegressionRunner
{
    private readonly RuleEngine _engine;

    public RuleRegressionRunner(RuleEngine engine)
    {
        _engine = engine;
    }

    public RuleRegressionReport Run(IEnumerable<RuleTestCase> cases, bool includeAutoActivation = true)
    {
        var results = new List<RuleCaseResult>();

        foreach (var testCase in cases)
        {
            results.Add(RunCase(testCase));
        }

        if (includeAutoActivation)
        {
            results.AddRange(RunAutoActivation());
        }

        return new RuleRegressionReport(results);
    }

    private RuleCaseResult RunCase(RuleTestCase testCase)
    {
        var verdict = _engine.Decide(testCase.ToContext());
        var actualKind = verdict.Kind.ToString();
        var actualIds = verdict.Selected.Select(r => r.Id).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        var expectedIds = testCase.ExpectedSelected.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        var expectedKind = string.IsNullOrWhiteSpace(testCase.ExpectedKind) ? "Selected" : testCase.ExpectedKind!;

        var pass = string.Equals(actualKind, expectedKind, StringComparison.OrdinalIgnoreCase) &&
                   actualIds.SequenceEqual(expectedIds, StringComparer.OrdinalIgnoreCase);

        var expected = $"{expectedKind}:{string.Join(",", expectedIds)}";
        var actual = $"{actualKind}:{string.Join(",", actualIds)}";
        return new RuleCaseResult(testCase.Id, pass, expected, actual, testCase.Note);
    }

    private IEnumerable<RuleCaseResult> RunAutoActivation()
    {
        foreach (var rule in _engine.Rules.OrderBy(r => r.Id, StringComparer.OrdinalIgnoreCase))
        {
            var context = BuildSatisfyingContext(rule);
            var activated = _engine.Activate(context);
            var pass = activated.Any(a => string.Equals(a.Rule.Id, rule.Id, StringComparison.OrdinalIgnoreCase));
            yield return new RuleCaseResult(
                $"auto:{rule.Id}",
                pass,
                $"{rule.Id} 可激活",
                pass ? "已激活" : "未激活",
                pass ? null : context.ToString());
        }
    }

    private static FactContext BuildSatisfyingContext(Rule rule)
    {
        var context = new FactContext();
        foreach (var precondition in rule.Preconditions)
        {
            if (precondition.Negate || precondition.Values.Count == 0)
            {
                continue;
            }

            context.Add(precondition.Key, precondition.Values[0]);
        }

        return context;
    }
}
