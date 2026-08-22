using System.Diagnostics;

namespace QuickCheck.Running;

/// <summary>
/// The quantile function of the standard normal distribution, for turning a significance level into
/// a z-score.
/// </summary>
internal static class NormalDistribution
{
    // Peter Acklam's rational approximation (2003), as Haskell QuickCheck uses: relative error
    // below 1.15e-9 over the whole range, which is ample for a z-score, and needs no dependency.
    // The refinement step Acklam suggests needs erfc, which System.Math lacks, so it is left out
    // as QuickCheck leaves it out.
    //
    // Coefficients and breakpoint from Acklam, "An algorithm for computing the inverse normal
    // cumulative distribution function". The original page is gone; the copy QuickCheck cites is
    // https://web.archive.org/web/20151110174102/http://home.online.no/~pjacklam/notes/invnorm/,
    // and QuickCheck's own invnormcdf is in
    // https://github.com/nick8325/quickcheck/blob/master/src/Test/QuickCheck/Test.hs.
    private static readonly double[] A =
    [
        -3.969683028665376e+01, 2.209460984245205e+02, -2.759285104469687e+02,
        1.383577518672690e+02, -3.066479806614716e+01, 2.506628277459239e+00
    ];

    private static readonly double[] B =
    [
        -5.447609879822406e+01, 1.615858368580409e+02, -1.556989798598866e+02,
        6.680131188771972e+01, -1.328068155288572e+01
    ];

    private static readonly double[] C =
    [
        -7.784894002430293e-03, -3.223964580411365e-01, -2.400758277161838e+00,
        -2.549732539343734e+00, 4.374664141464968e+00, 2.938163982698783e+00
    ];

    private static readonly double[] D =
    [
        7.784695709041462e-03, 3.224671290700398e-01, 2.445134137142996e+00, 3.754408661907416e+00
    ];

    private const double LowerBreakpoint = 0.02425;

    /// <summary>
    /// Returns the z-score below which a standard normal variable falls with probability
    /// <paramref name="probability"/>, which must be strictly between 0 and 1.
    /// </summary>
    public static double InverseCdf(double probability)
    {
        Debug.Assert(probability > 0 && probability < 1, "The probability must be strictly between 0 and 1.");

        if (probability < LowerBreakpoint)
        {
            return Tail(Math.Sqrt(-2 * Math.Log(probability)));
        }

        if (probability > 1 - LowerBreakpoint)
        {
            return -Tail(Math.Sqrt(-2 * Math.Log(1 - probability)));
        }

        var q = probability - 0.5;
        var r = q * q;

        return (((((A[0] * r + A[1]) * r + A[2]) * r + A[3]) * r + A[4]) * r + A[5]) * q
            / (((((B[0] * r + B[1]) * r + B[2]) * r + B[3]) * r + B[4]) * r + 1);

        static double Tail(double q) =>
            (((((C[0] * q + C[1]) * q + C[2]) * q + C[3]) * q + C[4]) * q + C[5])
            / ((((D[0] * q + D[1]) * q + D[2]) * q + D[3]) * q + 1);
    }
}
