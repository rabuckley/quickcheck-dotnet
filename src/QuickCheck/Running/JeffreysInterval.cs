namespace QuickCheck.Running;

/// <summary>
/// The 95% equal-tailed Jeffreys credible interval: the range in which the true rate behind
/// <c>count</c> hits in <c>total</c> trials plausibly lies, under the Jeffreys prior Beta(½, ½).
/// </summary>
internal static class JeffreysInterval
{
    // Brown, Cai and DasGupta, "Interval Estimation for a Binomial Proportion", Statistical
    // Science 16(2), 2001 (https://doi.org/10.1214/ss/1009213286) recommend the equal-tailed
    // Jeffreys interval alongside the Wilson score interval, including the modification at the
    // end points that Bounds applies.

    /// <summary>
    /// Returns the 2.5% and 97.5% quantiles of Beta(<paramref name="count"/> + ½,
    /// <paramref name="total"/> − <paramref name="count"/> + ½), with exactly 0 as the lower bound
    /// when nothing hit and exactly 1 as the upper bound when everything did.
    /// </summary>
    /// <remarks>
    /// The exact end points matter: the raw quantiles for 0 or 100 hits of 100 would be 0.000005
    /// and 0.999995, claiming an impossible sliver of certainty about rates no data rules out.
    /// </remarks>
    public static (double Lower, double Upper) Bounds(int count, int total)
    {
        var a = count + 0.5;
        var b = total - count + 0.5;

        var lower = count == 0 ? 0.0 : BetaDistribution.Quantile(0.025, a, b);
        var upper = count == total ? 1.0 : BetaDistribution.Quantile(0.975, a, b);

        return (lower, upper);
    }
}
