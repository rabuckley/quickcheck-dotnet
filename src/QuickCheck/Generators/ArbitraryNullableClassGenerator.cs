namespace QuickCheck.Generators;

/// <summary>
/// A generator for nullable classes.
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class ArbitraryNullableClassGenerator<T> : ArbitraryValueGenerator<T?>
    where T : class
{
    private readonly IArbitraryValueGenerator<T> _generator;
    private readonly double _nullProbability;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArbitraryNullableClassGenerator{T}"/> class.
    /// </summary>
    /// <param name="generator">A generator for non-nullable instances of <typeparamref name="T"/>.</param>
    /// <param name="random">A pseudo-random number generator.</param>
    /// <param name="nullProbability">The probability of generating a null value.</param>
    public ArbitraryNullableClassGenerator(
        IArbitraryValueGenerator<T> generator,
        Random random,
        double nullProbability = 0.25) : base(random)
    {
        _generator = generator;
        _nullProbability = nullProbability;
    }

    public override T? Generate()
    {
        return Random.NextDouble() < _nullProbability
            ? null
            : _generator.Generate();
    }

    public override IEnumerable<T?> Shrink(T? from)
    {
        if (from is null)
        {
            yield break;
        }

        yield return null;

        foreach (var shrunk in _generator.Shrink(from))
        {
            yield return shrunk;
        }
    }
}
