using System.Reflection;

// The notnull constraint is a warning, not an error, and the dictionary generator redraws null
// keys, so a Dictionary<int?, string> parameter is legal.
#pragma warning disable CS8714

namespace QuickCheck.Xunit.Tests;

public sealed class GeneratorResolverTests
{
    public sealed record Address(string Street, string? Flat);

    public sealed record Customer(string Name, Address Home, Address? Work, IReadOnlyList<Address> Previous);

    public sealed record Tree(List<Tree> Children);

    public sealed record Graph(Dictionary<string, Graph> Nodes);

    public sealed record Cycle(Dictionary<Cycle, string> Edges);

    public sealed class Chain(Chain next)
    {
        public Chain Next { get; } = next;
    }

    public abstract class Shape;

    public sealed class TwoConstructors
    {
        public TwoConstructors() { }
        public TwoConstructors(int x) => _ = x;
    }

    public readonly record struct Cents(int Value) : IArbitrary<Cents>
    {
        public static Generator<Cents> Arbitrary { get; } = Generate.Between(0, 99).Select(static c => new Cents(c));
    }

    public sealed record Price(Cents Amount, string Currency);

    public interface IStock : IArbitrary<IStock>
    {
        int Count { get; }

        static Generator<IStock> IArbitrary<IStock>.Arbitrary { get; } =
            Generate.Between(1, 9).Select(static count => (IStock)new Shelf(count));
    }

    public sealed record Shelf(int Count) : IStock;

    public static class ThrowingRegistry
    {
        public static Generator<int> Small => throw new InvalidOperationException("no generators today");
    }

    public static class UninitializableRegistry
    {
        public static Generator<int> Small = Generate.Between(0, 5);

        static UninitializableRegistry() => throw new InvalidOperationException("the registry blew up");
    }

    public sealed record Label(string Value)
    {
        public string Value { get; } = string.IsNullOrEmpty(Value)
            ? throw new ArgumentException("The value cannot be an empty string.", nameof(Value))
            : Value;
    }

    private sealed record Wrapped([Generator(nameof(Samples.Big))] int Value, [Generator(nameof(NamedRegistry.Loud))] string Text);

    public static class AutoPropertyRegistry
    {
        public static Generator<int> Small { get; } = Generate.Between(0, 5);
    }

    public static class Registry
    {
        public static Generator<string> Currency => Generate.Elements("GBP", "EUR");
        public static Generator<int> Small = Generate.Between(0, 5);
    }

    public static class AmbiguousRegistry
    {
        public static Generator<int> Small = Generate.Between(0, 5);
        public static Generator<int> AlsoSmall() => Generate.Between(0, 5);
    }

    public static class PrivateHelperRegistry
    {
        public static Generator<int> Small => Generate.Between(0, 5);

        private static Generator<int> Helper => Generate.Between(90, 99);
    }

    public static class PrivateOnlyRegistry
    {
        public static Generator<bool> Flag => Hidden.Select(static x => x > 0);

        private static Generator<int> Hidden => Generate.Constant(42);
    }

    private sealed class Samples
    {
        public void Nullables(int? maybe, string? text, Customer customer) => _ = (maybe, text, customer);
        public void Recursive(Tree tree) => _ = tree;
        public void Networked(Graph graph) => _ = graph;
        public void Cyclic(Cycle cycle) => _ = cycle;
        public void Collected(
            HashSet<int> set,
            ISet<string> setInterface,
            IReadOnlySet<bool> readOnlySet,
            Dictionary<string, int> dictionary,
            IDictionary<int, string?> nullableValues,
            Dictionary<int?, string> nullableKeys) =>
            _ = (set, setInterface, readOnlySet, dictionary, nullableValues, nullableKeys);
        public void Nullable_keys(Dictionary<int?, string> keyed) => _ = keyed;
        public void Endless(Chain chain) => _ = chain;
        public void Abstract(Shape shape) => _ = shape;
        public void Ambiguous(TwoConstructors value) => _ = value;
        public void Nested_arbitrary(Price price) => _ = price;
        public void Registered(Price price, int x) => _ = (price, x);
        public void Tuples((int, string?) pair, KeyValuePair<string, int> entry, Tuple<int, bool> boxed) => _ = (pair, entry, boxed);
        public void Named_ambiguity([Generator(nameof(Text))] string text) => _ = text;
        public void Named_in_a_record(Wrapped wrapped) => _ = wrapped;
        public void Validated(Label label) => _ = label;
        public void Counted(int x) => _ = x;
        public void Named_private([Generator("Helper")] int x) => _ = x;
        public void Stocked(IStock stock) => _ = stock;
        public void Temporal(DateTime moment, DateTimeOffset instant, DateOnly date, TimeOnly time, TimeSpan span, Guid id, DateTime? maybe) =>
            _ = (moment, instant, date, time, span, id, maybe);
        public static Generator<string> Text => Generate.Constant("test class");
        public static Generator<int> Big => Generate.Between(1000, 1099);
    }

    public static class NullableKeyRegistry
    {
        public static Generator<int?> Keys =>
            Generate.Between(100, 109).Select(static x => x % 3 == 0 ? null : (int?)x);
    }

    public static class NamedRegistry
    {
        public static Generator<string> Text => Generate.Constant("registry");
        public static Generator<string> Loud => Generate.Constant("REGISTRY");
    }

    private static Generator<PropertyArguments> Arguments(string method, Type? generators = null) =>
        PropertyMethod.Create(typeof(Samples).GetMethod(method, BindingFlags.Public | BindingFlags.Instance)!, generators).Arguments;

    private static PropertyDefinitionException Error(string method, Type? generators = null) =>
        Assert.Throws<PropertyDefinitionException>(() => Arguments(method, generators));

    [Fact]
    public void GeneratorResolver_WithNullableAnnotations_ShouldAddNullsAtEveryLevel()
    {
        // Arrange
        var arguments = Arguments(nameof(Samples.Nullables));

        // Act
        var samples = arguments.Sample(count: 300, seed: 1);
        var customers = samples.Select(static a => (Customer)a.Values[2]!).ToList();

        // Assert
        Assert.Contains(samples, static a => a.Values[0] is null);
        Assert.Contains(samples, static a => a.Values[0] is int);
        Assert.Contains(samples, static a => a.Values[1] is null);
        Assert.Contains(samples, static a => a.Values[1] is string);
        Assert.All(customers, static c => Assert.NotNull(c.Name));
        Assert.All(customers, static c => Assert.NotNull(c.Home));
        Assert.Contains(customers, static c => c.Work is null);
        Assert.Contains(customers, static c => c.Work is not null);
        Assert.Contains(customers, static c => c.Home.Flat is null);
        Assert.Contains(customers, static c => c.Previous.Count > 1);
    }

    [Fact]
    public void GeneratorResolver_WithDateTimeAndGuidParameters_ShouldUseTheBuiltInGenerators()
    {
        // Arrange
        var arguments = Arguments(nameof(Samples.Temporal));

        // Act
        var samples = arguments.Sample(count: 100, seed: 5);

        // Assert
        Assert.All(samples, static a => Assert.IsType<DateTime>(a.Values[0]));
        Assert.All(samples, static a => Assert.IsType<DateTimeOffset>(a.Values[1]));
        Assert.All(samples, static a => Assert.IsType<DateOnly>(a.Values[2]));
        Assert.All(samples, static a => Assert.IsType<TimeOnly>(a.Values[3]));
        Assert.All(samples, static a => Assert.IsType<TimeSpan>(a.Values[4]));
        Assert.All(samples, static a => Assert.IsType<Guid>(a.Values[5]));
        Assert.Contains(samples, static a => a.Values[6] is null);
        Assert.Contains(samples, static a => a.Values[6] is DateTime);
        Assert.Equal(100, samples.Select(static a => (Guid)a.Values[5]!).Distinct().Count());
    }

    [Fact]
    public void GeneratorResolver_WithRecursiveType_ShouldUnrollToTheDepthLimitAndEndInEmptyCollections()
    {
        // Arrange
        var arguments = Arguments(nameof(Samples.Recursive));

        // Act
        var trees = arguments.Sample(count: 50, seed: 2).Select(static a => (Tree)a.Values[0]!);

        // Assert
        Assert.All(trees, static tree => Assert.InRange(Depth(tree), 1, GeneratorResolver.MaxRecursionDepth));
        Assert.Contains(trees, static tree => Depth(tree) > 1);

        static int Depth(Tree tree) => 1 + (tree.Children.Count == 0 ? 0 : tree.Children.Max(Depth));
    }

    [Fact]
    public void GeneratorResolver_WithSetAndDictionaryParameters_ShouldGenerateEveryDeclaredShape()
    {
        // Arrange
        var arguments = Arguments(nameof(Samples.Collected));

        // Act
        var samples = arguments.Sample(count: 100, seed: 14);
        var nullableValues = samples.Select(static a => (IDictionary<int, string?>)a.Values[4]!).ToList();
        var nullableKeys = samples.Select(static a => (Dictionary<int?, string>)a.Values[5]!).ToList();

        // Assert
        Assert.All(samples, static a =>
        {
            Assert.IsType<HashSet<int>>(a.Values[0]);
            Assert.IsType<HashSet<string>>(a.Values[1]);
            Assert.IsType<HashSet<bool>>(a.Values[2]);
            Assert.IsType<Dictionary<string, int>>(a.Values[3]);
            Assert.IsType<Dictionary<int, string?>>(a.Values[4]);
            Assert.IsType<Dictionary<int?, string>>(a.Values[5]);
        });
        Assert.Contains(samples, static a => ((HashSet<int>)a.Values[0]!).Count > 1);
        Assert.Contains(samples, static a => ((ISet<string>)a.Values[1]!).Count > 1);
        Assert.Contains(samples, static a => ((IReadOnlySet<bool>)a.Values[2]!).Count > 1);
        Assert.Contains(samples, static a => ((Dictionary<string, int>)a.Values[3]!).Count > 1);
        Assert.Contains(nullableValues, static d => d.Values.Any(static v => v is null));
        Assert.Contains(nullableValues, static d => d.Values.Any(static v => v is not null));
        Assert.Contains(nullableKeys, static d => d.Count > 1);
        Assert.All(nullableKeys, static d => Assert.All(d.Keys, static k => Assert.True(k.HasValue)));
    }

    [Fact]
    public void GeneratorResolver_WithARegisteredNullableKeyGenerator_ShouldUseItAndRedrawItsNulls()
    {
        // Arrange
        var arguments = Arguments(nameof(Samples.Nullable_keys), typeof(NullableKeyRegistry));

        // Act
        var dictionaries = arguments.Sample(count: 100, seed: 15).Select(static a => (Dictionary<int?, string>)a.Values[0]!).ToList();

        // Assert
        Assert.All(dictionaries, static d => Assert.All(d.Keys, static k => Assert.InRange(k!.Value, 100, 109)));
        Assert.Contains(dictionaries, static d => d.Count == 5);
    }

    [Fact]
    public void GeneratorResolver_WithRecursiveDictionary_ShouldUnrollToTheDepthLimitAndEndInEmptyDictionaries()
    {
        // Arrange
        var arguments = Arguments(nameof(Samples.Networked));

        // Act
        var graphs = arguments.Sample(count: 50, seed: 15).Select(static a => (Graph)a.Values[0]!);

        // Assert
        Assert.All(graphs, static graph => Assert.InRange(Depth(graph), 1, GeneratorResolver.MaxRecursionDepth));
        Assert.Contains(graphs, static graph => Depth(graph) > 1);

        static int Depth(Graph graph) => 1 + (graph.Nodes.Count == 0 ? 0 : graph.Nodes.Values.Max(Depth));
    }

    [Fact]
    public void GeneratorResolver_WithARecursiveDictionaryKey_ShouldUnrollToTheDepthLimit()
    {
        // Arrange
        var arguments = Arguments(nameof(Samples.Cyclic));

        // Act
        var cycles = arguments.Sample(count: 50, seed: 16).Select(static a => (Cycle)a.Values[0]!);

        // Assert
        Assert.All(cycles, static cycle => Assert.InRange(Depth(cycle), 1, GeneratorResolver.MaxRecursionDepth));
        Assert.Contains(cycles, static cycle => Depth(cycle) > 1);

        static int Depth(Cycle cycle) => 1 + (cycle.Edges.Count == 0 ? 0 : cycle.Edges.Keys.Max(Depth));
    }

    [Fact]
    public void GeneratorResolver_WithUnderivableTypes_ShouldExplainWhy()
    {
        // Act & Assert
        Assert.Contains("Chain is recursive without a nullable or collection member", Error(nameof(Samples.Endless)).Message);
        Assert.Contains("no generator can be derived for Shape", Error(nameof(Samples.Abstract)).Message);
        Assert.Contains("TwoConstructors has 2 public constructors", Error(nameof(Samples.Ambiguous)).Message);
    }

    [Fact]
    public void GeneratorResolver_WithNestedMembers_ShouldUseIArbitraryAndTheRegistry()
    {
        // Arrange
        var nested = Arguments(nameof(Samples.Nested_arbitrary));
        var withRegistry = Arguments(nameof(Samples.Registered), typeof(Registry));

        // Act
        var prices = nested.Sample(count: 50, seed: 3).Select(static a => (Price)a.Values[0]!).ToList();
        var registered = withRegistry.Sample(count: 50, seed: 3).ToList();

        // Assert
        Assert.All(prices, static p => Assert.InRange(p.Amount.Value, 0, 99));
        Assert.All(registered, static a => Assert.Contains(((Price)a.Values[0]!).Currency, new[] { "GBP", "EUR" }));
        Assert.All(registered, static a => Assert.InRange((int)a.Values[1]!, 0, 5));
    }

    [Fact]
    public void GeneratorResolver_WithTuplesAndPairs_ShouldBuildThemFromTheirConstructors()
    {
        // Arrange
        var arguments = Arguments(nameof(Samples.Tuples));

        // Act
        var samples = arguments.Sample(count: 20, seed: 4);

        // Assert
        Assert.All(samples, static a =>
        {
            Assert.IsType<(int, string?)>(a.Values[0]);
            Assert.IsType<KeyValuePair<string, int>>(a.Values[1]);
            Assert.IsType<Tuple<int, bool>>(a.Values[2]);
        });
    }

    [Fact]
    public void GeneratorResolver_WithAmbiguousRegistryMembers_ShouldReportAnError()
    {
        // Act
        var registryByType = Error(nameof(Samples.Registered), typeof(AmbiguousRegistry));

        // Assert
        Assert.Contains("AmbiguousRegistry has more than one Generator<Int32> member (", registryByType.Message);
        Assert.Contains("name one with [Generator]", registryByType.Message);
    }

    [Fact]
    public void GeneratorResolver_WithPrivateRegistryMembers_ShouldNotMatchThemByType()
    {
        // Arrange
        var arguments = Arguments(nameof(Samples.Counted), typeof(PrivateHelperRegistry));

        // Act
        var samples = arguments.Sample(count: 20, seed: 11);

        // Assert
        Assert.All(samples, static a => Assert.InRange((int)a.Values[0]!, 0, 5));
    }

    [Fact]
    public void GeneratorResolver_WithOnlyAPrivateRegistryGenerator_ShouldFallThroughToTheBuiltIn()
    {
        // Arrange
        var arguments = Arguments(nameof(Samples.Counted), typeof(PrivateOnlyRegistry));

        // Act
        var samples = arguments.Sample(count: 20, seed: 12);

        // Assert
        Assert.Contains(samples, static a => (int)a.Values[0]! != 42);
    }

    [Fact]
    public void GeneratorResolver_WithNamedPrivateMember_ShouldStillUseIt()
    {
        // Arrange
        var arguments = Arguments(nameof(Samples.Named_private), typeof(PrivateHelperRegistry));

        // Act
        var samples = arguments.Sample(count: 20, seed: 13);

        // Assert
        Assert.All(samples, static a => Assert.InRange((int)a.Values[0]!, 90, 99));
    }

    [Fact]
    public void GeneratorResolver_WithRegistryAutoProperty_ShouldTreatItAsOneMemberRatherThanAnAmbiguity()
    {
        // Arrange
        var arguments = Arguments(nameof(Samples.Counted), typeof(AutoPropertyRegistry));

        // Act
        var samples = arguments.Sample(count: 20, seed: 5);

        // Assert
        Assert.All(samples, static a => Assert.InRange((int)a.Values[0]!, 0, 5));
    }

    [Fact]
    public void GeneratorResolver_WithNamedGeneratorsOnRecordParameters_ShouldApplyThem()
    {
        // Arrange
        var arguments = Arguments(nameof(Samples.Named_in_a_record), typeof(NamedRegistry));

        // Act
        var samples = arguments.Sample(count: 20, seed: 6).Select(static a => (Wrapped)a.Values[0]!);

        // Assert
        Assert.All(samples, static wrapped => Assert.InRange(wrapped.Value, 1000, 1099));
        Assert.All(samples, static wrapped => Assert.Equal("REGISTRY", wrapped.Text));
    }

    [Fact]
    public void GeneratorResolver_WithConstructorThatRejectsGeneratedArguments_ShouldNameTheTypeAndTheArguments()
    {
        // Arrange
        var arguments = Arguments(nameof(Samples.Validated));

        // Act
        var exception = Assert.Throws<PropertyDefinitionException>(() => arguments.Sample(count: 200, seed: 7));

        // Assert
        Assert.Contains("Deriving a generator for Label", exception.Message);
        Assert.Contains("Value = \"\"", exception.Message);
        Assert.Contains("public static Generator<Label> member on the Generators type", exception.Message);
        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    public void GeneratorResolver_WithInterfaceDeclaringIArbitrary_ShouldGenerateItsImplementations()
    {
        // Arrange
        var arguments = Arguments(nameof(Samples.Stocked));

        // Act
        var samples = arguments.Sample(count: 20, seed: 8);

        // Assert
        Assert.All(samples, static a => Assert.InRange(((IStock)a.Values[0]!).Count, 1, 9));
    }

    [Fact]
    public void GeneratorResolver_WithThrowingGeneratorMember_ShouldReportItsNameAndTheCause()
    {
        // Act
        var error = Error(nameof(Samples.Counted), typeof(ThrowingRegistry));
        var uninitializable = Error(nameof(Samples.Counted), typeof(UninitializableRegistry));

        // Assert
        Assert.Contains("'ThrowingRegistry.Small' threw InvalidOperationException: no generators today", error.Message);
        Assert.IsType<InvalidOperationException>(error.GetBaseException());
        Assert.Contains("'UninitializableRegistry.Small' threw InvalidOperationException: the registry blew up", uninitializable.Message);
    }

    [Fact]
    public void GeneratorResolver_WithNamedGeneratorOnTestClassAndGeneratorsType_ShouldUseTheClassAloneOrReportAmbiguity()
    {
        // Act
        var fromClass = Arguments(nameof(Samples.Named_ambiguity)).Sample(count: 1)[0].Values[0];
        var ambiguous = Error(nameof(Samples.Named_ambiguity), typeof(NamedRegistry));

        // Assert
        Assert.Equal("test class", fromClass);
        Assert.Contains("the generator name 'Text' is ambiguous", ambiguous.Message);
    }
}
