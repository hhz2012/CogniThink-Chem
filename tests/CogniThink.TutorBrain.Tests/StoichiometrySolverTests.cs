using System.Linq;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

public class StoichiometrySolverTests
{
    [Fact]
    public void Pdf025_真实题干_整题多模式返回null_逐子问聚合_AND判正()
    {
        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-025");

        // 整题：多模式并存 → 必须返回 null（交多部分分发逐子问求解）
        Assert.Null(StoichiometrySolver.TrySolve(c.Question));
        Assert.True(MultiPartUtility.IsMultiPart(c.Question), "pdf-025 应判定为多部分大题");

        // 逐子问求解（镜像 TryDispatchMultiPartAsync）
        var subs = MultiPartUtility.SplitSubQuestions(c.Question);
        Assert.Equal(6, subs.Count);

        var parts = new System.Collections.Generic.List<(int idx, string final)>();
        foreach (var (idx, subText) in subs)
        {
            var r = StoichiometrySolver.TrySolve(subText);
            Assert.NotNull(r);
            Assert.True(r!.Ok == true, $"子问({idx}) 应唯一解出，实际 Final={r.FinalResult}");
            parts.Add((idx, r.FinalResult!));
        }

        // 聚合（与分发口径一致：(1).. (2).. ...）
        var aggregated = string.Join(" ", parts.Select(p => $"({p.idx}){p.final}"));

        // 6 个关键词逐字包含（归一化后 Contains）
        foreach (var kw in c.AnswerKeywords)
            Assert.Contains(kw, aggregated);

        // multi_part 采纳门：AND 判分必须全中
        Assert.True(GatewayVsLlmEvaluationService.AllKeywordsHit(aggregated, c),
            $"聚合结论未通过 pdf-025 AND 判分：{aggregated}");
    }

    [Fact]
    public void A_分子数_标况体积()
    {
        var q = "3.01×10^23 个 H2 分子在标准状况下的体积约为__________。";
        var r = StoichiometrySolver.TrySolve(q);
        Assert.NotNull(r);
        Assert.Contains("11.2 L", r!.FinalResult); // 0.5 mol × 22.4 = 11.2 L
    }

    [Fact]
    public void B_质量体积_摩尔质量()
    {
        var q = "标准状况下 0.64 g 某气体的体积为 224 mL，则其摩尔质量为__________。";
        var r = StoichiometrySolver.TrySolve(q);
        Assert.NotNull(r);
        Assert.Contains("64 g/mol", r!.FinalResult); // M = 22.4 × 0.64 / 0.224 = 64
    }

    [Fact]
    public void C_符号式_阿伏加德罗常数_原样输出不计算()
    {
        var q = "若 a g 某气体 A2，含有的分子数为 N，c g A2 气体在标准状况下的体积约为 b L，则阿伏加德罗常数的数值可表示为__________(用含字母的代数式表示)。";
        var r = StoichiometrySolver.TrySolve(q);
        Assert.NotNull(r);
        Assert.Contains("22.4cN/ab", r!.FinalResult);
    }

    [Fact]
    public void D_符号式_气体溶解浓度_原样输出()
    {
        var q = "标准状况下将 a L X 气体(摩尔质量为 M g·mol^-1)全部溶于0.1 L 水(水的密度为1 g·cm^-3)中，所得溶液的密度为 d g·cm^-3，则此溶液的物质的量浓度为__________。";
        var r = StoichiometrySolver.TrySolve(q);
        Assert.NotNull(r);
        Assert.Contains("1.000da/(aM+2240) mol·L⁻¹", r!.FinalResult);
    }

    [Fact]
    public void E_两溶液混合_旁观离子浓度()
    {
        // 10 mL 2.00 M NaCl 与 10 mL 1.00 M AgNO3 混合，Na+ 旁观（系数1）
        // n(Na+)=1×0.010×2.00=0.020 mol；V总=0.020 L；[Na+]=1.00
        var q = "将10 mL 2.00 mol·L^-1 NaCl 溶液与10 mL 1.00 mol·L^-1 AgNO3 溶液相混合，则混合溶液中 Na+ 的物质的量浓度为__________。";
        var r = StoichiometrySolver.TrySolve(q);
        Assert.NotNull(r);
        Assert.Contains("1.00 mol·L⁻¹", r!.FinalResult);
    }

    [Fact]
    public void F_电荷平衡_未知离子浓度()
    {
        // 与 pdf-025 同离子种类、换数值：Na+(0.1)、Mg2+(0.2)、SO4^2-(0.15)，求 c(Cl-)
        // 正电荷 0.1×1 + 0.2×2 = 0.5；负电荷 0.15×2 = 0.3；c(Cl-) = (0.5-0.3)/1 = 0.2
        var q = "某混合溶液中含有离子：Na+、Mg2+、Cl-、SO4^2-，测得 Na+、Mg2+ 和 SO4^2- 的物质的量浓度依次为0.1 mol·L^-1、0.2 mol·L^-1、0.15 mol·L^-1，则该混合溶液中 c(Cl-) = __________。";
        var r = StoichiometrySolver.TrySolve(q);
        Assert.NotNull(r);
        Assert.Contains("0.2 mol·L⁻¹", r!.FinalResult);
    }

    [Fact]
    public void 域外_整题多模式并存_返回null_不蒙部分答案()
    {
        // 同时含 A（分子数+体积）与 B（摩尔质量）→ ≥2 模式 → null
        var q = "9.03×10^23 个 HCl 分子在标准状况下的体积为___；1.28 g 氧化物标况体积 448 mL，摩尔质量为___。";
        Assert.Null(StoichiometrySolver.TrySolve(q));
    }

    [Fact]
    public void 域外_缺数据_返回null_不蒙数()
    {
        // A 缺体积数据 → null
        Assert.Null(StoichiometrySolver.TrySolve("HCl 分子在标准状况下的体积约为___。"));
        // F 缺目标 c(...) → null
        Assert.Null(StoichiometrySolver.TrySolve("某混合溶液中含有离子：Na+、Cl-，测得 Na+ 的物质的量浓度依次为0.1 mol·L^-1，则 Cl- 浓度为___。"));
    }

    [Fact]
    public void 域外_非化学计量_返回null()
    {
        Assert.Null(StoichiometrySolver.TrySolve("下列溶液 pH 由大到小排序正确的是？"));
        Assert.Null(StoichiometrySolver.TrySolve("Fe2+ 与 NO3- 能大量共存吗？"));
    }
}
