using QuickCheck.Choices;

namespace QuickCheck.Generators;

internal sealed class ElementsGenerator<T> : Generator<T>
{
    private readonly T[] _items;

    public ElementsGenerator(T[] items)
    {
        _items = items;
    }

    protected internal override T Generate(ChoiceSource source) => _items[source.NextChoice((ulong)_items.Length - 1)];
}
