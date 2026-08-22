namespace QuickCheck;

/// <summary>
/// Represents a requirement stated with <see cref="Property.Cover"/>, and how the passed examples
/// of a check measured up to it.
/// </summary>
public sealed record CoverageRequirement
{
    private readonly int _testsRun;

    internal CoverageRequirement(string label, double minimumPercent, int count, int testsRun)
    {
        Label = label;
        MinimumPercent = minimumPercent;
        Count = count;
        _testsRun = testsRun;
    }

    /// <summary>Gets the label the requirement is about.</summary>
    public string Label { get; }

    /// <summary>
    /// Gets the minimum percentage of passed examples required to hit <see cref="Label"/>; the
    /// largest when several calls stated a minimum for the same label.
    /// </summary>
    public double MinimumPercent { get; }

    /// <summary>Gets the number of passed examples that hit <see cref="Label"/>.</summary>
    public int Count { get; }

    /// <summary>
    /// Gets <see cref="Count"/> as a percentage of <see cref="PropertyResult{T}.TestsRun"/>, or zero
    /// when no example passed.
    /// </summary>
    public double Percent => _testsRun == 0 ? 0 : Count * 100.0 / _testsRun;

    /// <summary>
    /// Gets a value indicating whether <see cref="Count"/> reaches <see cref="MinimumPercent"/> of
    /// the passed examples; a minimum of 100 requires every example to hit the label.
    /// </summary>
    // Compared without dividing, so that 100 means every example and 0 is met even when no example
    // passed.
    public bool IsMet => Count * 100.0 >= MinimumPercent * _testsRun;
}
