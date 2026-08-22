using QuickCheck.Running;

namespace QuickCheck.Tests.Running;

public sealed class NormalDistributionTests
{
    [Theory]
    [InlineData(0.5, 0)]
    [InlineData(0.975, 1.959964)]
    [InlineData(0.025, -1.959964)]
    [InlineData(0.5e-9, -6.1094)]
    [InlineData(0.25e-9, -6.2191)]
    public void InverseCdf_WithKnownQuantiles_ShouldMatchReferenceValues(double probability, double expected)
    {
        // Act
        var z = NormalDistribution.InverseCdf(probability);

        // Assert
        Assert.Equal(expected, z, tolerance: 1e-4);
    }
}
