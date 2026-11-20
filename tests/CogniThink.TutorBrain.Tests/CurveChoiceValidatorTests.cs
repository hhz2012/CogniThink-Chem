using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// Task 41：CurveChoiceValidator 护栏测试。
/// 真值源：<see cref="CurveQuery"/>（点集数学）。红线：零假阳性 / 零 harm——
/// 校验器凡返回非 null，其结论必被真实判分 <see cref="GatewayVsLlmEvaluationService.IsAnswerCorrect"/> 判为正确。
/// 6 道保底题（t4cur-01~06）用可控标注轴数据构造，答案由镜像 CurveQuery 数学自洽算出；
/// 真实曲线题（轴未标注 / 需化学推理）诚实 null 或必正确。
/// </summary>
public class CurveChoiceValidatorTests
{
    private static readonly List<QaTestCase> AllCases = GatewayVsLlmEvaluationService.BuildTestCases();

    /// <summary>6 道保底题的预期字母（Python 端镜像 CurveQuery 数学自洽核对）。</summary>
    public static readonly IReadOnlyDictionary<string, string> CurTruth = new Dictionary<string, string>
    {
        ["t4cur-01"] = "A", ["t4cur-02"] = "A", ["t4cur-03"] = "A",
        ["t4cur-04"] = "A", ["t4cur-05"] = "A", ["t4cur-06"] = "B",
    };

    public static IEnumerable<object[]> CurCases => CurTruth.Select(kv => new object[] { kv.Key, kv.Value });

    private static EngineRequest Req(QaTestCase c) => new() { RawText = c.Question, TaskType = "choice" };

    // ==================== 保底题直解（gain + 字母正确）====================

    [Theory]
    [MemberData(nameof(CurCases))]
    public void Cur_Solves_KnownBaseline(string id, string expected)
    {
        var c = AllCases.FirstOrDefault(x => x.Id == id);
        Assert.NotNull(c);
        var r = CurveChoiceValidator.TryDecide(Req(c!), c!.ImageAssets);
        Assert.NotNull(r);
        Assert.Equal(expected, r!.AnswerLetter);
    }

    // ==================== 全题库护栏（零 harm / 零假阳性）====================

    [Fact]
    public void Cur_ZeroHarm_AcrossAllBuildTestCases()
    {
        // 红线：凡非 null 必被真实判分判为正确。真实曲线题（轴未标注/需推理）诚实 null 或必正确。
        foreach (var c in AllCases)
        {
            var r = CurveChoiceValidator.TryDecide(Req(c), c.ImageAssets);
            if (r is null) continue;
            Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c),
                $"[harm] {c.Id} 结论 [{r.FinalResult}] 未被判为正确（type={c.Type}, answer=[{c.Answer}]）");
        }
    }

    [Fact]
    public void Cur_AllSixBaseline_AreGained()
    {
        foreach (var kv in CurTruth)
        {
            var c = AllCases.FirstOrDefault(x => x.Id == kv.Key);
            Assert.NotNull(c);
            var r = CurveChoiceValidator.TryDecide(Req(c!), c!.ImageAssets);
            Assert.NotNull(r);
            Assert.Equal(kv.Value, r!.AnswerLetter);
        }
    }

    // ==================== 端到端：经 CallEngineAsync 被网关采纳且判正 ====================

    [Fact]
    public async Task Cur_GatewayEndToEnd_AdoptsAndGradesCorrect()
    {
        var gw = (LlmGatewayService)RuntimeHelpers.GetUninitializedObject(typeof(LlmGatewayService));
        foreach (var kv in CurTruth)
        {
            var c = AllCases.First(x => x.Id == kv.Key);
            var resp = await gw.CallEngineAsync(Req(c), c.ImageAssets);
            Assert.True(resp.Ok == true, $"{kv.Key} 网关未返回 Ok：{resp.FinalResult}");
            Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(resp.FinalResult, c),
                $"[harm-e2e] {kv.Key} 网关结论 [{resp.FinalResult}] 未判正（answer=[{c.Answer}]）");
        }
    }

    // ==================== 域外守卫（返回 null，不蒙数）====================

    [Fact]
    public void Cur_NoCurveAsset_ReturnsNull()
    {
        var c = AllCases.First(x => x.Id == "t4cur-01");
        var r = CurveChoiceValidator.TryDecide(Req(c), new List<ImageAsset>());
        Assert.Null(r);
    }

    [Fact]
    public void Cur_UnlabeledAxis_ReturnsNull()
    {
        // 轴未标注 → 归一化 0~1，不能比对真实数值 → 拒绝
        using var doc = JsonDocument.Parse(
            "{\"xLabel\":\"x\",\"yLabel\":\"y\",\"xAxisLabeled\":false,\"yAxisLabeled\":true," +
            "\"series\":[{\"name\":\"甲\",\"points\":[[0,1],[1,2],[2,4]]}]}");
        var asset = new ImageAsset { Type = "curve", Payload = doc.RootElement.Clone() };
        var c = AllCases.First(x => x.Id == "t4cur-01");
        var r = CurveChoiceValidator.TryDecide(Req(c), new List<ImageAsset> { asset });
        Assert.Null(r);
    }

    [Fact]
    public void Cur_OptionOutsideSupportedAssertions_ReturnsNull()
    {
        // 含定性断言（无显式数值）→ 不可判 → 整题 null
        var raw = "如图为某曲线，下列说法正确的是（ ）\nA. 当 x=2 时，曲线甲 的 y 为 4\nB. 曲线甲先增后减\nC. 曲线甲 的 y 最大值 为 5\nD. 当 x=1 时，曲线甲 的 y 为 2";
        var c = AllCases.First(x => x.Id == "t4cur-02");
        var r = CurveChoiceValidator.TryDecide(new EngineRequest { RawText = raw, TaskType = "choice" }, c.ImageAssets);
        Assert.Null(r);
    }

    [Fact]
    public void Cur_StemNotPointingAtFigure_ReturnsNull()
    {
        var raw = "下列说法正确的是（ ）\nA. 当 x=2 时，曲线甲 的 y 为 4\nB. 曲线甲 的 y 最大值 为 6\nC. 曲线甲 的 y 最大值 为 3\nD. 曲线甲 的 y 最大值 为 4";
        var c = AllCases.First(x => x.Id == "t4cur-02");
        var r = CurveChoiceValidator.TryDecide(new EngineRequest { RawText = raw, TaskType = "choice" }, c.ImageAssets);
        Assert.Null(r);
    }
}
