namespace QuickCheck.Generators;

internal static class CharacterGenerator
{
    public static readonly Generator<char> Any = Generate.Frequency(
        (6, Generate.Between('a', 'z')),
        (2, Generate.Between('A', 'Z')),
        (2, Generate.Between('0', '9')),
        (3, Generate.Between(' ', '~')),
        (2, Generate.Between(char.MinValue, char.MaxValue)));
}
