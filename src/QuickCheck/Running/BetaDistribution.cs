using System.Diagnostics;

namespace QuickCheck.Running;

/// <summary>
/// The cumulative distribution function of the beta distribution (the regularized incomplete beta
/// function) and its quantile function, for turning a posterior over a rate into a credible
/// interval.
/// </summary>
internal static class BetaDistribution
{
    // I_x(a, b) is the continued fraction of DLMF 8.17.22 (https://dlmf.nist.gov/8.17#v)
    // evaluated by the modified Lentz method (Thompson and Barnett, "Coulomb and Bessel functions
    // of complex arguments and order", J. Comput. Phys. 64, 1986,
    // https://doi.org/10.1016/0021-9991(86)90046-X). The fraction converges fast only for x below
    // (a + 1)/(a + b + 2), so above that the symmetry I_x(a, b) = 1 - I_{1-x}(b, a) is applied
    // first. Convergence needs about 0.6 * sqrt(max(a, b)) iterations at worst: 16 at
    // (18.5, 82.5), around 550 at a = b = 10^6.
    //
    // ln B(a, b) = lnGamma(a) + lnGamma(b) - lnGamma(a + b) with the Lanczos approximation,
    // g = 7 and nine coefficients (Godfrey's set, also GSL's lanczos_7_c; see
    // https://en.wikipedia.org/wiki/Lanczos_approximation), relative error below 2e-15 for
    // x >= 1/2, because System.Math has no log-gamma.
    private static readonly double[] Lanczos =
    [
        0.99999999999980993, 676.5203681218851, -1259.1392167224028,
        771.32342877765313, -176.61502916214059, 12.507343278686905,
        -0.13857109526572012, 9.9843695780195716e-6, 1.5056327351493116e-7
    ];

    private const double LanczosG = 7;

    // The Lentz floor: small enough never to disturb a converged fraction, unlike double.Epsilon,
    // whose reciprocal is infinite.
    private const double Tiny = 1e-300;

    // Relative, on the factor the fraction's value is multiplied by each iteration.
    private const double Tolerance = 1e-15;

    // About 20 times the worst case observed, at a = b = 10^6.
    private const int MaxIterations = 10_000;

    /// <summary>
    /// Returns the probability that a Beta(<paramref name="a"/>, <paramref name="b"/>) variable
    /// falls below <paramref name="x"/>, which must be in [0, 1]; exactly 0 at 0 and 1 at 1.
    /// </summary>
    public static double Cdf(double x, double a, double b)
    {
        Debug.Assert(x >= 0 && x <= 1, "x must be between 0 and 1.");
        Debug.Assert(a > 0 && b > 0, "The shape parameters must be positive.");

        if (x is 0 or 1)
        {
            return x;
        }

        return x < (a + 1) / (a + b + 2)
            ? Math.Exp(a * Math.Log(x) + b * Math.Log(1 - x) - LogBeta(a, b)) / a * ContinuedFraction(x, a, b)
            : 1 - Math.Exp(b * Math.Log(1 - x) + a * Math.Log(x) - LogBeta(b, a)) / b * ContinuedFraction(1 - x, b, a);
    }

    /// <summary>
    /// Returns the value below which a Beta(<paramref name="a"/>, <paramref name="b"/>) variable
    /// falls with probability <paramref name="probability"/>, which must be strictly between 0
    /// and 1, to within 1e-12.
    /// </summary>
    public static double Quantile(double probability, double a, double b)
    {
        Debug.Assert(probability > 0 && probability < 1, "The probability must be strictly between 0 and 1.");

        var lower = 0.0;
        var upper = 1.0;

        // Bisection: the Cdf is monotone, and 40 halvings reach the tolerance.
        while (upper - lower > 1e-12)
        {
            var middle = (lower + upper) / 2;

            if (Cdf(middle, a, b) < probability)
            {
                lower = middle;
            }
            else
            {
                upper = middle;
            }
        }

        return (lower + upper) / 2;
    }

    private static double ContinuedFraction(double x, double a, double b)
    {
        var c = 1.0;
        var d = Reciprocal(1 - (a + b) * x / (a + 1));
        var value = d;

        for (var m = 1; m <= MaxIterations; m++)
        {
            var m2 = 2.0 * m;

            // The even-numbered coefficient d_2m, then the odd d_2m+1, of DLMF 8.17.22.
            var even = m * (b - m) * x / ((a - 1 + m2) * (a + m2));
            c = Floor(1 + even / c);
            d = Reciprocal(1 + even * d);
            value *= c * d;

            var odd = -(a + m) * (a + b + m) * x / ((a + m2) * (a + 1 + m2));
            c = Floor(1 + odd / c);
            d = Reciprocal(1 + odd * d);

            var factor = c * d;
            value *= factor;

            if (Math.Abs(factor - 1) < Tolerance)
            {
                return value;
            }
        }

        Debug.Fail($"The continued fraction did not converge for x = {x}, a = {a}, b = {b}.");
        return value;

        static double Floor(double term) => Math.Abs(term) < Tiny ? Tiny : term;

        static double Reciprocal(double term) => 1 / Floor(term);
    }

    private static double LogBeta(double a, double b) => LogGamma(a) + LogGamma(b) - LogGamma(a + b);

    private static double LogGamma(double x)
    {
        Debug.Assert(x >= 0.5, "x must be at least one half, which needs no reflection.");

        var series = Lanczos[0];

        for (var i = 1; i < Lanczos.Length; i++)
        {
            series += Lanczos[i] / (x - 1 + i);
        }

        var t = x - 0.5 + LanczosG;

        return 0.5 * Math.Log(2 * Math.PI) + (x - 0.5) * Math.Log(t) - t + Math.Log(series);
    }
}
