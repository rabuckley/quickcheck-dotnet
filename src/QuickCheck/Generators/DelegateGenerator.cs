using QuickCheck.Choices;

namespace QuickCheck.Generators;

internal sealed class DelegateGenerator<T> : Generator<T>
{
    private readonly Func<ChoiceSource, T> _generate;

    public DelegateGenerator(Func<ChoiceSource, T> generate)
    {
        _generate = generate;
    }

    protected internal override T Generate(ChoiceSource source) => _generate(source);
}
