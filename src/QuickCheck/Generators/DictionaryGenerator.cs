using QuickCheck.Choices;

namespace QuickCheck.Generators;

/// <summary>
/// Generates dictionaries with the span layout of <see cref="ListGenerator{T}"/>, so they shrink
/// the same way: by deleting entries and truncating. Each entry draws up to
/// <see cref="QuickCheck.Generate.MaxFilterAttempts"/> keys, skipping any that is null or already
/// present.
/// </summary>
internal sealed class DictionaryGenerator<TKey, TValue> : Generator<Dictionary<TKey, TValue>> where TKey : notnull
{
    private readonly Generator<TKey> _keys;
    private readonly Generator<TValue> _values;
    private readonly int _minLength;
    private readonly int _maxLength;
    private readonly double _continueProbability;

    public DictionaryGenerator(Generator<TKey> keys, Generator<TValue> values, int minLength, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfNegative(minLength);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, minLength);

        _keys = keys;
        _values = values;
        _minLength = minLength;
        _maxLength = maxLength;

        _continueProbability = CollectionLength.ContinueProbability(minLength, maxLength);
    }

    protected internal override Dictionary<TKey, TValue> Generate(ChoiceSource source)
    {
        // The entry generators close over the dictionary under construction, so they are built
        // per call: a generator instance is shared and may be drawn from concurrently.
        var dictionary = new Dictionary<TKey, TValue>(_minLength);

        var mandatoryEntry = QuickCheck.Generate.From(entrySource =>
        {
            if (TryAddEntry(entrySource, dictionary))
            {
                return true;
            }

            throw new DiscardException(
                $"No distinct key was generated after {QuickCheck.Generate.MaxFilterAttempts} attempts.");
        });

        var optionalEntry = QuickCheck.Generate.From(entrySource =>
            entrySource.NextBoolean(_continueProbability) && TryAddEntry(entrySource, dictionary));

        while (dictionary.Count < _minLength)
        {
            source.Draw(mandatoryEntry);
        }

        while (dictionary.Count < _maxLength && source.Draw(optionalEntry))
        {
        }

        return dictionary;
    }

    private bool TryAddEntry(ChoiceSource source, Dictionary<TKey, TValue> dictionary)
    {
        for (var attempt = 0; attempt < QuickCheck.Generate.MaxFilterAttempts; attempt++)
        {
            var key = source.Draw(_keys);

            // notnull is a compile-time warning, not a runtime check, so a key can still be null.
            if (key is not null && !dictionary.ContainsKey(key))
            {
                dictionary.Add(key, source.Draw(_values));
                return true;
            }
        }

        return false;
    }
}
