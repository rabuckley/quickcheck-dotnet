namespace QuickCheck.Running;

/// <summary>
/// The early give-up test run after each discard: the check ends as <see cref="PropertyOutcome.Exhausted"/>
/// as soon as the counts so far make it all but certain that the acceptance rate lies below what
/// the <see cref="CheckOptions.MaxDiscardRatio"/> budget tolerates, instead of spending the whole
/// budget to learn the same thing.
/// </summary>
/// <remarks>
/// "All but certain" is a posterior probability given the counts at one evaluation. It does not
/// bound how often the check gives up wrongly over a run, as <see cref="Confidence.Certainty"/>
/// bounds the coverage test across every look.
/// </remarks>
internal static class DiscardHealthCheck
{
    // The posterior is evaluated on every discard rather than at CoverageLook-style scheduled
    // looks, which miss most hopeless filters: at the defaults a 5%-acceptance filter slips past
    // 55-75% of the time, and at RunCount = 10 with ratio 1 the first look lands after the hard
    // budget. Continuous monitoring inflates the per-evaluation error rate, and the constant below
    // prices that in.

    // Simulated whole-run false-fire rates at the defaults are 1.2e-6 at 15% acceptance and
    // 4.6e-5 at 12%, while an impossible Assume fires at 80 discards instead of 1,001. A health
    // check affords this, not the coverage test's 1e-9.
    internal const double PosteriorTail = 1e-4;

    /// <summary>
    /// Tells whether the posterior probability is at least 1 − <see cref="PosteriorTail"/> that the
    /// acceptance rate behind <paramref name="passed"/> passes and <paramref name="discards"/>
    /// discards lies below 1 / (<paramref name="maxDiscardRatio"/> + 1), the rate at which the
    /// discard budget runs out before <see cref="CheckOptions.RunCount"/> passes.
    /// </summary>
    public static bool ShouldGiveUp(int passed, int discards, int maxDiscardRatio) =>
        AcceptanceBelowThreshold(passed, discards, 1.0 / ((long)maxDiscardRatio + 1))
            >= 1 - PosteriorTail;

    /// <summary>
    /// Returns the posterior probability that the acceptance rate behind <paramref name="passed"/>
    /// passes and <paramref name="discards"/> discards lies below <paramref name="threshold"/>,
    /// under the Jeffreys prior Beta(½, ½).
    /// </summary>
    internal static double AcceptanceBelowThreshold(int passed, int discards, double threshold) =>
        BetaDistribution.Cdf(threshold, passed + 0.5, discards + 0.5);
}
