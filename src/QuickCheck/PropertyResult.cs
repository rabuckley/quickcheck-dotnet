namespace QuickCheck;

/// <summary>
/// Creates the <see cref="PropertyResult{T}"/> for each way a check can end.
/// </summary>
internal static class PropertyResult
{
    public static PropertyResult<T> Passed<T>(
        ulong seed,
        int testsRun,
        int discards,
        PropertyStatistics statistics) => new()
    {
        Outcome = PropertyOutcome.Passed,
        Seed = seed,
        TestsRun = testsRun,
        Discards = discards,
        Statistics = statistics
    };

    public static PropertyResult<T> Exhausted<T>(
        ulong seed,
        int testsRun,
        int discards,
        PropertyStatistics statistics) => new()
    {
        Outcome = PropertyOutcome.Exhausted,
        Seed = seed,
        TestsRun = testsRun,
        Discards = discards,
        Statistics = statistics
    };

    public static PropertyResult<T> InsufficientCoverage<T>(
        ulong seed,
        int testsRun,
        int discards,
        PropertyStatistics statistics) => new()
    {
        Outcome = PropertyOutcome.InsufficientCoverage,
        Seed = seed,
        TestsRun = testsRun,
        Discards = discards,
        Statistics = statistics
    };

    public static PropertyResult<T> Falsified<T>(
        ulong seed,
        int testsRun,
        int discards,
        Counterexample<T> original,
        Counterexample<T> minimal,
        Replay replay,
        int shrinkAttempts,
        int shrinks,
        ShrinkLimit shrinkLimit,
        PropertyStatistics statistics) => new()
    {
        Outcome = PropertyOutcome.Falsified,
        Seed = seed,
        TestsRun = testsRun,
        Discards = discards,
        Statistics = statistics,
        Original = original,
        Minimal = minimal,
        Replay = replay,
        ShrinkAttempts = shrinkAttempts,
        Shrinks = shrinks,
        ShrinkLimit = shrinkLimit
    };
}
