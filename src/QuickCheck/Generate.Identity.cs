using QuickCheck.Generators;

namespace QuickCheck;

public static partial class Generate
{
    /// <summary>
    /// Creates a generator for GUIDs.
    /// </summary>
    /// <returns>
    /// A generator that produces GUIDs uniform over all 128 bits, so values are effectively unique,
    /// and shrinks towards <see cref="System.Guid.Empty"/>. Version and variant bits are not set.
    /// </returns>
    public static Generator<System.Guid> Guid() => GuidGenerator.Instance;
}
