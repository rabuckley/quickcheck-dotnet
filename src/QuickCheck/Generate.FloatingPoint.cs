using System.Numerics;
using QuickCheck.Generators;

namespace QuickCheck;

public static partial class Generate
{
    /// <summary>
    /// Creates a generator for floating-point values over the full range of
    /// <typeparamref name="T"/>, including its non-finite values.
    /// </summary>
    /// <typeparam name="T">
    /// The IEEE 754 type to generate, such as <see cref="double"/>, <see cref="float"/> or
    /// <see cref="Half"/>.
    /// </typeparam>
    /// <returns>
    /// A generator that produces values of <typeparamref name="T"/> of either sign as an integer
    /// significand times a power of the radix, small exponents and short significands most often,
    /// produces <see cref="IFloatingPointIeee754{TSelf}.NaN"/>, both infinities,
    /// <see cref="IMinMaxValue{TSelf}.MinValue"/>, <see cref="IMinMaxValue{TSelf}.MaxValue"/>,
    /// <see cref="IFloatingPointIeee754{TSelf}.Epsilon"/> of either sign and negative zero more
    /// often than chance, and shrinks towards positive zero.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// The generic math of <typeparamref name="T"/> does not decompose into an integer significand
    /// and a radix exponent.
    /// </exception>
    /// <remarks>
    /// About 60% of doubles are integers. Shrinking minimises the exponent before the
    /// significand, so a failure that depends on a threshold can end on the round value just past
    /// it rather than on the threshold itself. For finite values only, bound the range:
    /// <c>Generate.FloatingPoint(double.MinValue, double.MaxValue)</c>.
    /// </remarks>
    public static Generator<T> FloatingPoint<T>() where T : IFloatingPointIeee754<T>, IMinMaxValue<T> =>
        FloatingPointGenerator<T>.Unbounded(Ieee754Format<T>.Instance, T.NaN);

    /// <summary>
    /// Creates a generator for floating-point values within a specified range.
    /// </summary>
    /// <typeparam name="T">
    /// The IEEE 754 type to generate, such as <see cref="double"/>, <see cref="float"/> or
    /// <see cref="Half"/>.
    /// </typeparam>
    /// <param name="min">The inclusive lower bound of the values to generate.</param>
    /// <param name="max">The inclusive upper bound of the values to generate.</param>
    /// <returns>
    /// A generator that produces values from <paramref name="min"/> to <paramref name="max"/>,
    /// distributed as <see cref="FloatingPoint{T}()"/> within the range, never NaN, and an infinity
    /// only when a bound is infinite, produces the bounds and whichever of
    /// <see cref="IMinMaxValue{TSelf}.MinValue"/>, <see cref="IMinMaxValue{TSelf}.MaxValue"/>,
    /// <see cref="IFloatingPointIeee754{TSelf}.Epsilon"/> of either sign and negative zero lie
    /// within it more often than chance, and shrinks towards positive zero, or towards the value
    /// nearest zero with the fewest significant digits when the range excludes it.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="min"/> or <paramref name="max"/> is NaN, or <paramref name="min"/> is greater
    /// than <paramref name="max"/>. The bounds are sign-aware for zero, so positive zero is greater
    /// than negative zero.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// The generic math of <typeparamref name="T"/> does not decompose into an integer significand
    /// and a radix exponent.
    /// </exception>
    /// <remarks>
    /// A bound of zero carries its sign: <c>FloatingPoint(0.0, 10.0)</c> never produces negative
    /// zero. For an exclusive bound pass <c>T.BitIncrement(min)</c> or <c>T.BitDecrement(max)</c>.
    /// The sign is drawn uniformly when the range spans zero, however narrow a side is, so
    /// <c>FloatingPoint(-1.0, 1000.0)</c> is negative half the time and
    /// <c>FloatingPoint(-0.0, 10.0)</c>, whose negative side holds nothing but negative zero, is
    /// negative zero half the time. To include NaN in a bounded range, add it with
    /// <c>Generate.Frequency((15, bounded), (1, Generate.Constant(double.NaN)))</c>.
    /// </remarks>
    public static Generator<T> FloatingPoint<T>(T min, T max) where T : IFloatingPointIeee754<T>, IMinMaxValue<T> =>
        new FloatingPointGenerator<T>(Ieee754Format<T>.Instance, min, max);

    /// <summary>
    /// Creates a generator for decimals over the full range of <see cref="decimal"/>.
    /// </summary>
    /// <returns>
    /// A generator that produces values of either sign as an integer coefficient times a power of
    /// ten, small scales and short coefficients most often but every scale from 0 to 28, produces
    /// <see cref="decimal.MinValue"/>, <see cref="decimal.MaxValue"/> and the smallest magnitude,
    /// <c>0.0000000000000000000000000001m</c>, of either sign more often than chance, and shrinks
    /// towards <c>0m</c>.
    /// </returns>
    /// <remarks>
    /// The scale is drawn, so members of a cohort such as <c>1.0m</c> and <c>1.00m</c>, which
    /// compare equal but print differently, both appear. Shrinking minimises the scale before the
    /// coefficient, so a failure that depends on a threshold can end on the round value just past
    /// it rather than on the threshold itself.
    /// </remarks>
    public static Generator<decimal> Decimal() => FloatingPointGenerator<decimal>.Unbounded(DecimalFormat.Instance);

    /// <summary>
    /// Creates a generator for decimals within a specified range.
    /// </summary>
    /// <param name="min">The inclusive lower bound of the values to generate.</param>
    /// <param name="max">The inclusive upper bound of the values to generate.</param>
    /// <returns>
    /// A generator that produces values from <paramref name="min"/> to <paramref name="max"/>,
    /// distributed as <see cref="Decimal()"/> within the range, produces the bounds and whichever
    /// of <see cref="decimal.MinValue"/>, <see cref="decimal.MaxValue"/> and
    /// <c>±0.0000000000000000000000000001m</c> lie within it more often than chance, and shrinks
    /// towards <c>0m</c>, or towards the value nearest zero with the fewest significant digits
    /// when the range excludes it.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="min"/> is greater than <paramref name="max"/>.
    /// </exception>
    /// <remarks>
    /// The sign is drawn uniformly when the range spans zero, so <c>Decimal(-1m, 1000m)</c> is
    /// negative half the time.
    /// </remarks>
    public static Generator<decimal> Decimal(decimal min, decimal max) =>
        new FloatingPointGenerator<decimal>(DecimalFormat.Instance, min, max);
}
