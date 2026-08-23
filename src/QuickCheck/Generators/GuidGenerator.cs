using QuickCheck.Choices;

namespace QuickCheck.Generators;

/// <summary>
/// Generates GUIDs from sixteen uniform bytes, so values are uniform over all 128 bits and
/// effectively unique, and shrinking moves towards <see cref="Guid.Empty"/>. Version and variant
/// bits are left unset: <see cref="Guid"/> accepts any 128 bits, and setting them would make the
/// empty GUID unreachable.
/// </summary>
internal sealed class GuidGenerator : Generator<Guid>
{
    public static readonly GuidGenerator Instance = new();

    protected internal override Guid Generate(ChoiceSource source)
    {
        Span<byte> bytes = stackalloc byte[16];

        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)source.NextChoice(byte.MaxValue);
        }

        return new Guid(bytes);
    }
}
