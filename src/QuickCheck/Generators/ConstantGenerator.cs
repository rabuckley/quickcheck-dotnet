using QuickCheck.Choices;

namespace QuickCheck.Generators;

internal sealed class ConstantGenerator<T> : Generator<T>
{
    private readonly T _value;

    public ConstantGenerator(T value)
    {
        _value = value;
    }

    protected internal override T Generate(ChoiceSource source) => _value;
}
