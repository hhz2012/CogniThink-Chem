using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// CrystalCellValidator 聚焦单元测试（Spec: crystal-cell-validator, AC-1/AC-6/AC-8, FR-1~FR-6）。
/// 用合成资产验证均摊 / 配位数 / 密度 / 密度比数学与关键词命中；域外 / 缺数据 → null。
/// </summary>
public class CrystalCellValidatorTests
{
    private static ImageAsset Crystal(string json) =>
        new() { Type = "crystal", Source = "x.png", Payload = JsonDocument.Parse(json).RootElement };

    private static EngineResponse? Solve(string text, params ImageAsset[] assets) =>
        CrystalCellValidator.TrySolve(new EngineRequest { RawText = text, TaskType = "unknown" }, assets);

    [Fact]
    public void NiO_Count_Ni_Is4()
    {
        var asset = Crystal(@"{""key"":""NiO"",""label"":""NiO（岩盐）"",""basis"":[{""species"":""Ni"",""site"":""corner"",""count"":8},{""species"":""Ni"",""site"":""face"",""count"":6}]}");
        var resp = Solve("NiO 晶胞如右图所示，晶胞中含有 Ni2+的个数为________。", asset);
        Assert.NotNull(resp);
        Assert.True(resp!.Ok);
        Assert.Equal("4", resp.FinalResult);
    }

    [Fact]
    public void GrayTin_Coordination_And_WhiteGray_DensityRatio()
    {
        var white = Crystal(@"{""key"":""白锡"",""label"":""白锡（β-Sn）"",""molarMass"":119.0,""basis"":[{""species"":""Sn"",""site"":""corner"",""count"":8,""molarMass"":119.0},{""species"":""Sn"",""site"":""body"",""count"":1,""molarMass"":119.0}]}");
        var gray = Crystal(@"{""key"":""灰锡"",""label"":""灰锡（α-Sn）"",""molarMass"":119.0,""coordinationNumber"":4,""basis"":[{""species"":""Sn"",""site"":""corner"",""count"":8,""molarMass"":119.0},{""species"":""Sn"",""site"":""face"",""count"":6,""molarMass"":119.0},{""species"":""Sn"",""site"":""internal"",""count"":4,""molarMass"":119.0}]}");
        var resp = Solve("①灰锡配位数为____。②白锡和灰锡的密度之比为____。", white, gray);
        Assert.NotNull(resp);
        Assert.Contains("4", resp!.FinalResult);
        Assert.Contains("V2/4V1", resp.FinalResult);
    }

    [Fact]
    public void P4_Count16_And_Density_Matches_Keyword()
    {
        var asset = Crystal(@"{""key"":""P4"",""label"":""白磷 P₄（分子晶体）"",""a"":1.0,""atomsPerFormulaUnit"":4,""basis"":[{""species"":""P4"",""site"":""corner"",""count"":8,""molarMass"":124.0},{""species"":""P4"",""site"":""face"",""count"":6,""molarMass"":124.0}]}");
        var resp = Solve("该晶胞中含有的P原子的个数为____，则该晶体的密度为____g·cm^-3。", asset);
        Assert.NotNull(resp);
        Assert.Contains("16", resp!.FinalResult);

        // 用 pdf-222 的真实答案关键词校验 IsAnswerCorrect（归一化子串命中）
        var case222 = new QaTestCase
        {
            Type = "fill",
            Answer = "4.96×10^23 / (N_A · a^3)",
            AnswerKeywords = new List<string> { "4.96×10^23 / (N_A · a^3)" }
        };
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(resp.FinalResult, case222));
    }

    [Fact]
    public void NonCrystal_Asset_ReturnsNull()
    {
        var asset = new ImageAsset { Type = "molecule", Source = "x.png" };
        Assert.Null(Solve("晶胞中 Ni 的个数为", asset));
    }

    [Fact]
    public void NoCrystalIntent_ReturnsNull()
    {
        var asset = Crystal(@"{""key"":""NiO"",""basis"":[{""species"":""Ni"",""site"":""corner"",""count"":8}]}");
        Assert.Null(Solve("下列说法正确的是", asset));
    }

    [Fact]
    public void ChoiceTaskType_ReturnsNull()
    {
        var asset = Crystal(@"{""key"":""NiO"",""basis"":[{""species"":""Ni"",""site"":""corner"",""count"":8}]}");
        var resp = CrystalCellValidator.TrySolve(new EngineRequest { RawText = "晶胞中 Ni 的个数为", TaskType = "choice" }, new[] { asset });
        Assert.Null(resp);
    }

    [Fact]
    public void AsksCN_ButNoCoordNumberFact_ReturnsNull_NoFabrication()
    {
        var asset = Crystal(@"{""key"":""X"",""basis"":[{""species"":""A"",""site"":""corner"",""count"":8}]}");
        Assert.Null(Solve("X 的配位数为", asset));
    }

    [Fact]
    public void AsksDensity_ButMissingMolarMass_ReturnsNull()
    {
        var asset = Crystal(@"{""key"":""X"",""a"":1.0,""basis"":[{""species"":""A"",""site"":""corner"",""count"":8}]}");
        Assert.Null(Solve("晶体的密度为", asset));
    }

    [Fact]
    public void SymbolicRatio_DifferentMass_KeepsMassSymbols()
    {
        var a = Crystal(@"{""key"":""A"",""molarMass"":100.0,""basis"":[{""species"":""A"",""site"":""corner"",""count"":8,""molarMass"":100.0}]}");
        var b = Crystal(@"{""key"":""B"",""molarMass"":50.0,""basis"":[{""species"":""B"",""site"":""corner"",""count"":8,""molarMass"":50.0}]}");
        var resp = Solve("A 和 B 的密度之比为", a, b);
        Assert.NotNull(resp);
        Assert.Contains("M1", resp!.FinalResult);
        Assert.Contains("M2", resp.FinalResult);
    }

    private const string Mn3SbAssetJson =
        @"{""key"":""Mn3Sb"",""label"":""锑锰 Mn₃Sb（立方）"",""a"":1.0,""molarMass"":287.0,""formulaUnits"":1,""coordinationNumber"":12,""basis"":[{""species"":""Sb"",""site"":""corner"",""count"":8,""molarMass"":122.0},{""species"":""Mn"",""site"":""face"",""count"":6,""molarMass"":55.0}]}";

    [Fact]
    public void Mn3Sb_Coordination_ViaZhouWeiZuiJin_Wording_Returns12()
    {
        // pdf-178 题面不含「配位数」三字，用「每个 Sb 周围与它最近且相等距离的 Mn」表述 → 扩宽意图应识别
        var asset = Crystal(Mn3SbAssetJson);
        var resp = Solve("一种锑锰(Mn3Sb)合金的立方晶胞结构如右图所示。①该晶胞中，每个 Sb 周围与它最近且相等距离的 Mn 有______个。", asset);
        Assert.NotNull(resp);
        Assert.True(resp!.Ok);
        Assert.Contains("12", resp.FinalResult);

        var case178 = new QaTestCase
        {
            Type = "fill",
            Answer = "(1)12 (2)122×1×55×3 / (NA×10^-7)^3",
            AnswerKeywords = new List<string> { "12", "2.87×10^23 / (N_A · a^3)", "122×1×55×3 / (NA×10^-7)^3" }
        };
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(resp.FinalResult, case178));
    }

    [Fact]
    public void Mn3Sb_Density_FormulaUnits_MatchesOfficialEquivalent()
    {
        // 多物种 basis：Z(cell)=4（总原子），密度必须用 formulaUnits=1 → 2.87×10^23/(N_A·a³)，等价于官方 122×1×55×3/(NA×10^-7)^3
        var asset = Crystal(Mn3SbAssetJson);
        var resp = Solve("NA 为阿伏加德罗常数的值，晶胞边长为 a nm，则 Mn3Sb 晶体的密度为____g/cm^3（列出计算式即可）。", asset);
        Assert.NotNull(resp);
        Assert.Contains("2.87×10^23 / (N_A · a^3)", resp!.FinalResult);

        var case178 = new QaTestCase
        {
            Type = "fill",
            Answer = "(1)12 (2)122×1×55×3 / (NA×10^-7)^3",
            AnswerKeywords = new List<string> { "12", "2.87×10^23 / (N_A · a^3)", "122×1×55×3 / (NA×10^-7)^3" }
        };
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(resp.FinalResult, case178));
    }

    [Fact]
    public void BroadenCnIntent_DoesNotChange_NiO_Output_ZeroRegression()
    {
        // NiO 题面既无「配位数」也无「周围+最近」→ asksCN 仍为 false，产出仅均摊数 4（精确相等，无多余配位片段）
        var asset = Crystal(@"{""key"":""NiO"",""label"":""NiO（岩盐）"",""basis"":[{""species"":""Ni"",""site"":""corner"",""count"":8},{""species"":""Ni"",""site"":""face"",""count"":6}]}");
        var resp = Solve("NiO 晶胞如右图所示，晶胞中含有 Ni2+的个数为________。", asset);
        Assert.NotNull(resp);
        Assert.Equal("4", resp!.FinalResult);
    }
}
