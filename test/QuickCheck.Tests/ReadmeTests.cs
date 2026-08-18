namespace QuickCheck.Tests;

/// <summary>
/// The samples in readme.md, kept compiling and passing.
/// </summary>
public sealed class ReadmeTests
{
    private readonly record struct Money(long Amount, string Currency);

    [Fact]
    public void Reversing_twice_is_the_identity()
    {
        Property
            .ForAll(Generate.Integer<int>().List(), list =>
                list.AsEnumerable().Reverse().Reverse().SequenceEqual(list))
            .Assert();
    }

    [Fact]
    public void Dependent_generation_with_query_syntax()
    {
        var slices =
            from array in Generate.Integer<int>().Array(minLength: 1)
            from start in Generate.Between(0, array.Length - 1)
            from length in Generate.Between(0, array.Length - start)
            select (array, start, length);

        Property.ForAll(slices, slice =>
        {
            var (array, start, length) = slice;
            Assert.Equal(length, array.AsSpan(start, length).Length);
        }).Assert();
    }

    [Fact]
    public void Custom_generators_and_multi_argument_properties()
    {
        Generator<Money> money = Generate.From(source =>
            new Money(source.Draw(Generate.Between(0L, 1_000_000L)), source.Draw(Generate.Elements("GBP", "USD"))));

        Property.ForAll(money, money, (a, b) =>
        {
            Property.Assume(a.Currency == b.Currency);
            return new Money(a.Amount + b.Amount, a.Currency) == new Money(b.Amount + a.Amount, b.Currency);
        }).Assert();

        var evens = Generate.Integer<int>().Select(x => x * 2);
        var maybe = Generate.String().OrNull();
        var maybeInt = Generate.Integer<int>().Nullable();
        var either = Generate.OneOf(Generate.Constant(1), Generate.Constant(2));

        Property.ForAll(evens, maybe, maybeInt, (e, s, i) => e % 2 == 0).Assert();
        Assert.All(either.Sample(20), x => Assert.InRange(x, 1, 2));
    }

    [Fact]
    public async Task Asynchronous_bodies()
    {
        await Property.ForAll(Generate.String(), async s => Assert.Equal(s, await RoundTripAsync(s))).AssertAsync();
    }

    private static async Task<string> RoundTripAsync(string value)
    {
        await Task.Yield();
        return value;
    }
}
