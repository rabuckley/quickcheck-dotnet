using QuickCheck.Running;

namespace QuickCheck.Tests.Running;

public sealed class CoverageLookTests
{
    [Theory]
    [InlineData(100, 100, true)]
    [InlineData(150, 100, false)]
    [InlineData(200, 100, true)]
    [InlineData(300, 100, false)]
    [InlineData(400, 100, true)]
    [InlineData(150, 150, true)]
    [InlineData(1, 1, true)]
    [InlineData(50, 100, false)]
    public void IsDue_WithPassCount_ShouldBeTrueAtRunCountAndAtHundredTimesAPowerOfTwo(int passed, int runCount, bool expected)
    {
        // Act
        var isDue = CoverageLook.IsDue(passed, runCount);

        // Assert
        Assert.Equal(expected, isDue);
    }

    [Fact]
    public void Verdict_WithNoHitsAtTheFirstLook_ShouldRejectAHalfButNotYetATenth()
    {
        // Arrange
        var look = new CoverageLook(Confidence.Default, passed: 100, look: 0);

        // Act
        var half = look.Verdict(minimumPercent: 50, count: 0);
        var tenth = look.Verdict(minimumPercent: 10, count: 0);

        // Assert
        Assert.Equal(CoverageVerdict.Unmet, half);
        Assert.Equal(CoverageVerdict.Undecided, tenth);
    }

    [Fact]
    public void Verdict_WithRateKnownToLieInsideTheToleranceBand_ShouldBeMet()
    {
        // Arrange: after 51,200 examples at 47% the interval for a 50% minimum lies wholly inside
        // 45% to 50%, so the rate is known short of the minimum and known within tolerance.
        var look = new CoverageLook(Confidence.Default, passed: 51_200, look: 9);

        // Act
        var verdict = look.Verdict(minimumPercent: 50, count: 24_064);

        // Assert
        Assert.Equal(CoverageVerdict.Met, verdict);
    }
}
