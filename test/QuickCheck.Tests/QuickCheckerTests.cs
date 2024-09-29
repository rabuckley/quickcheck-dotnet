namespace QuickCheck.Tests;

public sealed class QuickCheckerTests
{
    [Fact]
    public void TestCase1_Arg1()
    {
        // Arrange
        static int Add(int a, int b)
        {
            if (a % 12 == 0)
            {
                throw new InvalidOperationException("This method can't add a number divisible by 12!");
            }

            return a + b;
        }

        var checker = new QuickChecker();

        // Act
        // checker.Run(Add);

        // Assert
        Assert.True(false);
    }
}