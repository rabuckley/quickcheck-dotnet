using QuickCheck.Choices;

namespace QuickCheck.Generators;

/// <summary>
/// Generates sets with the span layout of <see cref="ListGenerator{T}"/>, so they shrink the same
/// way: by deleting elements and truncating. Each element draws up to
/// <see cref="QuickCheck.Generate.MaxFilterAttempts"/> candidates, skipping any already present.
/// </summary>
internal sealed class HashSetGenerator<T> : Generator<HashSet<T>>
{
    private readonly Generator<T> _item;
    private readonly int _minLength;
    private readonly int _maxLength;
    private readonly double _continueProbability;

    public HashSetGenerator(Generator<T> item, int minLength, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentOutOfRangeException.ThrowIfNegative(minLength);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, minLength);

        _item = item;
        _minLength = minLength;
        _maxLength = maxLength;

        _continueProbability = CollectionLength.ContinueProbability(minLength, maxLength);
    }

    protected internal override HashSet<T> Generate(ChoiceSource source)
    {
        // The element generators close over the set under construction, so they are built per
        // call: a generator instance is shared and may be drawn from concurrently.
        var set = new HashSet<T>(_minLength);

        var mandatoryItem = QuickCheck.Generate.From(itemSource =>
        {
            if (TryAddItem(itemSource, set))
            {
                return true;
            }

            throw new DiscardException(
                $"No distinct element was generated after {QuickCheck.Generate.MaxFilterAttempts} attempts.");
        });

        var optionalItem = QuickCheck.Generate.From(itemSource =>
            itemSource.NextBoolean(_continueProbability) && TryAddItem(itemSource, set));

        while (set.Count < _minLength)
        {
            source.Draw(mandatoryItem);
        }

        while (set.Count < _maxLength && source.Draw(optionalItem))
        {
        }

        return set;
    }

    private bool TryAddItem(ChoiceSource source, HashSet<T> set)
    {
        for (var attempt = 0; attempt < QuickCheck.Generate.MaxFilterAttempts; attempt++)
        {
            if (set.Add(source.Draw(_item)))
            {
                return true;
            }
        }

        return false;
    }
}
