namespace CogniThink.TutorBrain.Tests;

using CogniThink.TutorBrain.Application.Chemistry;
using Xunit;

public sealed class ChemicalReverseInferenceEngineTests
{
    [Fact]
    public void 血红色溶液可由硫氰化钾与氯化铁解释()
    {
        var result = Reverse(new ReverseInferenceRequest
        {
            Known = { "KSCN" },
            SolutionColor = "血红色",
            TargetProducts = { "Fe(SCN)3" }
        });

        var best = result.Candidates[0];
        Assert.Equal("R-077", best.ReactionId);
        Assert.Contains(best.MissingReactants, m => m.Contains("FeCl3"));
        Assert.True(best.Verification?.Matched);
        Assert.Contains("血红色", result.Conclusion);
    }

    [Fact]
    public void 蓝色沉淀可由氢氧化钠与铜盐解释()
    {
        var result = Reverse(new ReverseInferenceRequest
        {
            Known = { "NaOH" },
            PrecipitateColor = "蓝色",
            TargetProducts = { "Cu(OH)2" }
        });

        Assert.NotEmpty(result.Candidates);
        Assert.Contains(result.Candidates, c =>
            c.MissingReactants.Any(m => m.Contains("Cu")) &&
            c.Verification?.Matched == true);
    }

    [Fact]
    public void 无色气泡可由盐酸与碳酸盐解释()
    {
        var result = Reverse(new ReverseInferenceRequest
        {
            Known = { "HCl" },
            Gas = "无色气泡",
            Phenomena = { "产生无色气泡" },
            MaxCandidates = 20
        });

        Assert.NotEmpty(result.Candidates);
        Assert.Contains(result.Candidates, c =>
            (c.ReactionId is "R-NA2CO3-HCL" or "R-CACO3-HCL" or "R-NAHCO3-HCL") &&
            c.Verification?.Matched == true);
    }

    [Fact]
    public void 红褐色沉淀可由氢氧化钠与氯化铁解释()
    {
        var result = Reverse(new ReverseInferenceRequest
        {
            Known = { "NaOH" },
            PrecipitateColor = "红褐色",
            TargetProducts = { "Fe(OH)3" }
        });

        Assert.Contains(result.Candidates, c =>
            c.ReactionId == "R-FECL3-NAOH" &&
            c.MissingReactants.Any(m => m.Contains("FeCl3")) &&
            c.Verification?.Matched == true);
    }

    [Fact]
    public void 已知反应物齐全时不再要求补充物质()
    {
        var result = Reverse(new ReverseInferenceRequest
        {
            Known = { "FeCl3", "KSCN" },
            TargetProducts = { "Fe(SCN)3" }
        });

        var best = result.Candidates.First(c => c.ReactionId == "R-077");
        Assert.Empty(best.MissingReactants);
        Assert.True(best.Verification?.Matched);
    }

    [Fact]
    public void 逆向路径可追溯到已知的氧化铁与盐酸()
    {
        var result = Reverse(new ReverseInferenceRequest
        {
            Known = { "Fe2O3" },
            TargetProducts = { "FeCl3" }
        });

        Assert.Contains(result.Candidates, c =>
            c.ReactionId == "R-FE2O3-HCL" &&
            c.MissingReactants.Any(m => m.Contains("HCl")) &&
            c.Verification?.Matched == true);
    }

    [Fact]
    public void 溶解或转化蓝色沉淀的反应不会被当成蓝色沉淀来源()
    {
        var result = Reverse(new ReverseInferenceRequest
        {
            Known = { "NaOH" },
            PrecipitateColor = "蓝色"
        });

        Assert.DoesNotContain(result.Candidates, c => c.ReactionId == "R-CUOH2-HEAT");
    }

    [Fact]
    public void 未知物质会被记录到警告而不是静默丢弃()
    {
        var result = Reverse(new ReverseInferenceRequest
        {
            Known = { "不存在的物质" },
            SolutionColor = "血红色"
        });

        Assert.Contains(result.Warnings, w => w.Contains("不存在的物质"));
    }

    [Fact]
    public void 没有任何观察信息时提示补充输入()
    {
        var result = Reverse(new ReverseInferenceRequest());

        Assert.Empty(result.Candidates);
        Assert.Contains("请至少提供", result.Conclusion);
    }

    // ==================== 新反应对应的逆向推理 ====================

    [Fact]
    public void 红棕色铁锈可由铁与氧气水逆向解释()
    {
        var result = Reverse(new ReverseInferenceRequest
        {
            Known = { "Fe" },
            PrecipitateColor = "红棕色"
        });

        Assert.Contains(result.Candidates, c =>
            c.ReactionId == "R-FE-RUST" &&
            c.MissingReactants.Any(m => m.Contains("O2") || m.Contains("水")));
    }

    [Fact]
    public void 白色硫酸铅沉淀可由铅蓄电池放电解释()
    {
        var result = Reverse(new ReverseInferenceRequest
        {
            Known = { "Pb", "PbO2" },
            TargetProducts = { "PbSO4" }
        });

        Assert.Contains(result.Candidates, c =>
            c.ReactionId == "R-LEAD-DISCHARGE" &&
            c.MissingReactants.Any(m => m.Contains("H2SO4")) &&
            c.Verification?.Matched == true);
    }

    [Fact]
    public void 紫色溶液可由苯酚与氯化铁解释()
    {
        var result = Reverse(new ReverseInferenceRequest
        {
            Known = { "C6H5OH" },
            SolutionColor = "紫色"
        });

        Assert.Contains(result.Candidates, c =>
            c.ReactionId == "R-PHENOL-FECL3" &&
            c.MissingReactants.Any(m => m.Contains("FeCl3")) &&
            c.Verification?.Matched == true);
    }

    [Fact]
    public void 白色沉淀可由硝酸银与氯化钠生成氯化银解释()
    {
        var result = Reverse(new ReverseInferenceRequest
        {
            Known = { "AgNO3" },
            PrecipitateColor = "白色"
        });

        Assert.Contains(result.Candidates, c =>
            c.MissingReactants.Any(m => m.Contains("NaCl")) &&
            c.Verification?.Matched == true);
    }

    [Fact]
    public void 黄色沉淀可由硝酸银与碘离子生成碘化银解释()
    {
        var result = Reverse(new ReverseInferenceRequest
        {
            Known = { "AgNO3" },
            PrecipitateColor = "黄色"
        });

        Assert.Contains(result.Candidates, c =>
            c.MissingReactants.Any(m => m.Contains("I-") || m.Contains("碘化")) &&
            c.Verification?.Matched == true);
    }

    [Fact]
    public void 无色氢气可由活泼金属与酸逆向解释()
    {
        var result = Reverse(new ReverseInferenceRequest
        {
            Known = { "HCl" },
            Gas = "无色气泡",
            Phenomena = { "产生无色气泡" },
            MaxCandidates = 30
        });

        Assert.Contains(result.Candidates, c =>
            c.Verification?.Matched == true &&
            (c.MissingReactants.Any(m => m.Contains("Fe") || m.Contains("Zn") || m.Contains("Mg"))));
    }

    [Fact]
    public void 黑色氧化铜可由氢氧化铜受热分解逆向解释()
    {
        var result = Reverse(new ReverseInferenceRequest
        {
            Known = { "Cu(OH)2" },
            TargetProducts = { "CuO" }
        });

        Assert.Contains(result.Candidates, c =>
            c.ReactionId == "R-CUOH2-HEAT" &&
            c.MissingReactants.Count == 0);
    }

    [Fact]
    public void 砖红色氧化亚铜可由乙醛与氢氧化铜逆向解释()
    {
        var result = Reverse(new ReverseInferenceRequest
        {
            Known = { "CH3CHO" },
            PrecipitateColor = "砖红色",
            TargetProducts = { "Cu2O" }
        });

        Assert.Contains(result.Candidates, c =>
            c.ReactionId == "R-CH3CHO-CUOH2" &&
            c.MissingReactants.Any(m => m.Contains("Cu(OH)2")));
    }

    [Fact]
    public void 白色三溴苯酚沉淀可由苯酚与溴水逆向解释()
    {
        var result = Reverse(new ReverseInferenceRequest
        {
            Known = { "C6H5OH" },
            PrecipitateColor = "白色"
        });

        Assert.Contains(result.Candidates, c =>
            c.ReactionId == "R-PHENOL-BROMINATION" &&
            c.MissingReactants.Any(m => m.Contains("Br2")));
    }

    private static ReverseInferenceResult Reverse(ReverseInferenceRequest request) =>
        ChemistryFixture.Reverse.Infer(request);
}
