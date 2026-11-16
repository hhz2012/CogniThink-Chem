namespace CogniThink.Chem.Core.Math;

/// <summary>
/// 精确有理数（long 分子/分母，构造即约分，分母恒正）。
/// 配平、氧化数、摩尔计算全部走该类型，杜绝浮点误差（氧化还原配平是重灾区）。
/// 高中适用边界：系数与氧化数均为小数值有理数，long 范围足够；
/// 若中间运算溢出会抛出 <see cref="OverflowException"/>（checked 语义），不静默截断。
/// </summary>
public readonly struct Rational : IEquatable<Rational>, IComparable<Rational>
{
    /// <summary>分子（已约分，符号承载在这里）。</summary>
    public long Num { get; }

    /// <summary>分母（已约分，恒为正；整数时分母为 1）。</summary>
    public long Den { get; }

    /// <summary>构造有理数并立即约分。<paramref name="den"/> 为 0 且 <paramref name="num"/> 非 0 时抛异常；
    /// (0,0) 容忍为 0 —— 这使 <c>default(Rational)</c>（未赋值的数组元素）语义上等于零。</summary>
    /// <exception cref="ArgumentException">分母为 0 且分子非 0。</exception>
    public Rational(long num, long den = 1)
    {
        if (den == 0)
        {
            if (num != 0)
            {
                throw new ArgumentException("分母不能为 0", nameof(den));
            }

            Num = 0;
            Den = 1;
            return;
        }

        if (num == 0)
        {
            Num = 0;
            Den = 1;
            return;
        }

        if (den < 0)
        {
            num = checked(-num);
            den = checked(-den);
        }

        var g = Gcd(System.Math.Abs(num), den);
        Num = num / g;
        Den = den / g;
    }

    public static Rational Zero { get; } = new(0, 1);

    public static Rational One { get; } = new(1, 1);

    public bool IsZero => Num == 0;

    public bool IsInteger => IsZero || Den == 1;

    public bool IsNegative => Num < 0;

    /// <summary>
    /// 运算安全分母：default(Rational) 存储为 0/0，参与交叉相乘会把对方项错误清零，
    /// 所有算术一律经此属性取分母（0/0 按 0/1 参与运算）。
    /// </summary>
    private long SafeDen => Den == 0 ? 1 : Den;

    public Rational Negate() => new(checked(-Num), SafeDen);

    public Rational Abs() => Num >= 0 ? this : Negate();

    public Rational Reciprocal()
    {
        if (IsZero)
        {
            throw new DivideByZeroException("0 没有倒数");
        }

        return new Rational(SafeDen, Num);
    }

    public static Rational operator +(Rational a, Rational b) =>
        new(checked(a.Num * b.SafeDen + b.Num * a.SafeDen), checked(a.SafeDen * b.SafeDen));

    public static Rational operator -(Rational a, Rational b) =>
        new(checked(a.Num * b.SafeDen - b.Num * a.SafeDen), checked(a.SafeDen * b.SafeDen));

    public static Rational operator *(Rational a, Rational b) =>
        new(checked(a.Num * b.Num), checked(a.SafeDen * b.SafeDen));

    public static Rational operator /(Rational a, Rational b)
    {
        if (b.IsZero)
        {
            throw new DivideByZeroException("除数为 0");
        }

        return new Rational(checked(a.Num * b.SafeDen), checked(a.SafeDen * b.Num));
    }

    public static Rational operator -(Rational a) => a.Negate();

    public static bool operator ==(Rational a, Rational b) => a.Equals(b);

    public static bool operator !=(Rational a, Rational b) => !a.Equals(b);

    public static bool operator <(Rational a, Rational b) => a.CompareTo(b) < 0;

    public static bool operator >(Rational a, Rational b) => a.CompareTo(b) > 0;

    public static bool operator <=(Rational a, Rational b) => a.CompareTo(b) <= 0;

    public static bool operator >=(Rational a, Rational b) => a.CompareTo(b) >= 0;

    public bool Equals(Rational other) =>
        (IsZero && other.IsZero) || (Num == other.Num && Den == other.Den);

    public override bool Equals(object? obj) => obj is Rational r && Equals(r);

    public override int GetHashCode() => IsZero ? 0 : HashCode.Combine(Num, Den);

    public int CompareTo(Rational other)
    {
        if (IsZero && other.IsZero)
        {
            return 0;
        }

        if (IsZero)
        {
            return other.Num > 0 ? -1 : 1;
        }

        if (other.IsZero)
        {
            return Num > 0 ? 1 : -1;
        }

        return checked(Num * other.SafeDen).CompareTo(checked(other.Num * SafeDen));
    }

    /// <summary>
    /// 解析整数、分数或有限小数：支持 "3"、"-2"、"7/2"、"35.5"、"-0.5"。
    /// 高中常用相对原子质量（如 Cl 35.5、Cu 63.5）均可精确表示。
    /// </summary>
    /// <exception cref="FormatException">格式非法。</exception>
    public static Rational Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new FormatException("有理数字符串为空");
        }

        var s = text.Trim();
        var slash = s.IndexOf('/');
        if (slash >= 0)
        {
            return new Rational(long.Parse(s[..slash]), long.Parse(s[(slash + 1)..]));
        }

        var dot = s.IndexOf('.');
        if (dot < 0)
        {
            return new Rational(long.Parse(s));
        }

        var sign = 1L;
        if (s.StartsWith('-'))
        {
            sign = -1L;
            s = s[1..];
            dot--;
        }
        else if (s.StartsWith('+'))
        {
            s = s[1..];
            dot--;
        }

        var digits = s.Remove(dot, 1);
        var scale = Pow10(s.Length - dot - 1);
        return new Rational(sign * long.Parse(digits), scale);
    }

    /// <summary>10 的 n 次幂（n 为小数位数，规模很小；整数运算避免浮点）。</summary>
    private static long Pow10(int n)
    {
        var result = 1L;
        for (var i = 0; i < n; i++)
        {
            result = checked(result * 10);
        }

        return result;
    }

    /// <summary>整数则输出 "3"，分数输出 "7/2"，零输出 "0"。</summary>
    public override string ToString() => IsZero ? "0" : (Den == 1 ? Num.ToString() : $"{Num}/{Den}");

    /// <summary>最大公约数（辗转相除）。</summary>
    public static long Gcd(long a, long b)
    {
        a = System.Math.Abs(a);
        b = System.Math.Abs(b);
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }

        return a == 0 ? 1 : a;
    }

    /// <summary>最小公倍数（用于通分场景，如系数整数化前的预处理）。</summary>
    public static long Lcm(long a, long b)
    {
        if (a == 0 || b == 0)
        {
            return 0;
        }

        return checked(System.Math.Abs(a) / Gcd(a, b) * System.Math.Abs(b));
    }
}
