using QuickCheck.Generators;

namespace QuickCheck.Tests.Generators;

public sealed class ArbitraryNullableStructGeneratorTests
{
    private struct TestStruct
    {
        public int Value { get; init; }
    }

    private sealed class TestClassGenerator : ArbitraryValueGenerator<TestStruct>
    {
        private readonly ArbitraryInt32Generator _inner;

        public TestClassGenerator(Random random) : base(random)
        {
            _inner = ArbitraryInt32Generator.Default;
        }

        public override TestStruct Generate()
        {
            return new TestStruct { Value = _inner.Generate() };
        }

        public override IEnumerable<TestStruct> Shrink(TestStruct from)
        {
            foreach (var shrunk in _inner.Shrink(from.Value))
            {
                yield return new TestStruct { Value = shrunk };
            }
        }
    }

    [Fact]
    public void Shrink_ShouldReturnNullValueFirst()
    {
        // Arrange
        var random = new Random(42);

        var generator = new ArbitraryNullableStructGenerator<TestStruct>(
            new TestClassGenerator(random),
            random);

        var initialValue = new TestStruct { Value = 42 };

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

        var generator = new ArbitraryNullableStructGenerator<TestStruct>(
            new TestClassGenerator(random),
            random);

        TestStruct? initialValue = null;

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

        var generator = new ArbitraryNullableStructGenerator<TestStruct>(
            new TestClassGenerator(random),
            random);

        var inner = new ArbitraryInt32Generator(random);

        const int initialValue = 42;

        // Act
        var shrinkResult =
            generator.Shrink(new TestStruct { Value = initialValue }).ToList();

        // Assert
        Assert.Null(shrinkResult.First());

        foreach (var (expected, actual) in inner.Shrink(initialValue)
                     .Zip(shrinkResult.Skip(1)))
        {
            Assert.Equal(expected, actual?.Value);
        }
    }
}
