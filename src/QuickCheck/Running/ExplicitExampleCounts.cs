namespace QuickCheck.Running;

/// <summary>
/// What became of the explicit examples a check was given, carried into whichever result the run
/// reaches so the report can account for every pin.
/// </summary>
/// <param name="Run">The number that were checked and passed.</param>
/// <param name="Discarded">The number an assumption in the body discarded.</param>
internal readonly record struct ExplicitExampleCounts(int Run, int Discarded)
{
    public static ExplicitExampleCounts None => default;
}
