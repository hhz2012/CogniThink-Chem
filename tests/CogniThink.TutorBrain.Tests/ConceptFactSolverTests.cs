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
/// 重点验证：25 道干净 concept/unknown 纯事实题被解出且判正；任何边界外/歧义一律 null，不损害精确率、不帮倒忙。
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
    // 正例：25 道干净候选均触发且经答案键判正
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
    [InlineData("d2-1")]
    [InlineData("d2-2")]
    [InlineData("d2-3")]
    [InlineData("d7-1")]
    [InlineData("d7-2")]
    [InlineData("d7-3")]
    [InlineData("d7-4")]
    [InlineData("d8-1")]
    [InlineData("d8-2")]
    [InlineData("d8-3")]
    [InlineData("d8-4")]
    [InlineData("d9-2")]
    [InlineData("d9-3")]
    [InlineData("d9-4")]
    [InlineData("pdf-516")]
    [InlineData("pdf-465")]
    [InlineData("pdf-389")]
    [InlineData("pdf-536")]
    [InlineData("pdf-415")]
    [InlineData("pdf-530")]
    [InlineData("pdf-535")]
    [InlineData("pdf-542")]
    [InlineData("pdf-543")]
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
            "pdf-510",
            "d2-1","d2-2","d2-3",
            "d7-1","d7-2","d7-3","d7-4",
            "d8-1","d8-2","d8-3","d8-4",
            "d9-2","d9-3","d9-4",
            "pdf-516",
            "pdf-278", // 题干补全后 Concept 正确解出子问(3)“KHC2O4 溶液呈酸性”(Ka2>Kb)，凡触发必判正
            "pdf-465","pdf-389","pdf-536",
            "pdf-415","pdf-530","pdf-535","pdf-542","pdf-543" // P1-4：自由填空概念事实（唯一匹配 + 0 漂移 + 答案键命中，凡触发必判正）
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
    public void 事实库_34条且分类分布符合预期()
    {
        var data = RuleStore.Load().Query<ConceptFactData>("concept-facts");
        Assert.Equal(34, data.Entries.Count);
        Assert.Equal(5, data.Entries.Count(e => e.Kind == "ion_test"));
        Assert.Equal(2, data.Entries.Count(e => e.Kind == "ion_color"));
        Assert.Equal(3, data.Entries.Count(e => e.Kind == "concept_def"));
        Assert.Equal(1, data.Entries.Count(e => e.Kind == "crystal_compare"));
        Assert.Equal(3, data.Entries.Count(e => e.Kind == "redox_product"));
        Assert.Equal(2, data.Entries.Count(e => e.Kind == "isomer_count"));
        Assert.Equal(1, data.Entries.Count(e => e.Kind == "intermolecular_force"));
        Assert.Equal(1, data.Entries.Count(e => e.Kind == "air_sensitive"));
        Assert.Equal(2, data.Entries.Count(e => e.Kind == "equiv_hydrogen"));
        Assert.Equal(1, data.Entries.Count(e => e.Kind == "chiral_center"));
        Assert.Equal(2, data.Entries.Count(e => e.Kind == "electrode_product"));
        Assert.Equal(1, data.Entries.Count(e => e.Kind == "ion_migration"));
        Assert.Equal(1, data.Entries.Count(e => e.Kind == "electrode_polarity"));
        Assert.Equal(1, data.Entries.Count(e => e.Kind == "bleaching"));
        Assert.Equal(1, data.Entries.Count(e => e.Kind == "colloid_property"));
        Assert.Equal(1, data.Entries.Count(e => e.Kind == "oxidizing_gas_test"));
        Assert.Equal(1, data.Entries.Count(e => e.Kind == "acid_base_property"));
        Assert.Equal(4, data.Entries.Count(e => e.Kind == "flow_concept"));
        Assert.Equal(1, data.Entries.Count(e => e.Kind == "lab_concept"));
        // 每条事实必须有触发条件与非空答案
        Assert.All(data.Entries, e =>
        {
            Assert.NotEmpty(e.RequiredAll);
            Assert.False(string.IsNullOrWhiteSpace(e.Answer));
        });
    }
}
