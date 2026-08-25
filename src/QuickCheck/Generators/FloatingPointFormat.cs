using System.Numerics;

namespace QuickCheck.Generators;

/// <summary>
/// The sign, integer significand and radix exponent of a value, <c>±k · r^e</c> for a finite one.
/// What the non-finite values take is the format's to fix, in
/// <see cref="IFloatingPointFormat{T}.Decompose"/> and <see cref="IFloatingPointFormat{T}.Compose"/>.
/// </summary>
internal readonly record struct FloatingPointParts(bool Negative, UInt128 Significand, int Exponent);

/// <summary>
/// What <see cref="FloatingPointGenerator{T}"/> needs from a floating-point type to draw it as an
/// integer significand times a power of the radix: its extent and extremes, exact conversions
/// between a value and its parts, and which significands at an exponent land inside a range.
/// </summary>
internal interface IFloatingPointFormat<T>
{
    /// <summary>The exponent of the smallest positive value, the finest quantum.</summary>
    int MinExponent { get; }

    /// <summary>The largest integer significand, the cap on every significand the format composes.</summary>
    UInt128 MaxSignificand { get; }

    /// <summary>
    /// The whole of the type: the infinities where it has them, its finite bounds otherwise.
    /// </summary>
    (T Min, T Max) FullRange { get; }

    /// <summary>The type's own extremes: its finite bounds and the smallest values of either sign.</summary>
    ReadOnlySpan<T> TypeEdges { get; }

    /// <summary>
    /// The value the parts encode, exactly: <c>Compose(Decompose(x))</c> is <c>x</c> for every
    /// value, non-finite included. A zero significand with a negative sign is negative zero where
    /// the type has one.
    /// </summary>
    T Compose(FloatingPointParts parts);

    /// <summary>
    /// Parts that compose back to <paramref name="value"/> exactly, at the exponent of its own
    /// encoding: zero is significand 0 at exponent 0. A non-finite value takes the exponent past
    /// the finite range, NaN with the largest significand there and every smaller one an infinity
    /// of the parts' sign.
    /// </summary>
    FloatingPointParts Decompose(T value);

    /// <summary>
    /// <see cref="Decompose"/> at the largest exponent at which the significand is still a whole
    /// number, which pins a value to one triple: <c>1.5</c> is <c>(3, −1)</c> rather than
    /// <c>(3 · 2^51, −52)</c>, <c>1.00m</c> is <c>(1, 0)</c>. Zero, whose exponent says nothing,
    /// and the non-finite values decompose as they are.
    /// </summary>
    FloatingPointParts Canonical(T value);

    /// <summary>
    /// The significands, [<c>Low</c>, <c>High</c>], for which the value encoded at
    /// <paramref name="exponent"/> lies in [<paramref name="lo"/>, <paramref name="hi"/>]
    /// (<c>0 ≤ lo ≤ hi</c>, <paramref name="hi"/> possibly infinite): <c>ceil(lo / r^e)</c> to
    /// <c>floor(hi / r^e)</c> capped at <see cref="MaxSignificand"/> at a finite exponent, and
    /// the infinities' significands at their exponent when <paramref name="hi"/> is infinite.
    /// <c>Low > High</c> when none fits: the exponent is outside the format's, or the ceiling
    /// exceeds the cap.
    /// </summary>
    (UInt128 Low, UInt128 High) SignificandBounds(T lo, T hi, int exponent);
}

/// <summary>The arithmetic on parts that the formats share.</summary>
file static class FormatArithmetic
{
    /// <summary>No significand fits: a <c>Low</c> above its <c>High</c>.</summary>
    public static readonly (UInt128 Low, UInt128 High) NoSignificands = (UInt128.One, UInt128.Zero);

    /// <summary>
    /// <paramref name="parts"/> at the largest exponent up to <paramref name="maxExponent"/> at
    /// which the significand is still a whole number. Zero and parts already at or above the
    /// ceiling come back unchanged.
    /// </summary>
    public static FloatingPointParts Reduce(FloatingPointParts parts, int radix, int maxExponent)
    {
        if (parts.Significand == UInt128.Zero)
        {
            return parts;
        }

        var step = (UInt128)radix;
        var significand = parts.Significand;
        var exponent = parts.Exponent;

        while (exponent < maxExponent && significand % step == UInt128.Zero)
        {
            significand /= step;
            exponent++;
        }

        return parts with { Significand = significand, Exponent = exponent };
    }
}

/// <summary>
/// The format of any IEEE 754 type, derived from its generic math: the precision is the exponent
/// of the first power of the radix whose successor is not representable, and the exponent range
/// follows from <c>ILogB(MaxValue)</c> by the IEEE identity <c>emin = 1 − emax</c>. The
/// non-finite values sit one exponent past <see cref="MaxExponent"/>, significand
/// <see cref="MaxSignificand"/> for NaN and any smaller one for the infinity of the parts' sign,
/// so lowering the exponent of an infinity lands on a large finite value rather than on zero.
/// <c>ILogB</c> is never called on a subnormal, because <see cref="Half"/>'s is wrong there.
/// </summary>
internal sealed class Ieee754Format<T> : IFloatingPointFormat<T> where T : IFloatingPointIeee754<T>, IMinMaxValue<T>
{
    // Built outside the type initialiser, and by a Lazy whose mode caches and rethrows the
    // failure, so a type that fails the self-check throws NotSupportedException on every access
    // rather than a TypeInitializationException wrapping it once.
    private static readonly Lazy<Ieee754Format<T>> LazyInstance =
        new(static () => new Ieee754Format<T>(), LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly T _radix;
    private readonly T _cap;
    private readonly T _smallestNormal;
    private readonly T[] _typeEdges;
    private readonly int _nonFiniteExponent;
    private readonly UInt128 _nan;
    private readonly UInt128 _maxInfinity;

    private Ieee754Format()
    {
        try
        {
            Radix = T.Radix;
            _radix = T.CreateChecked(Radix);

            var power = T.One;

            while (power + T.One != power)
            {
                power *= _radix;
                Precision++;

                if (Precision > 127)
                {
                    throw Unsupported();
                }
            }

            var maxValueLog = T.ILogB(T.MaxValue);
            MaxExponent = maxValueLog - Precision + 1;
            MinExponent = 2 - maxValueLog - Precision;
            MaxSignificand = Power(Radix, Precision) - UInt128.One;
            _cap = T.CreateChecked(MaxSignificand);
            _nonFiniteExponent = MaxExponent + 1;
            _nan = MaxSignificand;
            _maxInfinity = MaxSignificand - UInt128.One;
            _smallestNormal = T.ScaleB(T.One, 1 - maxValueLog);
        }
        catch (OverflowException)
        {
            throw Unsupported();
        }

        if (Compose(new FloatingPointParts(false, MaxSignificand, MaxExponent)) != T.MaxValue
            || Compose(new FloatingPointParts(false, UInt128.One, MinExponent)) != T.Epsilon
            || T.CreateChecked(Power(Radix, Precision - 1)) != T.ScaleB(_smallestNormal, -MinExponent))
        {
            throw Unsupported();
        }

        _typeEdges = [T.MinValue, T.MaxValue, T.Epsilon, -T.Epsilon];
    }

    /// <exception cref="NotSupportedException">
    /// <typeparamref name="T"/>'s generic math does not decompose into an integer significand and
    /// a radix exponent.
    /// </exception>
    public static Ieee754Format<T> Instance => LazyInstance.Value;

    public int Radix { get; }

    /// <summary>The number of radix digits in <see cref="MaxSignificand"/>.</summary>
    public int Precision { get; }

    public int MinExponent { get; }

    /// <summary>The exponent of <c>MaxValue</c>'s integer significand.</summary>
    public int MaxExponent { get; }

    public UInt128 MaxSignificand { get; }

    public (T Min, T Max) FullRange => (T.NegativeInfinity, T.PositiveInfinity);

    public ReadOnlySpan<T> TypeEdges => _typeEdges;

    public T Compose(FloatingPointParts parts)
    {
        if (parts.Exponent >= _nonFiniteExponent)
        {
            return parts.Significand == _nan
                ? T.NaN
                : parts.Negative ? T.NegativeInfinity : T.PositiveInfinity;
        }

        var value = T.ScaleB(T.CreateChecked(parts.Significand), parts.Exponent);
        return parts.Negative ? -value : value;
    }

    public FloatingPointParts Decompose(T value)
    {
        if (T.IsNaN(value))
        {
            return new FloatingPointParts(false, _nan, _nonFiniteExponent);
        }

        var negative = T.IsNegative(value);

        if (T.IsInfinity(value))
        {
            return new FloatingPointParts(negative, _maxInfinity, _nonFiniteExponent);
        }

        if (T.IsZero(value))
        {
            return new FloatingPointParts(negative, UInt128.Zero, 0);
        }

        var magnitude = T.Abs(value);
        var exponent = Math.Max(MinExponent, Magnitude(magnitude) - Precision + 1);
        return new FloatingPointParts(negative, UInt128.CreateChecked(T.ScaleB(magnitude, -exponent)), exponent);
    }

    public FloatingPointParts Canonical(T value) => FormatArithmetic.Reduce(Decompose(value), Radix, MaxExponent);

    public (UInt128 Low, UInt128 High) SignificandBounds(T lo, T hi, int exponent)
    {
        if (exponent == _nonFiniteExponent)
        {
            return T.IsInfinity(hi) ? (UInt128.Zero, _maxInfinity) : FormatArithmetic.NoSignificands;
        }

        if (exponent < MinExponent || exponent > MaxExponent || T.IsInfinity(lo))
        {
            return FormatArithmetic.NoSignificands;
        }

        var lowQuotient = T.ScaleB(lo, -exponent);

        if (!T.IsFinite(lowQuotient) || lowQuotient > _cap)
        {
            return FormatArithmetic.NoSignificands;
        }

        var lowCeiling = T.Ceiling(lowQuotient);

        // The quotient underflowed: lo is positive but below the quantum, so 1 is the first
        // multiple above it.
        var low = T.IsZero(lowCeiling) && !T.IsZero(lo) ? UInt128.One : UInt128.CreateChecked(lowCeiling);

        var highQuotient = T.ScaleB(hi, -exponent);
        var high = !T.IsFinite(highQuotient) || highQuotient > _cap
            ? MaxSignificand
            : UInt128.CreateChecked(T.Floor(highQuotient));

        return (low, high);
    }

    /// <summary><c>floor(log_r |x|)</c> of a finite non-zero <paramref name="value"/>.</summary>
    private int Magnitude(T value)
    {
        var magnitude = T.Abs(value);

        if (magnitude >= _smallestNormal)
        {
            return T.ILogB(magnitude);
        }

        var quantum = UInt128.CreateChecked(T.ScaleB(magnitude, -MinExponent));
        return MinExponent + DigitCount(quantum) - 1;
    }

    private int DigitCount(UInt128 value)
    {
        var radix = (UInt128)Radix;
        var digits = 0;

        for (; value != UInt128.Zero; value /= radix)
        {
            digits++;
        }

        return digits;
    }

    private static UInt128 Power(int radix, int exponent)
    {
        var result = UInt128.One;

        for (var i = 0; i < exponent; i++)
        {
            result = checked(result * (UInt128)radix);
        }

        return result;
    }

    private static NotSupportedException Unsupported() =>
        new($"{typeof(T).Name}'s generic math does not decompose into an integer significand and a radix exponent; "
            + "build its generator from Generate.From instead.");
}
