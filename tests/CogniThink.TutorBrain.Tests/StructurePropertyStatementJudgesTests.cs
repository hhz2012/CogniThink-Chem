using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CogniThink.Chem.Core.Rules;
using CogniThink.Chem.Core.Structure;
using CogniThink.TutorBrain.Application.Evaluation;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;
using Verdict = CogniThink.TutorBrain.Application.Gateway.Verdict;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// 结构/周期律/化学键比较陈述判定原语护栏（spec engine-ext-structure-periodic）。
/// 红线：零假阳性（不蒙数）；负例必须 Undecided；pdf-458 端到端判正 BD 且判分正确。
/// </summary>
public class StructurePropertyStatementJudgesTests
{
    private const string Stem = "下列说法正确的是";

    // ===== TR-1.1：真值数据加载 =====

    [Fact]
    public void Data_Loads_WithGaNAndNh3Bh3()
    {
        var data = RuleStore.Load().Query<StructurePropertyData>("structure-property-facts");
        Assert.NotNull(data);
        Assert.NotEmpty(data!.CrystalTypes);
        Assert.NotEmpty(data.CoordinateBonds);
        Assert.Contains(data.CrystalTypes, e => e.Formula == "GaN");
        Assert.Contains(data.CoordinateBonds, e => e.Species == "NH3BH3");
    }

    // ===== TR-2.1 / TR-2.2：电子排布 =====

    [Fact]
    public void ElectronConfig_GaCorrect_ReturnsTrue()
    {
        var j = new ElectronConfigStatementJudge();
        Assert.Equal(Verdict.True, j.Judge(Stem, "基态Ga原子核外电子排布式：[Ar]3d¹⁰4s²4p¹"));
    }

    [Fact]
    public void ElectronConfig_GaWrongPdf458A_ReturnsFalse()
    {
        var j = new ElectronConfigStatementJudge();
        // pdf-458-A：漏写 3d10 → 错
        Assert.Equal(Verdict.False, j.Judge(Stem, "基态Ga原子核外电子排布式：[Ar]4s²4p¹"));
    }

    [Fact]
    public void ElectronConfig_IonFe2PlusCorrect_ReturnsTrue()
    {
        var j = new ElectronConfigStatementJudge();
        Assert.Equal(Verdict.True, j.Judge(Stem, "Fe2+的电子排布式为[Ar]3d⁶"));
    }

    [Theory]
    [InlineData("某种不相关物质 Xyz 的电子排布式为 1s2")]     // 未知元素
    [InlineData("Ga 的原子半径大于 As")]                      // 无排布锚点
    [InlineData("Ga 的电子排布决定其性质")]                   // 无声称排布值
    [InlineData("Ga 与 Ge 的电子排布不同")]                   // 比较型
    [InlineData("基态Ga原子轨道表示式正确")]                  // 轨道表示式不可解析
    public void ElectronConfig_Negatives_ReturnsUndecided(string statement)
    {
        var j = new ElectronConfigStatementJudge();
        Assert.Equal(Verdict.Undecided, j.Judge(Stem, statement));
    }

    // ===== TR-3.1 / TR-3.2：VSEPR 键角数值 =====

    [Fact]
    public void BondAngle_Bcl3_120_ReturnsTrue_Pdf458D()
    {
        var j = new VseprBondAngleStatementJudge();
        Assert.Equal(Verdict.True, j.Judge(Stem, "BCl₃分子中键角为120°"));
    }

    [Fact]
    public void BondAngle_Bcl3_109_ReturnsFalse()
    {
        var j = new VseprBondAngleStatementJudge();
        Assert.Equal(Verdict.False, j.Judge(Stem, "BCl₃分子中键角为109°"));
    }

    [Fact]
    public void BondAngle_Nh3_107_ReturnsTrue()
    {
        var j = new VseprBondAngleStatementJudge();
        Assert.Equal(Verdict.True, j.Judge(Stem, "NH₃的键角为107°"));
    }

    [Theory]
    [InlineData("键角 NH₃>NO₃⁻")]          // 比较型
    [InlineData("XyZ 分子中键角为120°")]    // 未知微粒
    [InlineData("BCl₃的空间构型")]         // 无键角数值
    [InlineData("BCl₃键角")]              // 无数值
    public void BondAngle_Negatives_ReturnsUndecided(string statement)
    {
        var j = new VseprBondAngleStatementJudge();
        Assert.Equal(Verdict.Undecided, j.Judge(Stem, statement));
    }

    // ===== TR-4.1 / TR-4.2：晶体类型 =====

    [Fact]
    public void CrystalType_GaNMolecular_ReturnsFalse_Pdf458C()
    {
        var j = new CrystalTypeStatementJudge();
        Assert.Equal(Verdict.False, j.Judge(Stem, "氮化镓属于分子晶体"));
    }

    [Fact]
    public void CrystalType_GaNAtomic_ReturnsTrue()
    {
        var j = new CrystalTypeStatementJudge();
        Assert.Equal(Verdict.True, j.Judge(Stem, "氮化镓属于原子晶体"));
        Assert.Equal(Verdict.True, j.Judge(Stem, "氮化镓属于共价晶体")); // 共价晶体 ≡ 原子晶体
    }

    [Fact]
    public void CrystalType_SiO2Molecular_ReturnsFalse()
    {
        var j = new CrystalTypeStatementJudge();
        Assert.Equal(Verdict.False, j.Judge(Stem, "SiO₂是分子晶体"));
    }

    [Theory]
    [InlineData("X₃Y 属于分子晶体")]   // 表外物质
    [InlineData("氮化镓熔点很高")]    // 无晶体类型断言
    public void CrystalType_Negatives_ReturnsUndecided(string statement)
    {
        var j = new CrystalTypeStatementJudge();
        Assert.Equal(Verdict.Undecided, j.Judge(Stem, statement));
    }

    // ===== TR-5.1 / TR-5.2：配位键 =====

    [Fact]
    public void CoordinateBond_Nh3Bh3_ReturnsTrue_Pdf458B()
    {
        var j = new CoordinateBondStatementJudge();
        Assert.Equal(Verdict.True, j.Judge(Stem,
            "NH₃BH₃的结构式：H—N←B—H（图示为N与B间配位键，N上连两个H，B上连两个H）"));
    }

    [Fact]
    public void CoordinateBond_Ch4_ReturnsFalse()
    {
        var j = new CoordinateBondStatementJudge();
        Assert.Equal(Verdict.False, j.Judge(Stem, "CH₄含配位键"));   // 表内已知负例
    }

    [Theory]
    [InlineData("XYZ 含配位键")]                 // 表外物种
    [InlineData("NH₃BH₃是极性分子")]            // 无配位键断言
    public void CoordinateBond_Negatives_ReturnsUndecided(string statement)
    {
        var j = new CoordinateBondStatementJudge();
        Assert.Equal(Verdict.Undecided, j.Judge(Stem, statement));
    }

    // ===== TR-6.2：pdf-458 端到端 → BD 且判分正确 =====

    [Fact]
    public void Pdf458_MultiSelect_Solves_BD_AndGradedCorrect()
    {
        var raw = Stem + "\n"
                  + "A. 基态Ga原子核外电子排布式：[Ar]4s²4p¹\n"
                  + "B. NH₃BH₃的结构式：H—N←B—H（图示为N与B间配位键，N上连两个H，B上连两个H）\n"
                  + "C. 氮化镓属于分子晶体\n"
                  + "D. BCl₃分子中键角为120°";
        var r = MultiSelectChoiceValidator.TryDecide(new EngineRequest { RawText = raw });
        Assert.NotNull(r);
        Assert.Equal("BD", r!.AnswerLetter);
        Assert.Contains("BD", r.FinalResult);

        var c = GatewayVsLlmEvaluationService.BuildTestCases().First(x => x.Id == "pdf-458");
        Assert.True(GatewayVsLlmEvaluationService.IsAnswerCorrect(r.FinalResult, c));
    }
}
