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
    public void Dictionary_WithLengthBounds_ShouldRespectThemAndReachBothBounds()
    {
        // Act
        var dictionaries = Generate
            .Dictionary(Generate.Between(0, 100), Generate.Between(0, 100), minLength: 1, maxLength: 6)
            .Sample(count: 500, seed: 37);

        // Assert
        Assert.All(dictionaries, static d => Assert.InRange(d.Count, 1, 6));
        Assert.Contains(dictionaries, static d => d.Count == 1);
        Assert.Contains(dictionaries, static d => d.Count == 6);
    }

    [Fact]
    public void Dictionary_WithASmallKeyDomain_ShouldStopAtTheDomainSize()
    {
        // Arrange
        var generator = Generate.Dictionary(Generate.Boolean(), Generate.Between(0, 10));

        // Act
        var dictionaries = generator.Sample(count: 200, seed: 39);
        var result = Property.ForAll(generator, static _ => true).Check(new CheckOptions { Seed = 39 });

        // Assert
        Assert.All(dictionaries, static d => Assert.InRange(d.Count, 0, 2));
        Assert.Contains(dictionaries, static d => d.Count == 2);
        Assert.Equal(0, result.Discards);
    }

    [Fact]
    public void Dictionary_WithNullKeys_ShouldRejectThemLikeDuplicates()
    {
        // Arrange
        // The notnull constraint binds only at compile time, so a key generator can still hand the
        // dictionary a null; typed as Generator<string> it satisfies the constraint regardless.
        var keysWithNulls = Generate.From<string>(static source =>
            source.NextBoolean(0.7) ? source.Draw(Generate.String()) : null!);

        // Act
        var dictionaries = Generate
            .Dictionary(keysWithNulls, Generate.Between(0, 10))
            .Sample(count: 200, seed: 38);

        // Assert
        Assert.All(dictionaries, static d => Assert.DoesNotContain(null, d.Keys));

        // A null costs a redraw, not an entry, so the documented average of about minLength + 5
        // holds.
        Assert.True(dictionaries.Average(static d => d.Count) > 4);
    }

    [Fact]
    public void Dictionary_WithInvalidArguments_ShouldThrow()
    {
        // Arrange
        var keys = Generate.Between(0, 10);
        var values = Generate.Boolean();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => Generate.Dictionary<int, bool>(null!, values));
        Assert.Throws<ArgumentNullException>(() => Generate.Dictionary(keys, (Generator<bool>)null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => Generate.Dictionary(keys, values, minLength: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Generate.Dictionary(keys, values, minLength: 3, maxLength: 2));
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

    [Fact]
    public void Build_WithMemberGenerators_ShouldDrawEachInOrder()
    {
        // Arrange
        var drawn = 0;
        var counter = Generate.From(_ => ++drawn);
        var generator = Generate.Build(counter, counter, counter, static (a, b, c) => (a, b, c));

        // Act
        var value = generator.Sample(count: 1, seed: 20).Single();

        // Assert
        Assert.Equal((1, 2, 3), value);
    }

    [Fact]
    public void Build_WithTheSameSeed_ShouldMatchAHandWrittenFromGenerator()
    {
        // Arrange
        var amounts = Generate.Between(0L, 1_000_000L);
        var currencies = Generate.Elements("GBP", "USD");
        var built = Generate.Build(amounts, currencies, static (amount, currency) => (amount, currency));
        var handWritten = Generate.From(source => (source.Draw(amounts), source.Draw(currencies)));
        var options = new CheckOptions { Seed = 21 };

        // Act
        var builtSamples = built.Sample(count: 100, seed: 22);
        var handWrittenSamples = handWritten.Sample(count: 100, seed: 22);
        var builtResult = Property.ForAll(built, static money => money.amount < 1000).Check(options);
        var handWrittenResult = Property.ForAll(handWritten, static money => money.Item1 < 1000).Check(options);

        // Assert
        Assert.Equal(handWrittenSamples, builtSamples);
        Assert.Equal(handWrittenResult.Original!.Value, builtResult.Original!.Value);
        Assert.Equal(handWrittenResult.Minimal!.Value, builtResult.Minimal!.Value);
        Assert.Equal(handWrittenResult.ShrinkAttempts, builtResult.ShrinkAttempts);
    }

    [Fact]
    public void Build_WithAReplayToken_ShouldReproduceTheCounterexample()
    {
        // Arrange
        var people = Generate.Build(Generate.String(), Generate.Between(0, 150), static (name, age) => (name, age));
        var property = Property.ForAll(people, static person => person.age <= 100);
        var failed = property.Check(new CheckOptions { Seed = 28 });
        Assert.True(failed.IsFalsified);

        // Act
        var replayed = property.Check(new CheckOptions { Replay = Replay.Parse(failed.Replay!.Value.ToString()) });

        // Assert
        Assert.Equal(failed.Original!.Value, replayed.Original!.Value);
        Assert.Equal(failed.Minimal!.Value, replayed.Minimal!.Value);
        Assert.Equal(("", 101), replayed.Minimal.Value);
    }

    [Fact]
    public void Build_WithEightMembers_ShouldDrawEachInOrder()
    {
        // Arrange
        var generator = Generate.Build(
            Generate.Constant(1),
            Generate.Constant(2),
            Generate.Constant(3),
            Generate.Constant(4),
            Generate.Constant(5),
            Generate.Constant(6),
            Generate.Constant(7),
            Generate.Constant(8),
            static (a, b, c, d, e, f, g, h) => (a, b, c, d, e, f, g, h));

        // Act
        var built = generator.Sample(count: 1, seed: 23).Single();

        // Assert
        Assert.Equal((1, 2, 3, 4, 5, 6, 7, 8), built);
    }

    [Fact]
    public void Build_WithConstructThatAssumesARelation_ShouldDiscardTheExampleRatherThanFail()
    {
        // Arrange
        // The documented way to tie members together from construct: the drawn pair that breaks the
        // relation is discarded, not reported as a counterexample carrying a DiscardException.
        var intervals = Generate.Build(Generate.Between(0, 100), Generate.Between(0, 100), static (low, high) =>
        {
            Property.Assume(low <= high);
            return (low, high);
        });
        var property = Property.ForAll(intervals, static interval => interval.low <= interval.high);

        // Act
        var result = property.Check(new CheckOptions { Seed = 29, RunCount = 100 });

        // Assert
        Assert.Equal(PropertyOutcome.Passed, result.Outcome);
        Assert.Equal(100, result.TestsRun);
        Assert.True(result.Discards > 0);
    }

    [Fact]
    public void Build_WithANullArgument_ShouldThrowArgumentNullExceptionNamingIt()
    {
        // Arrange
        var integers = Generate.Integer<int>();

        // Act
        var nullSecond = Assert.Throws<ArgumentNullException>(() =>
            Generate.Build(integers, null!, static (int a, int b) => a + b));
        var nullConstruct = Assert.Throws<ArgumentNullException>(() =>
            Generate.Build(integers, integers, (Func<int, int, int>)null!));
        var nullEighth = Assert.Throws<ArgumentNullException>(() =>
            Generate.Build(
                integers, integers, integers, integers, integers, integers, integers, null!,
                static (int a, int b, int c, int d, int e, int f, int g, int h) => a));

        // Assert
        Assert.Equal("generator2", nullSecond.ParamName);
        Assert.Equal("construct", nullConstruct.ParamName);
        Assert.Equal("generator8", nullEighth.ParamName);
    }

    [Fact]
    public void Sequence_WithGenerators_ShouldDrawEachInOrderIntoAFreshArray()
    {
        // Arrange
        var drawn = 0;
        var counter = Generate.From(_ => ++drawn);
        var generator = Generate.Sequence(counter, counter, counter);

        // Act
        var samples = generator.Sample(count: 2, seed: 24);

        // Assert
        Assert.Equal([1, 2, 3], samples[0]);
        Assert.Equal([4, 5, 6], samples[1]);
        Assert.NotSame(samples[0], samples[1]);
    }

    [Fact]
    public void Sequence_WithNoGenerators_ShouldProduceEmptyArrays()
    {
        // Arrange
        var generator = Generate.Sequence<int>();

        // Act
        var samples = generator.Sample(count: 5, seed: 25);
        var result = Property.ForAll(generator, static _ => false).Check(new CheckOptions { Seed = 26 });

        // Assert
        Assert.All(samples, static items => Assert.Empty(items));
        Assert.True(result.IsFalsified);
        Assert.Empty(result.Minimal!.Value);
        Assert.Equal(0, result.ShrinkAttempts);
        Assert.Equal(ShrinkLimit.None, result.ShrinkLimit);
    }

    [Fact]
    public void Sequence_WithASequenceArgument_ShouldEnumerateItOnceWhenCalled()
    {
        // Arrange
        var enumerations = 0;

        IEnumerable<Generator<int>> Generators()
        {
            enumerations++;
            yield return Generate.Constant(1);
            yield return Generate.Constant(2);
        }

        // Act
        var generator = Generate.Sequence(Generators());
        var afterTheCall = enumerations;
        var samples = generator.Sample(count: 10, seed: 27);

        // Assert
        Assert.Equal(1, afterTheCall);
        Assert.Equal(1, enumerations);
        Assert.All(samples, static items => Assert.Equal([1, 2], items));
    }

    [Fact]
    public void Sequence_WithANullArgument_ShouldThrowArgumentNullExceptionNamingGenerators()
    {
        // Act
        var nullSequence = Assert.Throws<ArgumentNullException>(() => Generate.Sequence<int>(null!));
        var nullElement = Assert.Throws<ArgumentNullException>(() => Generate.Sequence(Generate.Constant(1), null!));

        // Assert
        Assert.Equal("generators", nullSequence.ParamName);
        Assert.Equal("generators", nullElement.ParamName);
    }
}
