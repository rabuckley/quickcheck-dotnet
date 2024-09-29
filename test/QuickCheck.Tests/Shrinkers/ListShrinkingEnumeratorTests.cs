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

    [Fact]
    public void MethodName_WithWhat_ShouldDoWhat()
    {
        List<List<int>> expected =
        [
            [],
            [2, 3],
            [0, 1],
            [1, 2, 3],
            [0, 2, 3],
            [0, 1, 3],
            [0, 1, 2],
            [0, 0, 2, 3],
            [0, 1, 0, 3],
            [0, 1, 1, 3],
            [0, 1, 2, 0],
            [0, 1, 2, 2],
        ];

        var iterator = ListShrinkingEnumerator.Create<List<int>, int>([0, 1, 2, 3], ArbitraryInt32Generator.Default);

        // Assert

        foreach (var (i, (e, a)) in expected.Zip(iterator).Index())
        {
            Assert.Equal(e, a);
        }
    }
}