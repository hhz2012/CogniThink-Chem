using System.Collections.Generic;
using System.Linq;
using CogniThink.Chem.Core.Rules;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// 网关层概念事实确定性求解器（阶段6-B / AC-24）验收 + 护栏测试。
/// 真值源：data/highschool/concept-facts.json（唯一，不调 LLM）。
/// 重点验证：11 道干净 concept/unknown 纯事实题被解出且判正；任何边界外/歧义一律 null，不损害精确率、不帮倒忙。
/// </summary>
public class ConceptFactSolverTests
{
    private static readonly Lazy<List<QaTestCase>> AllCases =
        new(() => GatewayVsLlmEvaluationService.BuildTestCases());

    private static EngineResponse? Solve(string raw) =>
        ConceptFactSolver.TrySolve(new EngineRequest { TaskType = "concept", RawText = raw });

    private static QaTestCase? Case(string id) =>
        AllCases.Value.FirstOrDefault(x => x.Id == id);

    // ============================================================
    // 正例：11 道干净候选均触发且经答案键判正
    // ============================================================
    [Theory]
    [InlineData("test-1")]
    [InlineData("test-2")]
    [InlineData("test-3")]
    [InlineData("test-4")]
    [InlineData("test-5")]
    [InlineData("property-1")]
    [InlineData("property-2")]
    [InlineData("concept-1")]
    [InlineData("concept-2")]
    [InlineData("concept-3")]
    [InlineData("pdf-510")]
    public void 候选_触发且判正(string id)
    {
        var c = Case(id);
        Assert.NotNull(c);
        var r = Solve(c!.Question);
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.False(string.IsNullOrWhiteSpace(r.FinalResult));
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c),
            $"{id} 应通过判分；answer={r.FinalResult}");
    }

    // ============================================================
    // 负例：查不到 / 歧义 / 空 → 一律 null（不蒙数）
    // ============================================================
    [Theory]
    [InlineData("水的沸点是多少？")]                                   // 无任何触发
    [InlineData("检验 Fe3+ 的实验现象是什么？")]                     // 检验+fe3+ 但缺「试剂/方法」→ 不命中
    [InlineData("Fe3+ 如何制备？")]                                  // 含 fe3+ 但缺「颜色」→ 不命中
    [InlineData("金刚石的硬度很大")]                                 // 含金刚石但缺「熔点/c60」→ 不命中
    [InlineData("")]                                                 // 空 → null
    [InlineData("检验 Fe3+ 和 Cl- 分别用什么试剂？")]               // 同时命中 2 条 → 歧义 → null
    public void 非命中或歧义_返回null(string raw) => Assert.Null(Solve(raw));

    [Fact]
    public void 选择题不触发()
    {
        // 生产钩子仅介入 concept/unknown；此处直连求解器确认题库中选择题无一误触发。
        var fired = AllCases.Value
            .Where(c => c.Type == "choice")
            .Count(c => Solve(c.Question) is not null);
        Assert.Equal(0, fired);
    }

    // ============================================================
    // 护栏：遍历全题库（与生产一致用例集），触发集锁定且凡触发必判正
    // ============================================================
    [Fact]
    public void Guardrail_全题库触发集锁定且凡触发必判正()
    {
        var cases = AllCases.Value;
        Assert.NotEmpty(cases);

        var expected = new HashSet<string>(System.StringComparer.Ordinal)
        {
            "test-1","test-2","test-3","test-4","test-5",
            "property-1","property-2",
            "concept-1","concept-2","concept-3",
            "pdf-510"
        };

        var fired = new List<string>();
        var notCorrect = new List<string>();
        foreach (var c in cases)
        {
            var r = Solve(c.Question);
            if (r is null) continue;
            fired.Add(c.Id);
            if (!GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c))
                notCorrect.Add($"{c.Id} => {r.FinalResult}");
        }

        // 凡触发必与答案一致（零假阳性 → 精确率 100%、不帮倒忙）
        Assert.Empty(notCorrect);
        // 触发集锁定：任何新增触发都必须先判明正确性
        Assert.Equal(expected.OrderBy(x => x), fired.Distinct().OrderBy(x => x));
    }

    // ============================================================
    // 数据驱动：事实全在 JSON，逻辑零硬编码
    // ============================================================
    [Fact]
    public void 事实库_11条且分类分布符合预期()
    {
        var data = RuleStore.Load().Query<ConceptFactData>("concept-facts");
        Assert.Equal(11, data.Entries.Count);
        Assert.Equal(5, data.Entries.Count(e => e.Kind == "ion_test"));
        Assert.Equal(2, data.Entries.Count(e => e.Kind == "ion_color"));
        Assert.Equal(3, data.Entries.Count(e => e.Kind == "concept_def"));
        Assert.Equal(1, data.Entries.Count(e => e.Kind == "crystal_compare"));
        // 每条事实必须有触发条件与非空答案
        Assert.All(data.Entries, e =>
        {
            Assert.NotEmpty(e.RequiredAll);
            Assert.False(string.IsNullOrWhiteSpace(e.Answer));
        });
    }
}
