using System.Diagnostics;
using Command = QuickCheck.ICommand<System.Collections.Generic.Dictionary<int, int>, QuickCheck.Tests.ReadmeTests.Store>;

namespace QuickCheck.Tests;

/// <summary>
/// The samples in src/QuickCheck/readme.md
/// </summary>
public sealed class ReadmeTests
{
    private readonly record struct Money(long Amount, string Currency);

    private readonly record struct Interval(int Low, int High);

    private readonly record struct Price(decimal Amount) : IArbitrary<Price>
    {
        public static Generator<Price> Arbitrary { get; } =
            Generate.Between(0, 1_000_000).Select(cents => new Price(cents / 100m));
    }

    private abstract record Expression;

    private sealed record Literal(int Value) : Expression;

    private sealed record Add(Expression Left, Expression Right) : Expression;

    public sealed class Store(bool ignoresOverwrites = false)
    {
        private readonly Dictionary<int, int> _entries = [];

        public int Count => _entries.Count;

        public void Put(int key, int value)
        {
            if (ignoresOverwrites && _entries.ContainsKey(key))
            {
                return;
            }

            _entries[key] = value;
        }

        public int Get(int key) => _entries.GetValueOrDefault(key);

        public void Delete(int key) => _entries.Remove(key);
    }

    private sealed record Put(int Key, int Value) : Command
    {
        public Dictionary<int, int> Update(Dictionary<int, int> model) { model[Key] = Value; return model; }
        public void Run(Dictionary<int, int> model, Store store) => store.Put(Key, Value);
    }

    private sealed record Get(int Key) : Command
    {
        public Dictionary<int, int> Update(Dictionary<int, int> model) => model;
        public void Run(Dictionary<int, int> model, Store store) =>
            Assert.Equal(model.GetValueOrDefault(Key), store.Get(Key));
    }

    private sealed record Delete(int Key) : Command
    {
        public bool Precondition(Dictionary<int, int> model) => model.ContainsKey(Key);
        public Dictionary<int, int> Update(Dictionary<int, int> model) { model.Remove(Key); return model; }
        public void Run(Dictionary<int, int> model, Store store) => store.Delete(Key);
    }

    private static Generator<Command> Next(Dictionary<int, int> model) => Generate.Frequency(
        (3, Generate.Build(Generate.Between(0, 3), Generate.Between(0, 100), Command (key, value) => new Put(key, value))),
        (2, Generate.Between(0, 3).Select(Command (key) => new Get(key))),
        (1, Generate.Between(0, 3).Select(Command (key) => new Delete(key))));

    /// <summary>The system of the readme's handle sample: real handles are indices into a list.</summary>
    public sealed class Files
    {
        private readonly List<bool> _open = [];

        public Dictionary<int, int> ByModelId { get; } = [];

        public int Open()
        {
            _open.Add(true);
            return _open.Count - 1;
        }

        public void Write(int handle) => Assert.True(_open[handle]);
    }

    private sealed class Handles
    {
        public int Next { get; set; }
        public List<int> Open { get; } = [];
    }

    private sealed record Open : ICommand<Handles, Files>
    {
        public Handles Update(Handles model) { model.Open.Add(model.Next); model.Next++; return model; }
        public void Run(Handles model, Files files) => files.ByModelId[model.Next] = files.Open();
    }

    private sealed record Write(int Id) : ICommand<Handles, Files>
    {
        public bool Precondition(Handles model) => model.Open.Contains(Id);
        public Handles Update(Handles model) => model;
        public void Run(Handles model, Files files) => files.Write(files.ByModelId[Id]);
    }

    private static Generator<ICommand<Handles, Files>> NextHandleCommand(Handles model) => Generate.Frequency(
        (2, Generate.Constant<ICommand<Handles, Files>>(new Open())),
        (1, Generate.Elements(model.Open.AsEnumerable().Reverse().DefaultIfEmpty(-1))
            .Select(ICommand<Handles, Files> (id) => new Write(id))));

    private static Generator<Expression> Expressions() => Generate.Frequency(
        (3, Generate.Integer<int>().Select(Expression (value) => new Literal(value))),
        (1, Generate.Deferred(() => Generate.Build(
            Expressions(), Expressions(), Expression (left, right) => new Add(left, right)))));

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
        Generator<Money> money = Generate.Build(
            Generate.Between(0L, 1_000_000L),
            Generate.Elements("GBP", "USD"),
            (amount, currency) => new Money(amount, currency));

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
    public void ReadmeSample_WithARelationBetweenMembersAndADependentDraw_ShouldRespectBoth()
    {
        // Arrange
        Generator<Interval> intervals = Generate.Tuple(Generate.Between(0, 100), Generate.Between(0, 100))
            .Where(pair => pair.Item1 <= pair.Item2)
            .Select(pair => new Interval(pair.Item1, pair.Item2));

        Generator<Money> roundMoney = Generate.From(source =>
        {
            var currency = source.Draw(Generate.Elements("GBP", "JPY"));
            var unit = currency == "JPY" ? 1L : 100L;
            return new Money(unit * source.Draw(Generate.Between(0L, 10_000L)), currency);
        });

        // Act
        var sampledIntervals = intervals.Sample(count: 200, seed: 1);
        var sampledMoney = roundMoney.Sample(count: 200, seed: 2);
        var minimal = Property.ForAll(intervals, static interval => interval.High < 50).Check(new CheckOptions { Seed = 3 }).Minimal!.Value;

        // Assert
        Assert.All(sampledIntervals, static interval => Assert.True(interval.Low <= interval.High));
        Assert.All(sampledMoney, static money => Assert.Equal(0, money.Amount % (money.Currency == "JPY" ? 1 : 100)));
        Assert.Contains(sampledMoney, static money => money.Currency == "JPY");
        Assert.Equal(new Interval(0, 50), minimal);
    }

    [Fact]
    public void ReadmeSample_WithSetsAndDictionaries_ShouldGenerateDistinctEntriesWithinBounds()
    {
        // Arrange
        var sets = Generate.Between(0, 100).HashSet(minLength: 1, maxLength: 8);
        var lookup = Generate.Dictionary(Generate.String(), Generate.Integer<int>());
        var flags = Generate.Boolean().HashSet();

        // Act
        var sampledSets = sets.Sample(count: 100, seed: 1);
        var sampledLookups = lookup.Sample(count: 100, seed: 2);
        var sampledFlags = flags.Sample(count: 100, seed: 3);

        // Assert
        Assert.All(sampledSets, static set => Assert.InRange(set.Count, 1, 8));
        Assert.Contains(sampledLookups, static d => d.Count > 1);
        Assert.All(sampledFlags, static set => Assert.InRange(set.Count, 0, 2));
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
    public void ReadmeSample_WithFloatingPointRecipes_ShouldStayInRangeAndShrinkToZero()
    {
        // Arrange
        var probabilities = Generate.FloatingPoint(0.0, 1.0);
        var finite = Generate.FloatingPoint(double.MinValue, double.MaxValue);
        var prices = Generate.Decimal(0.01m, 1000m);

        // Act
        var sampledProbabilities = probabilities.Sample(count: 200, seed: 1);
        var sampledFinite = finite.Sample(count: 500, seed: 2);
        var sampledPrices = prices.Sample(count: 200, seed: 3);
        var minimal = Property.ForAll(Generate.FloatingPoint<double>(), static _ => false).Check(new CheckOptions { Seed = 3 }).Minimal!.Value;
        var fraction = Property.ForAll(Generate.FloatingPoint(0.3, 0.9), static _ => false).Check(new CheckOptions { Seed = 3 }).Minimal!.Value;

        // Assert
        Assert.All(sampledProbabilities, static p => Assert.InRange(p, 0.0, 1.0));
        Assert.All(sampledFinite, static x => Assert.True(double.IsFinite(x)));
        Assert.All(sampledPrices, static p => Assert.InRange(p, 0.01m, 1000m));
        Assert.Equal("0", ValueFormatter.Format(minimal));
        Assert.Equal("0.5", ValueFormatter.Format(fraction));
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
    public void ReadmeSample_WithExplicitExample_ShouldFailOnThePinnedValueWithoutShrinkingIt()
    {
        // Arrange
        var property = Property
            .ForAll(Generate.Integer<int>(), Generate.Integer<int>(), (a, b) => _ = a / b)
            .Example((0, 0));

        // Act
        var exception = Assert.Throws<PropertyFailedException>(() => property.Assert());

        // Assert
        Assert.IsType<DivideByZeroException>(exception.InnerException);
        Assert.Contains("Falsified by an explicit example", exception.Message);
        Assert.Contains("Counterexample: (0, 0)", exception.Message);
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

    [Fact]
    public void ReadmeSample_WithStoreCommands_ShouldPassAgainstACorrectStore()
    {
        // Arrange
        var property = Property.ForAll(
            Generate.CommandSequence(() => new Dictionary<int, int>(), Next),
            sequence => sequence.Run(new Store(), (model, store) => Assert.Equal(model.Count, store.Count)));

        // Act & Assert
        property.Assert();
    }

    [Fact]
    public void ReadmeSample_WithAStoreThatIgnoresOverwrites_ShouldShrinkToThreeCommandsAndReplay()
    {
        // Arrange
        var property = Property.ForAll(
            Generate.CommandSequence(() => new Dictionary<int, int>(), Next),
            sequence => sequence.Run(new Store(ignoresOverwrites: true), (model, store) => Assert.Equal(model.Count, store.Count)));

        // Act
        var result = property.Check(new CheckOptions { Seed = 2024 });
        var replayed = property.Check(new CheckOptions { Replay = result.Replay });

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal([new Put(0, 0), new Put(0, 1), new Get(0)], result.Minimal.Value.Commands);
        Assert.Equal(
            "  Minimal counterexample: Put { Key = 0, Value = 0 }" + Environment.NewLine
            + "    Put { Key = 0, Value = 1 }" + Environment.NewLine
            + "    Get { Key = 0 }",
            result.ToString().Split(Environment.NewLine + "    threw ")[0].Split(Environment.NewLine, 2)[1]);
        Assert.True(replayed.IsFalsified);
        Assert.Equal(result.Original.Value.ToString(), replayed.Original!.Value.ToString());
    }

    [Fact]
    public void ReadmeSample_WithHandleIds_ShouldMapModelIdsToRealHandles()
    {
        // Arrange
        var property = Property.ForAll(
            Generate.CommandSequence(() => new Handles(), NextHandleCommand),
            sequence =>
            {
                Property.Cover(sequence.Commands.Any(static command => command is Write), 50, "writes");
                sequence.Run(new Files());
            });

        // Act & Assert
        property.Assert();
    }

    [Fact]
    public void ReadmeSample_WithStatisticsOverSequences_ShouldReportThem()
    {
        // Arrange
        var property = Property.ForAll(Generate.CommandSequence(() => new Dictionary<int, int>(), Next), sequence =>
        {
            Property.Collect("length", sequence.Commands.Count < 50 ? "under 50" : "50");
            Property.Cover(sequence.Commands.Any(command => command is Delete), 50, "has a delete");
            sequence.Run(new Store());
        });

        // Act
        var result = property.Check(new CheckOptions { Seed = 1 });

        // Assert
        Assert.Equal(PropertyOutcome.Passed, result.Outcome);
        Assert.Contains("has a delete", result.ToString());
        Assert.Contains("50", result.Statistics.Tables["length"].Keys);
    }
}
