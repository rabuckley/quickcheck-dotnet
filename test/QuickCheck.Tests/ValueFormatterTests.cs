namespace QuickCheck.Tests;

public sealed class ValueFormatterTests
{
    private sealed record Order(string Id, List<int> Quantities, Order? Parent);

    private sealed record Custom(int X)
    {
        public override string ToString() => "custom";
    }

    private readonly record struct Point(int X, int Y);

    private sealed record Empty;

    private sealed record Shipment(int Weight)
    {
        public string Carrier = "post";
        public bool Fragile;
    }

    private abstract record Shape(string Name);

    private sealed record Circle(int Radius, string Name) : Shape(Name);

    [Fact]
    public void Format_WithRecords_ShouldExpandTheirMembersAndFormatNestedValues()
    {
        // Arrange
        var order = new Order("a\"b", [1, 2], new Order("", [], null));

        // Act
        var formatted = ValueFormatter.Format(order);

        // Assert
        Assert.Equal(
            "Order { Id = \"a\\\"b\", Quantities = [1, 2], Parent = Order { Id = \"\", Quantities = [], Parent = null } }",
            formatted);
        Assert.Equal("Point { X = 1, Y = -2 }", ValueFormatter.Format(new Point(1, -2)));
        Assert.Equal("Empty { }", ValueFormatter.Format(new Empty()));
        Assert.Equal("Circle { Name = \"c\", Radius = 3 }", ValueFormatter.Format(new Circle(3, "c")));
    }

    [Fact]
    public void Format_WithPublicFields_ShouldExpandThemAlongsideProperties()
    {
        // Arrange
        var shipment = new Shipment(2) { Fragile = true };

        // Act
        var formatted = ValueFormatter.Format(shipment);

        // Assert
        Assert.Equal("Shipment { Weight = 2, Carrier = \"post\", Fragile = true }", formatted);
    }

    [Fact]
    public void Format_WithDateAndTimeValues_ShouldPrintIsoFormWithTicks()
    {
        // Arrange
        var midnight = new DateTime(2000, 1, 1);

        // Act & Assert
        Assert.Equal("2000-01-01T00:00:00", ValueFormatter.Format(midnight));
        Assert.Equal("2000-01-01T00:00:00.0000001", ValueFormatter.Format(midnight.AddTicks(1)));
        Assert.Equal("2000-01-01T00:00:00Z", ValueFormatter.Format(DateTime.SpecifyKind(midnight, DateTimeKind.Utc)));
        Assert.Equal("2000-01-01T00:00:00+00:00", ValueFormatter.Format(new DateTimeOffset(midnight, TimeSpan.Zero)));
        Assert.Equal("2000-01-01T00:00:00.5-05:30", ValueFormatter.Format(new DateTimeOffset(midnight.AddMilliseconds(500), new TimeSpan(-5, -30, 0))));
        Assert.Equal("2000-01-01", ValueFormatter.Format(new DateOnly(2000, 1, 1)));
        Assert.Equal("13:47:22.5", ValueFormatter.Format(new TimeOnly(13, 47, 22, 500)));
        Assert.Equal("00:00:00", ValueFormatter.Format(TimeOnly.MinValue));
        Assert.Equal("00:00:00.0000001", ValueFormatter.Format(TimeSpan.FromTicks(1)));
        Assert.Equal("00000000-0000-0000-0000-000000000000", ValueFormatter.Format(Guid.Empty));
    }

    [Fact]
    public void Format_WithFloatingPointValues_ShouldPrintTheShortestRoundTripFormAndSpecialValues()
    {
        // Act & Assert
        Assert.Equal("0.1", ValueFormatter.Format(0.1));
        Assert.Equal("5E-324", ValueFormatter.Format(double.Epsilon));
        Assert.Equal("-0", ValueFormatter.Format(-0.0));
        Assert.Equal("NaN", ValueFormatter.Format(float.NaN));
        Assert.Equal("Infinity", ValueFormatter.Format(Half.PositiveInfinity));
        Assert.Equal("-Infinity", ValueFormatter.Format(double.NegativeInfinity));
        Assert.Equal("1.00", ValueFormatter.Format(1.00m));
        Assert.Equal("0.0000000000000000000000000001", ValueFormatter.Format(new decimal(lo: 1, mid: 0, hi: 0, isNegative: false, scale: 28)));
    }

    [Fact]
    public void Format_WithKeyValuePairsAndDictionaries_ShouldFormatKeysAndValues()
    {
        // Arrange
        // The multi-entry case uses a pair sequence rather than a dictionary: both format through
        // the ordinary collection path, and Dictionary does not guarantee enumeration order.
        var pairs = new KeyValuePair<string, int>[] { new("a", 1), new("b", 2) };
        var dictionary = new Dictionary<string, int> { ["a"] = 1 };
        var nested = new KeyValuePair<char, List<int>>('a', [1, 2]);

        // Act & Assert
        Assert.Equal("\"a\": 1", ValueFormatter.Format(new KeyValuePair<string, int>("a", 1)));
        Assert.Equal("[\"a\": 1, \"b\": 2]", ValueFormatter.Format(pairs));
        Assert.Equal("[\"a\": 1]", ValueFormatter.Format(dictionary));
        Assert.Equal("'a': [1, 2]", ValueFormatter.Format(nested));
    }

    [Fact]
    public void Format_WithHandWrittenToString_ShouldUseIt()
    {
        // Arrange
        var custom = new Custom(1);

        // Act
        var formatted = ValueFormatter.Format(custom);

        // Assert
        Assert.Equal("custom", formatted);
    }
}
