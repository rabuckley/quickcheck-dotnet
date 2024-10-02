using QuickCheck.Generators;
using QuickCheck.Generators.Shrinkers;

namespace QuickCheck.Tests.Shrinkers;

public sealed class MemoryShrinkingEnumeratorTests
{
    [Fact]
    public void Shrink_With4()
    {
        // Arrange
        int[][] data = [[], [0], [2], [3]];
        var expected = data.Select(static arr => new Memory<int>(arr)).ToArray();

        var iterator = MemoryShrinkingEnumerator.Create(new Memory<int>([4]), ArbitraryInt32Generator.Default);

        // Act
        var actual = iterator.ToArray();

        // Assert
        AssertEqualMemories(expected, actual);
    }

    [Fact]
    public void Shrink_With1_2()
    {
        // Arrange
        int[][] data =
        [
            [],
            [2],
            [1],
            [0, 2],
            [1, 0],
            [1, 1],
        ];

        var expected = data.Select(static arr => new Memory<int>(arr)).ToArray();
        var iterator = MemoryShrinkingEnumerator.Create(new Memory<int>([1, 2]), ArbitraryInt32Generator.Default);

        // Act
        var actual = iterator.ToArray();

        // Assert
        AssertEqualMemories(expected, actual);
    }

    private static void AssertEqualMemories<T>(Memory<T>[] expected, Memory<T>[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);

        foreach (var (e, a) in expected.Zip(actual))
        {
            if (e.Length == 0 && a.Length == 0)
            {
                // Assert.Equal can return false for empty M<T> equality. Override.
                continue;
            }

            Assert.True(a.Span.SequenceEqual(e.Span));
        }
    }
}
