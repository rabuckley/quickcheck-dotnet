using QuickCheck.Choices;

namespace QuickCheck.Generators;

internal sealed class BooleanGenerator : Generator<bool>
{
    public static readonly BooleanGenerator Instance = new();

    protected internal override bool Generate(ChoiceSource source) => source.NextBoolean();
}
