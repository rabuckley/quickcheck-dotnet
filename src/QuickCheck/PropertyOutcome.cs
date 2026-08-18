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

    /// <summary>Too many examples were discarded to reach the required run count.</summary>
    Exhausted
}