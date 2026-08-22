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
