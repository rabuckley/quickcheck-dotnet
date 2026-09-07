namespace QuickCheck.Running;

/// <summary>
/// What a <see cref="Shrinker{T}"/> may spend: the candidates it may replay, and the choices those
/// replays may consume in total. Reaching either limit ends shrinking, so minimising a large example
/// costs a budget rather than being free per candidate.
/// </summary>
internal sealed class ShrinkBudget
{
    private readonly int _maxAttempts;
    private readonly int _maxWork;

    // A candidate is charged what it costs even when that overruns the limit, so the running total
    // can pass int.MaxValue where a wide limit meets large examples.
    private long _work;

    /// <param name="maxAttempts">The most candidates that may be replayed.</param>
    /// <param name="maxWork">The most choices those replays may consume in total.</param>
    public ShrinkBudget(int maxAttempts, int maxWork)
    {
        _maxAttempts = maxAttempts;
        _maxWork = maxWork;
    }

    /// <summary>The number of candidates replayed so far.</summary>
    public int Attempts { get; private set; }

    /// <summary>The limit that has been reached, or <see cref="ShrinkLimit.None"/>.</summary>
    public ShrinkLimit LimitReached =>
        Attempts >= _maxAttempts ? ShrinkLimit.Attempts
        : _work >= _maxWork ? ShrinkLimit.Work
        : ShrinkLimit.None;

    /// <summary>
    /// Whether a limit has been reached. Loops test this to exit early; only
    /// <see cref="TryBeginAttempt"/> enforces it, so dropping such a check costs iterations, never
    /// correctness.
    /// </summary>
    public bool Exhausted => LimitReached is not ShrinkLimit.None;

    /// <summary>
    /// Admits one more replay, or refuses because a limit has been reached. The limits are tested
    /// against the accumulated total and never against what a candidate would add, so an example
    /// larger than the whole budget is still replayed once rather than not shrunk at all.
    /// </summary>
    /// <returns>Whether the replay may go ahead.</returns>
    public bool TryBeginAttempt()
    {
        if (Exhausted)
        {
            return false;
        }

        Attempts++;
        return true;
    }

    /// <summary>
    /// Pays for a replay that has run. The cost is what the replay consumed, not the length of the
    /// candidate it started from: a source pads an exhausted prefix with zeros, and those zeros can
    /// select a branch that draws far more choices than the prefix held.
    /// </summary>
    /// <param name="choices">The number of choices the replay consumed.</param>
    public void Charge(int choices) => _work += choices;
}
