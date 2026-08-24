using QuickCheck.Choices;

namespace QuickCheck.Generators;

internal sealed class ListGenerator<T> : Generator<List<T>>
{
    private readonly Generator<T> _item;
    private readonly Generator<(bool More, T Item)> _optionalItem;
    private readonly int _minLength;
    private readonly int _maxLength;

    public ListGenerator(Generator<T> item, int minLength, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentOutOfRangeException.ThrowIfNegative(minLength);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, minLength);

        _item = item;
        _minLength = minLength;
        _maxLength = maxLength;

        var continueProbability = CollectionLength.ContinueProbability(minLength, maxLength);

        // Each optional element is guarded by a "more?" choice drawn inside the same span as the
        // element, so the shrinker can delete the pair as one unit, and minimising a guard to false
        // truncates the list.
        _optionalItem = QuickCheck.Generate.From(source => source.NextBoolean(continueProbability)
            ? (true, source.Draw(item))
            : (false, default!));
    }

    protected internal override List<T> Generate(ChoiceSource source)
    {
        var list = new List<T>(_minLength);

        while (list.Count < _minLength)
        {
            list.Add(source.Draw(_item));
        }

        while (list.Count < _maxLength)
        {
            var (more, item) = source.Draw(_optionalItem);

            if (!more)
            {
                break;
            }

            list.Add(item);
        }

        return list;
    }
}
