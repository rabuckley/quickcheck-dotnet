namespace QuickCheck.Generators;

public sealed class ArbitraryCharGenerator : ArbitraryValueGenerator<char>
{
    public static readonly ArbitraryCharGenerator Default = new();

    public ArbitraryCharGenerator() : base(Random.Shared)
    {
    }

    public ArbitraryCharGenerator(Random random) : base(random)
    {
    }

    public override char Generate()
    {
        return (char)Random.Next(char.MinValue, char.MaxValue);
    }
}