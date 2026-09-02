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

    public sealed class Store
    {
        private readonly Dictionary<int, int> _entries = [];

        public void Put(int key, int value) => _entries[key] = value;

        public int Get(int key) => _entries.GetValueOrDefault(key);
    }

    public sealed record Put(int Key, int Value) : ICommand<Dictionary<int, int>, Store>
    {
        public Dictionary<int, int> Update(Dictionary<int, int> model)
        {
            model[Key] = Value;
            return model;
        }

        public void Run(Dictionary<int, int> model, Store store) => store.Put(Key, Value);
    }

    public sealed record Get(int Key) : ICommand<Dictionary<int, int>, Store>
    {
        public Dictionary<int, int> Update(Dictionary<int, int> model) => model;

        public void Run(Dictionary<int, int> model, Store store) => Assert.Equal(model.GetValueOrDefault(Key), store.Get(Key));
    }

    private static Generator<CommandSequence<Dictionary<int, int>, Store>> StoreSequences { get; } =
        Generate.CommandSequence(() => new Dictionary<int, int>(), static _ => Generate.Frequency(
            (2, Generate.Build(Generate.Between(0, 3), Generate.Between(0, 100), ICommand<Dictionary<int, int>, Store> (key, value) => new Put(key, value))),
            (1, Generate.Between(0, 3).Select(ICommand<Dictionary<int, int>, Store> (key) => new Get(key)))));

    [Property]
    public void Property_WithIntStringAndListParameters_ShouldGenerateThemByDefault(int x, string s, List<byte> bytes)
    {
        _ = x;
        Assert.NotNull(s);
        Assert.NotNull(bytes);
    }

    [Property]
    public void Property_WithEverySupportedParameterShape_ShouldGenerateIt(
        bool flag, char c, long l, ushort us, Colour colour, int? maybe, string? maybeText,
        int[] array, IReadOnlyList<string> texts, (int, string) tuple, Person person, Money money,
        KeyValuePair<string, int> pair)
    {
        _ = (flag, c, l, us, colour, maybe, maybeText, array, texts, tuple, person, money, pair);
        Assert.NotNull(person.Name);
        Assert.InRange(money.Amount, 0, 1000);
    }

    [Property]
    public void Property_WithDateTimeAndGuidParameters_ShouldGenerateThemByDefault(
        DateTime moment, DateTimeOffset instant, DateOnly date, TimeOnly time, TimeSpan span, Guid? id)
    {
        _ = (instant, date, time, span, id);
        Assert.Equal(DateTimeKind.Unspecified, moment.Kind);
    }

    [Property]
    public void Property_WithRecursiveRecord_ShouldTerminate(Node node)
    {
        // Arrange
        var depth = 0;

        // Act
        for (var current = node; current is not null; current = current.Next)
        {
            depth++;
        }

        // Assert
        Assert.InRange(depth, 1, GeneratorResolver.MaxRecursionDepth);
    }

    [Property]
    public bool Property_WithBoolReturn_ShouldUseItAsTheVerdict(int x) => x + 0 == x;

    [Property]
    public async Task Property_WithAsyncBody_ShouldAwaitIt(int x)
    {
        await Task.Yield();
        Assert.Equal(x, x);
    }

    [Property]
    public async ValueTask<bool> Property_WithValueTaskOfBoolBody_ShouldUseItAsTheVerdict(string s)
    {
        await Task.Yield();
        return s.Length >= 0;
    }

    [Property]
    public void Property_WithNamedGeneratorOnTheTestClass_ShouldUseIt([Generator(nameof(Even))] int x)
    {
        Assert.Equal(0, x % 2);
    }

    [Property(Seed = 42)]
    public void Property_WithCommandSequenceFromNamedGenerator_ShouldRunItAgainstTheSystem(
        [Generator(nameof(StoreSequences))] CommandSequence<Dictionary<int, int>, Store> sequence)
    {
        Assert.NotEmpty(sequence.Commands);
        sequence.Run(new Store());
    }

    [Property(Generators = typeof(Generators))]
    public void Property_WithGeneratorsType_ShouldSupplyGeneratorsByTypeAndByName(
        int small, [Generator(nameof(Generators.Word))] string word, List<int> smalls)
    {
        Assert.InRange(small, -10, 10);
        Assert.Matches("^[a-z]{1,5}$", word);
        Assert.All(smalls, static x => Assert.InRange(x, -10, 10));
    }

    [Property]
    public void Property_WithExplicitGeneratorSource_ShouldAcceptAnyType([Generator(typeof(Generators), nameof(Generators.Small))] int x)
    {
        Assert.InRange(x, -10, 10);
    }

    [Property(RunCount = 20, Seed = 42)]
    public void Property_WithAssume_ShouldDiscardExamples(int x)
    {
        Property.Assume(x % 2 == 0);
        Assert.Equal(0, x % 2);
    }

    [Property(RunCount = 5)]
    public void Property_WithTestOutputAndFixtures_ShouldHaveThemAvailable(int x)
    {
        output.WriteLine($"example {x}");
    }

    [Property]
    public static void Property_WithStaticMethod_ShouldRunIt(int x) => Assert.Equal(x, x);

    [Property(Replay = "1:0", Seed = 1)]
    public void Property_WithReplay_ShouldRunTheNamedExample(int x) => _ = x;

    [Property]
    [Example(3)]
    [Example(-3)]
    public void Property_WithExplicitExamples_ShouldCheckThemAsWellAsGeneratedOnes(int x) => Assert.Equal(x, x);

    [Property]
    public void Property_WithNoParameters_ShouldStillRun() => Assert.True(true);
}
