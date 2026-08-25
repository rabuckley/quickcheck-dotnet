using System.Numerics;
using QuickCheck.Choices;
using QuickCheck.Generators;

namespace QuickCheck.Tests.Generators;

public sealed class FloatingPointGeneratorTests
{
    private static Labelled<T> Bounded<T>(T min, T max) where T : IFloatingPointIeee754<T>, IMinMaxValue<T> =>
        Label(new FloatingPointGenerator<T>(Ieee754Format<T>.Instance, min, max), min, max);

    private static Labelled<T> Unbounded<T>() where T : IFloatingPointIeee754<T>, IMinMaxValue<T> =>
        Label(
            FloatingPointGenerator<T>.Unbounded(Ieee754Format<T>.Instance, T.NaN),
            T.NegativeInfinity,
            T.PositiveInfinity);

    /// <summary>Names a generator by its type and bounds, so a failure says which one failed.</summary>
    private static Labelled<T> Label<T>(FloatingPointGenerator<T> generator, T min, T max) where T : IFloatingPoint<T> =>
        new($"{typeof(T).Name} [{ValueFormatter.Format(min)}, {ValueFormatter.Format(max)}]", generator);

    private sealed record Labelled<T>(string Name, FloatingPointGenerator<T> Generator) where T : IFloatingPoint<T>;

    [Fact]
    public void Draw_WithEveryEdgeForced_ShouldEmitItExactlyThroughTheOrdinaryChoices()
    {
        // Act & Assert
        Assert.Multiple(
            () => AssertEveryEdgeRoundTrips(
                Ieee754Format<double>.Instance,
                Unbounded<double>(),
                Bounded(-0.0, 5.0),
                Bounded(0.3, 0.9),
                Bounded(1e300, double.PositiveInfinity)),
            () => AssertEveryEdgeRoundTrips(Ieee754Format<float>.Instance, Unbounded<float>(), Bounded(float.MinValue, -1f)),
            () => AssertEveryEdgeRoundTrips(Ieee754Format<Half>.Instance, Unbounded<Half>(), Bounded((Half)(-5), (Half)5)));
    }

    /// <summary>
    /// Forces each generator's edges and checks the value comes back exactly, over the same number
    /// of choices an unforced draw takes and the same number on every range, so a recorded
    /// sequence replays whichever range and whichever kind of draw recorded it.
    /// </summary>
    private static void AssertEveryEdgeRoundTrips<T>(IFloatingPointFormat<T> format, params Labelled<T>[] generators)
        where T : IFloatingPoint<T>
    {
        var layouts = new List<(string Name, int Length)>();

        foreach (var (name, generator) in generators)
        {
            var drawn = ChoiceSource.FromRandom(Xoshiro256StarStar.ForRun(seed: 1, run: 0));
            generator.Draw(drawn, forced: null);
            layouts.Add((name, drawn.Recorded.Count));

            foreach (var edge in generator.Edges)
            {
                var expected = format.Compose(edge);
                var source = ChoiceSource.FromRandom(Xoshiro256StarStar.ForRun(seed: 1, run: 0));

                var forced = generator.Draw(source, edge);

                // Compared as printed and by sign, which separates -0 from 0 and keeps the
                // sign that printing loses on NaN.
                Assert.True(
                    ValueFormatter.Format(expected) == ValueFormatter.Format(forced) && T.IsNegative(expected) == T.IsNegative(forced),
                    $"{name}: {ValueFormatter.Format(expected)} was forced as {ValueFormatter.Format(forced)}");
                Assert.True(
                    source.Recorded.Count == drawn.Recorded.Count,
                    $"{name}: forcing {ValueFormatter.Format(expected)} took {source.Recorded.Count} choices, "
                    + $"an unforced draw takes {drawn.Recorded.Count}");
            }
        }

        Assert.All(layouts, layout => Assert.True(
            layout.Length == layouts[0].Length,
            $"{layout.Name} draws {layout.Length} choices, {layouts[0].Name} draws {layouts[0].Length}"));
    }

    [Fact]
    public void Edges_OfTheFullDoubleRange_ShouldBeTheEightSpecialValues()
    {
        // Arrange
        var format = Ieee754Format<double>.Instance;
        double[] expected =
            [double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue, double.MinValue, double.Epsilon, -double.Epsilon, -0.0];

        // Act
        var edges = Unbounded<double>().Generator.Edges.ToArray()
            .Select(edge => format.Compose(edge));

        // Assert
        // Compared as printed and by sign, which separates -0 from 0 and makes NaN equal to itself.
        Assert.Equal(expected.Select(Key).Order(), edges.Select(Key).Order());

        static (string Printed, bool Negative) Key(double value) => (ValueFormatter.Format(value), double.IsNegative(value));
    }

    [Fact]
    public void Edges_OfDoubleBoundsWithTrailingZeros_ShouldTakeTheSimplestExponent()
    {
        // Arrange
        var generator = Bounded(-1.5, 4.0).Generator;

        // Act
        var edges = generator.Edges.ToArray();

        // Assert
        Assert.Multiple(
            () => Assert.Contains(new FloatingPointParts(true, 3, -1), edges),
            () => Assert.Contains(new FloatingPointParts(false, UInt128.One, 2), edges));
    }

    [Fact]
    public void Decompose_OfAnyGeneratedValue_ShouldComposeBackToItAndCanonicaliseToOneTriple()
    {
        // Act & Assert
        Assert.Multiple(
            () => AssertDecomposeRoundTrips(Ieee754Format<double>.Instance, Generate.FloatingPoint<double>(), radix: 2, maxExponent: 971),
            () => AssertDecomposeRoundTrips(Ieee754Format<float>.Instance, Generate.FloatingPoint<float>(), radix: 2, maxExponent: 104),
            () => AssertDecomposeRoundTrips(Ieee754Format<Half>.Instance, Generate.FloatingPoint<Half>(), radix: 2, maxExponent: 5));
    }

    /// <summary>
    /// The decomposition contract over whatever the generator produces: the parts compose back to
    /// the value, the canonical parts still compose back, and the canonical significand no longer
    /// divides by <paramref name="radix"/> below <paramref name="maxExponent"/>, the format's
    /// exponent ceiling, which pins a value to one triple.
    /// </summary>
    private static void AssertDecomposeRoundTrips<T>(IFloatingPointFormat<T> format, Generator<T> generator, int radix, int maxExponent)
        where T : IFloatingPoint<T> =>
        Property.ForAll(generator, value =>
        {
            var parts = format.Decompose(value);
            var canonical = format.Canonical(value);

            AssertComposesBackTo(format, value, parts);
            AssertComposesBackTo(format, value, canonical);
            Assert.True(
                canonical.Significand == UInt128.Zero
                    || canonical.Exponent >= maxExponent
                    || canonical.Significand % (UInt128)radix != UInt128.Zero,
                $"{ValueFormatter.Format(value)} canonicalised to {canonical}, a significand still divisible "
                + $"by {radix} below the maximum exponent {maxExponent}");
        }).Assert();

    private static void AssertComposesBackTo<T>(IFloatingPointFormat<T> format, T value, FloatingPointParts parts)
        where T : IFloatingPoint<T>
    {
        var composed = format.Compose(parts);
        var round = $"{ValueFormatter.Format(value)} gave parts {parts} that composed back to {ValueFormatter.Format(composed)}";

        Assert.True(T.IsNaN(value) ? T.IsNaN(composed) : composed == value, round);
        Assert.True(T.IsNegative(composed) == T.IsNegative(value), $"{round}, losing the sign");
    }

    [Fact]
    public void CanonicalParts_OfRepresentativeValues_ShouldTakeTheLargestExponentAtWhichTheValueIsAMultiple()
    {
        // Arrange
        var format = Ieee754Format<double>.Instance;

        // Act & Assert
        Assert.Multiple(
            () => AssertEachCanonicalForm(
                format,
                (Math.Pow(2, 1023), new FloatingPointParts(false, UInt128.One << 52, 971)),
                (double.MaxValue, new FloatingPointParts(false, (UInt128.One << 53) - UInt128.One, 971)),
                (double.Epsilon, new FloatingPointParts(false, UInt128.One, -1074)),
                (-1.5, new FloatingPointParts(true, 3, -1)),
                (3 * double.Epsilon, new FloatingPointParts(false, 3, -1074)),
                (-0.0, new FloatingPointParts(true, UInt128.Zero, 0)),
                (double.NaN, new FloatingPointParts(false, (UInt128.One << 53) - UInt128.One, 972)),
                (double.NegativeInfinity, new FloatingPointParts(true, (UInt128.One << 53) - 2, 972))),
            () => AssertEachCanonicalForm(
                Ieee754Format<Half>.Instance,
                (Half.Epsilon, new FloatingPointParts(false, UInt128.One, -24)),
                (Half.ScaleB((Half)3, -23), new FloatingPointParts(false, 3, -23))));
    }

    /// <summary>
    /// Checks the parts each value takes as a generator edge, and names the value, so one wrong
    /// decomposition neither hides the others nor leaves the reader matching parts back to an
    /// input.
    /// </summary>
    private static void AssertEachCanonicalForm<T>(IFloatingPointFormat<T> format, params (T Value, FloatingPointParts Expected)[] cases)
        where T : IFloatingPoint<T> =>
        Assert.Multiple(cases.Select(expectation => (Action)(() =>
        {
            var actual = format.Canonical(expectation.Value);

            Assert.True(
                actual == expectation.Expected,
                $"{ValueFormatter.Format(expectation.Value)} decomposed to {actual}, expected {expectation.Expected}");
        })).ToArray());

    [Fact]
    public void Ieee754Format_ForEveryBuiltInType_ShouldDeriveTheIeeeConstants()
    {
        // Act
        var doubles = Ieee754Format<double>.Instance;
        var singles = Ieee754Format<float>.Instance;
        var halves = Ieee754Format<Half>.Instance;

        // Assert
        Assert.Multiple(
            () => Assert.Equal((53, -1074, 971), (doubles.Precision, doubles.MinExponent, doubles.MaxExponent)),
            () => Assert.Equal((24, -149, 104), (singles.Precision, singles.MinExponent, singles.MaxExponent)),
            () => Assert.Equal((11, -24, 5), (halves.Precision, halves.MinExponent, halves.MaxExponent)),
            () => Assert.Equal((UInt128.One << 11) - UInt128.One, halves.MaxSignificand));
    }

    [Fact]
    public void SignificandBounds_OfDoubles_ShouldAdmitTheInfinitiesAtTheirExponentAndNothingPastTheCap()
    {
        // Arrange
        var format = Ieee754Format<double>.Instance;
        var maxInfinity = (UInt128.One << 53) - 2;
        var justPastTheCap = Math.ScaleB(1.0, 53) + 2;

        // Act & Assert
        Assert.Multiple(
            () => Assert.Equal(((UInt128)2, (UInt128)3), format.SignificandBounds(0.3, 0.9, -2)),
            () => Assert.Equal((UInt128.Zero, maxInfinity), format.SignificandBounds(1e300, double.PositiveInfinity, 972)),
            () => AssertNoSignificands(format.SignificandBounds(1e300, 1e301, 972)),
            () => AssertNoSignificands(format.SignificandBounds(0.3, 0.9, -1075)),
            () => AssertNoSignificands(format.SignificandBounds(justPastTheCap, justPastTheCap, 0)));
    }

    private static void AssertNoSignificands((UInt128 Low, UInt128 High) bounds) =>
        Assert.True(bounds.Low > bounds.High, $"expected no significands, got [{bounds.Low}, {bounds.High}]");
}
