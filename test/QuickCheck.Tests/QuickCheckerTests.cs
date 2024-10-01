namespace QuickCheck.Tests;

public sealed class QuickCheckerTests
{
    [Fact]
    public void Run_WithOneArg_ShouldReduce()
    {
        // Arrange
        var checker = new QuickChecker();

        // Act
        var result = checker.Run<int, int>(Add, _ => true);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal(2, result.Input);
        
        return;
        
        static int Add(int a)
        {
            if (a != 0 && a % 2 == 0)
            {
                throw new InvalidOperationException("This method fails when all are even");
            }

            return a;
        }
    }

    [Fact]
    public void Run_WithTwoArgs_ShouldReduce()
    {
        // Arrange
        var checker = new QuickChecker();

        // Act
        var result = checker.Run<int, int, int>(TestFunc, _ => true);

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
                throw new InvalidOperationException("This method fails when all are even");
            }

            return a + b;
        }
    }


    [Fact]
    public void Run_WithThreeArgs_ShouldReduce()
    {
        // Arrange
        var checker = new QuickChecker();

        // Act
        var result = checker.Run<int, int, int, int>(TestFunc, _ => true);

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
            if (a != 0 && a % 2 == 0 && b != 0 && b % 2 == 0 && c != 0 && c % 2 == 0)
            {
                throw new InvalidOperationException("This method fails when all are even");
            }

            return a + b;
        }
    }
}