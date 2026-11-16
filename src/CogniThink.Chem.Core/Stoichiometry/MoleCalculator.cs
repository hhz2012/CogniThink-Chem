using CogniThink.Chem.Core.Chemistry;
using CogniThink.Chem.Core.Math;
using CogniThink.Chem.Core.Parsing;

namespace CogniThink.Chem.Core.Stoichiometry;

/// <summary>
/// 摩尔/浓度/产率计算器。全部采用 <see cref="Rational"/> 精确运算，杜绝浮点误差
/// （Cl 35.5、Cu 63.5 等半整数相对原子质量可精确表示）。
/// 高中适用边界：
/// <list type="bullet">
/// <item>相对原子质量取人教版教材附录常用值（一位小数精度）。</item>
/// <item>摩尔质量忽略电子质量（离子与中性分子同值）。</item>
/// <item>气体摩尔体积默认 22.4 L/mol，仅适用于标准状况（0 ℃、101 kPa）；
/// 条件是否成立由伴思层结合题干确认，引擎只按给定 Vm 计算。</item>
/// <item>阿伏加德罗常数取 6.02×10^23（尾数 602/100 × 10^23，精确有理表示）。</item>
/// </list>
/// 易错点：结晶水（CuSO4·5H2O）的摩尔质量必须包含结晶水，
/// 依赖 <see cref="FormulaParser"/> 的 "·" 合并解析。
/// </summary>
public static class MoleCalculator
{
    /// <summary>
    /// 高中常用相对原子质量表。来源：人教版《化学·必修第一册》附录常用相对原子质量。
    /// </summary>
    public static readonly IReadOnlyDictionary<string, Rational> AtomicMasses =
        new Dictionary<string, Rational>(StringComparer.Ordinal)
        {
            ["H"] = new(1), ["He"] = new(4), ["Li"] = new(7), ["Be"] = new(9),
            ["B"] = new(11), ["C"] = new(12), ["N"] = new(14), ["O"] = new(16),
            ["F"] = new(19), ["Ne"] = new(20), ["Na"] = new(23), ["Mg"] = new(24),
            ["Al"] = new(27), ["Si"] = new(28), ["P"] = new(31), ["S"] = new(32),
            ["Cl"] = Rational.Parse("35.5"), ["Ar"] = new(40), ["K"] = new(39),
            ["Ca"] = new(40), ["Cr"] = new(52), ["Mn"] = new(55), ["Fe"] = new(56),
            ["Co"] = new(59), ["Cu"] = Rational.Parse("63.5"), ["Zn"] = new(65),
            ["Br"] = new(80), ["Ag"] = new(108), ["Sn"] = new(119), ["I"] = new(127),
            ["Ba"] = new(137), ["Pt"] = new(195), ["Au"] = new(197), ["Hg"] = new(201),
            ["Pb"] = new(207)
        };

    /// <summary>阿伏加德罗常数尾数（6.02），与 <see cref="AvogadroExponent"/> 组合为 6.02×10^23。</summary>
    public static readonly Rational AvogadroMantissa = new(602, 100);

    /// <summary>阿伏加德罗常数的 10 的指数部分（23）。</summary>
    public const int AvogadroExponent = 23;

    /// <summary>标准状况气体摩尔体积 22.4 L/mol。</summary>
    public static readonly Rational StandardMolarVolume = new(224, 10);

    /// <summary>
    /// 计算摩尔质量（g/mol）。
    /// </summary>
    /// <param name="formula">化学式，如 "H2O"、"Fe2(SO4)3"、"CuSO4·5H2O"。电荷不影响结果。</param>
    /// <returns>精确有理数摩尔质量。</returns>
    /// <exception cref="FormulaException">化学式非法或含表外元素。</exception>
    public static Rational MolarMass(string formula)
    {
        var species = FormulaParser.Parse(formula);
        var total = Rational.Zero;
        foreach (var (element, count) in species.Elements)
        {
            if (!AtomicMasses.TryGetValue(element, out var mass))
            {
                throw new FormulaException($"元素 {element} 的相对原子质量未收录（高中常用表外）：{formula}");
            }

            total += mass * new Rational(count);
        }

        return total;
    }

    /// <summary>安全求摩尔质量；失败返回错误描述。</summary>
    public static bool TryMolarMass(string formula, out Rational mass, out string? error)
    {
        try
        {
            mass = MolarMass(formula);
            error = null;
            return true;
        }
        catch (FormulaException ex)
        {
            mass = Rational.Zero;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>物质的量 n = m / M。</summary>
    /// <param name="massGrams">质量（g）。</param>
    /// <param name="formula">化学式。</param>
    /// <exception cref="DivideByZeroException">摩尔质量为 0（不可能出现的化学式）。</exception>
    public static Rational Moles(Rational massGrams, string formula) =>
        massGrams / MolarMass(formula);

    /// <summary>物质的量浓度 c = n / V（V 单位 L）。</summary>
    /// <exception cref="DivideByZeroException">体积为 0。</exception>
    public static Rational MolarConcentration(Rational moles, Rational volumeLiters) =>
        moles / volumeLiters;

    /// <summary>溶液中溶质的物质的量 n = c·V。</summary>
    public static Rational MolesFromConcentration(Rational concentration, Rational volumeLiters) =>
        concentration * volumeLiters;

    /// <summary>稀释定律 c2 = c1·V1 / V2。</summary>
    /// <exception cref="DivideByZeroException">稀释后体积为 0。</exception>
    public static Rational Dilute(Rational c1, Rational v1, Rational v2) => c1 * v1 / v2;

    /// <summary>
    /// 产率 = 实际产量 / 理论产量（有理比值，1 = 100%）。
    /// 引擎只做计算；结果大于 1 属异常数据，由伴思层引导学生复查，引擎不篡改。
    /// </summary>
    /// <exception cref="ArgumentException">理论产量非正。</exception>
    public static Rational Yield(Rational actual, Rational theoretical)
    {
        if (theoretical.IsZero || theoretical.IsNegative)
        {
            throw new ArgumentException("理论产量必须为正", nameof(theoretical));
        }

        return actual / theoretical;
    }

    /// <summary>
    /// 由粒子数求物质的量：n = 尾数×10^指数 / (6.02×10^23)。
    /// 例：1.204×10^24 个分子 → MolesFromParticles(1204, 21) = 2 mol。
    /// 高中适用边界：尾数与指数取常见量级（10^18~10^27），超范围按 checked 语义抛溢出。
    /// </summary>
    public static Rational MolesFromParticles(Rational mantissa, int exponent)
    {
        var scaled = ScaleByPow10(mantissa, exponent - AvogadroExponent);
        return scaled / AvogadroMantissa;
    }

    /// <summary>
    /// 标准状况气体体积求物质的量：n = V / Vm。
    /// 易错点：非标准状况（常温常压等）不能用 22.4，条件判断属于题干理解，引擎不裁决。
    /// </summary>
    /// <param name="molarVolume">气体摩尔体积，默认 <see cref="StandardMolarVolume"/>。</param>
    public static Rational MolesFromGasVolume(Rational volumeLiters, Rational? molarVolume = null) =>
        volumeLiters / (molarVolume ?? StandardMolarVolume);

    /// <summary>有理数乘/除 10 的整数次幂（正幂乘、负幂除，精确无损）。</summary>
    private static Rational ScaleByPow10(Rational value, int power)
    {
        if (power == 0)
        {
            return value;
        }

        var factor = new Rational(Pow10(System.Math.Abs(power)));
        return power > 0 ? value * factor : value / factor;
    }

    private static long Pow10(int n)
    {
        var result = 1L;
        for (var i = 0; i < n; i++)
        {
            result = checked(result * 10);
        }

        return result;
    }
}
