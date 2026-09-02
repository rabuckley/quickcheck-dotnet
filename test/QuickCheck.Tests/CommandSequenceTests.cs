using System.Collections.Immutable;

namespace QuickCheck.Tests;

public sealed class CommandSequenceTests
{
    public enum StoreBug
    {
        None,

        /// <summary>A key above 100 is never stored.</summary>
        DropsLargeKeys,

        /// <summary>A deleted key still returns its last value.</summary>
        ReturnsDeletedValues,

        /// <summary>A deleted key is no longer readable but still counted.</summary>
        DeleteLeavesCount,
    }

    public sealed class PreconditionViolatedException(string message) : Exception(message);

    public sealed class Store(StoreBug bug = StoreBug.None)
    {
        private readonly Dictionary<int, int> _entries = [];
        private readonly Dictionary<int, int> _deleted = [];

        public int Count => bug is StoreBug.DeleteLeavesCount ? _entries.Count + _deleted.Count : _entries.Count;

        public void Put(int key, int value)
        {
            if (bug is StoreBug.DropsLargeKeys && key > 100)
            {
                return;
            }

            _entries[key] = value;
            _deleted.Remove(key);
        }

        public int Get(int key)
        {
            if (_entries.TryGetValue(key, out var value))
            {
                return value;
            }

            return bug is StoreBug.ReturnsDeletedValues ? _deleted.GetValueOrDefault(key) : 0;
        }

        public void Delete(int key)
        {
            if (!_entries.Remove(key, out var value))
            {
                throw new PreconditionViolatedException($"Delete({key}) ran on a key the store does not hold.");
            }

            _deleted[key] = value;
        }
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

        public void Run(Dictionary<int, int> model, Store store) =>
            Assert.Equal(model.GetValueOrDefault(Key), store.Get(Key));
    }

    public sealed record Delete(int Key) : ICommand<Dictionary<int, int>, Store>
    {
        public bool Precondition(Dictionary<int, int> model) => model.ContainsKey(Key);

        public Dictionary<int, int> Update(Dictionary<int, int> model)
        {
            model.Remove(Key);
            return model;
        }

        public void Run(Dictionary<int, int> model, Store store) => store.Delete(Key);
    }

    public static Generator<ICommand<Dictionary<int, int>, Store>> Next(Dictionary<int, int> model) => Generate.Frequency(
        (3, Generate.Build(Generate.Between(0, 3), Generate.Between(0, 100), ICommand<Dictionary<int, int>, Store> (key, value) => new Put(key, value))),
        (2, Generate.Between(0, 3).Select(ICommand<Dictionary<int, int>, Store> (key) => new Get(key))),
        (1, Generate.Between(0, 3).Select(ICommand<Dictionary<int, int>, Store> (key) => new Delete(key))));

    public static Generator<CommandSequence<Dictionary<int, int>, Store>> StoreSequences(int maxLength = 50) =>
        Generate.CommandSequence(() => new Dictionary<int, int>(), Next, maxLength);

    private sealed record Increment(List<int> Seen) : ICommand<int, List<int>>
    {
        public int Update(int model) => model + 1;

        public void Run(int model, List<int> system)
        {
            Seen.Add(model);
            system.Add(model);
        }
    }

    private static Generator<ICommand<int, List<int>>> Increments(List<int> seen) =>
        Generate.Constant<ICommand<int, List<int>>>(new Increment(seen));

    [Fact]
    public void CommandSequence_WhenSampled_ShouldStartEachSequenceFromAFreshModel()
    {
        // Arrange
        var factoryCalls = 0;
        var generator = Generate.CommandSequence(
            () =>
            {
                factoryCalls++;
                return new Dictionary<int, int>();
            },
            Next,
            maxLength: 20);

        // Act
        var sequences = generator.Sample(count: 30, seed: 1);

        // Assert
        Assert.Equal(30, factoryCalls);
        Assert.All(sequences, sequence => sequence.Run(new Store()));
    }

    [Fact]
    public void CommandSequence_WhenSampled_ShouldSatisfyEveryPreconditionAtItsPosition()
    {
        // Arrange
        var generator = StoreSequences();

        // Act
        var sequences = generator.Sample(count: 200, seed: 2);

        // Assert
        Assert.All(sequences, sequence =>
        {
            var model = new Dictionary<int, int>();

            foreach (var command in sequence.Commands)
            {
                Assert.True(command.Precondition(model));
                model = command.Update(model);
            }
        });
        Assert.Contains(sequences, sequence => sequence.Commands.Any(command => command is Delete));
    }

    [Fact]
    public void CommandSequence_WithMaxLength_ShouldNeverExceedItAndUsuallyReachIt()
    {
        // Arrange
        var generator = StoreSequences(maxLength: 10);

        // Act
        var sequences = generator.Sample(count: 100, seed: 3);

        // Assert
        Assert.All(sequences, sequence => Assert.InRange(sequence.Commands.Count, 0, 10));
        Assert.InRange(sequences.Count(sequence => sequence.Commands.Count == 10), 95, 100);
    }

    [Fact]
    public void CommandSequence_WhenNoCommandSatisfiesItsPrecondition_ShouldDiscardTheExample()
    {
        // Arrange
        var generator = Generate.CommandSequence(
            () => new Dictionary<int, int>(),
            _ => Generate.Between(0, 3).Select(ICommand<Dictionary<int, int>, Store> (key) => new Delete(key)));

        // Act
        var result = Property.ForAll(generator, static _ => true).Check(new CheckOptions { Seed = 4 });

        // Assert
        Assert.Throws<InvalidOperationException>(() => generator.Sample(count: 1, seed: 4));
        Assert.Equal(PropertyOutcome.Exhausted, result.Outcome);
        Assert.Equal(0, result.TestsRun);
        Assert.InRange(result.Discards, 1, int.MaxValue);
    }

    [Fact]
    public void CommandSequence_WhenCommandReturnsNull_ShouldEndTheSequenceWithoutDiscarding()
    {
        // Arrange
        var generator = Generate.CommandSequence(() => 0, model => model < 3 ? Increments([]) : null);

        // Act
        var sequences = generator.Sample(count: 50, seed: 5);
        var result = Property.ForAll(generator, static _ => true).Check(new CheckOptions { Seed = 5 });

        // Assert
        Assert.All(sequences, sequence => Assert.Equal(3, sequence.Commands.Count));
        Assert.Equal(PropertyOutcome.Passed, result.Outcome);
        Assert.Equal(0, result.Discards);
    }

    [Fact]
    public void CommandSequence_WhenSampled_ShouldNotRunAnyCommand()
    {
        // Arrange
        var seen = new List<int>();
        var generator = Generate.CommandSequence(() => 0, _ => Increments(seen), maxLength: 5);

        // Act
        var sequences = generator.Sample(count: 10, seed: 6);

        // Assert
        Assert.Empty(seen);
        Assert.All(sequences, sequence => Assert.Equal(5, sequence.Commands.Count));
    }

    [Fact]
    public void CommandSequence_WhenACommandGeneratorThrows_ShouldFailTheCheckWithThatException()
    {
        // Arrange
        // Elements over the empty model refuses the very first step, so nothing is drawn before it.
        var generator = Generate.CommandSequence(
            () => new Dictionary<int, int>(),
            model => Generate.Elements(model.Keys).Select(ICommand<Dictionary<int, int>, Store> (key) => new Delete(key)));
        var property = Property.ForAll(generator, static _ => true);

        // Act
        var result = property.Check(new CheckOptions { Seed = 7 });

        // Assert
        Assert.True(result.IsGenerationFailed);
        Assert.IsType<ArgumentException>(result.GenerationException);
    }

    [Fact]
    public void CommandSequence_WhenAGeneratorProducesANullCommand_ShouldThrow()
    {
        // Arrange
        var generator = Generate.CommandSequence(
            () => new Dictionary<int, int>(),
            _ => Generate.Constant<ICommand<Dictionary<int, int>, Store>>(null!));

        // Act
        void Act() => generator.Sample(count: 1, seed: 8);

        // Assert
        Assert.Throws<InvalidOperationException>(Act);
    }

    [Fact]
    public void CommandSequence_WithTheSameSeed_ShouldPrintTheSameText()
    {
        // Arrange
        var generator = StoreSequences();

        // Act
        var first = generator.Sample(count: 5, seed: 9).Select(static sequence => sequence.ToString());
        var second = generator.Sample(count: 5, seed: 9).Select(static sequence => sequence.ToString());

        // Assert
        Assert.Equal(first, second);
    }

    [Fact]
    public void CommandSequence_WithInvalidArguments_ShouldThrowNamingThem()
    {
        // Act
        var nullFactory = Assert.Throws<ArgumentNullException>(() =>
            Generate.CommandSequence<Dictionary<int, int>, Store>(null!, Next));
        var nullCommand = Assert.Throws<ArgumentNullException>(() =>
            Generate.CommandSequence<Dictionary<int, int>, Store>(() => [], null!));
        var zeroLength = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Generate.CommandSequence(() => new Dictionary<int, int>(), Next, maxLength: 0));

        // Assert
        Assert.Equal("initialModel", nullFactory.ParamName);
        Assert.Equal("command", nullCommand.ParamName);
        Assert.Equal("maxLength", zeroLength.ParamName);
    }

    [Fact]
    public void Run_ShouldGiveEachCommandTheModelBeforeItAndReturnTheFinalModel()
    {
        // Arrange
        var seen = new List<int>();
        var sequence = Generate.CommandSequence(() => 0, _ => Increments(seen), maxLength: 3).Sample(count: 1, seed: 10)[0];
        var system = new List<int>();

        // Act
        var final = sequence.Run(system);

        // Assert
        Assert.Equal([0, 1, 2], seen);
        Assert.Equal([0, 1, 2], system);
        Assert.Equal(3, final);
    }

    [Fact]
    public void Run_WithInvariant_ShouldCheckItBeforeTheFirstCommandAndAfterEachOne()
    {
        // Arrange
        var sequence = Generate.CommandSequence(() => 0, _ => Increments([]), maxLength: 3).Sample(count: 1, seed: 11)[0];
        var checkedModels = new List<int>();

        // Act
        sequence.Run(new List<int>(), (model, system) =>
        {
            checkedModels.Add(model);
            Assert.Equal(model, system.Count);
        });

        // Assert
        Assert.Equal([0, 1, 2, 3], checkedModels);
    }

    [Fact]
    public void Run_WithNullSystem_ShouldThrow()
    {
        // Arrange
        var sequence = StoreSequences().Sample(count: 1, seed: 12)[0];

        // Act
        void Act() => sequence.Run(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }

    private sealed record ImmutablePut(int Key, int Value) : ICommand<ImmutableDictionary<int, int>, Store>
    {
        public ImmutableDictionary<int, int> Update(ImmutableDictionary<int, int> model) => model.SetItem(Key, Value);

        public void Run(ImmutableDictionary<int, int> model, Store store) => store.Put(Key, Value);
    }

    private sealed record ImmutableDelete(int Key) : ICommand<ImmutableDictionary<int, int>, Store>
    {
        public bool Precondition(ImmutableDictionary<int, int> model) => model.ContainsKey(Key);

        public ImmutableDictionary<int, int> Update(ImmutableDictionary<int, int> model) => model.Remove(Key);

        public void Run(ImmutableDictionary<int, int> model, Store store)
        {
            Assert.Equal(model[Key], store.Get(Key));
            store.Delete(Key);
        }
    }

    [Fact]
    public void Run_WithAnImmutableModel_ShouldThreadTheUpdatedModelThroughEveryCommand()
    {
        // Arrange
        var generator = Generate.CommandSequence(
            () => ImmutableDictionary<int, int>.Empty,
            model => Generate.Frequency(
                (2, Generate.Build(Generate.Between(0, 3), Generate.Between(0, 100), ICommand<ImmutableDictionary<int, int>, Store> (key, value) => new ImmutablePut(key, value))),
                (1, Generate.Elements(model.Keys.DefaultIfEmpty(0)).Select(ICommand<ImmutableDictionary<int, int>, Store> (key) => new ImmutableDelete(key)))));

        // Act
        var sequences = generator.Sample(count: 100, seed: 13);
        var runs = sequences.Select(sequence =>
        {
            var store = new Store();
            var final = sequence.Run(store, (model, store) => Assert.Equal(model.Count, store.Count));
            return (final, store);
        }).ToList();

        // Assert
        Assert.Contains(sequences, sequence => sequence.Commands.Any(command => command is ImmutableDelete));
        Assert.All(runs, run =>
        {
            Assert.Equal(run.final.Count, run.store.Count);
            Assert.All(run.final, entry => Assert.Equal(entry.Value, run.store.Get(entry.Key)));
        });
    }

    [Fact]
    public void ToString_ShouldPrintOneCommandPerLine()
    {
        // Arrange
        var sequence = Generate.CommandSequence(
            () => new Dictionary<int, int>(),
            model => Generate.Constant<ICommand<Dictionary<int, int>, Store>>(model.Count == 0 ? new Put(1, 2) : new Get(1)),
            maxLength: 2).Sample(count: 1, seed: 14)[0];

        // Act
        var text = sequence.ToString();

        // Assert
        Assert.Equal("Put { Key = 1, Value = 2 }" + Environment.NewLine + "Get { Key = 1 }", text);
        Assert.Equal(text, ValueFormatter.Format(sequence));
    }

    [Fact]
    public void ToString_WithNoCommands_ShouldPrintEmptyBrackets()
    {
        // Arrange
        var sequence = Generate.CommandSequence<Dictionary<int, int>, Store>(() => [], static _ => null).Sample(count: 1, seed: 15)[0];

        // Act
        var text = sequence.ToString();

        // Assert
        Assert.Empty(sequence.Commands);
        Assert.Equal("[]", text);
    }
}
