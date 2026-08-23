using QuickCheck.Choices;
using QuickCheck.Generators;

namespace QuickCheck.Tests.Generators;

public sealed class IntegerRangeTests
{
    [Theory]
    [InlineData(-3, 10)]
    [InlineData(5, 9)]
    [InlineData(-9, -5)]
    [InlineData(0, 0)]
    public void Force_WhenGenerating_ShouldReturnEveryValueInTheRange(int min, int max)
    {
        // Arrange
        var range = new IntegerRange<int>(min, max);
        var source = ChoiceSource.FromRandom(Xoshiro256StarStar.ForRun(seed: 1, run: 0));

        // Act
        var forced = Enumerable.Range(min, max - min + 1).Select(value => range.Force(source, value)).ToList();

        // Assert
        Assert.Equal(Enumerable.Range(min, max - min + 1), forced);
        Assert.All(source.Recorded, choice => Assert.Equal((ulong)(max - min), choice.Max));
    }

    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(-1L)]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(long.MaxValue)]
    public void Force_WithTheFullLongRange_ShouldRoundTrip(long value)
    {
        // Arrange
        var range = new IntegerRange<long>(long.MinValue, long.MaxValue);
        var source = ChoiceSource.FromRandom(Xoshiro256StarStar.ForRun(seed: 1, run: 0));

        // Act
        var forced = range.Force(source, value);

        // Assert
        Assert.Equal(value, forced);
    }

    [Fact]
    public void Force_WithAValueOutsideTheRange_ShouldClampIt()
    {
        // Arrange
        var range = new IntegerRange<int>(5, 9);
        var source = ChoiceSource.FromRandom(Xoshiro256StarStar.ForRun(seed: 1, run: 0));

        // Act
        var below = range.Force(source, 1);
        var above = range.Force(source, 100);

        // Assert
        Assert.Equal(5, below);
        Assert.Equal(9, above);
    }

    [Fact]
    public void Force_WhenReplaying_ShouldReturnTheReplayedValue()
    {
        // Arrange
        var range = new IntegerRange<int>(-3, 10);
        var source = ChoiceSource.FromPrefix([new Choice(range.ToChoice(-2), 13)]);

        // Act
        var replayed = range.Force(source, 7);
        var padded = range.Force(source, 7);

        // Assert
        Assert.Equal(-2, replayed);
        Assert.Equal(0, padded);
    }
}
