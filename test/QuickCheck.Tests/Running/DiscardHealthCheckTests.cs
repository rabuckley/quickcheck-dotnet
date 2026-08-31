using QuickCheck.Running;

namespace QuickCheck.Tests.Running;

public sealed class DiscardHealthCheckTests
{
    // Posterior values computed independently of this implementation, at the default
    // MaxDiscardRatio's threshold 1 / 11.
    [Theory]
    [InlineData(0, 20, 0.951)]
    [InlineData(0, 30, 0.984)]
    [InlineData(0, 50, 0.998)]
    [InlineData(2, 50, 0.918)]
    [InlineData(5, 50, 0.474)]
    [InlineData(9, 50, 0.058)]
    public void AcceptanceBelowThreshold_WithResearchTableCounts_ShouldMatchTheResearchNumbers(
        int passed, int discards, double expected)
    {
        // Act
        var probability = DiscardHealthCheck.AcceptanceBelowThreshold(passed, discards, threshold: 1.0 / 11);

        // Assert
        Assert.Equal(expected, probability, tolerance: 1e-3);
    }

    [Fact]
    public void ShouldGiveUp_WithHopelessAndMerelyHeavyFilters_ShouldSeparateThem()
    {
        // Act & Assert: with nothing passing, the default ratio fires at exactly 80 discards.
        Assert.True(DiscardHealthCheck.ShouldGiveUp(passed: 0, discards: 80, maxDiscardRatio: 10));
        Assert.False(DiscardHealthCheck.ShouldGiveUp(passed: 0, discards: 79, maxDiscardRatio: 10));

        // A heavy but survivable filter is left to run.
        Assert.False(DiscardHealthCheck.ShouldGiveUp(passed: 9, discards: 50, maxDiscardRatio: 10));

        // The tightest ratio still fires within its own hard budget of 11.
        Assert.True(DiscardHealthCheck.ShouldGiveUp(passed: 0, discards: 11, maxDiscardRatio: 1));
    }

    [Fact]
    public void ShouldGiveUp_WithTheLargestRatio_ShouldTolerateAnyRate()
    {
        // Act & Assert: a budget this large tolerates every rate, so nothing is hopeless under it.
        Assert.False(DiscardHealthCheck.ShouldGiveUp(passed: 0, discards: 10_000, maxDiscardRatio: int.MaxValue));
    }
}
