using System.Diagnostics;

namespace QuickCheck.Tests;

/// <summary>
/// The samples in src/QuickCheck/readme.md
/// </summary>
public sealed class ReadmeTests
{
    private readonly record struct Money(long Amount, string Currency);

    private readonly record struct Price(decimal Amount) : IArbitrary<Price>
    {
        public static Generator<Price> Arbitrary { get; } =
            Generate.Between(0, 1_000_000).Select(cents => new Price(cents / 100m));
    }

    private abstract record Expression;

    private sealed record Literal(int Value) : Expression;

    private sealed record Add(Expression Left, Expression Right) : Expression;

    private static Generator<Expression> Expressions() => Generate.Frequency(
        (3, Generate.Integer<int>().Select(Expression (value) => new Literal(value))),
        (1, Generate.Deferred(() => Generate.Tuple(Expressions(), Expressions()))
            .Select(Expression (pair) => new Add(pair.Item1, pair.Item2))));

    [Fact]
    public void ReadmeSample_WithReverseTwice_ShouldBeTheIdentity()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>().List(), list =>
            list.AsEnumerable().Reverse().Reverse().SequenceEqual(list));

        // Act & Assert
        property.Assert();
    }

    [Fact]
    public void ReadmeSample_WithDependentGenerationInQuerySyntax_ShouldPass()
    {
        // Arrange
        var slices =
            from array in Generate.Integer<int>().Array(minLength: 1)
            from start in Generate.Between(0, array.Length - 1)
            from length in Generate.Between(0, array.Length - start)
            select (array, start, length);

        var property = Property.ForAll(slices, slice =>
        {
            var (array, start, length) = slice;
            Assert.Equal(length, array.AsSpan(start, length).Length);
        });

        // Act & Assert
        property.Assert();
    }

    [Fact]
    public void ReadmeSample_WithCustomGeneratorsAndMultiArgumentProperties_ShouldPass()
    {
        // Arrange
        Generator<Money> money = Generate.From(source =>
            new Money(source.Draw(Generate.Between(0L, 1_000_000L)), source.Draw(Generate.Elements("GBP", "USD"))));

        var evens = Generate.Integer<int>().Select(x => x * 2);
        var maybe = Generate.String().OrNull();
        var maybeInt = Generate.Integer<int>().Nullable();
        var either = Generate.OneOf(Generate.Constant(1), Generate.Constant(2));

        // Act & Assert
        Property.ForAll(money, money, (a, b) =>
        {
            Property.Assume(a.Currency == b.Currency);
            return new Money(a.Amount + b.Amount, a.Currency) == new Money(b.Amount + a.Amount, b.Currency);
        }).Assert();

        Property.ForAll(evens, maybe, maybeInt, (e, s, i) => e % 2 == 0).Assert();
        Assert.All(either.Sample(20), x => Assert.InRange(x, 1, 2));
    }

    [Fact]
    public void ReadmeSample_WithDateAndTimeRecipes_ShouldMixKindsAndFixTheOffset()
    {
        // Arrange
        var anyKind = Generate.Enum<DateTimeKind>().SelectMany(kind => Generate.DateTime(kind));

        var inIndia = Generate.DateTimeOffset(TimeSpan.FromHours(5.5));

        // Act
        var kinds = anyKind.Sample(count: 100, seed: 1).Select(static d => d.Kind).Distinct().ToList();
        var offsets = inIndia.Sample(count: 100, seed: 2);
        var minimal = Property.ForAll(Generate.DateTime(), static _ => false).Check(new CheckOptions { Seed = 3 }).Minimal!.Value;

        // Assert
        Assert.Equal(3, kinds.Count);
        Assert.All(offsets, static d => Assert.Equal(TimeSpan.FromHours(5.5), d.Offset));
        Assert.Equal("2000-01-01T00:00:00", ValueFormatter.Format(minimal));
    }

    [Fact]
    public void ReadmeSample_WithArbitraryOnTheType_ShouldGenerateFromIt()
    {
        // Arrange
        var property = Property.ForAll(Price.Arbitrary, price => price.Amount is >= 0 and <= 10_000);

        // Act & Assert
        property.Assert();
    }

    [Fact]
    public void ReadmeSample_WithDeferredRecursiveGenerator_ShouldTerminate()
    {
        // Arrange
        static int Evaluate(Expression expression) => expression switch
        {
            Literal literal => literal.Value,
            Add add => unchecked(Evaluate(add.Left) + Evaluate(add.Right)),
            _ => throw new UnreachableException(),
        };

        var property = Property.ForAll(Expressions(), expression =>
        {
            Property.Classify(expression is Add, "add");
            return Evaluate(expression) == Evaluate(expression);
        });

        // Act
        var result = property.Check(new CheckOptions { Seed = 1 });

        // Assert
        result.ThrowIfFailed();
        Assert.True(result.Statistics.Labels["add"] > 0);
    }

    [Fact]
    public void ReadmeSample_WithSample_ShouldFormatEachExample()
    {
        // Arrange
        var slices =
            from array in Generate.Integer<int>().Array(minLength: 1)
            from start in Generate.Between(0, array.Length - 1)
            select (array, start);

        // Act
        var formatted = slices.Sample(count: 5, seed: 1).Select(example => ValueFormatter.Format(example)).ToList();

        // Assert
        Assert.Equal(5, formatted.Count);
        Assert.All(formatted, text => Assert.StartsWith("([", text));
    }

    [Fact]
    public void ReadmeSample_WithReplayToken_ShouldRerunTheCounterexample()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>(), x => x >= 0);
        var failed = property.Check(new CheckOptions { Seed = 1 });
        Assert.Equal(PropertyOutcome.Falsified, failed.Outcome);

        // Act
        var replayed = property.Check(new CheckOptions { Replay = Replay.Parse(failed.Replay!.Value.ToString()) });

        // Assert
        Assert.Equal(PropertyOutcome.Falsified, replayed.Outcome);
        Assert.Equal(-1, replayed.Minimal!.Value);
        Assert.Equal(failed.Minimal!.Value, replayed.Minimal.Value);
    }

    [Fact]
    public void ReadmeSample_WithStatistics_ShouldPassAndPrintTheDistribution()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>().List(), list =>
        {
            Property.Classify(list.Count == 0, "empty");
            Property.Cover(list.Count >= 5, 20, "five or more");
            Property.Collect("sign of first", list.Count == 0 ? "none" : Math.Sign(list[0]).ToString());
            Assert.Equal(list, list.AsEnumerable().Reverse().Reverse());
        });

        // Act
        var result = property.Check(new CheckOptions { Seed = 1 });

        // Assert
        result.ThrowIfFailed();
        Assert.Contains("five or more (required 20%)", result.ToString());
        Assert.Contains("  sign of first:", result.ToString());
    }

    [Fact]
    public async Task ReadmeSample_WithAsynchronousBody_ShouldPass()
    {
        // Arrange
        var property = Property.ForAll(Generate.String(), async s => Assert.Equal(s, await RoundTripAsync(s)));

        // Act & Assert
        await property.AssertAsync();
    }

    private static async Task<string> RoundTripAsync(string value)
    {
        await Task.Yield();
        return value;
    }
}
