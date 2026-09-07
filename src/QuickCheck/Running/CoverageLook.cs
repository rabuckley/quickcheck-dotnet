using System.Numerics;

namespace QuickCheck.Running;

/// <summary>
/// One look of the sequential coverage test. Binomial Chernoff bounds control each tail's
/// error, and a summable budget controls the error across all looks for one requirement.
/// </summary>
internal readonly struct CoverageLook
{
    private readonly int _passed;
    private readonly double _tolerance;
    private readonly double _logInverseTailBudget;

    /// <summary>
    /// Prepares the <paramref name="look"/>th look (0-based, counted per check) after
    /// <paramref name="passed"/> examples.
    /// </summary>
    public CoverageLook(Confidence confidence, int passed, int look)
    {
        // Each tail spends 1 / (Certainty * 2^(look + 2)). The union bound over both
        // tails and all looks is at most 1 / Certainty. Logs avoid underflow at late looks.
        _passed = passed;
        _tolerance = confidence.Tolerance;
        _logInverseTailBudget = Math.Log(confidence.Certainty) + (look + 2.0) * Math.Log(2);
    }

    /// <summary>
    /// Tells whether a look is due after <paramref name="passed"/> examples: at
    /// <paramref name="runCount"/>, and at 100, 200, 400, 800, and so on.
    /// </summary>
    public static bool IsDue(int passed, int runCount) =>
        passed == runCount || (passed % 100 == 0 && BitOperations.IsPow2(passed / 100));

    /// <summary>
    /// Accepts when the tolerated minimum is excluded by the upper-tail test, rejects when
    /// the stated minimum is excluded by the lower-tail test, and otherwise keeps sampling.
    /// </summary>
    public CoverageVerdict Verdict(double minimumPercent, int count)
    {
        var minimum = minimumPercent / 100;
        var toleratedMinimum = _tolerance * minimum;
        var rate = (double)count / _passed;

        if (minimum == 0 || (rate > toleratedMinimum && Excludes(rate, count, toleratedMinimum)))
        {
            return CoverageVerdict.Met;
        }

        return rate < minimum && Excludes(rate, count, minimum)
            ? CoverageVerdict.Unmet
            : CoverageVerdict.Undecided;
    }

    private bool Excludes(double rate, int count, double probability)
    {
        // For an independent Bernoulli sample, the tail in the direction of the observed
        // rate q is at most exp(-n D(q || p)). This is a finite-sample inequality, unlike
        // Wilson's normal approximation. See the binomial derivation at
        // https://www.probabilitycourse.com/chapter6/6_2_3_chernoff_bounds.php.
        // Comparing at the two thresholds avoids solving for interval endpoints.
        if (probability == 0 || probability == 1)
        {
            return true;
        }

        var divergence = count == 0 ? 0 : count * (Math.Log(rate) - Math.Log(probability));
        if (count < _passed)
        {
            divergence += (_passed - count) * (double.LogP1(-rate) - double.LogP1(-probability));
        }

        // Leave room for roundoff in the logarithms and the subtraction of the two terms.
        return divergence > _logInverseTailBudget + 1e-12 * _passed;
    }
}
