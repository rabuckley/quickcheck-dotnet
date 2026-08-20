namespace QuickCheck;

/// <summary>
/// Represents the options that control how a <see cref="Property{T}"/> or an
/// <see cref="AsyncProperty{T}"/> is checked.
/// </summary>
public sealed record CheckOptions
{
    /// <summary>
    /// Gets the default options.
    /// </summary>
    public static CheckOptions Default { get; } = new();

    /// <summary>
    /// Gets the number of examples that must pass before the property is reported as passed. The
    /// default is 100.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is less than or equal to zero.
    /// </exception>
    public int RunCount
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 100;

    /// <summary>
    /// Gets the seed that examples are generated from, or <see langword="null"/> to choose a fresh
    /// seed for each check. The same seed and options always generate the same examples, and the seed
    /// used is reported in <see cref="PropertyResult{T}.Seed"/>.
    /// </summary>
    public ulong? Seed { get; init; }

    /// <summary>
    /// Gets the token identifying the single example to check, as reported by an earlier failure, or
    /// <see langword="null"/> to generate examples as usual. A replayed example is shrunk in the same
    /// way as a freshly generated failure.
    /// </summary>
    public Replay? Replay { get; init; }

    /// <summary>
    /// Gets the number of discarded examples tolerated for each example that must pass, before the
    /// check gives up with <see cref="PropertyOutcome.Exhausted"/>. The default is 10.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is less than or equal to zero.
    /// </exception>
    public int MaxDiscardRatio
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 10;

    /// <summary>
    /// Gets the maximum number of candidate examples the shrinker may evaluate while minimising a
    /// failure. The default is 10,000; zero disables shrinking. See
    /// <see cref="MaxShrinkWork"/>, which bounds the replay work those candidates may cost.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is negative.
    /// </exception>
    public int MaxShrinkAttempts
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = value;
        }
    } = 10_000;

    /// <summary>
    /// Gets the maximum total number of choices the shrinker may replay while minimising a failure,
    /// summed over the candidates it evaluates: a candidate costs one attempt and as many choices as
    /// it contains. Shrinking stops when this or <see cref="MaxShrinkAttempts"/> is spent, whichever
    /// comes first, so a counterexample with a very large choice sequence gives up early instead of
    /// spending the whole attempt budget replaying it. The default is 5,000,000, which leaves
    /// examples of up to 500 choices limited only by <see cref="MaxShrinkAttempts"/>; zero disables
    /// shrinking.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is negative.
    /// </exception>
    public int MaxShrinkWork
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = value;
        }
    } = 5_000_000;
}
