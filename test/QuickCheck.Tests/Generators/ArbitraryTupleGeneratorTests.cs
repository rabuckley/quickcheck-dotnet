using QuickCheck.Generators;

namespace QuickCheck.Tests.Generators;

public sealed class ArbitraryTupleValueGeneratorTests
{
    [Fact]
    public void Shrink()
    {
        // Arrange
        var generator = new ArbitraryTupleGenerator<int, int>(
                ArbitraryInt32Generator.Default,
                ArbitraryInt32Generator.Default,
                Random.Shared);

        // Act
        var items = generator.Shrink((3, 3)).ToList();

        // Assert
        Assert.Equal(4, items.Count);
        Assert.Equal((0, 3), items[0]);
        Assert.Equal((2, 3), items[1]);
        Assert.Equal((3, 0), items[2]);
        Assert.Equal((3, 2), items[3]);
    }
}
