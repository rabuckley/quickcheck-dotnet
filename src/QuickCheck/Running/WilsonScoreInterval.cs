namespace QuickCheck.Running;

/// <summary>
/// The Wilson score interval: a confidence interval for the true rate behind
/// <c>count</c> hits in <c>total</c> trials that, unlike the normal approximation, stays inside
/// [0, 1] and stays reliable at rates near 0 and 1 and at small <c>total</c>.
/// </summary>
internal static class WilsonScoreInterval
{
    // The interval is from Wilson, "Probable Inference, the Law of Succession, and Statistical
    // Inference", JASA 22(158), 1927, pp. 209-212 (https://doi.org/10.1080/01621459.1927.10502953).
    // Brown, Cai and DasGupta, "Interval Estimation for a Binomial Proportion", Statistical Science
    // 16(2), 2001 (https://doi.org/10.1214/ss/1009213286) measure how erratic the normal
    // approximation's coverage is at the rates and sample sizes a coverage check sees, and
    // recommend this interval instead. QuickCheck's wilson is in
    // https://github.com/nick8325/quickcheck/blob/master/src/Test/QuickCheck/Test.hs.

    /// <summary>
    /// Returns the upper bound for a positive <paramref name="z"/> and the lower bound for a
    /// negative one, in the form Haskell QuickCheck uses; not clamped.
    /// </summary>
    public static double Bound(int count, int total, double z)
    {
        double n = total;
        var estimate = count / n;
        var zSquared = z * z;

        return (estimate + zSquared / (2 * n) + z * Math.Sqrt(estimate * (1 - estimate) / n + zSquared / (4 * n * n)))
            / (1 + zSquared / n);
    }

    /// <summary>
    /// Returns the lower and upper bounds at z-score <paramref name="z"/>, clamped to [0, 1], with
    /// exactly 0 as the lower bound when nothing hit and exactly 1 as the upper bound when
    /// everything did.
    /// </summary>
    /// <remarks>
    /// The exact end points matter: the raw upper bound for every example hitting is a few ulps
    /// below 1, which would read a 100% requirement met by every example as certainly missed, and
    /// the raw lower bound for no hits can be a little below 0, which would keep a 0% requirement
    /// from ever being known met.
    /// </remarks>
    public static (double Lower, double Upper) Bounds(int count, int total, double z)
    {
        var lower = count == 0 ? 0.0 : Math.Clamp(Bound(count, total, -z), 0, 1);
        var upper = count == total ? 1.0 : Math.Clamp(Bound(count, total, z), 0, 1);

        return (lower, upper);
    }
}
