using System.Text.Json;
using System.Text.RegularExpressions;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// T1 全量回归护栏：加载 _evalscratch/task_inputs/T1.json（98 题），逐题调用
/// IonicEquationValidator（优先）→ RedoxChoiceValidator，断言：
/// <list type="bullet">
/// <item>已解题（经 llmReference 人工核验）返回期望字母；</item>
/// <item>其余所有题（含非选择题、概念题）必须返回 null（诚实拒答，零假阳性）。</item>
/// </list>
/// 任何未在白名单中的题返回非 null，或白名单题返回错误字母，即判失败。
/// </summary>
public class T1CoverageTests
{
    private static EngineRequest Req(string raw) => new() { TaskType = "choice", RawText = raw };

    /// <summary>已确定性解出的题（id → 期望字母）。每一项均与 expected + llmReference 人工核对一致。</summary>
    private static readonly Dictionary<string, string> Solved = new()
    {
        // IonicEquationValidator — 离子方程式正误
        ["pdf-035"] = "B",
        // pdf-050 / pdf-052 暂列白名单外：引擎会因「状态符号」归一化噪声触发零假阳性护栏而诚实返回 null。
        // 真正解出需向 infeasible-equation-patterns.json 追加化学规则（稀硝酸→NO、FeS 不拆、弱酸不拆），
        // 并配护栏单测验证零回归/零假阳性后再纳入。当前返回 null 非回归（tier2/stage6 亦未解出）。
        // IonicEquationValidator — 离子共存
        ["pdf-054"] = "B",
        ["pdf-055"] = "D",
        ["pdf-057"] = "B",
        ["pdf-058"] = "B",
        ["pdf-061"] = "C",
        ["pdf-062"] = "B",
        // RedoxChoiceValidator — 电极反应/半反应/方程 正误
        ["pdf-046"] = "A",
        ["pdf-053"] = "B",
        ["pdf-099"] = "A",
    };

    private static string? ExtractExpectedLetter(string expected)
    {
        var m = Regex.Match(expected ?? "", @"关键词[:：]\s*([A-D])\b");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string CombinedDecide(string question)
    {
        var o = IonicEquationValidator.TryDecide(Req(question));
        if (o is not null) return o.AnswerLetter;
        var r = RedoxChoiceValidator.TryDecide(Req(question));
        return r is null ? "null" : r.AnswerLetter;
    }

    public static IEnumerable<object[]> AllT1Items()
    {
        var repoRoot = ChemistryFixture.RepoRoot;
        var jsonPath = Path.Combine(repoRoot, "_evalscratch", "task_inputs", "T1.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var id = el.GetProperty("id").GetString()!;
            var question = el.GetProperty("question").GetString()!;
            var expected = el.TryGetProperty("expected", out var ep) ? ep.GetString() ?? "" : "";
            yield return new object[] { id, question, expected };
        }
    }

    [Theory]
    [MemberData(nameof(AllT1Items))]
    public void Every_T1_question_matches_coverage_contract(string id, string question, string expected)
    {
        var ans = CombinedDecide(question);

        if (Solved.TryGetValue(id, out var want))
        {
            // 白名单题：必须返回期望字母，且与 expected 字段一致
            Assert.Equal(want, ans);
            var exp = ExtractExpectedLetter(expected);
            Assert.True(exp is not null, $"{id}: expected 字段未解析出字母：{expected}");
            Assert.Equal(exp, ans);
        }
        else
        {
            // 非白名单题：必须诚实返回 null（零假阳性）
            Assert.Equal("null", ans);
        }
    }

    [Fact]
    public void Solved_count_is_10_and_letters_valid()
    {
        Assert.Equal(10, Solved.Count);
        Assert.All(Solved.Values, v => Assert.Contains(v, new[] { "A", "B", "C", "D" }));
    }
}
