using System.Collections.Immutable;
using static QuickCheck.Tests.CommandSequenceTests;
using AsyncStoreCommand = QuickCheck.IAsyncCommand<System.Collections.Generic.Dictionary<int, int>, QuickCheck.Tests.AsyncCommandSequenceTests.AsyncStore>;

namespace QuickCheck.Tests;

public sealed class AsyncCommandSequenceTests
{
    /// <summary>A <see cref="Store"/> whose every operation yields once, so a run really awaits.</summary>
    public sealed class AsyncStore(StoreBug bug = StoreBug.None)
    {
        private readonly Store _store = new(bug);

        public int Count => _store.Count;

        public async Task PutAsync(int key, int value)
        {
            await Task.Yield();
            _store.Put(key, value);
        }

        public async Task<int> GetAsync(int key)
        {
            await Task.Yield();
            return _store.Get(key);
        }

        public async Task DeleteAsync(int key)
        {
            await Task.Yield();
            _store.Delete(key);
        }
    }

    public sealed record Put(int Key, int Value) : AsyncStoreCommand
    {
        public Dictionary<int, int> Update(Dictionary<int, int> model)
        {
            model[Key] = Value;
            return model;
        }

        public Task RunAsync(Dictionary<int, int> model, AsyncStore store) => store.PutAsync(Key, Value);
    }

    public sealed record Get(int Key) : AsyncStoreCommand
    {
        public Dictionary<int, int> Update(Dictionary<int, int> model) => model;

        public async Task RunAsync(Dictionary<int, int> model, AsyncStore store) =>
            Assert.Equal(model.GetValueOrDefault(Key), await store.GetAsync(Key));
    }

    public sealed record Delete(int Key) : AsyncStoreCommand
    {
        public bool Precondition(Dictionary<int, int> model) => model.ContainsKey(Key);

        public Dictionary<int, int> Update(Dictionary<int, int> model)
        {
            model.Remove(Key);
            return model;
        }

        public Task RunAsync(Dictionary<int, int> model, AsyncStore store) => store.DeleteAsync(Key);
    }

    public static Generator<AsyncStoreCommand> Next(Dictionary<int, int> model) => Generate.Frequency(
        (3, Generate.Build(Generate.Between(0, 3), Generate.Between(0, 100), AsyncStoreCommand (key, value) => new Put(key, value))),
        (2, Generate.Between(0, 3).Select(AsyncStoreCommand (key) => new Get(key))),
        (1, Generate.Between(0, 3).Select(AsyncStoreCommand (key) => new Delete(key))));

    public static Generator<AsyncCommandSequence<Dictionary<int, int>, AsyncStore>> AsyncStoreSequences(int maxLength = 50) =>
        Generate.AsyncCommandSequence(() => new Dictionary<int, int>(), Next, maxLength);

    /// <summary>Records the model it saw and, around a yield, when it started and finished.</summary>
    private sealed record Increment(List<int> Seen, List<(int Model, string Event)> Events) : IAsyncCommand<int, List<int>>
    {
        public int Update(int model) => model + 1;

        public async Task RunAsync(int model, List<int> system)
        {
            Events.Add((model, "start"));
            Seen.Add(model);
            await Task.Yield();
            system.Add(model);
            Events.Add((model, "end"));
        }
    }

    private static Generator<IAsyncCommand<int, List<int>>> Increments(List<int> seen, List<(int, string)>? events = null) =>
        Generate.Constant<IAsyncCommand<int, List<int>>>(new Increment(seen, events ?? []));

    [Fact]
    public async Task AsyncCommandSequence_WhenSampled_ShouldStartEachSequenceFromAFreshModel()
    {
        // Arrange
        var factoryCalls = 0;
        var generator = Generate.AsyncCommandSequence(
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

        foreach (var sequence in sequences)
        {
            await sequence.RunAsync(new AsyncStore());
        }
    }

    [Fact]
    public void AsyncCommandSequence_WhenSampled_ShouldSatisfyEveryPreconditionAtItsPosition()
    {
        // Arrange
        var generator = AsyncStoreSequences();

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
    public void AsyncCommandSequence_WhenSampled_ShouldNotRunAnyCommand()
    {
        // Arrange
        var seen = new List<int>();
        var generator = Generate.AsyncCommandSequence(() => 0, _ => Increments(seen), maxLength: 5);

        // Act
        var sequences = generator.Sample(count: 10, seed: 6);

        // Assert
        Assert.Empty(seen);
        Assert.All(sequences, sequence => Assert.Equal(5, sequence.Commands.Count));
    }

    [Fact]
    public void AsyncCommandSequence_WithTheSameSeed_ShouldPrintTheSameText()
    {
        // Arrange
        var generator = AsyncStoreSequences();

        // Act
        var first = generator.Sample(count: 5, seed: 9).Select(static sequence => sequence.ToString());
        var second = generator.Sample(count: 5, seed: 9).Select(static sequence => sequence.ToString());

        // Assert
        Assert.Equal(first, second);
    }

    [Fact]
    public void AsyncCommandSequence_WithInvalidArguments_ShouldThrowNamingThem()
    {
        // Act
        var nullFactory = Assert.Throws<ArgumentNullException>(() =>
            Generate.AsyncCommandSequence<Dictionary<int, int>, AsyncStore>(null!, Next));
        var nullCommand = Assert.Throws<ArgumentNullException>(() =>
            Generate.AsyncCommandSequence<Dictionary<int, int>, AsyncStore>(() => [], null!));
        var zeroLength = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Generate.AsyncCommandSequence(() => new Dictionary<int, int>(), Next, maxLength: 0));

        // Assert
        Assert.Equal("initialModel", nullFactory.ParamName);
        Assert.Equal("command", nullCommand.ParamName);
        Assert.Equal("maxLength", zeroLength.ParamName);
    }

    [Fact]
    public async Task RunAsync_ShouldRunEachCommandToCompletionBeforeStartingTheNext()
    {
        // Arrange
        var events = new List<(int Model, string Event)>();
        var sequence = Generate.AsyncCommandSequence(() => 0, _ => Increments([], events), maxLength: 4).Sample(count: 1, seed: 10)[0];

        // Act
        await sequence.RunAsync(new List<int>());

        // Assert
        Assert.Equal(
            [(0, "start"), (0, "end"), (1, "start"), (1, "end"), (2, "start"), (2, "end"), (3, "start"), (3, "end")],
            events);
    }

    [Fact]
    public async Task RunAsync_ShouldGiveEachCommandTheModelBeforeItAndReturnTheFinalModel()
    {
        // Arrange
        var seen = new List<int>();
        var sequence = Generate.AsyncCommandSequence(() => 0, _ => Increments(seen), maxLength: 3).Sample(count: 1, seed: 10)[0];
        var system = new List<int>();

        // Act
        var final = await sequence.RunAsync(system);

        // Assert
        Assert.Equal([0, 1, 2], seen);
        Assert.Equal([0, 1, 2], system);
        Assert.Equal(3, final);
    }

    [Fact]
    public async Task RunAsync_WithInvariant_ShouldCheckItBeforeTheFirstCommandAndAfterEachOne()
    {
        // Arrange
        var sequence = Generate.AsyncCommandSequence(() => 0, _ => Increments([]), maxLength: 3).Sample(count: 1, seed: 11)[0];
        var checkedModels = new List<int>();

        // Act
        await sequence.RunAsync(new List<int>(), (model, system) =>
        {
            checkedModels.Add(model);
            Assert.Equal(model, system.Count);
        });

        // Assert
        Assert.Equal([0, 1, 2, 3], checkedModels);
    }

    [Fact]
    public async Task RunAsync_WithAsynchronousInvariant_ShouldAwaitItBeforeTheFirstCommandAndAfterEachOne()
    {
        // Arrange
        var sequence = Generate.AsyncCommandSequence(() => 0, _ => Increments([]), maxLength: 3).Sample(count: 1, seed: 11)[0];
        var checkedModels = new List<int>();

        // Act
        await sequence.RunAsync(new List<int>(), async (model, system) =>
        {
            await Task.Yield();
            checkedModels.Add(model);
            Assert.Equal(model, system.Count);
        });

        // Assert
        Assert.Equal([0, 1, 2, 3], checkedModels);
    }

    [Fact]
    public async Task RunAsync_WithAnAsynchronousInvariantThatFails_ShouldFaultTheRun()
    {
        // Arrange
        var sequence = Generate.AsyncCommandSequence(() => 0, _ => Increments([]), maxLength: 3).Sample(count: 1, seed: 11)[0];

        // Act
        Task Act() => sequence.RunAsync(new List<int>(), async (model, _) =>
        {
            await Task.Yield();
            Assert.NotEqual(2, model);
        });

        // Assert
        await Assert.ThrowsAsync<Xunit.Sdk.NotEqualException>(Act);
    }

    [Fact]
    public void RunAsync_WithNullSystem_ShouldThrowSynchronously()
    {
        // Arrange
        var sequence = AsyncStoreSequences().Sample(count: 1, seed: 12)[0];

        // Act
        void Act() => sequence.RunAsync(null!);
        void ActWithInvariant() => sequence.RunAsync(null!, static (_, _) => Task.CompletedTask);

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
        Assert.Throws<ArgumentNullException>(ActWithInvariant);
    }

    [Fact]
    public async Task CheckAsync_WhenACommandFails_ShouldReportItsExceptionUnwrapped()
    {
        // Arrange
        var property = Property.ForAll(AsyncStoreSequences(), sequence => sequence.RunAsync(new AsyncStore(StoreBug.ReturnsDeletedValues)));

        // Act
        var result = await property.CheckAsync(new CheckOptions { Seed = 16 });

        // Assert
        Assert.True(result.IsFalsified);
        Assert.IsType<Xunit.Sdk.EqualException>(result.Minimal.Exception);
    }

    private sealed record ImmutablePut(int Key, int Value) : IAsyncCommand<ImmutableDictionary<int, int>, AsyncStore>
    {
        public ImmutableDictionary<int, int> Update(ImmutableDictionary<int, int> model) => model.SetItem(Key, Value);

        public Task RunAsync(ImmutableDictionary<int, int> model, AsyncStore store) => store.PutAsync(Key, Value);
    }

    private sealed record ImmutableDelete(int Key) : IAsyncCommand<ImmutableDictionary<int, int>, AsyncStore>
    {
        public bool Precondition(ImmutableDictionary<int, int> model) => model.ContainsKey(Key);

        public ImmutableDictionary<int, int> Update(ImmutableDictionary<int, int> model) => model.Remove(Key);

        public async Task RunAsync(ImmutableDictionary<int, int> model, AsyncStore store)
        {
            Assert.Equal(model[Key], await store.GetAsync(Key));
            await store.DeleteAsync(Key);
        }
    }

    [Fact]
    public async Task RunAsync_WithAnImmutableModel_ShouldThreadTheUpdatedModelThroughEveryCommand()
    {
        // Arrange
        var generator = Generate.AsyncCommandSequence(
            () => ImmutableDictionary<int, int>.Empty,
            model => Generate.Frequency(
                (2, Generate.Build(Generate.Between(0, 3), Generate.Between(0, 100), IAsyncCommand<ImmutableDictionary<int, int>, AsyncStore> (key, value) => new ImmutablePut(key, value))),
                (1, Generate.Elements(model.Keys.DefaultIfEmpty(0)).Select(IAsyncCommand<ImmutableDictionary<int, int>, AsyncStore> (key) => new ImmutableDelete(key)))));

        // Act
        var sequences = generator.Sample(count: 100, seed: 13);
        var runs = new List<(ImmutableDictionary<int, int> Final, AsyncStore Store)>();

        foreach (var sequence in sequences)
        {
            var store = new AsyncStore();
            var final = await sequence.RunAsync(store, (model, store) => Assert.Equal(model.Count, store.Count));
            runs.Add((final, store));
        }

        // Assert
        Assert.Contains(sequences, sequence => sequence.Commands.Any(command => command is ImmutableDelete));

        foreach (var (final, store) in runs)
        {
            Assert.Equal(final.Count, store.Count);

            foreach (var entry in final)
            {
                Assert.Equal(entry.Value, await store.GetAsync(entry.Key));
            }
        }
    }

    [Fact]
    public void ToString_ShouldPrintOneCommandPerLine()
    {
        // Arrange
        var sequence = Generate.AsyncCommandSequence(
            () => new Dictionary<int, int>(),
            model => Generate.Constant<AsyncStoreCommand>(model.Count == 0 ? new Put(1, 2) : new Get(1)),
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
        var sequence = Generate.AsyncCommandSequence<Dictionary<int, int>, AsyncStore>(() => [], static _ => null).Sample(count: 1, seed: 15)[0];

        // Act
        var text = sequence.ToString();

        // Assert
        Assert.Empty(sequence.Commands);
        Assert.Equal("[]", text);
    }
}
