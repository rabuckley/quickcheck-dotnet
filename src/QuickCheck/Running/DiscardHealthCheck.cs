namespace QuickCheck.Running;

/// <summary>
/// The early give-up test run after each discard: the check ends as <see cref="PropertyOutcome.Exhausted"/>
/// as soon as the acceptance rate is known, to the stated certainty, to lie below what the
/// <see cref="CheckOptions.MaxDiscardRatio"/> budget tolerates, instead of spending the whole budget
/// to learn the same thing.
/// </summary>
internal static class DiscardHealthCheck
{
    // The posterior is evaluated on every discard rather than at CoverageLook-style scheduled
    // looks, which miss most hopeless filters: at the defaults a 5%-acceptance filter slips past
    // 55-75% of the time, and at RunCount = 10 with ratio 1 the first look lands after the hard
    // budget. Continuous monitoring inflates the per-evaluation error rate, and the constant below
    // prices that in.

    // 1e-4 per evaluation: simulated whole-run false-fire rates at the defaults are 1.2e-6 at 15%
    // acceptance and 4.6e-5 at 12%, while an impossible Assume fires at 80 discards instead of
    // 1,001. A health check affords this, not the coverage test's 1e-9.
    internal const long Certainty = 10_000;

    /// <summary>
    /// Tells whether the check should give up after <paramref name="passed"/> passes and
    /// <paramref name="discards"/> discards: whether the acceptance rate is known, to 1 in
    /// <see cref="Certainty"/>, to lie below 1 / (<paramref name="maxDiscardRatio"/> + 1), the
    /// rate at which the discard budget runs out before <see cref="CheckOptions.RunCount"/> passes.
    /// </summary>
    public static bool ShouldGiveUp(int passed, int discards, int maxDiscardRatio) =>
        AcceptanceBelowThreshold(passed, discards, 1.0 / ((long)maxDiscardRatio + 1))
            >= 1 - 1.0 / Certainty;

    /// <summary>
    /// Returns the posterior probability that the acceptance rate behind <paramref name="passed"/>
    /// passes and <paramref name="discards"/> discards lies below <paramref name="threshold"/>,
    /// under the Jeffreys prior Beta(½, ½).
    /// </summary>
    internal static double AcceptanceBelowThreshold(int passed, int discards, double threshold) =>
        BetaDistribution.Cdf(threshold, passed + 0.5, discards + 0.5);
}
