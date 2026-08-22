namespace QuickCheck.Tests;

/// <summary>
/// The samples in readme.md, kept compiling and passing.
/// </summary>
public sealed class ReadmeTests
{
    private readonly record struct Money(long Amount, string Currency);

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
