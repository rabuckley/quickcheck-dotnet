namespace QuickCheck;

/// <summary>
/// Specifies the outcome of checking a property.
/// </summary>
public enum PropertyOutcome
{
    /// <summary>Every example passed.</summary>
    Passed,

    /// <summary>An example falsified the property.</summary>
    Falsified,

    /// <summary>
    /// Too many examples were discarded to reach the required run count, or the discard rate made
    /// reaching it hopeless.
    /// </summary>
    Exhausted,

    /// <summary>
    /// Every example passed but a <see cref="Property.Cover"/> requirement is known to be missed,
    /// to the certainty of <see cref="CheckOptions.CoverageConfidence"/>. Only that option produces
    /// this outcome; by default an unmet requirement is a warning on a <see cref="Passed"/> result.
    /// </summary>
    InsufficientCoverage,

    /// <summary>
    /// A generator threw while producing an example, so the check ended without the property being
    /// checked on it.
    /// </summary>
    GenerationFailed
}