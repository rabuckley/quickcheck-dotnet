using QuickCheck.Running;

namespace QuickCheck.Tests.Running;

public sealed class BetaDistributionTests
{
    // The analytic rows follow from closed forms: I_x(½, ½) = (2/π)·asin(√x),
    // I_x(2, 3) = x²(6 − 8x + 3x²), I_x(a, 1) = x^a and I_x(1, b) = 1 − (1 − x)^b. The rest were
    // checked against scipy.stats.beta.
    [Theory]
    [InlineData(0.5, 0.5, 0.5, 0.5)]
    [InlineData(0.001, 0.5, 0.5, 0.0201350416)]
    [InlineData(0.3, 2, 3, 0.3483)]
    [InlineData(0.25, 0.5, 1, 0.5)]
    [InlineData(0.75, 1, 0.5, 0.5)]
    [InlineData(0.2, 18.5, 82.5, 0.684600)]
    [InlineData(0.5, 40.5, 60.5, 0.977522)]
    [InlineData(0.2, 180.5, 820.5, 0.944504)]
    [InlineData(0.01, 0.5, 100.5, 0.844259)]
    [InlineData(0.5, 500000.5, 500000.5, 0.5)]
    public void Cdf_WithKnownValues_ShouldMatchReferenceValues(double x, double a, double b, double expected)
    {
        // Act
        var probability = BetaDistribution.Cdf(x, a, b);

        // Assert
        Assert.Equal(expected, probability, tolerance: 1e-6);
    }

    [Fact]
    public void Cdf_WithZeroOrOne_ShouldBeExactlyZeroOrOne()
    {
        // Act
        var atZero = BetaDistribution.Cdf(0, 18.5, 82.5);
        var atOne = BetaDistribution.Cdf(1, 18.5, 82.5);

        // Assert
        Assert.Equal(0.0, atZero);
        Assert.Equal(1.0, atOne);
    }

    [Theory]
    [InlineData(0.025, 50.5, 50.5, 0.403174)]
    [InlineData(0.975, 50.5, 50.5, 0.596826)]
    [InlineData(0.025, 3.5, 97.5, 0.008520)]
    [InlineData(0.975, 3.5, 97.5, 0.077888)]
    [InlineData(0.975, 0.5, 100.5, 0.024745)]
    public void Quantile_WithKnownQuantiles_ShouldInvertTheCdf(double probability, double a, double b, double expected)
    {
        // Act
        var x = BetaDistribution.Quantile(probability, a, b);

        // Assert
        Assert.Equal(expected, x, tolerance: 1e-6);
    }
}
