using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
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

    // ---------- TryCalculateKspPrecipitation（B-LlmMain T4 组 2：沉淀类型外置 + 线程化） ----------

    [Fact]
    public void Ksp_AgCl_AgI_滴加AgNO3_I先沉淀_且带可溯源节点()
    {
        var r = InvokeStatic("TryCalculateKspPrecipitation",
            "已知 Ksp(AgCl)=1.8×10^-10，Ksp(AgI)=8.3×10^-17，向等浓度的 Cl- 和 I- 混合溶液中滴加 AgNO3，谁先沉淀？");
        Assert.NotNull(r);
        Assert.True(r!.Ok == true, r.FinalResult);
        Assert.Contains("先沉淀", r.FinalResult);
        Assert.Contains("I-", r.FinalResult); // AgI 所需 Ag+ 浓度更小 → 先沉淀

        var nodes = r.ReasoningNodes;
        Assert.Equal(3, nodes.Count); // 2 个沉淀事实节点 + 1 个汇总节点

        var validIds = LoadPrecipitateIds();
        var factNodes = nodes.Take(2).ToList();
        // R4：每个事实节点 KnowledgeId 必须是 precipitate-types.json 中的真实条目
        Assert.All(factNodes, n =>
        {
            Assert.Equal(EvidenceLevel.KnowledgeFact, n.EvidenceLevel);
            Assert.StartsWith("ppt:", n.KnowledgeId);
            Assert.Contains(n.KnowledgeId!, validIds);
        });
        // 节点顺序 = 所需阳离子浓度升序（AgI < AgCl）
        Assert.Equal(new[] { "ppt:AgI", "ppt:AgCl" }, factNodes.Select(n => n.KnowledgeId).ToArray());
        Assert.Equal(Enumerable.Range(1, 3), nodes.Select(n => n.Step));
        // 汇总节点 Conclusion 字节级等于整题 FinalResult
        Assert.Equal(r.FinalResult, nodes[2].Conclusion);
        Assert.Equal(new HashSet<string> { "ppt:AgI", "ppt:AgCl" }, nodes[2].Premises.ToHashSet());
    }

    [Fact]
    public void Ksp_仅一个Ksp_返回null()
    {
        var r = InvokeStatic("TryCalculateKspPrecipitation", "已知 Ksp(AgCl)=1.8×10^-10，求其溶解度");
        Assert.Null(r);
    }

    [Fact]
    public void Ksp_未知沉淀化学式_返回null_不猜测()
    {
        // 两个 Ksp 但化学式不在 precipitate-types.json → 命中 0 → null（不臆造节点）
        var r = InvokeStatic("TryCalculateKspPrecipitation",
            "已知 Ksp(XyzAb)=1.8×10^-10，Ksp(QrCd)=8.3×10^-17，谁先沉淀？");
        Assert.Null(r);
    }

    [Fact]
    public void PrecipitateTypesJson_存在_且条目id唯一非空()
    {
        var path = Path.Combine(ResolveRepoRoot(), "data", "highschool", "precipitate-types.json");
        Assert.True(File.Exists(path), $"外置事实文件缺失：{path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var entries = doc.RootElement.GetProperty("entries").EnumerateArray().ToList();
        Assert.Equal(23, entries.Count); // 与原硬编码 dict 条目数一致

        var ids = new HashSet<string>();
        foreach (var e in entries)
        {
            var id = e.GetProperty("id").GetString();
            Assert.False(string.IsNullOrWhiteSpace(id), "条目 id 不得为空（R4 真实锚点）");
            Assert.True(ids.Add(id!), $"条目 id 重复：{id}");
            // formula 键不得为空（它是 Ordinal 查找键，外置后必须存活）
            Assert.False(string.IsNullOrWhiteSpace(e.GetProperty("formula").GetString()));
        }
        Assert.Contains("ppt:AgCl", ids);
        Assert.Contains("ppt:Mg(OH)2", ids); // 含括号的键字节级保留
    }

    private static string ResolveRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "data", "knowledge", "knowledge.json")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new DirectoryNotFoundException("找不到 repo 根（缺少 data/knowledge/knowledge.json）");
    }

    private static HashSet<string> LoadPrecipitateIds()
    {
        var path = Path.Combine(ResolveRepoRoot(), "data", "highschool", "precipitate-types.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var ids = new HashSet<string>();
        foreach (var e in doc.RootElement.GetProperty("entries").EnumerateArray())
            ids.Add(e.GetProperty("id").GetString()!);
        return ids;
    }
}
