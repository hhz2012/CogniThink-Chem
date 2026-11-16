using CogniThink.Chem.Core.Data;
using CogniThink.Chem.Core.Diagnostics;
using CogniThink.Chem.Core.Evaluation;
using CogniThink.Chem.Core.Knowledge;
using CogniThink.Chem.Core.Rules;
using Xunit;

namespace CogniThink.Chem.Tests;

public class KnowledgeAndRuleTests
{
    private static readonly ChemData Data = ChemDataLoader.LoadAll();

    [Fact]
    public void KnowledgeBase_LoadsAndValidates()
    {
        var report = Data.Knowledge.Validate();
        Assert.True(report.Ok, report.ToString());
        Assert.True(Data.Knowledge.Count >= 40);
        Assert.Contains(Data.Knowledge.All, i => i.Id == "KH-IN-03" && i.Tier == KnowledgeTier.Main);
    }

    [Fact]
    public void KnowledgeBase_PrerequisiteChain_IsTransitiveAndCycleSafe()
    {
        var chain = Data.Knowledge.PrerequisiteChain("KH-RP-06").Select(i => i.Id).ToList();
        Assert.Contains("KH-RP-05", chain);
        Assert.Contains("KH-RP-04", chain);
        Assert.DoesNotContain("KH-RP-06", chain);
        Assert.Equal(chain.Count, chain.Distinct().Count());
    }

    [Fact]
    public void KnowledgeBase_SearchByModuleAndKeyword()
    {
        var organic = Data.Knowledge.Search(module: KnowledgeModule.Organic);
        Assert.Contains(organic, i => i.Id == "KH-OR-04");

        var hit = Data.Knowledge.Search(keyword: "水解");
        Assert.Contains(hit, i => i.Id == "KH-RP-06");
    }

    [Fact]
    public void RuleSet_HasNoErrorsOrConflicts()
    {
        var report = RuleSetValidator.Validate(Data.Rules, Data.Knowledge);
        Assert.True(report.Ok, report.ToString());
        Assert.Empty(ConflictScanner.Scan(Data.Rules));
    }

    [Fact]
    public void RuleSet_EveryRuleHasExplicitOrAutoTest()
    {
        var engine = new RuleEngine(Data.Rules);
        var runner = new RuleRegressionRunner(engine);
        var report = runner.Run(Data.RuleTests);

        Assert.True(report.Ok, string.Join(Environment.NewLine, report.Failures.Select(f => f.ToString())));
        Assert.Equal(Data.Rules.Count, report.Cases.Count(c => c.Id.StartsWith("auto:", StringComparison.Ordinal)));
    }

    [Fact]
    public void Arbitration_ExceptionOverridesGeneralRule()
    {
        var general = Rule("GEN-001", RuleLayer.Structural, Predicate("property", "bonding", "ionic"),
            Pre("compound_class", "salt"));
        var exception = Rule("EXC-001", RuleLayer.Exception, Predicate("property", "bonding", "covalent"),
            Pre("substance", "AlCl3"));
        exception.Overrides.Add("GEN-001");

        var engine = new RuleEngine(new[] { general, exception });
        var verdict = engine.Decide(new FactContext().Add("compound_class", "salt").Add("substance", "AlCl3"));

        Assert.Equal(VerdictKind.Selected, verdict.Kind);
        Assert.Equal("EXC-001", Assert.Single(verdict.Selected).Id);
        Assert.Contains(verdict.Rejected, r => r.Rule.Id == "GEN-001" && r.Stage == "例外覆盖");
    }

    [Fact]
    public void Arbitration_TieIsReportedAsUnresolved()
    {
        var a = Rule("A-001", RuleLayer.Structural, Predicate("property", "x", "a"), Pre("key", "v"));
        var b = Rule("B-001", RuleLayer.Structural, Predicate("property", "x", "b"), Pre("key", "v"));

        var verdict = new RuleEngine(new[] { a, b }).Decide(new FactContext().Add("key", "v"));

        Assert.Equal(VerdictKind.Unresolved, verdict.Kind);
        Assert.Equal(2, verdict.Selected.Count);
        Assert.NotNull(verdict.Note);
    }

    [Fact]
    public void Arbitration_L0ConservationViolationIsRejected()
    {
        var bad = Rule("BAD-001", RuleLayer.Structural, Predicate("reaction", "Na+Cl2", "Na+Cl2=NaCl"), Pre("key", "v"));

        var engine = new RuleEngine(new[] { bad }, new RejectingConservationValidator());
        var verdict = engine.Decide(new FactContext().Add("key", "v"));

        Assert.Equal(VerdictKind.NoMatch, verdict.Kind);
        Assert.Contains(verdict.Rejected, r => r.Rule.Id == "BAD-001" && r.Stage == "L0 守恒");
    }

    [Fact]
    public void Arbitration_ExtendedContentIsNotUsedForJudgement()
    {
        var required = Rule("REQ-001", RuleLayer.Structural, Predicate("property", "x", "a"), Pre("key", "v"));
        var extended = Rule("EXT-001", RuleLayer.Structural, Predicate("property", "x", "b"), Pre("key", "v"));
        extended.GradeScope = GradeScope.Extended;

        var verdict = new RuleEngine(new[] { required, extended }).Decide(new FactContext().Add("key", "v"));

        Assert.Equal(VerdictKind.Selected, verdict.Kind);
        Assert.Equal("REQ-001", Assert.Single(verdict.Selected).Id);
        Assert.Contains(verdict.Rejected, r => r.Rule.Id == "EXT-001" && r.Stage == "学段合规");
    }

    [Fact]
    public void Arbitration_RuleRelyingOnDefaultAssumptionLoses()
    {
        var withDefault = Rule("DEF-001", RuleLayer.Structural, Predicate("property", "x", "a"), Pre("key", "v"));
        withDefault.Preconditions.Add(new RulePrecondition
        {
            Key = "temperature",
            Values = { "room" },
            DefaultAssumption = "常温"
        });

        var explicitRule = Rule("EXP-001", RuleLayer.Structural, Predicate("property", "x", "b"), Pre("key", "v"));
        explicitRule.Preconditions.Add(Pre("phase", "aq"));

        var verdict = new RuleEngine(new[] { withDefault, explicitRule })
            .Decide(new FactContext().Add("key", "v").Add("temperature", "room").Add("phase", "aq"));

        Assert.Equal(VerdictKind.Selected, verdict.Kind);
        Assert.Equal("EXP-001", Assert.Single(verdict.Selected).Id);
    }

    [Fact]
    public void Arbitration_PendingReviewRuleIsExcluded()
    {
        var pending = Rule("PEN-001", RuleLayer.Structural, Predicate("property", "x", "a"), Pre("key", "v"));
        pending.PendingReview = true;

        var verdict = new RuleEngine(new[] { pending }).Decide(new FactContext().Add("key", "v"));

        Assert.Equal(VerdictKind.NoMatch, verdict.Kind);
        Assert.Contains(verdict.Rejected, r => r.Rule.Id == "PEN-001" && r.Stage == "教材一致性");
    }

    [Fact]
    public void ConflictScanner_DetectsNegatedConclusions()
    {
        var positive = Rule("P-001", RuleLayer.Structural, Predicate("solubility", "CaSO4", "soluble"), Pre("key", "v"));
        var negative = Rule("N-001", RuleLayer.Structural, Predicate("solubility", "CaSO4", "not:soluble"), Pre("key", "v"));
        positive.ConflictGroup = "g";
        negative.ConflictGroup = "g";

        var conflicts = ConflictScanner.Scan(new[] { positive, negative });

        Assert.Single(conflicts);
    }

    [Fact]
    public void RuleSetValidator_FlagsExceptionWithoutTrigger()
    {
        var exception = Rule("EXC-X", RuleLayer.Exception, Predicate("property", "x", "y"));
        var knowledge = new KnowledgeBase(new[]
        {
            new KnowledgeItem { Id = "KH-T", Name = "测试", TextbookRef = "测试" }
        });
        exception.KnowledgeId = "KH-T";

        var report = RuleSetValidator.Validate(new[] { exception }, knowledge);

        Assert.False(report.Ok);
        Assert.Contains(report.Errors, e => e.Contains("没有触发条件"));
    }

    private static Proposition Predicate(string predicate, string subject, string? obj) =>
        new() { Predicate = predicate, Subject = subject, Object = obj };

    private static RulePrecondition Pre(string key, string value) =>
        new() { Key = key, Values = { value } };

    private static Rule Rule(string id, RuleLayer layer, Proposition conclusion, params RulePrecondition[] preconditions)
    {
        var rule = new Rule
        {
            Id = id,
            Name = id,
            Layer = layer,
            KnowledgeId = "KH-T",
            Conclusion = conclusion,
            Evidence = EvidenceType.Qualitative,
            GradeScope = GradeScope.Required,
            Source = "测试"
        };
        rule.Preconditions.AddRange(preconditions);
        return rule;
    }

    private sealed class RejectingConservationValidator : IConservationValidator
    {
        public ValidationReport Validate(Proposition proposition)
        {
            var report = new ValidationReport("test");
            if (proposition.Predicate == "reaction")
            {
                report.AddError("原子不守恒");
            }

            return report;
        }
    }
}
