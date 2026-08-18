using System.Reflection;

namespace QuickCheck.Xunit.Tests;

public sealed class GeneratorResolverTests
{
    public sealed record Address(string Street, string? Flat);

    public sealed record Customer(string Name, Address Home, Address? Work, IReadOnlyList<Address> Previous);

    public sealed record Tree(List<Tree> Children);

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
        public static Generator<string> Text => Generate.Constant("test class");
        public static Generator<int> Big => Generate.Between(1000, 1099);
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
    public void Nullable_annotations_add_nulls_at_every_level()
    {
        var samples = Arguments(nameof(Samples.Nullables)).Sample(count: 300, seed: 1);

        Assert.Contains(samples, static a => a.Values[0] is null);
        Assert.Contains(samples, static a => a.Values[0] is int);
        Assert.Contains(samples, static a => a.Values[1] is null);
        Assert.Contains(samples, static a => a.Values[1] is string);

        var customers = samples.Select(static a => (Customer)a.Values[2]!).ToList();
        Assert.All(customers, static c => Assert.NotNull(c.Name));
        Assert.All(customers, static c => Assert.NotNull(c.Home));
        Assert.Contains(customers, static c => c.Work is null);
        Assert.Contains(customers, static c => c.Work is not null);
        Assert.Contains(customers, static c => c.Home.Flat is null);
        Assert.Contains(customers, static c => c.Previous.Count > 1);
    }

    [Fact]
    public void Recursive_types_unroll_to_the_depth_limit_and_end_in_empty_collections()
    {
        var trees = Arguments(nameof(Samples.Recursive)).Sample(count: 50, seed: 2).Select(static a => (Tree)a.Values[0]!);

        Assert.All(trees, static tree => Assert.InRange(Depth(tree), 1, GeneratorResolver.MaxRecursionDepth));
        Assert.Contains(trees, static tree => Depth(tree) > 1);

        static int Depth(Tree tree) => 1 + (tree.Children.Count == 0 ? 0 : tree.Children.Max(Depth));
    }

    [Fact]
    public void Types_that_cannot_be_derived_are_explained()
    {
        Assert.Contains("Chain is recursive without a nullable or collection member", Error(nameof(Samples.Endless)).Message);
        Assert.Contains("no generator can be derived for Shape", Error(nameof(Samples.Abstract)).Message);
        Assert.Contains("TwoConstructors has 2 public constructors", Error(nameof(Samples.Ambiguous)).Message);
    }

    [Fact]
    public void Nested_members_use_IArbitrary_and_the_registry()
    {
        var prices = Arguments(nameof(Samples.Nested_arbitrary)).Sample(count: 50, seed: 3).Select(static a => (Price)a.Values[0]!).ToList();
        Assert.All(prices, static p => Assert.InRange(p.Amount.Value, 0, 99));

        var registered = Arguments(nameof(Samples.Registered), typeof(Registry)).Sample(count: 50, seed: 3).ToList();
        Assert.All(registered, static a => Assert.Contains(((Price)a.Values[0]!).Currency, new[] { "GBP", "EUR" }));
        Assert.All(registered, static a => Assert.InRange((int)a.Values[1]!, 0, 5));
    }

    [Fact]
    public void Tuples_and_pairs_are_built_from_their_constructors()
    {
        var samples = Arguments(nameof(Samples.Tuples)).Sample(count: 20, seed: 4);

        Assert.All(samples, static a =>
        {
            Assert.IsType<(int, string?)>(a.Values[0]);
            Assert.IsType<KeyValuePair<string, int>>(a.Values[1]);
            Assert.IsType<Tuple<int, bool>>(a.Values[2]);
        });
    }

    [Fact]
    public void Ambiguous_lookups_are_errors()
    {
        var registryByType = Error(nameof(Samples.Registered), typeof(AmbiguousRegistry));
        Assert.Contains("AmbiguousRegistry has more than one Generator<Int32> member (", registryByType.Message);
        Assert.Contains("name one with [Generator]", registryByType.Message);
    }

    [Fact]
    public void A_registrys_private_members_are_not_candidates_for_type_matching()
    {
        var samples = Arguments(nameof(Samples.Counted), typeof(PrivateHelperRegistry)).Sample(count: 20, seed: 11);

        Assert.All(samples, static a => Assert.InRange((int)a.Values[0]!, 0, 5));
    }

    [Fact]
    public void A_registry_with_only_a_private_generator_falls_through_to_the_built_in()
    {
        var samples = Arguments(nameof(Samples.Counted), typeof(PrivateOnlyRegistry)).Sample(count: 20, seed: 12);

        Assert.Contains(samples, static a => (int)a.Values[0]! != 42);
    }

    [Fact]
    public void A_private_member_can_still_be_named()
    {
        var samples = Arguments(nameof(Samples.Named_private), typeof(PrivateHelperRegistry)).Sample(count: 20, seed: 13);

        Assert.All(samples, static a => Assert.InRange((int)a.Values[0]!, 90, 99));
    }

    [Fact]
    public void A_registrys_auto_property_is_one_member_rather_than_an_ambiguity()
    {
        var samples = Arguments(nameof(Samples.Counted), typeof(AutoPropertyRegistry)).Sample(count: 20, seed: 5);

        Assert.All(samples, static a => Assert.InRange((int)a.Values[0]!, 0, 5));
    }

    [Fact]
    public void Named_generators_apply_to_a_records_positional_parameters()
    {
        var samples = Arguments(nameof(Samples.Named_in_a_record), typeof(NamedRegistry))
            .Sample(count: 20, seed: 6)
            .Select(static a => (Wrapped)a.Values[0]!);

        Assert.All(samples, static wrapped => Assert.InRange(wrapped.Value, 1000, 1099));
        Assert.All(samples, static wrapped => Assert.Equal("REGISTRY", wrapped.Text));
    }

    [Fact]
    public void A_constructor_that_rejects_generated_arguments_names_the_type_and_the_arguments()
    {
        var arguments = Arguments(nameof(Samples.Validated));

        var exception = Assert.Throws<PropertyDefinitionException>(() => arguments.Sample(count: 200, seed: 7));

        Assert.Contains("Deriving a generator for Label", exception.Message);
        Assert.Contains("Value = \"\"", exception.Message);
        Assert.Contains("public static Generator<Label> member on the Generators type", exception.Message);
        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    public void An_interface_declaring_IArbitrary_generates_its_own_implementations()
    {
        var samples = Arguments(nameof(Samples.Stocked)).Sample(count: 20, seed: 8);

        Assert.All(samples, static a => Assert.InRange(((IStock)a.Values[0]!).Count, 1, 9));
    }

    [Fact]
    public void A_generator_member_that_throws_is_reported_with_its_name_and_the_cause()
    {
        var error = Error(nameof(Samples.Counted), typeof(ThrowingRegistry));

        Assert.Contains("'ThrowingRegistry.Small' threw InvalidOperationException: no generators today", error.Message);
        Assert.IsType<InvalidOperationException>(error.GetBaseException());

        var uninitializable = Error(nameof(Samples.Counted), typeof(UninitializableRegistry));

        Assert.Contains("'UninitializableRegistry.Small' threw InvalidOperationException: the registry blew up", uninitializable.Message);
    }

    [Fact]
    public void Named_generators_are_found_on_the_test_class_and_the_generators_type_but_not_both()
    {
        var fromClass = Arguments(nameof(Samples.Named_ambiguity)).Sample(count: 1)[0].Values[0];
        Assert.Equal("test class", fromClass);

        var ambiguous = Error(nameof(Samples.Named_ambiguity), typeof(NamedRegistry));
        Assert.Contains("the generator name 'Text' is ambiguous", ambiguous.Message);
    }
}
