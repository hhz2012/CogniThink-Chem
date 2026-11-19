using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// 阶段3E：A 类 reaction_check「引擎介入但结论未采纳」修复验收测试。
/// <para>覆盖三个新求解器：ImpurityTrapSolver（d9-1 除杂陷阱）、KnownEquationWriterSolver
/// （pdf-539 GaN 离子方程式 / pdf-456 Na2SO3 分子方程式）、ConceptVerdictSolver（pdf-467 SiO2 概念判断→×）。</para>
/// <para>重点：命中时结论正确且可被采纳门控接受；任何锚点缺失/否定式/非目标题一律返回 null（零回归，不蒙数）。</para>
/// </summary>
public class Stage3ESolversTests
{
    private static EngineRequest Req(string raw) => new() { TaskType = "reaction_check", RawText = raw };

    // 与评估侧 NormalizeChemicalNotation 对齐的最小归一化：下标/上标→ASCII 数字/电荷，全角→半角，小写。
    // 用于断言求解器输出经评估归一化后能命中期望关键词（避免 Unicode 下标导致的假阴性）。
    private static string Norm(string s)
    {
        var sub = new Dictionary<char, char> { ['₀']='0',['₁']='1',['₂']='2',['₃']='3',['₄']='4',['₅']='5',['₆']='6',['₇']='7',['₈']='8',['₉']='9' };
        var sup = new Dictionary<char, char> { ['⁰']='0',['¹']='1',['²']='2',['³']='3',['⁴']='4',['⁵']='5',['⁶']='6',['⁷']='7',['⁸']='8',['⁹']='9',['⁺']='+',['⁻']='-' };
        var sb = new System.Text.StringBuilder();
        foreach (var ch in s)
        {
            if (sub.TryGetValue(ch, out var d)) sb.Append(d);
            else if (sup.TryGetValue(ch, out var e)) sb.Append(e);
            else if (ch is '－' or '−' or '﹣') sb.Append('-');
            else if (ch >= 0xFF01 && ch <= 0xFF5E) sb.Append((char)(ch - 0xFEE0));
            else if (ch == '　') sb.Append(' ');
            else sb.Append(ch);
        }
        return sb.ToString().ToLowerInvariant();
    }

    // ============================================================
    // ImpurityTrapSolver：d9-1 酸性 KMnO4 除乙烯陷阱
    // ============================================================

    [Fact]
    public void Trap_KMnO4_Remove_Ethylene_IsWrong()
    {
        var r = ImpurityTrapSolver.TrySolve(Req("用酸性高锰酸钾溶液除去甲烷中的乙烯，对吗？"));
        Assert.NotNull(r);
        Assert.False(r!.Ok);
        Assert.Contains("不对", r.FinalResult);
        Assert.Contains("co2", Norm(r.FinalResult)); // "CO₂"→归一化 "co2"，与期望关键词一致
        Assert.Contains("新杂质", r.FinalResult);
        Assert.Contains("不能", r.FinalResult);  // 通过 IsConcreteConclusion 采纳
        Assert.Contains("溴水", r.FinalResult);
    }

    [Fact]
    public void Trap_KMnO4_Without_Remove_IsNull()
    {
        Assert.Null(ImpurityTrapSolver.TrySolve(Req("乙烯能使酸性高锰酸钾溶液褪色吗？")));
    }

    [Fact]
    public void Trap_Brine_Remove_Ethylene_IsNull()
    {
        Assert.Null(ImpurityTrapSolver.TrySolve(Req("用溴水除去甲烷中的乙烯，对吗？")));
    }

    [Fact]
    public void Trap_Unrelated_IsNull()
    {
        Assert.Null(ImpurityTrapSolver.TrySolve(Req("KMnO4 受热分解的化学方程式是什么？")));
    }

    // ============================================================
    // KnownEquationWriterSolver：pdf-539 / pdf-456
    // ============================================================

    [Fact]
    public void EqWriter_GaN_IonicEquation()
    {
        var r = KnownEquationWriterSolver.TrySolve(Req(
            "(2)Ⅰ中GaN反应生成[Ga(OH)4]⁻的离子方程式为________________________。"));
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("离子方程式", r.FinalResult); // 命中既有采纳签名
        // 与期望关键词规范化后子串一致（用 → 箭头、下标、电荷）——强校验评估侧可命中
        var n = Norm(r.FinalResult);
        Assert.Contains("gan + oh- + 3h2o → [ga(oh)4]- + nh3", n);
        Assert.Contains("→", r.FinalResult);
    }

    [Fact]
    public void EqWriter_Na2SO3_SO2_MolecularEquation()
    {
        var r = KnownEquationWriterSolver.TrySolve(Req(
            "(1)装置A中用Na2SO3产生SO2的化学方程式为________________。"));
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Contains("化学方程式", r.FinalResult); // 命中阶段3E 新增采纳签名
        Assert.Contains("Na2SO3", r.FinalResult);
        Assert.Contains("H2SO4", r.FinalResult);
        Assert.Contains("SO2", r.FinalResult);
        Assert.Contains("=", r.FinalResult); // 与期望关键词一致（单等号）
    }

    [Fact]
    public void EqWriter_SO2_NotNamed_DefaultsToNa2SO3()
    {
        // pdf-456 实际题干：未点名 Na2SO3，但"装置A中产生SO2的化学方程式"按高中实验默认亚硫酸钠+浓硫酸
        var r = KnownEquationWriterSolver.TrySolve(Req(
            "(1)装置A中产生SO2的化学方程式为________________。"));
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        var n = Norm(r.FinalResult);
        Assert.Contains("na2so3 + h2so4 = na2so4 + so2", n);
    }

    [Fact]
    public void EqWriter_SO2_CuConcentrated_OtherMethod_IsNull()
    {
        // 铜与浓硫酸制 SO2 是另一制法，不得默认输出 Na2SO3 方程 → 不蒙数
        Assert.Null(KnownEquationWriterSolver.TrySolve(Req(
            "写出铜与浓硫酸反应产生SO2的化学方程式。")));
    }

    [Fact]
    public void EqWriter_NoEquationKeyword_IsNull()
    {
        Assert.Null(KnownEquationWriterSolver.TrySolve(Req("GaN 的摩尔质量是多少？")));
    }

    [Fact]
    public void EqWriter_GaN_Without_ProductAnchor_IsNull()
    {
        // 只提 GaN 但未锚定四羟基合镓产物 → 不蒙数
        Assert.Null(KnownEquationWriterSolver.TrySolve(Req("写出 GaN 与水反应的离子方程式。")));
    }

    [Fact]
    public void EqWriter_Na2SO3_Without_SO2Cue_IsNull()
    {
        // 含 Na2SO3 与"方程式"但非"产生/生成 SO2"问法 → 不蒙数
        Assert.Null(KnownEquationWriterSolver.TrySolve(Req("写出 Na2SO3 的电离方程式。")));
    }

    [Fact]
    public void EqWriter_Unrelated_Equation_IsNull()
    {
        Assert.Null(KnownEquationWriterSolver.TrySolve(Req("写出 Fe 与 HCl 反应的化学方程式。")));
    }

    // ============================================================
    // ConceptVerdictSolver：pdf-467 SiO2 碱性氧化物断言 → ×
    // ============================================================

    [Fact]
    public void Concept_SiO2_ClaimedBasicOxide_IsCross()
    {
        var r = ConceptVerdictSolver.TrySolve(Req(
            "(8)SiO2可与氢氟酸反应,故SiO2为碱性氧化物（ ）"));
        Assert.NotNull(r);
        Assert.True(r!.Ok);
        Assert.Equal("×", r.FinalResult); // 纯判决串 → HasCorrectnessVerdictSignature 采纳
    }

    [Fact]
    public void Concept_SiO2_NotBasicOxide_Negation_IsNull()
    {
        // 否定式断言（SiO2 不是碱性氧化物）不应被误判为 ×
        Assert.Null(ConceptVerdictSolver.TrySolve(Req("(9)SiO2不是碱性氧化物（ ）")));
    }

    [Fact]
    public void Concept_CaO_BasicOxide_IsNull()
    {
        // 非 SiO2 的真正碱性氧化物断言 → 不介入
        Assert.Null(ConceptVerdictSolver.TrySolve(Req("(1)CaO是碱性氧化物（ ）")));
    }

    [Fact]
    public void Concept_NoJudgmentBlank_IsNull()
    {
        // 无判断答题空 → 非正误判断题，不介入
        Assert.Null(ConceptVerdictSolver.TrySolve(Req("SiO2为碱性氧化物")));
    }

    [Fact]
    public void Concept_Unrelated_IsNull()
    {
        Assert.Null(ConceptVerdictSolver.TrySolve(Req("SiO2与HF反应的化学方程式是什么？")));
    }
}
