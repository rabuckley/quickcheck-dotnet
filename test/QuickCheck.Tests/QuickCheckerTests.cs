using QuickCheck.Generators;

namespace QuickCheck.Tests;

public sealed class QuickCheckerTests
{
    [Fact]
    public void Quickstart_Sample()
    {
        var qc = QuickChecker.CreateDefault();

        var generator = new ArbitraryListGenerator<List<int>, int>(
            ArbitraryInt32Generator.Default, Random.Shared, size: 20);

        qc.AddGenerator(generator);

        var result = qc.Run(
            target: static (List<int> list) => ((IEnumerable<int>)list).Reverse().Reverse().SequenceEqual(list),
            validate: static (result) => result);

        Assert.False(result.IsError);
    }

    [Fact]
    public void Run_FindsEvenNumberErrorCase()
    {
        // Arrange
        var checker = QuickChecker.CreateDefault();

        // Act
        var result = checker.Run(static (int a) => Add(a));

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(0, result.Input % 2);

        return;

        static int Add(int a)
        {
            if (a != 0 && a % 2 == 0)
            {
                throw new InvalidOperationException(
                    "This method fails when all are even");
            }

            return a;
        }
    }

    [Fact]
    public void Run_WithTwoArgs_ShouldReduce()
    {
        // Arrange
        var checker = QuickChecker.CreateDefault();

        // Act
        var result = checker.Run((int a, int b) => TestFunc(a, b));

        // Assert
        Assert.True(result.IsError);

        // Depending on the seed, we may or may not reduce to the optimal input of (2, 2)
        // but it would be wrong to have found anything indivisible by 2.
        Assert.Equal(0, result.Input.Item1 % 2);
        Assert.Equal(0, result.Input.Item2 % 2);

        return;

        static int TestFunc(int a, int b)
        {
            if (a != 0 && a % 2 == 0 && b != 0 && b % 2 == 0)
            {
                throw new InvalidOperationException(
                    "This method fails when all are even");
            }

            return a + b;
        }
    }


    [Fact]
    public void Run_WithThreeArgs_ShouldReduce()
    {
        // Arrange
        var checker = QuickChecker.CreateDefault();

        // Act
        var result = checker.Run((int a, int b, int c) => TestFunc(a, b, c));

        // Assert
        Assert.True(result.IsError);

        // Depending on the seed, we may or may not reduce to the optimal input of (2, 2, 2)
        // but it would be wrong to have found anything indivisible by 2.
        Assert.Equal(0, result.Input.Item1 % 2);
        Assert.Equal(0, result.Input.Item2 % 2);
        Assert.Equal(0, result.Input.Item3 % 2);

        return;

        static int TestFunc(int a, int b, int c)
        {
            if (a != 0
                && a % 2 == 0
                && b != 0
                && b % 2 == 0
                && c != 0
                && c % 2 == 0)
            {
                throw new InvalidOperationException(
                    "This method fails when all are even");
            }

            return a + b;
        }
    }
}
