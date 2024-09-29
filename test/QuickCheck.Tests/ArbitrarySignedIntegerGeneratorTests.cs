using System.Runtime.InteropServices;
using QuickCheck.Generators;

namespace QuickCheck.Tests;

public class ArbitrarySignedIntegerGeneratorTests
{
    [Fact]
    public void Create_WithPositiveInt()
    {
        // Arrange
        ReadOnlySpan<int> expected =
        [
            0, 8622, 12933, 15089, 16167, 16706, 16975, 17110, 17177, 17211, 17228, 17236, 17240, 17242, 17243
        ];

        // Act
        var actual = ArbitraryInt32Generator.Default.Shrink(17244).ToList();

        // Assert
        Assert.Equal(expected, CollectionsMarshal.AsSpan(actual));
    }

    [Fact]
    public void Create_WithNegativeInt()
    {
        // Arrange
        ReadOnlySpan<int> expected =
        [
            0, 17244, -8622, -12933, -15089, -16167, -16706, -16975, -17110, -17177, -17211, -17228, -17236, -17240,
            -17242, -17243
        ];

        // Act
        var actual = ArbitraryInt32Generator.Default.Shrink(-17244).ToList();

        // Assert
        Assert.Equal(expected, CollectionsMarshal.AsSpan(actual));
    }

    [Fact]
    public void Create_WithZero_ShouldReturnEmpty()
    {
        // Act
        var actual = ArbitraryInt32Generator.Default.Shrink(0);

        // Assert
        Assert.Empty(actual);
    }
}