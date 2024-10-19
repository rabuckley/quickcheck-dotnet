namespace QuickCheck;

public abstract class ArbitraryValueGenerator<T> : IArbitraryValueGenerator<T>
{
    /// <param name="random">The random number generator to use.</param>
    protected ArbitraryValueGenerator(Random random)
    {
        Random = random;
    }

    /// <summary>
    /// The pseudo-random number generator to use when generating values.
    /// </summary>
    protected Random Random { get; }

    /// <summary>
    /// Generates an arbitrary <see cref="T"/>.
    /// </summary>
    public abstract T Generate();

    /// <summary>
    /// Chooses a random <see cref="T"/> from <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The options to choose from.</param>
    public virtual T Choose(ReadOnlySpan<T> options)
    {
        return Random.GetItems(options, 1)[0];
    }

    /// <summary>
    /// <para>
    /// Shrinks the value <paramref name="from"/> to a smaller value.
    /// </para>
    /// <para>
    /// If a value cannot be shrunk, an empty enumerable should be returned.
    /// </para>
    /// </summary>
    /// <param name="from">The value to shrink from.</param>
    /// <returns>
    /// An enumerable of values smaller than <paramref name="from"/>.
    /// </returns>
    public virtual IEnumerable<T> Shrink(T from)
    {
        return [];
    }
}
