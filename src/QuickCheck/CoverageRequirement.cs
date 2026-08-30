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
/// Whether the check considers the requirement met. By default, whether <paramref name="Count"/>
/// reaches <paramref name="MinimumPercent"/> of the passed examples; a minimum of 100 requires
/// every example to hit the label, and an unmet requirement prints as
/// <c>Only x% label, but required y%</c> on a <see cref="PropertyOutcome.Passed"/> result. Under
/// <see cref="CheckOptions.CoverageConfidence"/>, a requirement is met unless the check has found
/// the rate short of the minimum to the stated confidence, so every requirement of a
/// <see cref="PropertyOutcome.Passed"/> result is met. A replayed example checks nothing, so every
/// requirement of a <see cref="CheckOptions.Replay"/> result is met.
/// </param>
public sealed record CoverageRequirement(string Label, double MinimumPercent, int Count, bool IsMet);
