namespace QuickCheck;

public interface IArbitraryValueGenerator<T>
{
    public T Generate();

    public T Choose(ReadOnlySpan<T> options);

    public IEnumerable<T> Shrink(T from);
}