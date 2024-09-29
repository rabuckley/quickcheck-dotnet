namespace QuickCheck.Generators;

public class ArbitraryBooleanGenerator : ArbitraryValueGenerator<bool>
{
    public static readonly ArbitraryBooleanGenerator Default = new();

    public ArbitraryBooleanGenerator() : base(Random.Shared)
    {
    }

    public ArbitraryBooleanGenerator(Random random) : base(random)
    {
    }

    public override bool Generate()
    {
        return Random.NextDouble() >= 0.5;
    }

    public override IEnumerable<bool> Shrink(bool from)
    {
        if (from)
        {
            return [false];
        }

        return [];
    }
}