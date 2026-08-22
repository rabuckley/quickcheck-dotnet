namespace QuickCheck;

/// <summary>
/// Represents a requirement stated with <see cref="Property.Cover"/>, and how the passed examples
/// of a check measured up to it.
/// </summary>
/// <param name="Label">The label the requirement is about.</param>
/// <param name="MinimumPercent">
/// The minimum percentage of passed examples required to hit <paramref name="Label"/>; the largest
/// when several calls stated a minimum for the same label.
/// </param>
/// <param name="Count">The number of passed examples that hit <paramref name="Label"/>.</param>
/// <param name="IsMet">
/// Whether <paramref name="Count"/> reaches <paramref name="MinimumPercent"/> of the passed
/// examples; a minimum of 100 requires every example to hit the label.
/// </param>
public sealed record CoverageRequirement(string Label, double MinimumPercent, int Count, bool IsMet);
