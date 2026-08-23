namespace QuickCheck.Tests;

public sealed class GenerateIdentityTests
{
    [Fact]
    public void Guid_WithManySamples_ShouldBeDistinctAndShrinkToEmpty()
    {
        // Arrange
        var generator = Generate.Guid();

        // Act
        var samples = generator.Sample(count: 200, seed: 1);
        var minimal = Property.ForAll(generator, static _ => false).Check(new CheckOptions { Seed = 2 }).Minimal!.Value;

        // Assert
        Assert.Equal(200, samples.Distinct().Count());
        Assert.Equal(Guid.Empty, minimal);
    }
}
