using static QuickCheck.Tests.CommandSequenceTests;
using StoreCommand = QuickCheck.ICommand<System.Collections.Generic.Dictionary<int, int>, QuickCheck.Tests.CommandSequenceTests.Store>;

namespace QuickCheck.Tests;

public sealed class ShrinkingTests
{
    private static readonly CheckOptions Seeded = new() { Seed = 2024, RunCount = 200 };

    [Fact]
    public void Shrinking_WithSelectAndWhere_ShouldFindTheSmallestFailingValue()
    {
        // Arrange
        // Even multiples of three, i.e. multiples of six.
        var generator = Generate.Between(0, 100_000)
            .Select(static x => x * 2)
            .Where(static x => x % 3 == 0);
        var property = Property.ForAll(generator, static x => x < 100);

        // Act
        var result = property.Check(Seeded);

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal(102, result.Minimal.Value);
        Assert.Equal(ShrinkLimit.None, result.ShrinkLimit);
    }

    [Fact]
    public void Shrinking_WithSelectManyQuerySyntax_ShouldFindTheSmallestFailingList()
    {
        // Arrange
        var generator =
            from length in Generate.Between(0, 20)
            from items in Generate.Between(0, 1000).List(length, length)
            select items;
        var property = Property.ForAll(generator, static items => items.All(static x => x < 50));

        // Act
        var result = property.Check(Seeded);

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal([50], result.Minimal.Value);
    }

    [Fact]
    public void Shrinking_WithAList_ShouldDeleteElementsAndShrinkTheRest()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>().List(), static items => items.Sum(static x => (long)x) < 100);

        // Act
        var result = property.Check(Seeded);

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal([100], result.Minimal.Value);
    }

    [Fact]
    public void Shrinking_WithAHashSet_ShouldDeleteElementsAndShrinkTheRest()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>().HashSet(), static set => set.Sum(static x => (long)x) < 100);

        // Act
        var result = property.Check(Seeded);

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal([100], result.Minimal.Value);
    }

    [Fact]
    public void Shrinking_WithAHashSetCountProperty_ShouldFindTheSmallestDistinctElements()
    {
        // Arrange
        var property = Property.ForAll(Generate.Between(0, 1000).HashSet(), static set => set.Count < 3);

        // Act
        var result = property.Check(Seeded);

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal([0, 1, 2], result.Minimal.Value.Order());
    }

    [Fact]
    public void Shrinking_WithADictionary_ShouldShrinkToOneMinimalEntry()
    {
        // Arrange
        var property = Property.ForAll(
            Generate.Dictionary(Generate.Between(0, 1000), Generate.Between(0, 1000)),
            static d => d.Values.Sum() < 100);

        // Act
        var result = property.Check(Seeded);

        // Assert
        Assert.True(result.IsFalsified);
        var entry = Assert.Single(result.Minimal.Value);
        Assert.Equal(0, entry.Key);
        Assert.Equal(100, entry.Value);
    }

    [Fact]
    public void Shrinking_WithNestedLists_ShouldMergeSiblingsToOneList()
    {
        // Arrange
        // Seed 136 strands span deletion at [[0], [0, 0]]: removing the first
        // list's terminator shifts every later choice one slot, which breaks
        // the failure, so only the terminator/guard merge reaches one list.
        var property = Property.ForAll(
            Generate.Integer<int>().List().List(),
            static outer => outer.Sum(static inner => inner.Count) < 3);

        // Act
        var result = property.Check(new CheckOptions { Seed = 136, RunCount = 200 });

        // Assert
        Assert.True(result.IsFalsified);
        var inner = Assert.Single(result.Minimal.Value);
        Assert.Equal([0, 0, 0], inner);
    }

    [Fact]
    public void Shrinking_WithNestedLists_ShouldMergeAndConcentrateTheSum()
    {
        // Arrange
        var property = Property.ForAll(
            Generate.Between(0, 1000).List().List(),
            static outer => outer.Sum(static inner => inner.Sum()) < 100);

        // Act
        var result = property.Check(Seeded);

        // Assert
        Assert.True(result.IsFalsified);
        var inner = Assert.Single(result.Minimal.Value);
        Assert.Equal([100], inner);
    }

    [Fact]
    public void Shrinking_WithAFailureNeedingTwoLists_ShouldNotMergeThem()
    {
        // Arrange
        var property = Property.ForAll(
            Generate.Between(0, 1000).List().List(),
            static outer => outer.Count(static inner => inner.Count > 0) < 2);

        // Act
        var result = property.Check(Seeded);

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal([[0], [0]], result.Minimal.Value);
    }

    [Fact]
    public void Shrinking_WithAListOfSets_ShouldMergeToOneSet()
    {
        // Arrange
        var property = Property.ForAll(
            Generate.Between(0, 1000).HashSet().List(),
            static outer => outer.Sum(static set => set.Count) < 3);

        // Act
        var result = property.Check(Seeded);

        // Assert
        Assert.True(result.IsFalsified);
        var set = Assert.Single(result.Minimal.Value);
        Assert.Equal([0, 1, 2], set.Order());
    }

    [Fact]
    public void Shrinking_WithADuplicate_ShouldFindTheSmallestPair()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>().List(), static items => items.Distinct().Count() == items.Count);

        // Act
        var result = property.Check(Seeded);

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal([0, 0], result.Minimal.Value);
    }

    [Fact]
    public void Shrinking_WithNegativeIntegers_ShouldMoveTowardsZero()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>(), static x => x > -10);

        // Act
        var result = property.Check(Seeded);

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal(-10, result.Minimal.Value);
    }

    [Fact]
    public void Shrinking_WithAString_ShouldFindTheShortestFailingOne()
    {
        // Arrange
        var property = Property.ForAll(Generate.String(), static s => !s.Contains('z'));

        // Act
        var result = property.Check(Seeded);

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal("z", result.Minimal.Value);
    }

    [Fact]
    public void Shrinking_WithAPair_ShouldShrinkBothJointly()
    {
        // Arrange
        var property = Property.ForAll(Generate.Between(0, 1000), Generate.Between(0, 1000), static (a, b) => a + b < 100);

        // Act
        var result = property.Check(Seeded);

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal((0, 100), result.Minimal.Value);
    }

    [Fact]
    public void Shrinking_WithATripleRelatedAcrossAMember_ShouldShrinkTheOuterPairJointly()
    {
        // Arrange
        // The string between the pair shrinks to a single guard choice, which leaves the pair
        // within the redistribution window.
        var property = Property.ForAll(
            Generate.Between(0, 1000),
            Generate.String(),
            Generate.Between(0, 1000),
            static (a, _, c) => a + c < 100);

        // Act
        var result = property.Check(Seeded);

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal((0, "", 100), result.Minimal.Value);
    }

    [Fact]
    public void Shrinking_WithTwoDistinctFailures_ShouldNotSlideIntoTheOtherOne()
    {
        // Arrange
        // Large values throw one exception, small ones another; the shrinker
        // must keep the failure it started with rather than sliding to the
        // "simpler" small-value bug.
        var property = Property.ForAll(Generate.Between(0, 1_000_000), static x =>
        {
            if (x >= 500_000)
            {
                throw new InvalidOperationException("large");
            }

            if (x is > 0 and < 10)
            {
                throw new ArgumentException("small");
            }
        });

        // Act
        var result = property.Check(new CheckOptions { Seed = 7, RunCount = 500 });

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal(result.Original.Exception?.GetType(), result.Minimal.Exception?.GetType());

        if (result.Minimal.Exception is InvalidOperationException)
        {
            Assert.Equal(500_000, result.Minimal.Value);
        }
        else
        {
            Assert.Equal(1, result.Minimal.Value);
        }
    }

    [Fact]
    public void Shrinking_WithAGeneratorThatThrowsOnACandidate_ShouldKeepTheCounterexample()
    {
        // Arrange
        // Generation rarely violates the guard, but deleting the low member's span replays the
        // high member's choice into its slot and pads the high member with 0, so shrinking
        // routinely does.
        var refusals = 0;
        var intervals = Generate.From(source =>
        {
            var low = source.Draw(Generate.Between(0, 1));
            var high = source.Draw(Generate.Between(0, 1000));

            if (high < low)
            {
                refusals++;
            }

            return new Interval(low, high);
        });
        var property = Property.ForAll(intervals, static interval => interval.High < 500);

        // Act
        var result = property.Check(Seeded);

        // Assert
        Assert.True(refusals > 0);
        Assert.True(result.IsFalsified);
        Assert.Equal(new Interval(0, 500), result.Minimal.Value);
        Assert.NotNull(result.Replay);
        Assert.Equal(ShrinkLimit.None, result.ShrinkLimit);
    }

    private sealed record Interval
    {
        public Interval(int low, int high)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(high, low);
            Low = low;
            High = high;
        }

        public int Low { get; }

        public int High { get; }
    }

    [Fact]
    public void Shrinking_WithBuildOverIndependentMembers_ShouldShrinkEachToItsMinimum()
    {
        // Arrange
        var generator = Generate.Build(
            Generate.Integer<int>(),
            Generate.String(),
            Generate.Boolean(),
            static (number, text, flag) => (number, text, flag));
        var property = Property.ForAll(generator, static value => value.number <= 100);

        // Act
        var result = property.Check(Seeded);

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal((101, "", false), result.Minimal.Value);
    }

    [Fact]
    public void Shrinking_WithBuildOverRelatedMembers_ShouldMatchTheTupleLayout()
    {
        // Arrange
        // The same members as the ForAll triple above, so the same minimum is expected.
        var generator = Generate.Build(
            Generate.Between(0, 1000),
            Generate.String(),
            Generate.Between(0, 1000),
            static (a, text, c) => (a, text, c));
        var property = Property.ForAll(generator, static value => value.a + value.c < 100);

        // Act
        var result = property.Check(Seeded);

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal((0, "", 100), result.Minimal.Value);
    }

    [Fact]
    public void Shrinking_WithASequence_ShouldShrinkEachElementIndependently()
    {
        // Arrange
        var generator = Generate.Sequence(Enumerable.Repeat(Generate.Between(0, 1000), 8));
        var property = Property.ForAll(generator, static items => items.Sum() < 100);

        // Act
        var result = property.Check(Seeded);

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal([0, 0, 0, 0, 0, 0, 0, 100], result.Minimal.Value);
    }

    [Fact]
    public void Shrinking_WithZeroMaxShrinkAttempts_ShouldBeDisabled()
    {
        // Arrange
        var property = Property.ForAll(Generate.Between(1000, 1_000_000), static x => x < 1000);

        // Act
        var result = property.Check(Seeded with { MaxShrinkAttempts = 0 });

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal(0, result.ShrinkAttempts);
        Assert.Equal(result.Original.Value, result.Minimal.Value);
        Assert.Equal(ShrinkLimit.Attempts, result.ShrinkLimit);
    }

    [Fact]
    public void Shrinking_WithZeroMaxShrinkWork_ShouldBeDisabled()
    {
        // Arrange
        var property = Property.ForAll(Generate.Between(1000, 1_000_000), static x => x < 1000);

        // Act
        var result = property.Check(Seeded with { MaxShrinkWork = 0 });

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal(0, result.ShrinkAttempts);
        Assert.Equal(result.Original.Value, result.Minimal.Value);
    }

    [Fact]
    public void Shrinking_WithALargeExampleAndASmallWorkBudget_ShouldStopEarly()
    {
        // Arrange
        // Each candidate replays about 2,000 choices, so the 20,000-choice budget
        // buys roughly ten of them — nowhere near the 10,000 attempts still allowed.
        var property = Property.ForAll(
            Generate.Integer<int>().List(minLength: 2_000, maxLength: 2_000),
            static items => items.Count < 5);

        // Act
        var result = property.Check(Seeded with { MaxShrinkAttempts = 10_000, MaxShrinkWork = 20_000 });

        // Assert
        Assert.True(result.IsFalsified);
        Assert.InRange(result.ShrinkAttempts, 1, 20);
        Assert.Equal(ShrinkLimit.Work, result.ShrinkLimit);
        Assert.Contains("MaxShrinkWork", result.ToString());
    }

    /// <summary>The store commands over keys wide enough that two of them rarely agree by chance.</summary>
    private static Generator<StoreCommand> WideKeyNext(Dictionary<int, int> model) => Generate.Frequency(
        (3, Generate.Build(Generate.Between(0, 1000), Generate.Between(0, 100), StoreCommand (key, value) => new Put(key, value))),
        (2, Generate.Between(0, 1000).Select(StoreCommand (key) => new Get(key))));

    [Fact]
    public void Shrinking_WithATwoCommandBug_ShouldFindTheShortestSequenceWithAgreeingKeys()
    {
        // Arrange
        var generator = Generate.CommandSequence(() => new Dictionary<int, int>(), WideKeyNext);
        var property = Property.ForAll(generator, sequence => sequence.Run(new Store(StoreBug.DropsLargeKeys)));

        // Act
        var result = property.Check(Seeded);

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal([new Put(101, 1), new Get(101)], result.Minimal.Value.Commands);
        Assert.Equal(ShrinkLimit.None, result.ShrinkLimit);
    }

    [Fact]
    public void Shrinking_WithAPrecondition_ShouldNeverRunACommandInAStateItForbids()
    {
        // Arrange
        var tripwireHits = 0;
        var property = Property.ForAll(StoreSequences(), sequence =>
        {
            try
            {
                sequence.Run(new Store(StoreBug.ReturnsDeletedValues));
            }
            catch (PreconditionViolatedException)
            {
                tripwireHits++;
                throw;
            }
        });

        // Act
        var result = property.Check(Seeded);

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal([new Put(0, 1), new Delete(0), new Get(0)], result.Minimal.Value.Commands);
        Assert.IsType<Xunit.Sdk.EqualException>(result.Minimal.Exception);
        Assert.Equal(0, tripwireHits);
    }

    [Fact]
    public void Shrinking_WithAnInvariantViolation_ShouldFindTheShortestSequenceThatBreaksIt()
    {
        // Arrange
        var property = Property.ForAll(StoreSequences(), sequence =>
            sequence.Run(new Store(StoreBug.DeleteLeavesCount), (model, store) => Assert.Equal(model.Count, store.Count)));

        // Act
        var result = property.Check(Seeded);

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal([new Put(0, 0), new Delete(0)], result.Minimal.Value.Commands);
    }

    private sealed class BoundedStack(int capacity)
    {
        private readonly Stack<int> _values = new();

        public int Count => _values.Count;

        public void Push(int value)
        {
            if (_values.Count < capacity)
            {
                _values.Push(value);
            }
        }

        public int Pop() => _values.Pop();
    }

    private sealed record Push(int Value) : ICommand<List<int>, BoundedStack>
    {
        public List<int> Update(List<int> model)
        {
            model.Add(Value);
            return model;
        }

        public void Run(List<int> model, BoundedStack stack)
        {
            stack.Push(Value);
            Assert.Equal(model.Count + 1, stack.Count);
        }
    }

    private sealed record Pop : ICommand<List<int>, BoundedStack>
    {
        public bool Precondition(List<int> model) => model.Count > 0;

        public List<int> Update(List<int> model)
        {
            model.RemoveAt(model.Count - 1);
            return model;
        }

        public void Run(List<int> model, BoundedStack stack) => Assert.Equal(model[^1], stack.Pop());
    }

    [Fact]
    public void Shrinking_WithACapacityBug_ShouldFindExactlyEnoughPushesAndNoPops()
    {
        // Arrange
        var generator = Generate.CommandSequence<List<int>, BoundedStack>(() => [], _ => Generate.Frequency(
            (2, Generate.Between(0, 100).Select(ICommand<List<int>, BoundedStack> (value) => new Push(value))),
            (1, Generate.Constant<ICommand<List<int>, BoundedStack>>(new Pop()))));
        var property = Property.ForAll(generator, sequence => sequence.Run(new BoundedStack(3)));

        // Act
        var result = property.Check(Seeded);

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal([new Push(0), new Push(0), new Push(0), new Push(0)], result.Minimal.Value.Commands);
    }
}
