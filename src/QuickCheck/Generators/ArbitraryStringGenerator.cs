namespace QuickCheck.Generators;

public sealed class ArbitraryStringGenerator : ArbitraryValueGenerator<string>
{
    private readonly int _size;
    private readonly ArbitraryCharGenerator _inner;

    public static readonly ArbitraryStringGenerator Default = new(Random.Shared, 64);

    public ArbitraryStringGenerator(Random random, int size) : base(random)
    {
        _size = size;
        _inner = new ArbitraryCharGenerator(random);
    }

    public override string Generate()
    {
        var length = Random.Shared.Next(0, _size);

        if (length == 0)
        {
            return string.Empty;
        }

        return string.Create(length, _inner, (span, generator) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = generator.Generate();
            }
        });
    }
}