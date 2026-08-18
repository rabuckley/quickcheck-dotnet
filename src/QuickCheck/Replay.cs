using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace QuickCheck;

/// <summary>
/// Represents the identity of one generated example, the example at index <see cref="Run"/> of the
/// stream seeded by <see cref="Seed"/>, so that a failure can be reproduced through
/// <see cref="CheckOptions.Replay"/>.
/// </summary>
/// <param name="Seed">The seed the check ran with.</param>
/// <param name="Run">The zero-based index of the example within the check.</param>
public readonly record struct Replay(ulong Seed, int Run)
{
    /// <summary>Returns the string representation of the token.</summary>
    /// <returns>The token in the form <c>seed:run</c>, as accepted by <see cref="Parse"/>.</returns>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Seed}:{Run}");

    /// <summary>
    /// Converts the string representation of a replay token to its <see cref="Replay"/> equivalent.
    /// </summary>
    /// <param name="text">A token in the form produced by <see cref="ToString"/>.</param>
    /// <returns>The token that <paramref name="text"/> represents.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException"><paramref name="text"/> is not in the correct format.</exception>
    public static Replay Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return TryParse(text, out var replay)
            ? replay
            : throw new FormatException($"'{text}' is not a valid replay token; expected 'seed:run'.");
    }

    /// <summary>
    /// Converts the string representation of a replay token to its <see cref="Replay"/> equivalent. A
    /// return value indicates whether the conversion succeeded.
    /// </summary>
    /// <param name="text">A token in the form produced by <see cref="ToString"/>.</param>
    /// <param name="replay">
    /// When this method returns, contains the token that <paramref name="text"/> represents if the
    /// conversion succeeded, or the default value if it failed.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="text"/> was converted successfully; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public static bool TryParse([NotNullWhen(true)] string? text, out Replay replay)
    {
        replay = default;

        if (text is null)
        {
            return false;
        }

        var separator = text.IndexOf(':');

        if (separator < 0
            || !ulong.TryParse(text.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var seed)
            || !int.TryParse(text.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var run))
        {
            return false;
        }

        replay = new Replay(seed, run);
        return true;
    }
}
