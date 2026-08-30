using QuickCheck.Running;

namespace QuickCheck.Tests.Running;

public sealed class JeffreysIntervalTests
{
    [Fact]
    public void Bounds_WithEighteenOfAHundred_ShouldMatchTheTextbookInterval()
    {
        // Act
        var (lower, upper) = JeffreysInterval.Bounds(count: 18, total: 100);

        // Assert
        Assert.Equal(0.1144, lower, tolerance: 1e-3);
        Assert.Equal(0.2638, upper, tolerance: 1e-3);
    }

    [Fact]
    public void Bounds_WithNoHitsOrEveryHit_ShouldReachExactlyZeroAndOne()
    {
        // Act
        var (lowerOfNone, upperOfNone) = JeffreysInterval.Bounds(count: 0, total: 100);
        var (_, upperOfAll) = JeffreysInterval.Bounds(count: 100, total: 100);

        // Assert
        Assert.Equal(0.0, lowerOfNone);
        Assert.Equal(0.0247, upperOfNone, tolerance: 1e-3);
        Assert.Equal(1.0, upperOfAll);
    }
}
