namespace QuickCheck;

/// <summary>
/// Represents how sure a check must be before it decides that a <see cref="Property.Cover"/>
/// requirement is met or missed, for <see cref="CheckOptions.CoverageConfidence"/>. It is the
/// confidence of Haskell QuickCheck's
/// <see href="https://hackage-content.haskell.org/package/QuickCheck-2.18.0.0/docs/Test-QuickCheck.html#v:checkCoverage"><c>checkCoverage</c></see>,
/// with the same defaults.
/// </summary>
/// <remarks>
/// <para>
/// The check keeps generating examples until each requirement's rate so far is far enough from
/// its minimum that chance alone is unlikely to explain it, or one requirement's rate is known to
/// fall short. A true rate at or above the minimum is accepted and one below
/// <see cref="Tolerance"/> times the minimum is rejected, each wrongly at most once in
/// <see cref="Certainty"/> checks. A true rate between the two may be accepted or rejected, and is
/// the slowest to decide, slowest of all in the middle of the band: with the defaults and a
/// minimum of 50%, a true rate of 50% decides after about 6,400 examples and one of 47% after
/// about 25,600, so state the minimum you need rather than the rate you expect.
/// </para>
/// </remarks>
public sealed record Confidence
{
    /// <summary>
    /// Gets the default confidence: one wrong decision in a billion checks, and a tolerance of 0.9.
    /// </summary>
    public static Confidence Default { get; } = new();

    /// <summary>
    /// Gets the inverse of the chance that one check wrongly fails or wrongly passes a requirement:
    /// a value of n means at most one check in n decides wrongly. The default is 1,000,000,000.
    /// <see href="https://hackage-content.haskell.org/package/QuickCheck-2.18.0.0/docs/Test-QuickCheck.html#t:Confidence">
    /// Haskell QuickCheck's rule of thumb</see> is 100 times the number of
    /// <see cref="Property.Cover"/> calls in the suite, times how often the suite is expected to
    /// run, so that a wrong decision is unlikely over the life of the project.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 2.</exception>
    public long Certainty
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 2L);
            field = value;
        }
    } = 1_000_000_000;

    /// <summary>
    /// Gets the fraction of the stated minimum that a true rate must fall below before the check is
    /// certain to reject it; a rate between <see cref="Tolerance"/> times the minimum and the
    /// minimum may be accepted or rejected. The default is 0.9. Nearer 1 makes the check stricter
    /// and much slower, because halving the band's width quadruples the examples needed: with a
    /// minimum of 50%, the slowest rate takes about 25,600 examples at 0.9 and about 3,300,000 at
    /// 0.99.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is not greater than 0 and less than 1.
    /// </exception>
    public double Tolerance
    {
        get;
        init
        {
            if (!(value > 0 && value < 1))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), value, "The tolerance must be greater than 0 and less than 1.");
            }

            field = value;
        }
    } = 0.9;
}
