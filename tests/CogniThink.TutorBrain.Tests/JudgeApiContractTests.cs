using System.Collections.Generic;
using System.Linq;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// B-judge-API Task 1：判官裁决契约加法扩展暴露命中 id（AC1 / AC2）。
/// 红线：原 JudgeStatement / IStatementJudge.Judge 行为字节级不变（委托等价）；
/// 仅 FactBase 命中带 id；Counting / RedoxElectron 命中不带 id（R4，引擎推导无锚点）；
/// 冲突不蒙数（返回 Undecided + 空 id）。
/// </summary>
public class JudgeApiContractTests
{
    // general-choice-facts GC-004-B：match=[次磷酸, NaH2PO2, 正盐]，stemMatch=[化学研究中常用的方法, 分类方法正确的是]，verdict=true。
    private const string Stem004 = "化学研究中常用的方法，下列分类方法正确的是";

    // ===== AC1：FactChoiceMatcher 加法重载 =====

    [Fact]
    public void JudgeStatementWithIds_Hit_ReturnsIdAndSameVerdict()
    {
        var (v, ids) = FactChoiceMatcher.JudgeStatementWithIds(Stem004, "次磷酸（NaH2PO2）是正盐");
        Assert.Equal(FactChoiceMatcher.StatementVerdict.True, v);
        Assert.Contains("GC-004-B", ids);
        Assert.All(ids, id => Assert.False(string.IsNullOrEmpty(id)));

        // 原方法委托等价（字节级行为不变）
        Assert.Equal(v, FactChoiceMatcher.JudgeStatement(Stem004, "次磷酸（NaH2PO2）是正盐"));
    }

    [Fact]
    public void JudgeStatementWithIds_Miss_ReturnsUndecidedAndEmptyIds()
    {
        var (v, ids) = FactChoiceMatcher.JudgeStatementWithIds(Stem004, "某种不相关物质 Xyz 属于正盐");
        Assert.Equal(FactChoiceMatcher.StatementVerdict.Undecided, v);
        Assert.Empty(ids);
        Assert.Equal(v, FactChoiceMatcher.JudgeStatement(Stem004, "某种不相关物质 Xyz 属于正盐"));
    }

    [Fact]
    public void JudgeStatement_DelegationEquivalent_AcrossStatements()
    {
        // 覆盖命中 / 未命中 / stem 未限定三类，断言原方法与新重载 verdict 完全一致
        var statements = new (string Stem, string Stmt)[]
        {
            (Stem004, "次磷酸（NaH2PO2）是正盐"),
            (Stem004, "某种不相关物质 Xyz 属于正盐"),
            ("完全无关的另一道题题干", "次磷酸（NaH2PO2）是正盐"),
            ("", "水在常温下是液体"),
        };
        foreach (var (stem, stmt) in statements)
        {
            Assert.Equal(
                FactChoiceMatcher.JudgeStatement(stem, stmt),
                FactChoiceMatcher.JudgeStatementWithIds(stem, stmt).Verdict);
        }
    }

    // ===== AC2：FactBaseStatementJudge / CompositeStatementJudge 暴露 id =====

    [Fact]
    public void FactBaseJudge_JudgeWithIds_HitCarriesId()
    {
        var judge = new FactBaseStatementJudge();
        var (v, ids) = judge.JudgeWithIds(Stem004, "次磷酸（NaH2PO2）是正盐");
        Assert.Equal(Verdict.True, v);
        Assert.Contains("GC-004-B", ids);
        // 原 Judge 委托等价
        Assert.Equal(v, judge.Judge(Stem004, "次磷酸（NaH2PO2）是正盐"));
    }

    [Fact]
    public void CompositeJudge_FactBaseHit_CarriesId()
    {
        var composite = new CompositeStatementJudge(
            new FactBaseStatementJudge(),
            new CountingStatementJudge(),
            new RedoxElectronStatementJudge());
        var (v, ids) = composite.JudgeWithIds(Stem004, "次磷酸（NaH2PO2）是正盐");
        Assert.Equal(Verdict.True, v);
        Assert.NotEmpty(ids);
        Assert.Contains("GC-004-B", ids);
        Assert.Equal(v, composite.Judge(Stem004, "次磷酸（NaH2PO2）是正盐"));
    }

    [Fact]
    public void CompositeJudge_CountingHit_NoId_EngineDerivedR4()
    {
        // 计数原语命中（引擎推导，无暴露 id）→ verdict 确定但 id 为空（R4 不发节点）
        var composite = new CompositeStatementJudge(
            new FactBaseStatementJudge(),
            new CountingStatementJudge(),
            new RedoxElectronStatementJudge());
        var (v, ids) = composite.JudgeWithIds("", "分子式为 C4H10 的同分异构体有 2 种");
        Assert.Equal(Verdict.True, v);
        Assert.Empty(ids);
    }

    [Fact]
    public void CompositeJudge_Judge_DelegationEquivalent()
    {
        var composite = new CompositeStatementJudge(
            new FactBaseStatementJudge(),
            new CountingStatementJudge(),
            new RedoxElectronStatementJudge());
        var statements = new (string Stem, string Stmt)[]
        {
            (Stem004, "次磷酸（NaH2PO2）是正盐"),
            ("", "分子式为 C4H10 的同分异构体有 2 种"),
            ("", "完全无关的陈述 Xyz"),
        };
        foreach (var (stem, stmt) in statements)
        {
            Assert.Equal(composite.Judge(stem, stmt), composite.JudgeWithIds(stem, stmt).Verdict);
        }
    }
}
