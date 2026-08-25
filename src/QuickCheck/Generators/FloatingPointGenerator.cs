using System.Numerics;
using QuickCheck.Choices;

namespace QuickCheck.Generators;

/// <summary>
/// Generates floating-point values in [min, max] as an integer significand times a power of the
/// radix, most significant component first: the sign (when both are admissible), the bit width
/// of the exponent, the exponent within that width, then the significand's bit length and the
/// significand within the bounds that exponent leaves. Choice 0 throughout is +0, or the value
/// nearest zero with the fewest digits at the simplest exponent when the range excludes zero, and
/// lowering the exponent width before the significand turns a shrunk fraction into an integer
/// that the significand pass then binary-searches. The non-finite values sit at the top of their
/// own exponent's significands, so a non-finite counterexample shrinks down through large finite
/// values rather than dropping to zero; an infinity is admissible on a side whose bound is
/// infinite, and NaN only on <see cref="Unbounded(IFloatingPointFormat{T}, T)"/>. One draw in
/// sixteen forces one of the range's edges through the same choices.
/// </summary>
/// <remarks>
/// Each side of zero has its own admissible exponent interval, which <see cref="Side"/> works out.
/// The width buckets are symmetric about zero, so clamping a large positive exponent into a
/// narrower width lands on the bucket's most negative end; the shrinker still converges,
/// intermediate candidates just look odd.
/// </remarks>
internal sealed class FloatingPointGenerator<T> : Generator<T> where T : IFloatingPoint<T>
{
    private readonly IFloatingPointFormat<T> _format;
    private readonly Side? _positive;
    private readonly Side? _negative;
    private readonly bool _wideSignificand;
    private readonly FloatingPointParts[] _edges;

    public FloatingPointGenerator(IFloatingPointFormat<T> format, T min, T max)
        : this(format, min, max, nan: null)
    {
    }

    private FloatingPointGenerator(IFloatingPointFormat<T> format, T min, T max, FloatingPointParts? nan)
    {
        if (T.IsNaN(min))
        {
            throw new ArgumentOutOfRangeException(nameof(min), "min must not be NaN.");
        }

        if (T.IsNaN(max))
        {
            throw new ArgumentOutOfRangeException(nameof(max), "max must not be NaN.");
        }

        var positiveZeroAboveNegativeZero = T.IsZero(min) && T.IsZero(max) && !T.IsNegative(min) && T.IsNegative(max);

        if (min > max || positiveZeroAboveNegativeZero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(min), $"min ({ValueFormatter.Format(min)}) must not exceed max ({ValueFormatter.Format(max)}).");
        }

        _format = format;
        _wideSignificand = format.MaxSignificand > ulong.MaxValue;

        if (!T.IsNegative(max))
        {
            _positive = new Side(format, nan, lo: T.IsNegative(min) ? T.Zero : min, hi: max);
        }

        if (T.IsNegative(min))
        {
            _negative = new Side(format, nan, lo: T.IsNegative(max) ? T.Abs(max) : T.Zero, hi: T.Abs(min));
        }

        // An edge takes its canonical exponent, so shrinking a forced one starts from the fewest
        // digits rather than from whatever exponent the format decomposed to.
        var edges = new List<FloatingPointParts> { format.Canonical(min), format.Canonical(max) };

        foreach (var edge in format.TypeEdges)
        {
            if (min <= edge && edge <= max)
            {
                edges.Add(format.Canonical(edge));
            }
        }

        var negativeZero = new FloatingPointParts(Negative: true, UInt128.Zero, Exponent: 0);

        if (_negative is { } negative && T.IsZero(negative.Lo) && T.IsNegative(format.Compose(negativeZero)))
        {
            edges.Add(negativeZero);
        }

        if (nan is { } nanEdge)
        {
            edges.Add(nanEdge);
        }

        _edges = [.. edges.Distinct()];
    }

    /// <summary>
    /// A generator over the whole of <typeparamref name="T"/> including <paramref name="nan"/>,
    /// the only range whose values include NaN.
    /// </summary>
    public static FloatingPointGenerator<T> Unbounded(IFloatingPointFormat<T> format, T nan)
    {
        var (min, max) = format.FullRange;
        return new FloatingPointGenerator<T>(format, min, max, format.Canonical(nan));
    }

    internal ReadOnlySpan<FloatingPointParts> Edges => _edges;

    protected internal override T Generate(ChoiceSource source) =>
        Draw(source, source.SampleEdge<FloatingPointParts>(_edges));

    /// <summary>
    /// Draws a value, or emits <paramref name="forced"/> through the same choices while
    /// generating.
    /// </summary>
    internal T Draw(ChoiceSource source, FloatingPointParts? forced)
    {
        bool negative;

        if (_positive is not null && _negative is not null)
        {
            negative = Choice(source, forced is { } f ? f.Negative ? 1UL : 0UL : null, maxInclusive: 1) == 1;
        }
        else
        {
            // A fixed choice keeps the layout the same length on every range, so a replayed
            // sequence lines up whichever side it came from.
            Choice(source, forced is null ? null : 0UL, maxInclusive: 0);
            negative = _negative is not null;
        }

        var side = negative ? _negative! : _positive!;
        var width = forced is { } forcedWidth
            ? side.Widths.Force(source, BitLength(Math.Abs(forcedWidth.Exponent)))
            : side.Widths.Draw(source);
        var limit = (1 << width) - 1;
        var exponents = new IntegerRange<int>(Math.Max(side.ExponentLow, -limit), Math.Min(side.ExponentHigh, limit));
        var exponent = forced is { } forcedExponent
            ? exponents.Force(source, forcedExponent.Exponent)
            : exponents.Draw(source);
        var (low, high) = side.Bounds(exponent);
        var significands = new SignificandRange(low, high, _wideSignificand);
        var significand = forced is { } forcedSignificand
            ? significands.Force(source, forcedSignificand.Significand)
            : significands.Draw(source);

        return _format.Compose(new FloatingPointParts(negative, significand, exponent));
    }

    private static ulong Choice(ChoiceSource source, ulong? forced, ulong maxInclusive) =>
        forced is { } value ? source.ForceChoice(value, maxInclusive) : source.NextChoice(maxInclusive);

    private static int BitLength(int value) => 32 - BitOperations.LeadingZeroCount((uint)value);

    /// <summary>
    /// The magnitudes one sign can take, [<see cref="Lo"/>, <see cref="Hi"/>], and the exponents
    /// admissible for them: those at which some nonzero multiple lies within the magnitudes with
    /// a significand within the cap, an interval because "a multiple fits" is downward closed in
    /// the exponent and "the significand fits" is upward closed. The format's
    /// <see cref="IFloatingPointFormat{T}.SignificandBounds"/> answers admissibility, and admits
    /// the non-finite exponent when <see cref="Hi"/> is infinite. A side of nothing but zero
    /// admits only exponent 0.
    /// </summary>
    private sealed class Side
    {
        private readonly IFloatingPointFormat<T> _format;
        private readonly FloatingPointParts? _nan;

        public Side(IFloatingPointFormat<T> format, FloatingPointParts? nan, T lo, T hi)
        {
            _format = format;
            _nan = nan;
            Lo = lo;
            Hi = hi;

            if (T.IsZero(hi))
            {
                (ExponentLow, ExponentHigh) = (0, 0);
            }
            else
            {
                // Each bound's own exponent is admissible, so walking out from it reaches the
                // interval's end in few steps: none down for an IEEE type, whose Decompose already
                // takes the lowest exponent that fits the cap, at most the scale range down for a
                // decimal such as 5m, and at most the precision up.
                var low = T.IsZero(lo) ? format.MinExponent : format.Decompose(lo).Exponent;
                var high = format.Decompose(hi).Exponent;

                while (Admits(low - 1))
                {
                    low--;
                }

                while (Admits(high + 1))
                {
                    high++;
                }

                (ExponentLow, ExponentHigh) = (low, high);
            }

            var containsZero = ExponentLow <= 0 && 0 <= ExponentHigh;
            var widthLow = containsZero ? 0 : BitLength(Math.Min(Math.Abs(ExponentLow), Math.Abs(ExponentHigh)));
            var widthHigh = Math.Max(BitLength(Math.Abs(ExponentLow)), BitLength(Math.Abs(ExponentHigh)));
            Widths = new IntegerRange<int>(widthLow, widthHigh);
        }

        public T Lo { get; }

        public T Hi { get; }

        public int ExponentLow { get; }

        public int ExponentHigh { get; }

        public IntegerRange<int> Widths { get; }

        /// <summary>
        /// The significands admissible at <paramref name="exponent"/>: the format's, with NaN
        /// raising the top of its exponent on the one generator that includes it. That NaN sits
        /// above the infinities is the only part of the format's encoding the generator relies on.
        /// </summary>
        public (UInt128 Low, UInt128 High) Bounds(int exponent)
        {
            var (low, high) = _format.SignificandBounds(Lo, Hi, exponent);
            return _nan is { } nan && exponent == nan.Exponent ? (low, nan.Significand) : (low, high);
        }

        private bool Admits(int exponent)
        {
            var (low, high) = Bounds(exponent);
            return high >= UInt128.Max(low, UInt128.One);
        }
    }
}
