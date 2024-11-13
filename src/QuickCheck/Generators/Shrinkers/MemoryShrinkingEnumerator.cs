using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace QuickCheck.Generators.Shrinkers;

/// <summary>
/// A static class for creating instances of <see cref="MemoryShrinkingEnumerator{TItem}"/>.
/// </summary>
public static class MemoryShrinkingEnumerator
{
    public static IEnumerable<Memory<TItem>> Create<TItem>(
        Memory<TItem> from,
        IArbitraryValueGenerator<TItem> valueGenerator)
    {
        if (from.Length == 0)
        {
            return [];
        }

        var size = from.Length;

        return new MemoryShrinkingEnumerator<TItem>(from, size: size,
            offset: size, valueGenerator);
    }
}

public sealed class
    MemoryShrinkingEnumerator<TItem> : IEnumerable<Memory<TItem>>,
    IEnumerator<Memory<TItem>>
{
    private Memory<TItem> _current;
    private readonly Memory<TItem> _from;
    private int _size;
    private int _offset;
    private readonly IArbitraryValueGenerator<TItem> _valueGenerator;
    private IEnumerator<TItem> _shrinker;

    internal MemoryShrinkingEnumerator(
        Memory<TItem> from,
        int size,
        int offset,
        IArbitraryValueGenerator<TItem> valueGenerator)
    {
        ArgumentOutOfRangeException.ThrowIfZero(from.Length);

        _from = from;
        _size = size;
        _offset = offset;
        _valueGenerator = valueGenerator;
        _shrinker = _valueGenerator.Shrink(from.Span[0]).GetEnumerator();
        _current = new Memory<TItem>();
    }

    public bool MoveNext()
    {
        // Try an empty List<T> for the first call.
        if (_size == _from.Length)
        {
            _size /= 2;
            _offset = _size;
            _current = Memory<TItem>.Empty;
            return true;
        }

        if (_size > 0)
        {
            var front = _from[..(_offset - _size)];
            var back = _from[_offset..];

            TItem[] val = [..front.Span, ..back.Span];

            _offset += _size;

            if (_offset > _from.Length)
            {
                _size /= 2;
                _offset = _size;
            }

            _current = val;
            return true;
        }

        // We've shrunk the List as far as possible. Now shrink the values.
        if (_offset == 0)
        {
            _offset = 1;
        }

        if (NextSmallestValue(out var n))
        {
            var front = _from[..(_offset - 1)];
            var back = _from[_offset..];
            TItem[] arr = [..front.Span, n, ..back.Span];
            _current = arr;
            return true;
        }

        return false;
    }

    private bool NextSmallestValue([NotNullWhen(true)] out TItem? value)
    {
        while (true)
        {
            if (_shrinker.MoveNext())
            {
                value = _shrinker.Current;
                Debug.Assert(value is not null);
                return true;
            }

            if (_from.Length <= _offset)
            {
                value = default;
                return false;
            }

            var n = _from.Span[_offset];
            _shrinker.Dispose();
            _shrinker = _valueGenerator.Shrink(n).GetEnumerator();
            _offset += 1;
        }
    }

    public IEnumerator<Memory<TItem>> GetEnumerator() => this;

    IEnumerator IEnumerable.GetEnumerator() => this;

    Memory<TItem> IEnumerator<Memory<TItem>>.Current => _current;

    object IEnumerator.Current => _current;

    public void Reset()
    {
        throw new NotSupportedException();
    }

    public void Dispose()
    {
        _shrinker.Dispose();
    }
}
