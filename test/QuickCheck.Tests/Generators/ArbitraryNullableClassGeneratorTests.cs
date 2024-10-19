using QuickCheck.Generators;

namespace QuickCheck.Tests.Generators;

public sealed class ArbitraryNullableClassGeneratorTests
{
    private class TestClass
    {
        public int Value { get; init; }
    }

    private sealed class TestClassGenerator : ArbitraryValueGenerator<TestClass>
    {
        private readonly ArbitraryInt32Generator _inner;

        public TestClassGenerator(Random random) : base(random)
        {
            _inner = ArbitraryInt32Generator.Default;
        }

        public override TestClass Generate()
        {
            return new TestClass { Value = _inner.Generate() };
        }

        public override IEnumerable<TestClass> Shrink(TestClass from)
        {
            foreach (var shrunk in _inner.Shrink(from.Value))
            {
                yield return new TestClass { Value = shrunk };
            }
        }
    }

    [Fact]
    public void Shrink_ShouldReturnNullValueFirst()
    {
        // Arrange
        var random = new Random(42);

        var generator = new ArbitraryNullableClassGenerator<TestClass>(
            new TestClassGenerator(random),
            random);

        var initialValue = new TestClass { Value = 42 };

        // Act
        var shrinkResult = generator.Shrink(initialValue);

        // Assert
        Assert.Null(shrinkResult.First());
    }

    [Fact]
    public void Shrink_WithNullValue_ShouldReturnNoItems()
    {
        // Arrange
        var random = new Random(42);

        var generator = new ArbitraryNullableClassGenerator<TestClass>(
            new TestClassGenerator(random),
            random);

        TestClass? initialValue = null;

        // Act
        var shrinkResult = generator.Shrink(initialValue);

        // Assert
        Assert.Empty(shrinkResult);
    }

    [Fact]
    public void Shrink_WithNonNullResult_ShouldReturnInnerItemsInTurn()
    {
        // Arrange
        var random = new Random(42);

        var generator = new ArbitraryNullableClassGenerator<TestClass>(
            new TestClassGenerator(random),
            random);

        var inner = new ArbitraryInt32Generator(random);

        const int initialValue = 42;

        // Act
        var shrinkResult =
            generator.Shrink(new TestClass { Value = initialValue }).ToList();

        // Assert
        Assert.Null(shrinkResult.First());

        foreach (var (expected, actual) in inner.Shrink(initialValue)
                     .Zip(shrinkResult.Skip(1)))
        {
            Assert.Equal(expected, actual?.Value);
        }
    }
}
