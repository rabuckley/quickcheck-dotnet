using QuickCheck.Generators;
using QuickCheck.Generators.Shrinkers;

namespace QuickCheck.Tests.Shrinkers;

public sealed class ListShrinkingEnumeratorTests
{
    [Fact]
    public void Shrink_With4()
    {
        // Arrange
        List<List<int>> expected = [[], [0], [2], [3]];
        var iterator = ListShrinkingEnumerator.Create<List<int>, int>([4], ArbitraryInt32Generator.Default);

        // Act
        var actual = iterator.ToList();

        // Assert
        Assert.Equal(expected, actual);
    }
}
