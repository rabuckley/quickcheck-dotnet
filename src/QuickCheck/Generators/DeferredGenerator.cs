using QuickCheck.Choices;

namespace QuickCheck.Generators;

internal sealed class DeferredGenerator<T> : Generator<T>
{
    private readonly Func<Generator<T>> _factory;
    private Generator<T>? _generator;

    public DeferredGenerator(Func<Generator<T>> factory)
    {
        _factory = factory;
    }

    protected internal override T Generate(ChoiceSource source)
    {
        _generator ??= _factory()
            ?? throw new InvalidOperationException("The deferred generator factory returned null.");

        return source.Draw(_generator);
    }
}
