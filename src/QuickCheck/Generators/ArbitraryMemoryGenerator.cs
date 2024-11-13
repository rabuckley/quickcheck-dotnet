using QuickCheck.Generators.Shrinkers;

namespace QuickCheck.Generators;

public class ArbitraryMemoryGenerator<T> : ArbitraryValueGenerator<Memory<T>>
{
    private readonly ArbitraryValueGenerator<T> _valueGenerator;
    private readonly int _maximumSize;

    public ArbitraryMemoryGenerator(ArbitraryValueGenerator<T> valueGenerator, Random random, int maximumSize) :
        base(random)
    {
        _valueGenerator = valueGenerator;
        _maximumSize = maximumSize;
    }

    public override Memory<T> Generate()
    {
        var length = Random.Next(0, _maximumSize);
        var array = new T[length];

        for (var i = 0; i < length; i++)
        {
            array[i] = _valueGenerator.Generate();
        }

        return new Memory<T>(array);
    }

    public override IEnumerable<Memory<T>> Shrink(Memory<T> from)
    {
        return from.IsEmpty switch
        {
            true => [],
            _ => MemoryShrinkingEnumerator.Create(from, _valueGenerator)
        };
    }
}