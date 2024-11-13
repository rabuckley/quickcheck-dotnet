namespace QuickCheck;

/// <summary>
/// The options class used to configure the behavior of a
/// <see cref="QuickChecker"/>.
/// </summary>
public sealed class QuickCheckerOptions
{
    /// <summary>
    /// A read-only singleton instance of the default options.
    /// </summary>
    public static readonly QuickCheckerOptions Default = new();

    /// <summary>
    /// <para>
    /// The number of iterations to run against the target function.
    /// </para>
    /// <para>The default value is 10_000.</para>
    /// </summary>
    public int RunCount { get; set; } = 10_000;

    /// <summary>
    /// <para>
    /// The pseudo-random number generator used for generating random values.
    /// If unset, the default <see cref="Random.Shared"/> instance will be used.
    /// </para>
    /// </summary>
    public Random? Random { get; set; }

    /// <summary>
    /// The maximum size to use when generating collections, strings, and other
    /// data structures with a size. If null, the generators will use their own
    /// defaults.
    /// <remarks>
    /// Note, this value is only applied to the default generators. Custom
    /// generators should be configured as needed before registering with the
    /// <see cref="QuickChecker"/>.
    /// </remarks>
    /// </summary>
    public int? MaximumSize { get; set; } = null;
}
