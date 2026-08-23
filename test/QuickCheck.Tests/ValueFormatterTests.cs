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
