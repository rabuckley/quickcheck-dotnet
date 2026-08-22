using System.Numerics;

namespace QuickCheck.Running;

/// <summary>
/// One look of the sequential coverage test run under <see cref="CheckOptions.CoverageConfidence"/>:
/// Haskell QuickCheck's <c>checkCoverage</c>. Each look compares a Wilson interval for a
/// <see cref="Property.Cover"/> requirement with the stated minimum, spending less of the error
/// budget at each successive look so that the stated certainty holds however many looks a check
/// takes.
/// </summary>
internal readonly struct CoverageLook
{
    // QuickCheck's loop is timeToCheckCoverage, sufficientlyCovered and insufficientlyCovered in
    // https://github.com/nick8325/quickcheck/blob/master/src/Test/QuickCheck/Test.hs. It spends a
    // full 1 / certainty at every look, so its stated certainty holds per look rather than per
    // check; halving the budget at each look instead costs 4% more passes at the first look and
    // 36% at the tenth, and makes the certainty hold over the whole run however long it goes on.

    private readonly int _passed;
    private readonly double _tolerance;
    private readonly double _z;

    /// <summary>
    /// Prepares the <paramref name="look"/>th look (0-based, counted per check) after
    /// <paramref name="passed"/> examples.
    /// </summary>
    public CoverageLook(Confidence confidence, int passed, int look)
    {
        // Look j spends alpha_j = (1 / Certainty) / 2^(j + 1), so the looks together spend at most
        // 1 / Certainty. The z-score comes from the lower tail, because 1 - alpha / 2 rounds to 1
        // for small alpha.
        var alpha = Math.ScaleB(1.0 / confidence.Certainty, -(look + 1));

        _passed = passed;
        _tolerance = confidence.Tolerance;
        _z = -NormalDistribution.InverseCdf(alpha / 2);
    }

    /// <summary>
    /// Tells whether a look is due after <paramref name="passed"/> examples: at
    /// <paramref name="runCount"/>, and at 100, 200, 400, 800, and so on.
    /// </summary>
    public static bool IsDue(int passed, int runCount) =>
        passed == runCount || (passed % 100 == 0 && BitOperations.IsPow2(passed / 100));

    /// <summary>
    /// Decides a requirement for <paramref name="minimumPercent"/> that <paramref name="count"/> of
    /// the passed examples hit. A requirement is <see cref="CoverageVerdict.Met"/> when the
    /// interval's lower bound reaches the tolerance times the minimum, which a rate known to lie
    /// inside the tolerance band satisfies, and <see cref="CoverageVerdict.Unmet"/> when it is not
    /// met and the upper bound is below the minimum.
    /// </summary>
    public CoverageVerdict Verdict(double minimumPercent, int count)
    {
        var minimum = minimumPercent / 100;
        var (lower, upper) = WilsonScoreInterval.Bounds(count, _passed, _z);

        if (lower >= _tolerance * minimum)
        {
            return CoverageVerdict.Met;
        }

        return upper < minimum ? CoverageVerdict.Unmet : CoverageVerdict.Undecided;
    }
}
