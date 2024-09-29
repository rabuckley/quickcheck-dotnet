using System.Diagnostics;
using QuickCheck.Generators.Shrinkers;

namespace QuickCheck.Generators;

public sealed class ArbitraryListGenerator<TList, TItem> : ArbitraryValueGenerator<TList>
    where TList : IList<TItem>, new()
{
    private readonly ArbitraryValueGenerator<TItem> _valueGenerator;

    public ArbitraryListGenerator(
        ArbitraryValueGenerator<TItem> valueGenerator,
        Random random,
        int size) : base(random)
    {
        _valueGenerator = valueGenerator;
        Size = size;
    }

    public int Size { get; }


    public override TList Generate()
    {
        var size = Random.Next(0, Size);
        var list = new TList();

        for (var i = 0; i < size; i++)
        {
            list.Add(_valueGenerator.Generate());
        }

        return list;
    }

    public override IEnumerable<TList> Shrink(TList from)
    {
        return from.Count switch
        {
            < 0 => throw new UnreachableException(),
            0 => [],
            _ => ListShrinkingEnumerator.Create(from, _valueGenerator)
        };
    }
}