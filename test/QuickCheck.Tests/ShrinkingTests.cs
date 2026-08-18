namespace QuickCheck.Tests;

public sealed class ShrinkingTests
{
    private static readonly CheckOptions Seeded = new() { Seed = 2024, RunCount = 200 };

    [Fact]
    public void Shrinks_through_Select_and_Where_to_the_smallest_failing_value()
    {
        // Even multiples of three, i.e. multiples of six.
        var generator = Generate.Between(0, 100_000)
            .Select(static x => x * 2)
            .Where(static x => x % 3 == 0);

        var result = Property.ForAll(generator, static x => x < 100).Check(Seeded);

        Assert.True(result.IsFalsified);
        Assert.Equal(102, result.Minimal.Value);
    }

    [Fact]
    public void Shrinks_through_SelectMany_query_syntax()
    {
        var generator =
            from length in Generate.Between(0, 20)
            from items in Generate.Between(0, 1000).List(length, length)
            select items;

        var result = Property.ForAll(generator, static items => items.All(static x => x < 50)).Check(Seeded);

        Assert.True(result.IsFalsified);
        Assert.Equal([50], result.Minimal.Value);
    }

    [Fact]
    public void Shrinks_a_list_by_deleting_elements_and_shrinking_the_rest()
    {
        var result = Property
            .ForAll(Generate.Integer<int>().List(), static items => items.Sum(static x => (long)x) < 100)
            .Check(Seeded);

        Assert.True(result.IsFalsified);
        Assert.Equal([100], result.Minimal.Value);
    }

    [Fact]
    public void Shrinks_a_duplicate_to_the_smallest_pair()
    {
        var result = Property
            .ForAll(Generate.Integer<int>().List(), static items => items.Distinct().Count() == items.Count)
            .Check(Seeded);

        Assert.True(result.IsFalsified);
        Assert.Equal([0, 0], result.Minimal.Value);
    }

    [Fact]
    public void Shrinks_negative_integers_towards_zero()
    {
        var result = Property.ForAll(Generate.Integer<int>(), static x => x > -10).Check(Seeded);

        Assert.True(result.IsFalsified);
        Assert.Equal(-10, result.Minimal.Value);
    }

    [Fact]
    public void Shrinks_a_string_to_the_shortest_failing_one()
    {
        var result = Property.ForAll(Generate.String(), static s => !s.Contains('z')).Check(Seeded);

        Assert.True(result.IsFalsified);
        Assert.Equal("z", result.Minimal.Value);
    }

    [Fact]
    public void Shrinks_a_pair_jointly()
    {
        var result = Property
            .ForAll(Generate.Between(0, 1000), Generate.Between(0, 1000), static (a, b) => a + b < 100)
            .Check(Seeded);

        Assert.True(result.IsFalsified);
        Assert.Equal(100, result.Minimal.Value.Item1 + result.Minimal.Value.Item2);
    }

    [Fact]
    public void Does_not_shrink_into_a_different_failure()
    {
        // Large values throw one exception, small ones another; the shrinker
        // must keep the failure it started with rather than sliding to the
        // "simpler" small-value bug.
        var result = Property.ForAll(Generate.Between(0, 1_000_000), static x =>
        {
            if (x >= 500_000)
            {
                throw new InvalidOperationException("large");
            }

            if (x is > 0 and < 10)
            {
                throw new ArgumentException("small");
            }
        }).Check(new CheckOptions { Seed = 7, RunCount = 500 });

        Assert.True(result.IsFalsified);
        Assert.Equal(result.Original.Exception?.GetType(), result.Minimal.Exception?.GetType());

        if (result.Minimal.Exception is InvalidOperationException)
        {
            Assert.Equal(500_000, result.Minimal.Value);
        }
        else
        {
            Assert.Equal(1, result.Minimal.Value);
        }
    }

    [Fact]
    public void Shrinking_can_be_disabled()
    {
        var result = Property
            .ForAll(Generate.Between(1000, 1_000_000), static x => x < 1000)
            .Check(Seeded with { MaxShrinkAttempts = 0 });

        Assert.True(result.IsFalsified);
        Assert.Equal(0, result.ShrinkAttempts);
        Assert.Equal(result.Original.Value, result.Minimal.Value);
    }
}
