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
/// Task 40：MoleculeChoiceValidator 护栏测试。
/// 真值源：<see cref="MoleculeAnalyzer"/>（RDKit）。红线：零假阳性 / 零 harm——
/// 校验器凡返回非 null，其结论必被真实判分 <see cref="GatewayVsLlmEvaluationService.IsAnswerCorrect"/> 判为正确。
/// 10 道保底题（t4mol-01~10）经 RDKit 真值逐题核对；28 道 molecule 真题诚实 null 或必正确。
/// </summary>
public class MoleculeChoiceValidatorTests
{
    private static readonly List<QaTestCase> AllCases = GatewayVsLlmEvaluationService.BuildTestCases();

    /// <summary>10 道保底题的预期字母（与 RDKit 真值逐题核对一致）。</summary>
    public static readonly IReadOnlyDictionary<string, string> MolTruth = new Dictionary<string, string>
    {
        ["t4mol-01"] = "A", ["t4mol-02"] = "C", ["t4mol-03"] = "A", ["t4mol-04"] = "C",
        ["t4mol-05"] = "A", ["t4mol-06"] = "D", ["t4mol-07"] = "B", ["t4mol-08"] = "C",
        ["t4mol-09"] = "A", ["t4mol-10"] = "C",
    };

    public static IEnumerable<object[]> MolCases => MolTruth.Select(kv => new object[] { kv.Key, kv.Value });

    private static EngineRequest Req(QaTestCase c) => new() { RawText = c.Question, TaskType = "choice" };

    // ==================== 保底题直解（gain + 字母正确）====================

    [Theory]
    [MemberData(nameof(MolCases))]
    public void Mol_Solves_KnownBaseline(string id, string expected)
    {
        var c = AllCases.FirstOrDefault(x => x.Id == id);
        Assert.NotNull(c); // 保底题必须已并入题库
        var r = MoleculeChoiceValidator.TryDecide(Req(c!), c!.ImageAssets);
        Assert.NotNull(r);
        Assert.Equal(expected, r!.AnswerLetter);
    }

    // ==================== 全题库护栏（零 harm / 零假阳性）====================

    [Fact]
    public void Mol_ZeroHarm_AcrossAllBuildTestCases()
    {
        // 红线：凡非 null 必被真实判分判为正确。28 道 molecule 真题诚实 null 或必正确。
        foreach (var c in AllCases)
        {
            var r = MoleculeChoiceValidator.TryDecide(Req(c), c.ImageAssets);
            if (r is null) continue;
            Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c),
                $"[harm] {c.Id} 结论 [{r.FinalResult}] 未被判为正确（type={c.Type}, answer=[{c.Answer}]）");
        }
    }

    [Fact]
    public void Mol_AllTenBaseline_AreGained()
    {
        // 增量核对：10 道保底题必须全部被解出且字母正确（gain，不得丢失）。
        foreach (var kv in MolTruth)
        {
            var c = AllCases.FirstOrDefault(x => x.Id == kv.Key);
            Assert.NotNull(c);
            var r = MoleculeChoiceValidator.TryDecide(Req(c!), c!.ImageAssets);
            Assert.NotNull(r);
            Assert.Equal(kv.Value, r!.AnswerLetter);
        }
    }

    // ==================== 端到端：经 CallEngineAsync 被网关采纳且判正 ====================

    [Fact]
    public async Task Mol_GatewayEndToEnd_AdoptsAndGradesCorrect()
    {
        // 免 LLM：GetUninitializedObject 构造网关；MoleculeChoiceValidator 静态无状态，
        // 在 Concept 之后短路返回，不触发依赖 _chem 的下游校验器。
        var gw = (LlmGatewayService)RuntimeHelpers.GetUninitializedObject(typeof(LlmGatewayService));
        foreach (var kv in MolTruth)
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
    public void Mol_NoMoleculeAsset_ReturnsNull()
    {
        var c = AllCases.First(x => x.Id == "t4mol-01");
        var r = MoleculeChoiceValidator.TryDecide(Req(c), new List<ImageAsset>());
        Assert.Null(r);
    }

    [Fact]
    public void Mol_InvalidAsset_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("{\"valid\":false,\"smiles\":\"c1ccccc1\"}");
        var asset = new ImageAsset { Type = "molecule", Payload = doc.RootElement.Clone() };
        var c = AllCases.First(x => x.Id == "t4mol-01");
        var r = MoleculeChoiceValidator.TryDecide(Req(c), new List<ImageAsset> { asset });
        Assert.Null(r);
    }

    [Fact]
    public void Mol_StemNotPointingAtFigure_ReturnsNull()
    {
        // 题干不含「如图/结构简式/所示」等指向词 → 守卫拒绝，避免误伤纯文字有机题。
        var raw = "下列关于苯的说法正确的是（ ）\nA. 分子式为 C6H6\nB. 不饱和度为 4\nC. 含有苯环\nD. 是极性分子";
        var c = AllCases.First(x => x.Id == "t4mol-01");
        var r = MoleculeChoiceValidator.TryDecide(new EngineRequest { RawText = raw, TaskType = "choice" }, c.ImageAssets);
        Assert.Null(r);
    }

    [Fact]
    public void Mol_OptionOutsideSupportedAssertions_ReturnsNull()
    {
        // 含一个无法归入支持断言的选项（共平面几何，RDKit 2D 不可靠）→ 整题 null（不蒙数）。
        var raw = "如图为苯的结构简式，下列说法正确的是（ ）\nA. 分子式为 C6H6\nB. 所有碳原子共平面\nC. 含有苯环\nD. 不饱和度为 4";
        var c = AllCases.First(x => x.Id == "t4mol-01");
        var r = MoleculeChoiceValidator.TryDecide(new EngineRequest { RawText = raw, TaskType = "choice" }, c.ImageAssets);
        Assert.Null(r);
    }
}
