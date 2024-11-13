namespace QuickCheck.Generators;

/// <summary>
/// A generator for nullable structs.
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class ArbitraryNullableStructGenerator<T> : ArbitraryValueGenerator<T?>
    where T : struct
{
    private readonly IArbitraryValueGenerator<T> _generator;
    private readonly double _nullProbability;

    public ArbitraryNullableStructGenerator(
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

        foreach (var shrunk in _generator.Shrink(from.Value))
        {
            yield return shrunk;
        }
    }
}
