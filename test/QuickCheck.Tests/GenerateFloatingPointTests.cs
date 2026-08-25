using System.Numerics;
using System.Runtime.InteropServices;
using QuickCheck.Choices;

namespace QuickCheck.Tests;

public sealed class GenerateFloatingPointTests
{
    private static T Minimal<T>(Generator<T> generator, Func<T, bool>? property = null, ulong seed = 19) =>
        Property.ForAll(generator, property ?? (static _ => false)).Check(new CheckOptions { Seed = seed }).Minimal!.Value;

    [Fact]
    public void FloatingPoint_WithFullRange_ShouldProduceEverySpecialValue()
    {
        // Act
        var doubles = Generate.FloatingPoint<double>().Sample(count: 2000, seed: 1);
        var singles = Generate.FloatingPoint<float>().Sample(count: 2000, seed: 2);
        var halves = Generate.FloatingPoint<Half>().Sample(count: 2000, seed: 3);
        var natives = Generate.FloatingPoint<NFloat>().Sample(count: 2000, seed: 4);

        // Assert
        Assert.Multiple(
            () => AssertEverySpecialValue(doubles),
            () => AssertEverySpecialValue(singles),
            () => AssertEverySpecialValue(halves),
            () => AssertEverySpecialValue(natives));
    }

    private static void AssertEverySpecialValue<T>(IReadOnlyList<T> samples) where T : IFloatingPointIeee754<T>, IMinMaxValue<T> =>
        Assert.Multiple(
            () => AssertAny(samples, "NaN", static x => T.IsNaN(x)),
            () => AssertAny(samples, "+Infinity", static x => T.IsPositiveInfinity(x)),
            () => AssertAny(samples, "-Infinity", static x => T.IsNegativeInfinity(x)),
            () => AssertAny(samples, "+0", static x => T.IsZero(x) && !T.IsNegative(x)),
            () => AssertAny(samples, "-0", static x => T.IsZero(x) && T.IsNegative(x)),
            () => AssertAny(samples, "a subnormal", static x => T.IsSubnormal(x)),
            () => AssertAny(samples, "MinValue", static x => x == T.MinValue),
            () => AssertAny(samples, "MaxValue", static x => x == T.MaxValue),
            () => AssertAny(samples, "Epsilon", static x => x == T.Epsilon),
            () => AssertAny(samples, "an integer in 1 to 100", static x => T.IsInteger(x) && x >= T.One && x <= T.CreateChecked(100)),
            () => AssertAny(samples, "a non-integer", static x => T.IsFinite(x) && !T.IsInteger(x)));

    /// <summary>Names what was looked for, so a failure says more than "filter not matched".</summary>
    private static void AssertAny<T>(IReadOnlyList<T> samples, string description, Func<T, bool> predicate) =>
        Assert.True(samples.Any(predicate), $"no {description} among the {samples.Count} {typeof(T).Name} samples");

    private static void AssertNone<T>(IReadOnlyList<T> samples, string description, Func<T, bool> predicate) =>
        Assert.True(
            !samples.Any(predicate),
            $"{samples.Count(predicate)} of the {samples.Count} {typeof(T).Name} samples are {description}");

    // The distinct count is what stops a generator that emits nothing but the two bounds from
    // satisfying every other assertion here. It is a floor, not the observed value, so a relayout
    // of the choices moves it without breaking the test. A degenerate range has one value to reach.
    [Theory]
    [InlineData(0.0, 1000.0, 500)]
    [InlineData(0.3, 0.9, 300)]
    [InlineData(-1.0, 1.0, 500)]
    [InlineData(1e300, 1e301, 500)]
    [InlineData(double.Epsilon, double.MaxValue, 500)]
    [InlineData(2.5, 2.5, 1)]
    public void FloatingPoint_WithBounds_ShouldSpreadAcrossTheRangeAndReachBothBounds(double min, double max, int leastDistinct)
    {
        // Act
        var samples = Generate.FloatingPoint(min, max).Sample(count: 2000, seed: 5);

        // Assert
        Assert.All(samples, x => Assert.InRange(x, min, max));
        AssertNone(samples, "NaN", static x => double.IsNaN(x));
        Assert.InRange(samples.Count(x => x == min), 20, 2000);
        Assert.InRange(samples.Count(x => x == max), 20, 2000);
        Assert.InRange(samples.Distinct().Count(), leastDistinct, 2000);
    }

    [Fact]
    public void FloatingPoint_WithHalfBounds_ShouldStayInRangeAndReachBothBounds()
    {
        // Act
        var halves = Generate.FloatingPoint((Half)(-5), (Half)5).Sample(count: 500, seed: 6);

        // Assert
        Assert.All(halves, x => Assert.InRange(x, (Half)(-5), (Half)5));
        Assert.Contains((Half)(-5), halves);
        Assert.Contains((Half)5, halves);
        Assert.Contains(halves, static x => !Half.IsInteger(x));
    }

    [Fact]
    public void FloatingPoint_WithSignedZeroBounds_ShouldHonourTheSignOfZero()
    {
        // Act
        var negativeZero = Generate.FloatingPoint(-0.0, -0.0).Sample(count: 50, seed: 7);
        var positiveZero = Generate.FloatingPoint(0.0, 0.0).Sample(count: 50, seed: 8);
        var nonNegative = Generate.FloatingPoint(0.0, 5.0).Sample(count: 2000, seed: 9);
        var fromNegativeZero = Generate.FloatingPoint(-0.0, 5.0).Sample(count: 2000, seed: 10);

        // Assert
        Assert.Multiple(
            () => Assert.All(negativeZero, static x => Assert.True(x == 0 && double.IsNegative(x), $"{ValueFormatter.Format(x)} is not -0")),
            () => Assert.All(positiveZero, static x => Assert.True(x == 0 && !double.IsNegative(x), $"{ValueFormatter.Format(x)} is not +0")),
            () => AssertNone(nonNegative, "negative in [0, 5]", static x => double.IsNegative(x)),
            () => AssertAny(nonNegative, "+0 in [0, 5]", static x => x == 0),
            () => AssertAny(fromNegativeZero, "-0 in [-0, 5]", static x => x == 0 && double.IsNegative(x)),
            () => AssertAny(fromNegativeZero, "a value above zero in [-0, 5]", static x => x > 0),
            () => AssertNone(fromNegativeZero, "below zero in [-0, 5]", static x => x < 0));
    }

    [Fact]
    public void FloatingPoint_WithAnInfiniteBound_ShouldProduceThatInfinityButNeverNaN()
    {
        // Act
        var nonNegative = Generate.FloatingPoint(0.0, double.PositiveInfinity).Sample(count: 2000, seed: 11);
        var nonPositive = Generate.FloatingPoint(float.NegativeInfinity, 0f).Sample(count: 2000, seed: 12);
        var infinite = Generate.FloatingPoint(double.PositiveInfinity, double.PositiveInfinity).Sample(count: 20, seed: 13);

        // Assert
        Assert.Multiple(
            () => AssertAny(nonNegative, "+Infinity in [0, +Infinity]", static x => double.IsPositiveInfinity(x)),
            () => AssertNone(nonNegative, "NaN or negative in [0, +Infinity]", static x => double.IsNaN(x) || double.IsNegative(x)),
            () => AssertAny(nonNegative, "a finite value above zero in [0, +Infinity]", static x => double.IsFinite(x) && x > 0),
            () => AssertAny(nonPositive, "-Infinity in [-Infinity, 0]", static x => float.IsNegativeInfinity(x)),
            () => AssertNone(nonPositive, "NaN or above zero in [-Infinity, 0]", static x => float.IsNaN(x) || x > 0),
            () => AssertAny(nonPositive, "MinValue in [-Infinity, 0]", static x => x == float.MinValue),
            () => Assert.All(infinite, static x => Assert.Equal(double.PositiveInfinity, x)));
    }

    [Fact]
    public void FloatingPoint_WithInvalidBounds_ShouldThrowArgumentOutOfRangeException()
    {
        // Act
        var nanMin = Assert.Throws<ArgumentOutOfRangeException>(() => Generate.FloatingPoint(double.NaN, 1.0));
        var nanMax = Assert.Throws<ArgumentOutOfRangeException>(() => Generate.FloatingPoint(1.0, double.NaN));
        var inverted = Assert.Throws<ArgumentOutOfRangeException>(() => Generate.FloatingPoint(1.0, 0.0));
        var invertedZeros = Assert.Throws<ArgumentOutOfRangeException>(() => Generate.FloatingPoint(0.0, -0.0));
        var invertedHalves = Assert.Throws<ArgumentOutOfRangeException>(() => Generate.FloatingPoint(Half.One, Half.Zero));

        // Assert
        Assert.Equal("min", nanMin.ParamName);
        Assert.Equal("max", nanMax.ParamName);
        Assert.Equal("min", inverted.ParamName);
        Assert.Equal("min", invertedZeros.ParamName);
        Assert.Equal("min", invertedHalves.ParamName);
    }

    [Fact]
    public void FloatingPoint_WithFullRange_ShouldFavourSmallExponentsButStillReachTheWholeRange()
    {
        // Act
        var samples = Generate.FloatingPoint<double>().Sample(count: 4000, seed: 14);

        // Assert
        Assert.InRange(samples.Count(static x => x != 0 && Math.Abs(x) <= 1e6), 1000, 4000);
        Assert.InRange(samples.Count(HasSmallExponent), 1000, 2000);
        Assert.InRange(samples.Count(static x => double.IsInteger(x)), 1600, 3200);

        // Drawn rather than forced: the edges alone cannot satisfy these, so a generator whose
        // exponent never leaves the small buckets fails here even though every edge still appears.
        Assert.InRange(samples.Count(static x => double.IsFinite(x) && Math.Abs(x) > 1e100 && Math.Abs(x) != double.MaxValue), 50, 4000);
        Assert.InRange(samples.Count(static x => double.IsSubnormal(x) && Math.Abs(x) != double.Epsilon), 3, 4000);

        // k · 2^e for an integer k and |e| <= 7, said without the generator's own decomposition: a
        // multiple of 2^-7 that is not a multiple of 2^8. Zero and the non-finite values fail both.
        static bool HasSmallExponent(double value) =>
            double.IsInteger(Math.ScaleB(value, 7)) && !double.IsInteger(Math.ScaleB(value, -8));
    }

    [Fact]
    public void FloatingPoint_WithFalsifiedProperty_ShouldShrinkTowardsTheSimplestValue()
    {
        // Act
        var full = Minimal(Generate.FloatingPoint<double>());
        var fraction = Minimal(Generate.FloatingPoint(0.3, 0.9));
        var narrowFraction = Minimal(Generate.FloatingPoint(0.3, 0.4));
        var positive = Minimal(Generate.FloatingPoint(3.0, 9.0));
        var negative = Minimal(Generate.FloatingPoint(-9.0, -3.0));
        var huge = Minimal(Generate.FloatingPoint(1e300, 1e301));
        var halves = Minimal(Generate.FloatingPoint<Half>());

        // Assert
        Assert.Multiple(
            () => Assert.True(full == 0 && !double.IsNegative(full), $"the full range shrank to {ValueFormatter.Format(full)}"),
            () => Assert.Equal(0.5, fraction),
            () => Assert.Equal(0.375, narrowFraction),
            () => Assert.Equal(3.0, positive),
            () => Assert.Equal(-3.0, negative),
            () => Assert.Equal(1e300, huge),
            () => Assert.True(halves == Half.Zero && !Half.IsNegative(halves), $"Half shrank to {ValueFormatter.Format(halves)}"));
    }

    [Fact]
    public void FloatingPoint_WithAThresholdProperty_ShouldShrinkToJustPastTheThreshold()
    {
        // Act
        var minimal = Enumerable.Range(1, 8)
            .Select(seed => Minimal(Generate.FloatingPoint<double>(), static x => x <= 100, (ulong)seed))
            .ToList();

        // Assert
        Assert.All(minimal, static x => Assert.True(double.IsInteger(x)));
        Assert.All(minimal, static x => Assert.InRange(x, 101, 128));
        Assert.Contains(101.0, minimal);
    }

    [Fact]
    public void FloatingPoint_WithAFailureOnlyAtNaN_ShouldShrinkToNaN()
    {
        // Act
        var result = Property.ForAll(Generate.FloatingPoint<double>(), static x => !double.IsNaN(x))
            .Check(new CheckOptions { Seed = 15, RunCount = 1000 });

        // Assert
        Assert.True(double.IsNaN(result.Minimal!.Value));
    }

    [Fact]
    public void FloatingPoint_WithAFailureOnlyForNegatives_ShouldShrinkToMinusOne()
    {
        // Act
        var minimal = Minimal(Generate.FloatingPoint<double>(), static x => x >= 0);
        var minimalHalf = Minimal(Generate.FloatingPoint<Half>(), static x => x >= Half.Zero);

        // Assert
        Assert.Equal(-1.0, minimal);
        Assert.Equal(Half.NegativeOne, minimalHalf);
    }

    [Fact]
    public void FloatingPoint_WithAListSumProperty_ShouldShrinkToOneElement()
    {
        // Act
        var minimal = Minimal(Generate.FloatingPoint<double>().List(), static list => list.Sum() <= 100);

        // Assert
        var element = Assert.Single(minimal);
        Assert.InRange(element, 100, 200);
    }

    [Fact]
    public void FloatingPoint_OnAnyReplayedVariant_ShouldStayInRange()
    {
        // Act & Assert
        Assert.Multiple(
            () => AssertEveryReplayedVariantStaysInRange(Generate.FloatingPoint(0.0, 1000.0), 0, 1000),
            () => AssertEveryReplayedVariantStaysInRange(Generate.FloatingPoint(0.3, 0.9), 0.3, 0.9),
            () => AssertEveryReplayedVariantStaysInRange(Generate.FloatingPoint(-1.0, 1e10), -1, 1e10),
            () => AssertEveryReplayedVariantStaysInRange(Generate.FloatingPoint(-0.0, 5.0), -0.0, 5),
            () => AssertEveryReplayedVariantStaysInRange(Generate.FloatingPoint(1e300, double.PositiveInfinity), 1e300, double.PositiveInfinity),
            () => AssertEveryReplayedVariantStaysInRange(
                Generate.FloatingPoint(double.Epsilon, double.MaxValue), double.Epsilon, double.MaxValue),
            () => AssertEveryReplayedVariantStaysInRange(Generate.FloatingPoint(double.MinValue, double.MaxValue), double.MinValue, double.MaxValue));
    }

    [Fact]
    public void Decimal_OnAnyReplayedVariant_ShouldStayInRange()
    {
        // Act & Assert
        Assert.Multiple(
            () => AssertEveryReplayedVariantStaysInRange(Generate.Decimal(), decimal.MinValue, decimal.MaxValue),
            () => AssertEveryReplayedVariantStaysInRange(Generate.Decimal(0.01m, 1000m), 0.01m, 1000m),
            () => AssertEveryReplayedVariantStaysInRange(
                Generate.Decimal(decimal.MaxValue - 1, decimal.MaxValue), decimal.MaxValue - 1, decimal.MaxValue));
    }

    /// <summary>
    /// Replays every one-choice mutation of a recorded draw, every truncation of it, and a
    /// wholesale reshuffle, so a choice the generator no longer interprets the way it recorded it
    /// shows up as an out-of-range value rather than as an exception deep inside shrinking.
    /// </summary>
    private static void AssertEveryReplayedVariantStaysInRange<T>(Generator<T> generator, T min, T max) where T : IFloatingPoint<T>
    {
        var range = $"[{ValueFormatter.Format(min)}, {ValueFormatter.Format(max)}]";
        var random = Xoshiro256StarStar.ForRun(seed: 16, run: 0);

        for (var run = 0; run < 200; run++)
        {
            var drawn = ChoiceSource.FromRandom(Xoshiro256StarStar.ForRun(seed: 17, run));
            generator.Generate(drawn);

            var variants = drawn.Recorded
                .Select((_, index) => (
                    Kind: $"mutated choice {index}",
                    Choices: drawn.Recorded.Select((c, i) => i == index ? c with { Value = random.NextUInt64Inclusive(c.Max) } : c).ToList()))
                .Concat(Enumerable.Range(0, drawn.Recorded.Count)
                    .Select(length => (Kind: $"truncated to {length}", Choices: drawn.Recorded.Take(length).ToList())))
                .Append((Kind: "reshuffled", Choices: drawn.Recorded.Select(c => c with { Value = random.NextUInt64Inclusive(c.Max) }).ToList()));

            foreach (var (kind, choices) in variants)
            {
                var replayed = generator.Generate(ChoiceSource.FromPrefix(choices));
                var where = $"{range} run {run} {kind}: {ValueFormatter.Format(replayed)}";

                Assert.True(!T.IsNaN(replayed), $"NaN from {where}");
                Assert.True(min <= replayed && replayed <= max, $"out of range from {where}");
                Assert.True(!T.IsNegative(replayed) || !T.IsZero(replayed) || T.IsNegative(min), $"-0 from a non-negative range, {where}");
            }
        }
    }

    [Fact]
    public void Decimal_WithFullRange_ShouldReachExtremesAndEveryScale()
    {
        // Arrange
        var quantum = new decimal(lo: 1, mid: 0, hi: 0, isNegative: false, scale: 28);

        // Act
        var samples = Generate.Decimal().Sample(count: 2000, seed: 18);

        // Assert
        Assert.Contains(decimal.MinValue, samples);
        Assert.Contains(decimal.MaxValue, samples);
        Assert.Contains(quantum, samples);
        Assert.Contains(samples, static x => x != 0 && decimal.IsInteger(x) && Scale(x) == 0);
        Assert.Equal(Enumerable.Range(0, 29), samples.Select(Scale).Distinct().Order());
        Assert.Contains(samples, static x => Math.Abs(x) > ulong.MaxValue && Math.Abs(x) < decimal.MaxValue);
        Assert.Contains(samples, static x => Scale(x) > 0 && x == Math.Round(x, Scale(x) - 1));
        Assert.InRange(samples.Count(static x => Scale(x) == 0), 400, 1000);
    }

    /// <summary>
    /// Each range with the least number of distinct values it must produce, which is what stops a
    /// generator that emits nothing but the two bounds from satisfying every other assertion. The
    /// top two bounds are one quantum apart, so between them there is nothing but the bounds.
    /// </summary>
    public static TheoryData<decimal, decimal, int> DecimalRanges =>
        new() { { 0, 1000, 500 }, { 0.3m, 0.9m, 500 }, { decimal.MaxValue - 1, decimal.MaxValue, 2 }, { -5, -5, 1 } };

    [Theory]
    [MemberData(nameof(DecimalRanges))]
    public void Decimal_WithBounds_ShouldSpreadAcrossTheRangeAndReachBothBounds(decimal min, decimal max, int leastDistinct)
    {
        // Act
        var samples = Generate.Decimal(min, max).Sample(count: 2000, seed: 19);

        // Assert
        Assert.All(samples, x => Assert.InRange(x, min, max));
        Assert.InRange(samples.Count(x => x == min), 15, 2000);
        Assert.InRange(samples.Count(x => x == max), 15, 2000);
        Assert.InRange(samples.Distinct().Count(), leastDistinct, 2000);
    }

    [Fact]
    public void Decimal_WithFalsifiedProperty_ShouldShrinkTowardsTheSimplestValue()
    {
        // Act
        var full = Minimal(Generate.Decimal());
        var fraction = Minimal(Generate.Decimal(0.3m, 0.9m));
        var zeroToFraction = Minimal(Generate.Decimal(0m, 0.5m));
        var zeroToQuantum = Minimal(Generate.Decimal(0m, 0.0000000000000000000000000001m));
        var positive = Minimal(Generate.Decimal(3, 9));
        var top = Minimal(Generate.Decimal(decimal.MaxValue - 1, decimal.MaxValue));
        var negative = Minimal(Generate.Decimal(), static x => x >= 0);
        var pastThreshold = Minimal(Generate.Decimal(), static x => x <= 100);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("0", ValueFormatter.Format(full)),
            () => Assert.Equal("0.3", ValueFormatter.Format(fraction)),
            // A range of nothing but fractions still reports its zero at scale 0, not as 0.0.
            () => Assert.Equal("0", ValueFormatter.Format(zeroToFraction)),
            () => Assert.Equal("0", ValueFormatter.Format(zeroToQuantum)),
            () => Assert.Equal("3", ValueFormatter.Format(positive)),
            () => Assert.Equal(decimal.MaxValue - 1, top),
            () => Assert.Equal("-1", ValueFormatter.Format(negative)),
            () => Assert.InRange(pastThreshold, 101, 128));
    }

    [Fact]
    public void Decimal_WithASignBitOnAZeroBound_ShouldTreatItAsZero()
    {
        // Arrange, decimal's negative-zero representation is one nothing distinguishes from 0m
        // and that ordinary arithmetic produces.
        var negated = decimal.Negate(0m);
        var rounded = Math.Round(-0.4m);

        // Act
        var fromZero = Generate.Decimal(0m, 5m).Sample(count: 2000, seed: 20);
        var fromNegated = Generate.Decimal(negated, 5m).Sample(count: 2000, seed: 20);
        var fromRounded = Generate.Decimal(rounded, 5m).Sample(count: 2000, seed: 20);
        var degenerate = Generate.Decimal(0m, negated).Sample(count: 20, seed: 21);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(fromZero, fromNegated),
            () => Assert.Equal(fromZero, fromRounded),
            () => Assert.All(degenerate, static x => Assert.Equal(0m, x)));
    }

    [Fact]
    public void Decimal_WithInvalidBounds_ShouldThrowArgumentOutOfRangeException()
    {
        // Act
        var inverted = Assert.Throws<ArgumentOutOfRangeException>(() => Generate.Decimal(1, 0));

        // Assert
        Assert.Equal("min", inverted.ParamName);
    }

    private static int Scale(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0xFF;
}
