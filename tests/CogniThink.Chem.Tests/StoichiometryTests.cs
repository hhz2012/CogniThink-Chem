using CogniThink.Chem.Core.Chemistry;
using CogniThink.Chem.Core.Math;
using CogniThink.Chem.Core.Stoichiometry;
using Xunit;

namespace CogniThink.Chem.Tests;

/// <summary>摩尔/浓度/产率计算测试：半整数相对原子质量、结晶水、NA 换算、稀释与产率。</summary>
public class StoichiometryTests
{
    [Theory]
    [InlineData("H2O", 18, 1)]
    [InlineData("CO2", 44, 1)]
    [InlineData("NaCl", 117, 2)]            // 58.5 = 117/2
    [InlineData("Fe2(SO4)3", 400, 1)]
    [InlineData("CuSO4·5H2O", 499, 2)]      // 249.5（Cu 63.5），结晶水必须计入
    [InlineData("CaCO3", 100, 1)]
    public void MolarMass_ExactRational(string formula, long num, long den) =>
        Assert.Equal(new Rational(num, den), MoleCalculator.MolarMass(formula));

    [Fact]
    public void MolarMass_IonIgnoresElectronMass() =>
        Assert.Equal(MoleCalculator.MolarMass("SO4"), MoleCalculator.MolarMass("SO4^2-"));

    [Fact]
    public void MolarMass_UnknownElement_Fails()
    {
        Assert.Throws<FormulaException>(() => MoleCalculator.MolarMass("Xy2O3"));
        Assert.False(MoleCalculator.TryMolarMass("Xy2O3", out _, out var error));
        Assert.Contains("Xy", error);
    }

    [Fact]
    public void Moles_MassOverMolarMass()
    {
        // 9 g 水 = 0.5 mol
        Assert.Equal(new Rational(1, 2), MoleCalculator.Moles(new Rational(9), "H2O"));
        // 28 g 铁 = 0.5 mol
        Assert.Equal(new Rational(1, 2), MoleCalculator.Moles(new Rational(28), "Fe"));
    }

    [Fact]
    public void Concentration_AndDilution()
    {
        // 0.5 mol 溶于 0.2 L → 2.5 mol/L
        Assert.Equal(new Rational(5, 2), MoleCalculator.MolarConcentration(new Rational(1, 2), new Rational(1, 5)));
        // 2 mol/L × 1 L 稀释到 4 L → 0.5 mol/L
        Assert.Equal(new Rational(1, 2), MoleCalculator.Dilute(new Rational(2), new Rational(1), new Rational(4)));
        // n = c·V
        Assert.Equal(new Rational(1, 2), MoleCalculator.MolesFromConcentration(new Rational(5, 2), new Rational(1, 5)));
    }

    [Fact]
    public void Yield_ActualOverTheoretical()
    {
        Assert.Equal(new Rational(4, 5), MoleCalculator.Yield(new Rational(8), new Rational(10)));
        Assert.Throws<ArgumentException>(() => MoleCalculator.Yield(new Rational(1), Rational.Zero));
    }

    [Fact]
    public void ParticlesToMoles_UsesAvogadroExactly()
    {
        // 1.204×10^24 个 = 2 mol
        Assert.Equal(new Rational(2), MoleCalculator.MolesFromParticles(new Rational(1204), 21));
        // 3.01×10^23 个 = 0.5 mol
        Assert.Equal(new Rational(1, 2), MoleCalculator.MolesFromParticles(new Rational(301), 21));
    }

    [Fact]
    public void GasVolumeAtStp_UsesStandardMolarVolume()
    {
        // 标准状况 44.8 L = 2 mol
        Assert.Equal(new Rational(2), MoleCalculator.MolesFromGasVolume(new Rational(448, 10)));
        // 标准状况 11.2 L CO2 = 0.5 mol
        Assert.Equal(new Rational(1, 2), MoleCalculator.MolesFromGasVolume(new Rational(112, 10)));
    }
}
