using Xunit;

namespace QuickCheck.Xunit.Tests;

/// <summary>
/// Properties that run through the real xUnit pipeline. Every method here is
/// a passing property; the failure paths are exercised in-process by
/// <see cref="PropertyTestCaseTests"/>.
/// </summary>
public sealed class PropertyAttributeTests(ITestOutputHelper output)
{
    public enum Colour { Red, Green, Blue }

    public sealed record Person(string Name, int Age, Colour Favourite);

    public sealed record Node(int Value, Node? Next, List<Node> Children);

    public readonly record struct Money(decimal Amount) : IArbitrary<Money>
    {
        public static Generator<Money> Arbitrary { get; } = Generate.Between(0, 1_000_00).Select(static cents => new Money(cents / 100m));
    }

    public static class Generators
    {
        public static Generator<int> Small => Generate.Between(-10, 10);
        public static Generator<string> Word() => Generate.String(Generate.Between('a', 'z'), minLength: 1, maxLength: 5);
    }

    private static Generator<int> Even { get; } = Generate.Integer<int>().Select(static x => x * 2);

    [Property]
    public void Integers_strings_and_lists_are_generated_by_default(int x, string s, List<byte> bytes)
    {
        _ = x;
        Assert.NotNull(s);
        Assert.NotNull(bytes);
    }

    [Property]
    public void Every_supported_shape_is_generated(
        bool flag, char c, long l, ushort us, Colour colour, int? maybe, string? maybeText,
        int[] array, IReadOnlyList<string> texts, (int, string) tuple, Person person, Money money,
        KeyValuePair<string, int> pair)
    {
        _ = (flag, c, l, us, colour, maybe, maybeText, array, texts, tuple, person, money, pair);
        Assert.NotNull(person.Name);
        Assert.InRange(money.Amount, 0, 1000);
    }

    [Property]
    public void Recursive_records_terminate(Node node)
    {
        var depth = 0;

        for (var current = node; current is not null; current = current.Next)
        {
            depth++;
        }

        Assert.InRange(depth, 1, GeneratorResolver.MaxRecursionDepth);
    }

    [Property]
    public bool Bool_returns_are_the_property(int x) => x + 0 == x;

    [Property]
    public async Task Async_bodies_are_awaited(int x)
    {
        await Task.Yield();
        Assert.Equal(x, x);
    }

    [Property]
    public async ValueTask<bool> ValueTask_bool_bodies_are_the_property(string s)
    {
        await Task.Yield();
        return s.Length >= 0;
    }

    [Property]
    public void Named_generators_come_from_the_test_class([Generator(nameof(Even))] int x)
    {
        Assert.Equal(0, x % 2);
    }

    [Property(Generators = typeof(Generators))]
    public void The_generators_type_supplies_generators_by_type_and_by_name(
        int small, [Generator(nameof(Generators.Word))] string word, List<int> smalls)
    {
        Assert.InRange(small, -10, 10);
        Assert.Matches("^[a-z]{1,5}$", word);
        Assert.All(smalls, static x => Assert.InRange(x, -10, 10));
    }

    [Property]
    public void Explicit_sources_can_name_any_type([Generator(typeof(Generators), nameof(Generators.Small))] int x)
    {
        Assert.InRange(x, -10, 10);
    }

    [Property(RunCount = 20, Seed = 42)]
    public void Assume_discards_examples(int x)
    {
        Property.Assume(x % 2 == 0);
        Assert.Equal(0, x % 2);
    }

    [Property(RunCount = 5)]
    public void Test_output_and_fixtures_are_available(int x)
    {
        output.WriteLine($"example {x}");
    }

    [Property]
    public static void Static_methods_are_supported(int x) => Assert.Equal(x, x);

    [Property(Replay = "1:0", Seed = 1)]
    public void Replay_runs_the_named_example(int x) => _ = x;

    [Property]
    public void Properties_without_parameters_still_run() => Assert.True(true);
}
