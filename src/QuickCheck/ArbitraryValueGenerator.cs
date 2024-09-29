namespace QuickCheck;

public abstract class ArbitraryValueGenerator<T> : IArbitraryValueGenerator<T>
{
    /// <param name="random">The random number generator to use.</param>
    protected ArbitraryValueGenerator(Random random)
    {
        Random = random;
    }

    protected Random Random { get; }

    /// <summary>
    /// Generates an arbitrary <see cref="T"/>.
    /// </summary>
    public abstract T Generate();

    /// <summary>
    /// Chooses a random <see cref="T"/> from <paramref name="options"/>.
    /// </summary>
    /// <param name="options"></param>
    public virtual T Choose(ReadOnlySpan<T> options)
    {
        return Random.GetItems(options, 1)[0];
    }

    public virtual IEnumerable<T> Shrink(T from)
    {
        return [];
    }
}

public abstract class ArbitraryMultipleValueGenerator<T, TCollection> : ArbitraryValueGenerator<TCollection>
    where TCollection : IEnumerable<T>
{
    protected ArbitraryMultipleValueGenerator(Random random) : base(random)
    {
    }
}