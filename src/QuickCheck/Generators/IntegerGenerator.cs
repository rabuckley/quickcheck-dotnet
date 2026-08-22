using System.Numerics;
using QuickCheck.Choices;

namespace QuickCheck.Generators;

/// <summary>
/// Generates integers in [min, max] from a single choice, laid out so that choice 0 is the value
/// nearest zero and increasing choices alternate outwards (0, +1, -1, +2, -2, …) until one bound is
/// exhausted. Shrinking a choice therefore shrinks the magnitude of the integer.
/// </summary>
internal sealed class IntegerGenerator<T> : Generator<T> where T : IBinaryInteger<T>
{
    private readonly T _target;
    private readonly ulong _below;
    private readonly ulong _above;
    private readonly ulong _maxChoice;
    private readonly ulong[] _boundaryChoices;

    public IntegerGenerator(T min, T max)
    {
        if (min > max)
        {
            throw new ArgumentOutOfRangeException(
                nameof(min), $"min ({min}) must not exceed max ({max}).");
        }

        if (!TryGetDistance(min, max, out _maxChoice))
        {
            throw new ArgumentOutOfRangeException(
                nameof(max), "The range [min, max] must span at most 64 bits.");
        }

        _target = T.Clamp(T.Zero, min, max);

        // Both distances are within the span, which has just been checked.
        TryGetDistance(min, _target, out _below);
        TryGetDistance(_target, max, out _above);

        _boundaryChoices = [ToChoice(_below, above: false), ToChoice(_above, above: true)];
    }

    protected internal override T Generate(ChoiceSource source) =>
        FromChoice(source.NextChoice(_maxChoice, _boundaryChoices));

    /// <summary>
    /// The distance from <paramref name="from"/> up to <paramref name="to"/>, which must not be
    /// below it, or <see langword="false"/> if that distance needs more than 64 bits. A type too
    /// narrow to hold the distance wraps on subtraction, but the wrapped bits are still the
    /// distance, so it is read back unsigned rather than through a wider signed type — no such type
    /// can hold every <typeparamref name="T"/>.
    /// </summary>
    internal static bool TryGetDistance(T from, T to, out ulong distance)
    {
        var difference = unchecked(to - from);
        var byteCount = difference.GetByteCount();
        var bytes = byteCount <= 16 ? stackalloc byte[byteCount] : new byte[byteCount];
        difference.WriteLittleEndian(bytes);

        distance = 0;

        for (var i = 0; i < bytes.Length; i++)
        {
            if (i < sizeof(ulong))
            {
                distance |= (ulong)bytes[i] << (i * 8);
            }
            else if (bytes[i] != 0)
            {
                distance = 0;
                return false;
            }
        }

        return true;
    }

    private ulong ToChoice(ulong magnitude, bool above)
    {
        if (magnitude == 0)
        {
            return 0;
        }

        var symmetric = Math.Min(_below, _above);

        if (magnitude > symmetric)
        {
            return symmetric + magnitude;
        }

        return above ? 2 * magnitude - 1 : 2 * magnitude;
    }

    private T FromChoice(ulong choice)
    {
        if (choice == 0)
        {
            return _target;
        }

        var symmetric = Math.Min(_below, _above);

        if (choice <= 2 * symmetric)
        {
            // Alternate: odd steps go up, even steps go down.
            return (choice & 1) == 1 ? Add(_target, (choice + 1) / 2) : Subtract(_target, choice / 2);
        }

        // One side is exhausted; the remainder continues on the other side.
        var offset = choice - symmetric;

        return _above > _below ? Add(_target, offset) : Subtract(_target, offset);
    }

    // Both bounds and the result are in range, so the wrapping these may do on the way there is
    // undone by the time the value comes back out.
    private static T Add(T value, ulong distance) => unchecked(value + T.CreateTruncating(distance));

    private static T Subtract(T value, ulong distance) => unchecked(value - T.CreateTruncating(distance));
}
