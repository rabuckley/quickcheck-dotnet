using QuickCheck.Choices;

namespace QuickCheck.Tests.Choices;

public sealed class ChoiceSourceTests
{
    [Fact]
    public void ForceChoice_WhenGenerating_ShouldRecordTheForcedValue()
    {
        // Arrange
        var source = ChoiceSource.FromRandom(Xoshiro256StarStar.ForRun(seed: 1, run: 0));

        // Act
        var forced = source.ForceChoice(7, 9);
        var drawn = source.NextChoice(3);

        // Assert
        Assert.Equal(7UL, forced);
        Assert.Equal([new Choice(7, 9), new Choice(drawn, 3)], source.Recorded);
    }

    [Fact]
    public void ForceChoice_WhenReplaying_ShouldReplayTheClampedPrefixThenPadWithZero()
    {
        // Arrange
        var source = ChoiceSource.FromPrefix([new Choice(5, 9), new Choice(12, 20)]);

        // Act
        var replayed = source.ForceChoice(7, 9);
        var clamped = source.ForceChoice(2, 9);
        var padded = source.ForceChoice(4, 9);

        // Assert
        Assert.Equal(5UL, replayed);
        Assert.Equal(9UL, clamped);
        Assert.Equal(0UL, padded);
        Assert.Equal([new Choice(5, 9), new Choice(9, 9), new Choice(0, 9)], source.Recorded);
    }

    [Fact]
    public void Draw_WithAWrapperAroundOneInnerDraw_ShouldRecordOneSpanForBoth()
    {
        // Arrange
        // Each member is one choice wrapped in a Select, which closes on the same bounds as the
        // choice it wraps; the pair around both members is the only span that adds structure.
        var member = Generate.Between(0, 10).Select(static x => x + 1);
        var pair = Generate.Tuple(member, member);
        var source = ChoiceSource.FromRandom(Xoshiro256StarStar.ForRun(seed: 1, run: 0));

        // Act
        source.Draw(pair);

        // Assert
        Assert.Equal(2, source.Recorded.Count);
        Assert.Equal([new ChoiceSpan(0, 1), new ChoiceSpan(1, 2), new ChoiceSpan(0, 2)], source.Spans);
    }

    [Fact]
    public void SampleEdge_ShouldRollOnlyWhenGeneratingAndConsumeNoChoice()
    {
        // Arrange
        var generating = ChoiceSource.FromRandom(Xoshiro256StarStar.ForRun(seed: 1, run: 0));
        var replaying = ChoiceSource.FromPrefix([new Choice(1, 1)]);

        // Act
        var edges = Enumerable.Range(0, 2000).Select(_ => generating.SampleEdge([10, 20, 30])).ToList();
        var replayed = replaying.SampleEdge([10, 20, 30]);

        // Assert
        Assert.Empty(generating.Recorded);
        Assert.InRange(edges.Count(static e => e is not null), 60, 200);
        Assert.Equal([10, 20, 30], edges.Where(static e => e is not null).Distinct().Order());
        Assert.Null(replayed);
    }
}
