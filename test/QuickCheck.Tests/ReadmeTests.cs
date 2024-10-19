using QuickCheck.Generators;

namespace QuickCheck.Tests;

public sealed class ReadmeTests
{
    // This file tests all code samples in the README.md file. Any changes to the README.md file must be reflected here.

    [Fact]
    public void ReverseReverse()
    {
        static Memory<T> Reverse<T>(Memory<T> memory)
        {
            var span = memory.Span;
            var reversed = new T[span.Length];

            for (var i = 0; i < span.Length; i++)
            {
                reversed[span.Length - i - 1] = span[i];
            }

            return new Memory<T>(reversed);
        }

        var qc = QuickChecker.CreateDefault();

        var generator = new ArbitraryMemoryGenerator<int>(
            ArbitraryInt32Generator.Default,
            Random.Shared,
            maximumSize: 32);

        qc.AddGenerator(generator);

        var result = qc.Run(
            target: static (Memory<int> memory) => Reverse(Reverse(memory)).Span.SequenceEqual(memory.Span),
            validate: static (result) => result);

        Assert.False(result.IsError);
    }

    [Fact]
    public void CreateDefaultQuickChecker()
    {
        // Using the default options
        var qc = QuickChecker.CreateDefault();

        // Using custom options
        var configuredQc = QuickChecker.CreateDefault(options =>
        {
            options.RunCount = 1000;
            options.Random = new Random(42);
        });
    }

    [Fact]
    public void CreateEmptyQuickChecker()
    {
        // Using the default options
        var qc = QuickChecker.CreateEmpty();

        // Using custom options
        var configuredQc = QuickChecker.CreateEmpty(options =>
        {
            options.RunCount = 1000;
            options.Random = new Random(42);
        });
    }

    [Fact]
    public void AddGenerator()
    {
        var qc = QuickChecker.CreateEmpty();
        qc.AddGenerator(ArbitraryInt32Generator.Default);
    }

    [Fact]
    public void RunATest()
    {
        static int NotSoSafeDivide(int a, int b) => a / b;

        var qc = QuickChecker.CreateDefault();

        var result = qc.Run(static (int a, int b) => NotSoSafeDivide(a, b));

        Assert.True(result.IsError);
        Assert.True(result.Exception is DivideByZeroException);
    }
}
