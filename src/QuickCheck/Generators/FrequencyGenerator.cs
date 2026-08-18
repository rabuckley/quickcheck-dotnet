using QuickCheck.Choices;

namespace QuickCheck.Generators;

internal sealed class FrequencyGenerator<T> : Generator<T>
{
    private readonly (int Weight, Generator<T> Generator)[] _weighted;
    private readonly ulong _totalWeight;

    public FrequencyGenerator((int Weight, Generator<T> Generator)[] weighted)
    {
        _weighted = weighted;

        foreach (var (weight, _) in weighted)
        {
            _totalWeight += (ulong)weight;
        }
    }

    protected internal override T Generate(ChoiceSource source)
    {
        // One choice over the cumulative weight range: choice 0 always maps to
        // the first generator, so shrinking moves towards it.
        var roll = source.NextChoice(_totalWeight - 1);
        ulong cumulative = 0;

        foreach (var (weight, generator) in _weighted)
        {
            cumulative += (ulong)weight;

            if (roll < cumulative)
            {
                return source.Draw(generator);
            }
        }

        throw new System.Diagnostics.UnreachableException();
    }
}
