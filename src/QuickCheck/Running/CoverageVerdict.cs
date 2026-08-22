namespace QuickCheck.Running;

/// <summary>
/// What one <see cref="CoverageLook"/> decides about one <see cref="Property.Cover"/> requirement.
/// </summary>
internal enum CoverageVerdict
{
    /// <summary>The interval spans the minimum; more examples are needed.</summary>
    Undecided,

    /// <summary>The rate is known to reach <see cref="Confidence.Tolerance"/> times the minimum.</summary>
    Met,

    /// <summary>The rate is known to fall short of the minimum, and is not known to be within tolerance.</summary>
    Unmet
}
