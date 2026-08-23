using System.Numerics;
using QuickCheck.Choices;

namespace QuickCheck.Generators;

/// <summary>
/// Generates integers in [min, max] from a single choice through an <see cref="IntegerRange{T}"/>,
/// so shrinking moves towards the value nearest zero.
/// </summary>
internal sealed class IntegerGenerator<T> : Generator<T> where T : IBinaryInteger<T>
{
    private readonly IntegerRange<T> _range;

    public IntegerGenerator(T min, T max) => _range = new IntegerRange<T>(min, max);

    protected internal override T Generate(ChoiceSource source) => _range.Draw(source);
}
