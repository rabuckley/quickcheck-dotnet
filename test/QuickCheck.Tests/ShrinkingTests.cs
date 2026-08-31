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
}
