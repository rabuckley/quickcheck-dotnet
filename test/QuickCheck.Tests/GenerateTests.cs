using System.Numerics;

namespace QuickCheck.Tests;

public sealed class GenerateTests
{
    private enum Colour { Red, Green, Blue }

    [Fact]
    public void Between_stays_in_range_and_reaches_both_bounds()
    {
        var samples = Generate.Between(-5, 7).Sample(count: 2000, seed: 1);

        Assert.All(samples, static x => Assert.InRange(x, -5, 7));
        Assert.Contains(-5, samples);
        Assert.Contains(7, samples);
        Assert.Contains(0, samples);
    }

    [Fact]
    public void Between_rejects_inverted_or_oversized_ranges()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Generate.Between(10, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Generate.Between(Int128.MinValue, Int128.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => Generate.Integer<UInt128>());
        Assert.Throws<ArgumentOutOfRangeException>(() => Generate.Between(BigInteger.Zero, BigInteger.One << 64));
    }

    [Fact]
    public void Between_supports_narrow_ranges_of_types_wider_than_64_bits()
    {
        var far = BigInteger.One << 200;
        var huge = Generate.Between(UInt128.MaxValue - 5, UInt128.MaxValue).Sample(count: 500, seed: 17);
        var big = Generate.Between(-far, -far + 5).Sample(count: 500, seed: 18);

        Assert.All(huge, static x => Assert.InRange(x, UInt128.MaxValue - 5, UInt128.MaxValue));
        Assert.Contains(UInt128.MaxValue, huge);
        Assert.Contains(UInt128.MaxValue - 5, huge);
        Assert.All(big, x => Assert.InRange(x, -far, -far + 5));
        Assert.Contains(-far, big);
        Assert.Contains(-far + 5, big);
    }

    [Fact]
    public void Between_shrinks_towards_the_bound_nearest_zero()
    {
        static T Minimal<T>(Generator<T> generator) =>
            Property.ForAll(generator, static _ => false).Check(new CheckOptions { Seed = 19 }).Minimal!.Value;

        Assert.Equal(0, Minimal(Generate.Integer<int>()));
        Assert.Equal(0UL, Minimal(Generate.Integer<ulong>()));
        Assert.Equal(3, Minimal(Generate.Between(3, 9)));
        Assert.Equal(-3, Minimal(Generate.Between(-9, -3)));
        Assert.Equal(long.MaxValue - 5, Minimal(Generate.Between(long.MaxValue - 5, long.MaxValue)));
        Assert.Equal(UInt128.MaxValue - 5, Minimal(Generate.Between(UInt128.MaxValue - 5, UInt128.MaxValue)));
    }

    [Fact]
    public void Integer_covers_the_full_range_including_extremes()
    {
        var bytes = Generate.Integer<sbyte>().Sample(count: 3000, seed: 2);
        var ulongs = Generate.Integer<ulong>().Sample(count: 3000, seed: 3);
        var longs = Generate.Integer<long>().Sample(count: 3000, seed: 4);

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
    public void Collections_respect_length_bounds_and_produce_variety()
    {
        var lists = Generate.Boolean().List(minLength: 2, maxLength: 5).Sample(count: 500, seed: 5);
        var strings = Generate.String(maxLength: 8).Sample(count: 500, seed: 6);
        var arrays = Generate.Integer<int>().Array(maxLength: 3).Sample(count: 200, seed: 7);

        Assert.All(lists, static l => Assert.InRange(l.Count, 2, 5));
        Assert.Contains(lists, static l => l.Count == 2);
        Assert.Contains(lists, static l => l.Count == 5);
        Assert.All(strings, static s => Assert.InRange(s.Length, 0, 8));
        Assert.Contains(strings, static s => s.Length == 0);
        Assert.Contains(strings, static s => s.Length == 8);
        Assert.Contains(arrays, static a => a.Length == 3);
    }

    [Fact]
    public void Choice_generators_cover_every_alternative_and_respect_weights()
    {
        var elements = Generate.Elements("a", "b", "c").Sample(count: 300, seed: 8);
        var enums = Generate.Enum<Colour>().Sample(count: 300, seed: 9);
        var oneOf = Generate.OneOf(Generate.Constant(1), Generate.Constant(2), Generate.Constant(3)).Sample(count: 300, seed: 10);
        var weighted = Generate.Frequency((9, Generate.Constant("common")), (1, Generate.Constant("rare"))).Sample(count: 1000, seed: 11);

        Assert.Equal(["a", "b", "c"], elements.Distinct().Order());
        Assert.Equal([Colour.Red, Colour.Green, Colour.Blue], enums.Distinct().Order());
        Assert.Equal([1, 2, 3], oneOf.Distinct().Order());
        Assert.InRange(weighted.Count(static s => s == "rare"), 40, 200);
    }

    [Fact]
    public void OneOf_draws_uniformly_across_its_alternatives()
    {
        var uniform = Generate.OneOf(Generate.Constant(1), Generate.Constant(2), Generate.Constant(3)).Sample(count: 3000, seed: 20);

        Assert.All<int>([1, 2, 3], value => Assert.InRange(uniform.Count(x => x == value), 800, 1200));
    }

    [Fact]
    public void Nullable_combinators_produce_both_null_and_values()
    {
        var strings = Generate.String().OrNull(nullProbability: 0.3).Sample(count: 200, seed: 12);
        var ints = Generate.Integer<int>().Nullable(nullProbability: 0.3).Sample(count: 200, seed: 13);

        Assert.Contains(strings, static s => s is null);
        Assert.Contains(strings, static s => s is not null);
        Assert.Contains(ints, static i => i is null);
        Assert.Contains(ints, static i => i is not null);
    }

    [Fact]
    public void Defer_allows_recursive_generators()
    {
        Generator<int>? depthGen = null;
        depthGen = Generate.Frequency(
            (3, Generate.Constant(0)),
            (1, Generate.Deferred(() => depthGen!).Select(static d => d + 1)));

        var depths = depthGen.Sample(count: 500, seed: 14);

        Assert.Contains(0, depths);
        Assert.Contains(depths, static d => d >= 2);
    }

    [Fact]
    public void Sample_is_deterministic_per_seed()
    {
        var generator = Generate.Tuple(Generate.Integer<int>(), Generate.String(), Generate.Boolean());

        Assert.Equal(generator.Sample(count: 50, seed: 15), generator.Sample(count: 50, seed: 15));
        Assert.NotEqual(generator.Sample(count: 50, seed: 15), generator.Sample(count: 50, seed: 16));
    }
}
