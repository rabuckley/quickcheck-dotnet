using QuickCheck.Running;

namespace QuickCheck.Tests.Running;

public sealed class WilsonScoreIntervalTests
{
    [Fact]
    public void Bounds_WithHalfTheTrialsHittingAtNinetyFivePercent_ShouldMatchTheTextbookInterval()
    {
        // Act
        var (lower, upper) = WilsonScoreInterval.Bounds(count: 50, total: 100, z: 1.959964);

        // Assert
        Assert.Equal(0.4038, lower, tolerance: 1e-3);
        Assert.Equal(0.5962, upper, tolerance: 1e-3);
    }

    [Fact]
    public void Bounds_WithNoHitsOrEveryHit_ShouldReachExactlyZeroAndOne()
    {
        // Act
        var (lowerOfNone, _) = WilsonScoreInterval.Bounds(count: 0, total: 100, z: 6.22);
        var (_, upperOfAll) = WilsonScoreInterval.Bounds(count: 100, total: 100, z: 6.22);

        // Assert
        Assert.Equal(0.0, lowerOfNone);
        Assert.Equal(1.0, upperOfAll);
    }
}
