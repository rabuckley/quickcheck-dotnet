using System.Numerics;

namespace QuickCheck.Choices;

/// <summary>
/// Represents the stream of choices that a <see cref="Generator{T}"/> draws from while generating a
/// value.
/// </summary>
/// <remarks>
/// <para>
/// A source either invents choices from a seeded pseudo-random number generator, when generating, or
/// replays a recorded prefix, when shrinking or reproducing, and in both cases records exactly what
/// was consumed. When a replayed prefix runs out, the source supplies the simplest choice, 0, rather
/// than failing, which lets a shrunk choice sequence that is too short still describe a valid,
/// smaller value.
/// </para>
/// <para>
/// A custom generator should prefer <see cref="Draw{T}"/> over the primitive <c>Next</c> methods, so
/// that the structure of its value is visible to the shrinker.
/// </para>
/// </remarks>
public sealed class ChoiceSource
{
    /// <summary>
    /// The upper bound on choices in one example. Generation past this point
    /// discards the example rather than running unbounded.
    /// </summary>
    internal const int MaxChoices = 100_000;

    // 1 in 16 draws with declared boundaries snap to one, so edge cases show
    // up early.
    private const double BoundaryProbability = 1.0 / 16;

    // Ranges wider than this are sampled with a size bias (7 in 8 draws pick a
    // narrower bit-width first) so small values are common; narrower ranges
    // are uniform, which keeps Elements/Frequency/char draws unweighted.
    private const int UniformBitLength = 24;
    private const double SizeBiasProbability = 7.0 / 8;

    private readonly IReadOnlyList<Choice>? _prefix;
    private readonly Xoshiro256StarStar? _random;
    private readonly List<Choice> _recorded = [];
    private readonly List<ChoiceSpan> _spans = [];
    private readonly Stack<int> _openSpans = new();

    private ChoiceSource(IReadOnlyList<Choice>? prefix, Xoshiro256StarStar? random)
    {
        _prefix = prefix;
        _random = random;
    }

    internal static ChoiceSource FromRandom(Xoshiro256StarStar random) =>
        new(prefix: null, random);

    /// <summary>
    /// Creates a source that replays <paramref name="prefix"/> and then pads
    /// with minimal choices.
    /// </summary>
    internal static ChoiceSource FromPrefix(IReadOnlyList<Choice> prefix) =>
        new(prefix, random: null);

    internal IReadOnlyList<Choice> Recorded => _recorded;

    internal IReadOnlyList<ChoiceSpan> Spans => _spans;

    /// <summary>
    /// Draws a value from the specified generator, recording the choices it consumes as one structural
    /// span.
    /// </summary>
    /// <typeparam name="T">The type of value to draw.</typeparam>
    /// <param name="generator">The generator to draw from.</param>
    /// <returns>The value that <paramref name="generator"/> produced.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="generator"/> is <see langword="null"/>.</exception>
    /// <exception cref="DiscardException">The example exceeded the maximum number of choices.</exception>
    public T Draw<T>(Generator<T> generator)
    {
        ArgumentNullException.ThrowIfNull(generator);

        _openSpans.Push(_recorded.Count);

        try
        {
            return generator.Generate(this);
        }
        finally
        {
            var start = _openSpans.Pop();

            // Empty spans carry no structure worth deleting.
            if (_recorded.Count > start)
            {
                _spans.Add(new ChoiceSpan(start, _recorded.Count));
            }
        }
    }

    /// <summary>
    /// Draws an integer from 0 to <paramref name="maxInclusive"/>. Zero is treated as the simplest
    /// value: shrinking moves towards it, and for wide ranges generation favours small values.
    /// </summary>
    /// <param name="maxInclusive">The inclusive upper bound of the choice.</param>
    /// <param name="boundaries">
    /// The choices that map to edge cases of the generated value, such as the choices for a type's
    /// minimum and maximum. Generation picks one of these, or 0, or <paramref name="maxInclusive"/>,
    /// more often than chance.
    /// </param>
    /// <returns>The choice that was drawn or replayed.</returns>
    /// <exception cref="DiscardException">The example exceeded the maximum number of choices.</exception>
    public ulong NextChoice(ulong maxInclusive, params ReadOnlySpan<ulong> boundaries)
    {
        ThrowIfFull();

        var index = _recorded.Count;
        ulong value;

        if (_prefix is not null && index < _prefix.Count)
        {
            value = Math.Min(_prefix[index].Value, maxInclusive);
        }
        else if (_random is not null)
        {
            value = Sample(_random, maxInclusive, boundaries);
        }
        else
        {
            value = 0;
        }

        return Record(value, maxInclusive);
    }

    /// <summary>
    /// Draws a boolean that is <see langword="true"/> with a given probability during generation.
    /// </summary>
    /// <param name="probabilityOfTrue">
    /// The probability of drawing <see langword="true"/>, from 0 to 1. The default is 0.5.
    /// </param>
    /// <returns>The boolean that was drawn or replayed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="probabilityOfTrue"/> is less than 0 or greater than 1.
    /// </exception>
    /// <exception cref="DiscardException">The example exceeded the maximum number of choices.</exception>
    /// <remarks>
    /// <see langword="false"/> is the simpler value, so shrinking moves towards it.
    /// </remarks>
    public bool NextBoolean(double probabilityOfTrue = 0.5)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(probabilityOfTrue, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(probabilityOfTrue, 1);
        ThrowIfFull();

        var index = _recorded.Count;
        ulong value;

        if (_prefix is not null && index < _prefix.Count)
        {
            value = Math.Min(_prefix[index].Value, 1);
        }
        else if (_random is not null)
        {
            value = _random.NextDouble() < probabilityOfTrue ? 1UL : 0UL;
        }
        else
        {
            value = 0;
        }

        return Record(value, 1) == 1;
    }

    private void ThrowIfFull()
    {
        if (_recorded.Count >= MaxChoices)
        {
            throw new DiscardException("Example exceeded the maximum number of choices.");
        }
    }

    private ulong Record(ulong value, ulong maxInclusive)
    {
        _recorded.Add(new Choice(value, maxInclusive));
        return value;
    }

    private static ulong Sample(Xoshiro256StarStar random, ulong max, ReadOnlySpan<ulong> boundaries)
    {
        if (max == 0)
        {
            return 0;
        }

        if (!boundaries.IsEmpty && random.NextDouble() < BoundaryProbability)
        {
            var pick = random.NextUInt64Inclusive((ulong)boundaries.Length + 1);

            return pick switch
            {
                0 => 0,
                1 => max,
                _ => Math.Min(boundaries[(int)pick - 2], max)
            };
        }

        var bitLength = 64 - BitOperations.LeadingZeroCount(max);

        if (bitLength <= UniformBitLength || random.NextDouble() >= SizeBiasProbability)
        {
            return random.NextUInt64Inclusive(max);
        }

        // Pick a bit-width uniformly, then a value within it: a log-uniform
        // distribution so small values are common but the full range is
        // reachable.
        var width = 1 + (int)random.NextUInt64Inclusive((ulong)bitLength - 1);
        var widthMax = width == 64 ? ulong.MaxValue : (1UL << width) - 1;

        return random.NextUInt64Inclusive(Math.Min(max, widthMax));
    }
}
