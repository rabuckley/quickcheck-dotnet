using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace QuickCheck.Generators.Shrinkers;

/// <summary>
/// A static class for creating instances of <see cref="ListShrinkingEnumerator{TList,TItem}"/>.
/// </summary>
public static class ListShrinkingEnumerator
{
    /// <summary>
    /// Creates an IEnumerable of <typeparamref name="TList"/>, from smallest to largest, from <paramref name="from"/>.
    /// </summary>
    /// <param name="from">The <typeparamref name="TList"/> to attempt to shrink.</param>
    /// <param name="valueGenerator">A value generator for the list's items.</param>
    /// <typeparam name="TList">The type of the list.</typeparam>
    /// <typeparam name="TItem">The type of the list's items.</typeparam>
    public static IEnumerable<TList> Create<TList, TItem>(TList from, IArbitraryValueGenerator<TItem> valueGenerator)
        where TList : IList<TItem>, new()
    {
        if (from.Count == 0)
        {
            return [];
        }

        var size = from.Count;
        return new ListShrinkingEnumerator<TList, TItem>(from, size: size, offset: size, valueGenerator);
    }
}

/// <summary>
/// <para>An enumerator for shrinking arbitrary types which implement <see cref="IList{T}"/>.</para>
/// <para>To construct an instance, call <see cref="ListShrinkingEnumerator.Create{TList, TItem}"/>.</para>
/// </summary>
/// <typeparam name="TList">The list type, implementing <see cref="IList{T}"/> and providing a default constructor.</typeparam>
/// <typeparam name="TItem">The type of item that the <typeparamref name="TList"/> contains.</typeparam>
public sealed class ListShrinkingEnumerator<TList, TItem> : IEnumerable<TList>, IEnumerator<TList>
    where TList : IList<TItem>, new()

{
    private TList _current;
    private readonly TList _from;
    private int _size;
    private int _offset;
    private readonly IArbitraryValueGenerator<TItem> _valueGenerator;
    private IEnumerator<TItem> _shrinker;

    internal ListShrinkingEnumerator(TList from, int size, int offset, IArbitraryValueGenerator<TItem> valueGenerator)
    {
        ArgumentOutOfRangeException.ThrowIfZero(from.Count);

        _from = from;
        _size = size;
        _offset = offset;
        _valueGenerator = valueGenerator;
        _shrinker = _valueGenerator.Shrink(from[0]).GetEnumerator();
        _current = [];
    }

    public bool MoveNext()
    {
        // Try an empty List<T> for the first call.
        if (_size == _from.Count)
        {
            _size /= 2;
            _offset = _size;
            _current = [];
            return true;
        }

        if (_size > 0)
        {
            // Shrink by `(_offset - _size) - _offset`
            var en = _from
                .Take(_offset - _size)
                .Concat(_from.Skip(_offset));

            var ret = new TList();

            foreach (var item in en)
            {
                ret.Add(item);
            }

            _offset += _size;

            if (_offset > _from.Count)
            {
                _size /= 2;
                _offset = _size;
            }

            _current = ret;
            return true;
        }

        // We've shrunk the List as far as possible. Now shrink the values.
        if (_offset == 0)
        {
            _offset = 1;
        }

        if (NextValue(out var n))
        {
            var en = _from
                .Take(_offset - 1)
                .Append(n)
                .Concat(_from.Skip(_offset));

            var ret = new TList();

            foreach (var item in en)
            {
                ret.Add(item);
            }

            _current = ret;
            return true;
        }

        return false;
    }

    private bool NextValue([NotNullWhen(true)] out TItem? value)
    {
        while (true)
        {
            if (_shrinker.MoveNext())
            {
                value = _shrinker.Current;
                Debug.Assert(value is not null);
                return true;
            }

            var n = _from.ElementAtOrDefault(_offset);

            if (n is null || Comparer<TItem>.Default.Compare(n, default) == 0)
            {
                value = default;
                return false;
            }

            _shrinker.Dispose();
            _shrinker = _valueGenerator.Shrink(n).GetEnumerator();
        }
    }

    public IEnumerator<TList> GetEnumerator() => this;

    IEnumerator IEnumerable.GetEnumerator() => this;

    TList IEnumerator<TList>.Current => _current;

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