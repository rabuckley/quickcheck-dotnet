using System.Collections.ObjectModel;

namespace QuickCheck;

/// <summary>
/// Represents what the examples of a check reported through <see cref="Property.Classify"/>,
/// <see cref="Property.Label"/>, <see cref="Property.Collect"/> and <see cref="Property.Cover"/>.
/// </summary>
/// <remarks>
/// Every count is a number of passed examples: a label or collected value counts at most once per
/// example however often the body reports it, and the examples that were discarded, falsified the
/// property, or were evaluated while shrinking never count. Percentages are therefore of
/// <see cref="PropertyResult{T}.TestsRun"/>. The collections are immutable snapshots keyed with
/// ordinal comparison, and are empty rather than <see langword="null"/> when nothing was reported.
/// </remarks>
public sealed class PropertyStatistics
{
    internal static PropertyStatistics Empty { get; } = new(
        ReadOnlyDictionary<string, int>.Empty,
        ReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>.Empty,
        ReadOnlyCollection<CoverageRequirement>.Empty);

    internal PropertyStatistics(
        IReadOnlyDictionary<string, int> labels,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> tables,
        IReadOnlyList<CoverageRequirement> coverage)
    {
        Labels = labels;
        Tables = tables;
        Coverage = coverage;
    }

    /// <summary>
    /// Gets the number of passed examples that hit each label given to <see cref="Property.Classify"/>,
    /// <see cref="Property.Label"/> or <see cref="Property.Cover"/>. A label registered with a
    /// <see langword="false"/> condition on every example is present with a count of zero.
    /// </summary>
    public IReadOnlyDictionary<string, int> Labels { get; }

    /// <summary>
    /// Gets, for each table name given to <see cref="Property.Collect"/>, the number of passed
    /// examples that collected each value into it. Values are the strings the body passed, verbatim.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Tables { get; }

    /// <summary>
    /// Gets the requirement stated by each label given to <see cref="Property.Cover"/>, with the
    /// largest minimum percentage stated for it, ordered by label.
    /// </summary>
    public IReadOnlyList<CoverageRequirement> Coverage { get; }
}
