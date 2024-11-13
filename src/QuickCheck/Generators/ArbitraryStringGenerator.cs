namespace QuickCheck.Generators;

public sealed class ArbitraryStringGenerator : ArbitraryValueGenerator<string>
{
    private readonly int _size;
    private readonly ArbitraryCharGenerator _inner;

    /// <summary>
    /// Gets a read-only singleton instance of <see cref="ArbitraryStringGenerator"/>
    /// using the default <see cref="Random.Shared"/> pseudo-random number generator,
    /// and with a maximum generated length of 64 characters.
    /// </summary>
    public static readonly ArbitraryStringGenerator Default = new();

    /// <summary>
    /// Creates a new <see cref="ArbitraryStringGenerator"/> using the default <see cref="Random.Shared"/>
    /// pseudo-random number generator, and with a maximum generated length of 64 characters.
    /// </summary>
    public ArbitraryStringGenerator() : base(Random.Shared)
    {
        _size = 64;
        _inner = ArbitraryCharGenerator.Default;
    }

    /// <summary>
    /// Creates a new instance of <see cref="ArbitraryStringGenerator"/> with the specified <paramref name="random"/> instance
    /// using the specified <paramref name="size"/>.
    /// </summary>
    /// <param name="random">The pseudo-random number generator to use.</param>
    /// <param name="size">The maximum length of the generated string.</param>
    public ArbitraryStringGenerator(Random random, int size = 64) : base(random)
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
