using System.Numerics;

namespace QuickCheck.Tests;

public sealed class GenerateTests
{
    private enum Colour { Red, Green, Blue }

    [Fact]
    public void Between_WithManySamples_ShouldStayInRangeAndReachBothBounds()
    {
        // Arrange
        var generator = Generate.Between(-5, 7);

        // Act
        var samples = generator.Sample(count: 2000, seed: 1);

        // Assert
        Assert.All(samples, static x => Assert.InRange(x, -5, 7));
        Assert.Contains(-5, samples);
        Assert.Contains(7, samples);
        Assert.Contains(0, samples);
    }

    [Fact]
    public void Between_WithInvertedOrOversizedRange_ShouldThrowArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => Generate.Between(10, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Generate.Between(Int128.MinValue, Int128.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => Generate.Between(BigInteger.Zero, BigInteger.One << 64));
    }

    [Fact]
    public void Integer_WithTypeWiderThan64Bits_ShouldThrowNotSupportedException()
    {
        // Act & Assert
        Assert.Throws<NotSupportedException>(() => Generate.Integer<UInt128>());
    }

    [Fact]
    public void Between_WithNarrowRangeOfTypeWiderThan64Bits_ShouldStayInRangeAndReachBothBounds()
    {
        // Arrange
        var far = BigInteger.One << 200;

        // Act
        var huge = Generate.Between(UInt128.MaxValue - 5, UInt128.MaxValue).Sample(count: 500, seed: 17);
        var big = Generate.Between(-far, -far + 5).Sample(count: 500, seed: 18);

        // Assert
        Assert.All(huge, static x => Assert.InRange(x, UInt128.MaxValue - 5, UInt128.MaxValue));
        Assert.Contains(UInt128.MaxValue, huge);
        Assert.Contains(UInt128.MaxValue - 5, huge);
        Assert.All(big, x => Assert.InRange(x, -far, -far + 5));
        Assert.Contains(-far, big);
        Assert.Contains(-far + 5, big);
    }

    [Fact]
    public void Between_WithFalsifiedProperty_ShouldShrinkTowardsTheBoundNearestZero()
    {
        // Arrange
        static T Minimal<T>(Generator<T> generator) =>
            Property.ForAll(generator, static _ => false).Check(new CheckOptions { Seed = 19 }).Minimal!.Value;

        // Act & Assert
        Assert.Equal(0, Minimal(Generate.Integer<int>()));
        Assert.Equal(0UL, Minimal(Generate.Integer<ulong>()));
        Assert.Equal(3, Minimal(Generate.Between(3, 9)));
        Assert.Equal(-3, Minimal(Generate.Between(-9, -3)));
        Assert.Equal(long.MaxValue - 5, Minimal(Generate.Between(long.MaxValue - 5, long.MaxValue)));
        Assert.Equal(UInt128.MaxValue - 5, Minimal(Generate.Between(UInt128.MaxValue - 5, UInt128.MaxValue)));
    }

    [Fact]
    public void Integer_WithManySamples_ShouldCoverTheFullRangeIncludingExtremes()
    {
        // Act
        var bytes = Generate.Integer<sbyte>().Sample(count: 3000, seed: 2);
        var ulongs = Generate.Integer<ulong>().Sample(count: 3000, seed: 3);
        var longs = Generate.Integer<long>().Sample(count: 3000, seed: 4);

        // Assert
        Assert.Contains(sbyte.MinValue, bytes);
        Assert.Contains(sbyte.MaxValue, bytes);
        Assert.Contains(ulong.MaxValue, ulongs);
        Assert.Contains(0UL, ulongs);
        Assert.Contains(long.MinValue, longs);
        Assert.Contains(long.MaxValue, longs);
        Assert.Contains(longs, static x => x is > 0 and < 100);
        Assert.Contains(longs, static x => x is < 0 and > -100);
    }

    [Fact]
    public void Collections_WithLengthBounds_ShouldRespectThemAndProduceVariety()
    {
        // Act
        var lists = Generate.Boolean().List(minLength: 2, maxLength: 5).Sample(count: 500, seed: 5);
        var strings = Generate.String(maxLength: 8).Sample(count: 500, seed: 6);
        var arrays = Generate.Integer<int>().Array(maxLength: 3).Sample(count: 200, seed: 7);

        // Assert
        Assert.All(lists, static l => Assert.InRange(l.Count, 2, 5));
        Assert.Contains(lists, static l => l.Count == 2);
        Assert.Contains(lists, static l => l.Count == 5);
        Assert.All(strings, static s => Assert.InRange(s.Length, 0, 8));
        Assert.Contains(strings, static s => s.Length == 0);
        Assert.Contains(strings, static s => s.Length == 8);
        Assert.Contains(arrays, static a => a.Length == 3);
    }

    [Fact]
    public void HashSet_WithLengthBounds_ShouldRespectThemAndReachBothBounds()
    {
        // Act
        var bounded = Generate.Between(0, 20).HashSet(minLength: 2, maxLength: 5).Sample(count: 500, seed: 33);
        var unbounded = Generate.Between(0, 20).HashSet().Sample(count: 500, seed: 34);

        // Assert
        Assert.All(bounded, static s => Assert.InRange(s.Count, 2, 5));
        Assert.Contains(bounded, static s => s.Count == 2);
        Assert.Contains(bounded, static s => s.Count == 5);
        Assert.Contains(unbounded, static s => s.Count == 0);
    }

    [Fact]
    public void HashSet_WithASmallDomain_ShouldStopAtTheDomainSize()
    {
        // Arrange
        var generator = Generate.Boolean().HashSet();

        // Act
        var sets = generator.Sample(count: 200, seed: 35);
        var result = Property.ForAll(generator, static _ => true).Check(new CheckOptions { Seed = 35 });

        // Assert
        Assert.All(sets, static s => Assert.InRange(s.Count, 0, 2));
        Assert.Contains(sets, static s => s.Count == 2);

        // Sample retries a discarded example a hundred times over, so only a check shows that
        // running out of distinct elements ends the set rather than discarding the example.
        Assert.Equal(0, result.Discards);
    }

    [Fact]
    public void HashSet_WithMinLengthAboveTheDomainSize_ShouldDiscardEveryExample()
    {
        // Arrange
        var generator = Generate.Boolean().HashSet(minLength: 3);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => generator.Sample(count: 10, seed: 36));
    }

    [Fact]
    public void HashSet_WithInvalidBounds_ShouldThrowArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => Generate.Boolean().HashSet(minLength: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Generate.Boolean().HashSet(minLength: 3, maxLength: 2));
    }

    [Fact]
    public void ChoiceGenerators_WithManySamples_ShouldCoverEveryAlternativeAndRespectWeights()
    {
        // Act
        var elements = Generate.Elements("a", "b", "c").Sample(count: 300, seed: 8);
        var enums = Generate.Enum<Colour>().Sample(count: 300, seed: 9);
        var oneOf = Generate.OneOf(Generate.Constant(1), Generate.Constant(2), Generate.Constant(3)).Sample(count: 300, seed: 10);
        var weighted = Generate.Frequency((9, Generate.Constant("common")), (1, Generate.Constant("rare"))).Sample(count: 1000, seed: 11);

        // Assert
        Assert.Equal(["a", "b", "c"], elements.Distinct().Order());
        Assert.Equal([Colour.Red, Colour.Green, Colour.Blue], enums.Distinct().Order());
        Assert.Equal([1, 2, 3], oneOf.Distinct().Order());
        Assert.InRange(weighted.Count(static s => s == "rare"), 40, 200);
    }

    [Fact]
    public void ChoiceGenerators_WithCollectionArguments_ShouldPickFromTheirItems()
    {
        // Arrange
        var items = new List<string> { "a", "b" };
        var generators = new List<Generator<int>> { Generate.Constant(1), Generate.Constant(2) };
        var weighted = new List<(int Weight, Generator<int> Generator)> { (1, Generate.Constant(3)), (1, Generate.Constant(4)) };

        // Act
        var elements = Generate.Elements(items).Sample(count: 100, seed: 30);
        var oneOf = Generate.OneOf(generators).Sample(count: 100, seed: 31);
        var frequency = Generate.Frequency(weighted).Sample(count: 100, seed: 32);

        // Assert
        Assert.Equal(["a", "b"], elements.Distinct().Order());
        Assert.Equal([1, 2], oneOf.Distinct().Order());
        Assert.Equal([3, 4], frequency.Distinct().Order());
    }

    [Fact]
    public void OneOf_WithManySamples_ShouldDrawUniformlyAcrossItsAlternatives()
    {
        // Arrange
        var generator = Generate.OneOf(Generate.Constant(1), Generate.Constant(2), Generate.Constant(3));

        // Act
        var uniform = generator.Sample(count: 3000, seed: 20);

        // Assert
        Assert.All<int>([1, 2, 3], value => Assert.InRange(uniform.Count(x => x == value), 800, 1200));
    }

    [Fact]
    public void NullableCombinators_WithManySamples_ShouldProduceBothNullAndValues()
    {
        // Act
        var strings = Generate.String().OrNull(nullProbability: 0.3).Sample(count: 200, seed: 12);
        var ints = Generate.Integer<int>().Nullable(nullProbability: 0.3).Sample(count: 200, seed: 13);

        // Assert
        Assert.Contains(strings, static s => s is null);
        Assert.Contains(strings, static s => s is not null);
        Assert.Contains(ints, static i => i is null);
        Assert.Contains(ints, static i => i is not null);
    }

    [Fact]
    public void Deferred_WithRecursiveGenerator_ShouldTerminateAndRecurse()
    {
        // Arrange
        Generator<int>? depthGen = null;
        depthGen = Generate.Frequency(
            (3, Generate.Constant(0)),
            (1, Generate.Deferred(() => depthGen!).Select(static d => d + 1)));

        // Act
        var depths = depthGen.Sample(count: 500, seed: 14);

        // Assert
        Assert.Contains(0, depths);
        Assert.Contains(depths, static d => d >= 2);
    }

    [Fact]
    public void Sample_WithTheSameSeed_ShouldBeDeterministic()
    {
        // Arrange
        var generator = Generate.Tuple(Generate.Integer<int>(), Generate.String(), Generate.Boolean());

        // Act
        var first = generator.Sample(count: 50, seed: 15);
        var second = generator.Sample(count: 50, seed: 15);
        var other = generator.Sample(count: 50, seed: 16);

        // Assert
        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }
}
