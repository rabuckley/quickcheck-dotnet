namespace QuickCheck;

/// <summary>
/// The budget limit that ended shrinking before it converged, reported by
/// <see cref="PropertyResult{T}.ShrinkLimit"/>.
/// </summary>
public enum ShrinkLimit
{
    /// <summary>No limit was reached: shrinking ran until no candidate improved.</summary>
    None,

    /// <summary><see cref="CheckOptions.MaxShrinkAttempts"/> was spent.</summary>
    Attempts,

    /// <summary><see cref="CheckOptions.MaxShrinkWork"/> was spent.</summary>
    Work
}
