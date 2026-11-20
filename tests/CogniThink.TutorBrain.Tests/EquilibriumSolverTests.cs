using System.Reflection;
using CogniThink.TutorBrain.Application.Common;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// Task 26 (AC-26)：CallEquilibrium 末尾新增的两个子求解器单测。
/// 复用 Chem.Core 既有引擎 / 数据，失败一律返回 null（不猜）。
/// 方法为 private static，经反射调用。
/// </summary>
public class EquilibriumSolverTests
{
    private static EngineResponse? InvokeStatic(string name, string raw)
    {
        var m = typeof(LlmGatewayService).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(m);
        return (EngineResponse?)m!.Invoke(null, new object[] { raw });
    }

    // ---------- TrySolveConservationEquation ----------

    [Fact]
    public void Na2CO3_电荷守恒_返回含2c的电荷式()
    {
        var r = InvokeStatic("TrySolveConservationEquation", "Na2CO3 溶液中，电荷守恒式正确的是");
        Assert.NotNull(r);
        Assert.True(r!.Ok == true, r.FinalResult);
        Assert.Contains("2c", r.FinalResult);          // d4-3 关键词
        Assert.Contains("电荷守恒式", r.FinalResult);
        Assert.Contains("c(Na", r.FinalResult);
    }

    [Fact]
    public void Na2CO3_物料守恒_返回物料式()
    {
        var r = InvokeStatic("TrySolveConservationEquation", "Na2CO3 溶液的物料守恒式");
        Assert.NotNull(r);
        Assert.True(r!.Ok == true, r.FinalResult);
        Assert.Contains("物料守恒式", r.FinalResult);
    }

    [Fact]
    public void 非守恒题面_返回null()
    {
        var r = InvokeStatic("TrySolveConservationEquation", "下列说法正确的是 Na2CO3 溶液");
        Assert.Null(r);
    }

    [Fact]
    public void 未知溶质_返回null_不猜测()
    {
        // 引擎对规则外溶质返回 NoSolution → 求解器必须 null，不得臆造守恒式
        var r = InvokeStatic("TrySolveConservationEquation", "XyzAbc 溶液中，电荷守恒式正确的是");
        Assert.Null(r);
    }

    [Fact]
    public void 无溶液字样_返回null()
    {
        var r = InvokeStatic("TrySolveConservationEquation", "电荷守恒式是什么");
        Assert.Null(r);
    }

    // ---------- TryOrderAcidStrength ----------

    [Fact]
    public void 三酸由弱到强_排序正确()
    {
        // pdf-514：HCN Ka=4.9e-10 < H2CO3 Ka1=4.5e-7 < CH3COOH Ka=1.75e-5
        var r = InvokeStatic("TryOrderAcidStrength", "CH3COOH、H2CO3、HCN 的酸性由弱到强的顺序");
        Assert.NotNull(r);
        Assert.True(r!.Ok == true, r.FinalResult);
        Assert.Contains("HCN < H2CO3 < CH3COOH", r.FinalResult);  // pdf-514 关键词
    }

    [Fact]
    public void 由强到弱_反向排序()
    {
        var r = InvokeStatic("TryOrderAcidStrength", "CH3COOH、H2CO3、HCN 酸性由强到弱");
        Assert.NotNull(r);
        Assert.True(r!.Ok == true, r.FinalResult);
        Assert.Contains("CH3COOH > H2CO3 > HCN", r.FinalResult);
    }

    [Fact]
    public void 库外酸_返回null_不猜测()
    {
        // HNO2 不在 acid-constants.json → 任一酸缺数据即 null
        var r = InvokeStatic("TryOrderAcidStrength", "HNO2、HF 酸性由弱到强");
        Assert.Null(r);
    }

    [Fact]
    public void 仅一种酸_返回null()
    {
        var r = InvokeStatic("TryOrderAcidStrength", "CH3COOH 酸性由弱到强");
        Assert.Null(r);
    }

    [Fact]
    public void 无方向词_返回null()
    {
        var r = InvokeStatic("TryOrderAcidStrength", "CH3COOH、H2CO3、HCN 酸性比较");
        Assert.Null(r);
    }
}
