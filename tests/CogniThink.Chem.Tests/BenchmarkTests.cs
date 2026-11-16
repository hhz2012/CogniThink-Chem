using CogniThink.Chem.Core.Benchmark;
using Xunit;

namespace CogniThink.Chem.Tests;

/// <summary>
/// 标准测试集（推断类实际问题）基准测试。
/// 语义：推断动作属于 LLM 层；本测试用题目中的标准答案（expected）代替 LLM 输出，
/// 验证引擎对"标准答案放行、典型错解捕获、计算精确复算"的行为与预期一致。
/// 数据源：data/benchmark/problems/*.json（42 题，5 类）。
/// </summary>
public class BenchmarkTests
{
    private static readonly IReadOnlyList<ProblemSpec> All = BenchmarkLoader.LoadAll();

    public static IEnumerable<object[]> AllCaseIds() => All.Select(p => new object[] { p.Id });

    private static ProblemSpec ById(string id) => All.Single(p => p.Id == id);

    // ---------- 1. 数据集规模 ----------

    [Fact]
    public void Dataset_Loads_WithExpectedCounts()
    {
        Assert.True(All.Count >= 40, $"题目总数 {All.Count} < 40");
        foreach (var category in BenchmarkLoader.CategoryFiles.Keys)
        {
            var count = All.Count(p => p.Category == category);
            Assert.True(count >= 8, $"{category} 仅 {count} 题（< 8）");
        }
    }

    // ---------- 2. Schema 自检 ----------

    [Fact]
    public void Dataset_SchemaSelfCheck_Passes()
    {
        var issues = BenchmarkLoader.ValidateSchema(All);
        Assert.True(issues.Count == 0, string.Join("\n", issues));
    }

    [Fact]
    public void EquationCases_AllHaveDistractors()
    {
        // 含方程式的题目（产物/成分/过程/共存/带反应的计算）必须配干扰项
        var missing = All
            .Where(p => p.Expected.Equations.Count > 0 && p.Distractors.Count == 0)
            .Select(p => p.Id)
            .ToList();
        Assert.True(missing.Count == 0, "缺少干扰项：" + string.Join(", ", missing));
    }

    // ---------- 3. 逐题验证：引擎行为与预期一致 ----------

    [Theory]
    [MemberData(nameof(AllCaseIds))]
    public void BenchmarkCase_EngineBehavior_MatchesExpectation(string caseId)
    {
        var problem = ById(caseId);
        var result = BenchmarkVerifier.Verify(problem);
        Assert.True(result.Passed, $"{caseId}:\n{string.Join("\n", result.Mismatches)}");
    }

    // ---------- 4. 测试输出结构（BenchmarkResult）完整性 ----------

    [Fact]
    public void BenchmarkResult_OutputStructure_Complete()
    {
        var sample = ById("IDENT-002");
        var result = BenchmarkVerifier.Verify(sample);

        Assert.Equal(sample.Id, result.CaseId);
        Assert.Equal(sample.Category, result.Category);
        Assert.Equal(sample.Expected.Equations.Count, result.AnswerReports.Count);
        Assert.Equal(sample.Distractors.Count, result.DistractorOutcomes.Count);
        Assert.All(result.DistractorOutcomes, o => Assert.True(o.CaughtAsExpected, o.DistractorId));
        Assert.True(result.Passed);
    }

    [Fact]
    public void CalcResult_ExactRational_NoFloatingPointDrift()
    {
        // 计算题复算明细必须输出精确有理值（如 2.24 = 56/25），不允许浮点近似
        var sample = ById("CALC-003");
        var result = BenchmarkVerifier.Verify(sample);
        Assert.True(result.Passed, string.Join("\n", result.Mismatches));
        Assert.Contains("56/25", result.CalcDetail);
    }
}
