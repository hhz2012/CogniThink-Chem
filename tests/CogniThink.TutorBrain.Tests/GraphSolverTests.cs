using System.Text.Json;
using CogniThink.Chem.Core.Inference;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>GraphSolver：graph payload → Chem.Core 引擎适配层</summary>
public class GraphSolverTests
{
    private static ImageAsset MakeGraphAsset(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return new ImageAsset { Type = "graph", Payload = doc.RootElement.Clone() };
    }

    /// <summary>TR-3.1 + TR-3.3：中文占位标签映射为字母后引擎接受，返回正确赋值。</summary>
    [Fact]
    public void Assign_可解转化链_返回唯一赋值()
    {
        var asset = MakeGraphAsset(@"
        {
          ""nodes"":[{""id"":""甲"",""label"":""甲""},{""id"":""乙"",""label"":""乙""},{""id"":""丙"",""label"":""丙""}],
          ""edges"":[{""from"":""甲"",""to"":""乙"",""cond"":""CO2""},{""from"":""乙"",""to"":""丙"",""cond"":""CO2过量""}]
        }");

        var result = GraphSolver.Solve(asset, new[] { "NaOH", "Na2CO3", "NaHCO3" });

        Assert.Equal(InferenceStatus.Solved, result.Status);
        Assert.Equal("NaOH", result.Assignment["甲"]);
        Assert.Equal("Na2CO3", result.Assignment["乙"]);
        Assert.Equal("NaHCO3", result.Assignment["丙"]);
        Assert.Contains("甲=NaOH", result.FinalResult);
        Assert.Contains("乙=Na2CO3", result.FinalResult);
    }

    /// <summary>TR-3.2：多解时如实 Underdetermined，不臆造唯一结论。</summary>
    [Fact]
    public void Assign_多解_如实欠定不蒙数()
    {
        var asset = MakeGraphAsset(@"
        {
          ""nodes"":[{""id"":""甲"",""label"":""甲""},{""id"":""乙"",""label"":""乙""}],
          ""edges"":[{""from"":""甲"",""to"":""乙"",""cond"":""CO2""}]
        }");
        // NaOH→Na2CO3（NA-014）与 Ca(OH)2→CaCO3（C-008）均能一步与 CO2 转化 → 两组解
        var result = GraphSolver.Solve(asset, new[] { "NaOH", "Na2CO3", "Ca(OH)2", "CaCO3" });

        Assert.Equal(InferenceStatus.Underdetermined, result.Status);
        // 不输出唯一赋值结论
        Assert.DoesNotContain("甲=NaOH", result.FinalResult);
        Assert.Contains("无法唯一确定", result.FinalResult);
    }

    /// <summary>非 graph 资产 / 无 payload：防护返回 NoSolution，不抛异常。</summary>
    [Fact]
    public void Solve_非Graph资产_返回无解不崩溃()
    {
        var asset = new ImageAsset { Type = "curve", Payload = null };
        var result = GraphSolver.Solve(asset, new[] { "NaOH" });
        Assert.Equal(InferenceStatus.NoSolution, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.FinalResult));
    }

    /// <summary>节点均为已知物质（无占位标签）：如实返回无赋值，不崩。</summary>
    [Fact]
    public void Assign_无占位标签_返回无解()
    {
        var asset = MakeGraphAsset(@"
        {
          ""nodes"":[{""id"":""NaOH"",""label"":""NaOH""},{""id"":""Na2CO3"",""label"":""Na2CO3""}],
          ""edges"":[{""from"":""NaOH"",""to"":""Na2CO3"",""cond"":""CO2""}]
        }");
        var result = GraphSolver.Solve(asset, new[] { "NaOH", "Na2CO3", "NaHCO3" });
        Assert.Equal(InferenceStatus.NoSolution, result.Status);
    }

    /// <summary>
    /// 离子等价匹配（AC-3 回归保护）：知识库无 CaCl2→CaCO3 分子条目，但水溶液中 CaCl2 电离出
    /// Ca^2+，可命中离子条目 Ca^2+ + CO3^2- → CaCO3，故一步转化判可行；真正无支撑的 K2CO3→KNO3
    /// 仍判不可行。任一侧唯一即 Solved，由网关按问法极性选答案（infeasible → 唯一不可行 B）。
    /// </summary>
    [Fact]
    public void VerifyOptions_离子等价匹配_CaCl2到CaCO3可行而K2CO3到KNO3不可行()
    {
        var asset = MakeGraphAsset(@"
        {
          ""nodes"":[{""id"":""甲"",""label"":""甲""},{""id"":""乙"",""label"":""乙""}],
          ""edges"":[{""from"":""甲"",""to"":""乙"",""cond"":""""}]
        }");
        var opts = new Dictionary<string, IReadOnlyList<string>>
        {
            ["A"] = new[] { "CaCl2", "CaCO3" },
            ["B"] = new[] { "K2CO3", "KNO3" },
        };

        var result = GraphSolver.Solve(asset, Array.Empty<string>(), GraphSolveMode.VerifyOptions, opts);

        Assert.Equal(InferenceStatus.Solved, result.Status);
        Assert.True(result.OptionFeasibility["A"], "CaCl2→CaCO3 应通过离子等价判为可行");
        Assert.False(result.OptionFeasibility["B"], "K2CO3→KNO3 应判为不可行（知识库无支撑）");
        var infeasible = result.OptionFeasibility.Where(kv => !kv.Value).Select(kv => kv.Key).ToList();
        Assert.Single(infeasible);
        Assert.Equal("B", infeasible[0]);
    }
}
