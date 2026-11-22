using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// B-LlmMain Task 4 组 1：标况非气态事实外置（standard-state-phases.json）+ 线程化（AC5）。
/// 断言命中处 HitId 指向真实 JSON 条目（R4 不臆造）、FinalResult 字节级不变（含下标 ₃ 不规范）、
/// 未命中返回 null（节点天然缺席），以及 JSON 文件存在且条目 id 唯一非空。
/// </summary>
public class StoichiometryStateTrapNodesTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void HitId_SO3_ReturnsRealJsonId()
    {
        var r = StoichiometryStateTrapSolver.TrySolve("标准状况下，22.4L SO3 含有的分子数是 NA 吗？");
        Assert.NotNull(r);
        Assert.Equal("sstp-so3", r.Value.HitId);
        // FinalResult 用命中别名原文插入：输入 SO3 → 输出含 SO3（不规范为其他写法）
        Assert.Contains("SO3", r.Value.FinalResult);
        Assert.Contains("固体", r.Value.FinalResult);
    }

    [Fact]
    public void HitId_SO3_Subscript_ReturnsSameId_AndPreservesByteInFinalResult()
    {
        // 下标 ₃ 是独立匹配键（与 SO3 同条目 sstp-so3）；输出必须保留下标字节，不得规范为 SO3
        var r = StoichiometryStateTrapSolver.TrySolve("标准状况下，22.4L SO₃ 含有的分子数是 NA 吗？");
        Assert.NotNull(r);
        Assert.Equal("sstp-so3", r.Value.HitId);
        Assert.Contains("SO₃", r.Value.FinalResult);
        Assert.DoesNotContain("SO3", r.Value.FinalResult); // 字节级保留：不混入无下标写法
    }

    [Fact]
    public void HitId_Hexane_ReturnsRealJsonId()
    {
        var r = StoichiometryStateTrapSolver.TrySolve("标准状况下，22.4L 己烷含有的分子数是 NA 吗？");
        Assert.NotNull(r);
        Assert.Equal("sstp-hexane", r.Value.HitId);
        Assert.Contains("己烷", r.Value.FinalResult);
        Assert.Contains("液体", r.Value.FinalResult);
    }

    [Fact]
    public void NoMatch_ReturnsNull_HitIdAbsent_NoFalsePositive()
    {
        // CO2 标况是气体 → 不在事实表 → null（不臆造节点）
        var r = StoichiometryStateTrapSolver.TrySolve("标准状况下，22.4L CO2 含有的分子数是 NA 吗？");
        Assert.Null(r);
    }

    [Fact]
    public void JsonFile_Exists_WithUniqueNonEmptyIds()
    {
        var path = Path.Combine(RepoRoot, "data", "knowledge", "standard-state-phases.json");
        Assert.True(File.Exists(path), $"外置事实文件缺失：{path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var entries = doc.RootElement.GetProperty("entries").EnumerateArray().ToList();
        Assert.NotEmpty(entries);

        var ids = new HashSet<string>();
        foreach (var e in entries)
        {
            var id = e.GetProperty("id").GetString();
            Assert.False(string.IsNullOrWhiteSpace(id), "条目 id 不得为空（R4 真实锚点）");
            Assert.True(ids.Add(id!), $"条目 id 重复：{id}");
        }

        Assert.Contains("sstp-so3", ids);
        Assert.Contains("sstp-hexane", ids);
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
}
