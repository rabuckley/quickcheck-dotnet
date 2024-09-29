using QuickCheck.Generators.Shrinkers;

namespace QuickCheck.Generators;

public class ArbitraryMemoryGenerator<T> : ArbitraryValueGenerator<Memory<T>>
{
    private readonly ArbitraryValueGenerator<T> _valueGenerator;
    private readonly int _size;

    public ArbitraryMemoryGenerator(ArbitraryValueGenerator<T> valueGenerator, Random random, int size) :
        base(random)
    {
        _valueGenerator = valueGenerator;
        _size = size;
    }

    public override Memory<T> Generate()
    {
        var length = Random.Next(0, _size);
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