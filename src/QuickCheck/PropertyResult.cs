using QuickCheck.Running;

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
        ExplicitExampleCounts explicitExamples,
        PropertyStatistics statistics) => new()
    {
        Outcome = PropertyOutcome.Passed,
        Seed = seed,
        TestsRun = testsRun,
        Discards = discards,
        ExplicitExamplesRun = explicitExamples.Run,
        ExplicitExamplesDiscarded = explicitExamples.Discarded,
        Statistics = statistics
    };

    public static PropertyResult<T> Exhausted<T>(
        ulong seed,
        int testsRun,
        int discards,
        ExplicitExampleCounts explicitExamples,
        PropertyStatistics statistics) => new()
    {
        Outcome = PropertyOutcome.Exhausted,
        Seed = seed,
        TestsRun = testsRun,
        Discards = discards,
        ExplicitExamplesRun = explicitExamples.Run,
        ExplicitExamplesDiscarded = explicitExamples.Discarded,
        Statistics = statistics
    };

    /// <summary>
    /// The result of a generator throwing on a freshly drawn choice sequence, which ends the check
    /// without the property being checked on that example. It carries a
    /// <see cref="PropertyResult{T}.Replay"/> because the seed and run number that drew the
    /// sequence draw it again, and no counterexample because no value was produced.
    /// </summary>
    public static PropertyResult<T> GenerationFailed<T>(
        ulong seed,
        int testsRun,
        int discards,
        ExplicitExampleCounts explicitExamples,
        Replay replay,
        Exception exception,
        PropertyStatistics statistics) => new()
    {
        Outcome = PropertyOutcome.GenerationFailed,
        Seed = seed,
        TestsRun = testsRun,
        Discards = discards,
        ExplicitExamplesRun = explicitExamples.Run,
        ExplicitExamplesDiscarded = explicitExamples.Discarded,
        Statistics = statistics,
        Replay = replay,
        GenerationException = exception
    };

    public static PropertyResult<T> InsufficientCoverage<T>(
        ulong seed,
        int testsRun,
        int discards,
        ExplicitExampleCounts explicitExamples,
        PropertyStatistics statistics) => new()
    {
        Outcome = PropertyOutcome.InsufficientCoverage,
        Seed = seed,
        TestsRun = testsRun,
        Discards = discards,
        ExplicitExamplesRun = explicitExamples.Run,
        ExplicitExamplesDiscarded = explicitExamples.Discarded,
        Statistics = statistics
    };

    public static PropertyResult<T> Falsified<T>(
        ulong seed,
        int testsRun,
        int discards,
        ExplicitExampleCounts explicitExamples,
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
        ExplicitExamplesRun = explicitExamples.Run,
        ExplicitExamplesDiscarded = explicitExamples.Discarded,
        Statistics = statistics,
        Original = original,
        Minimal = minimal,
        Replay = replay,
        ShrinkAttempts = shrinkAttempts,
        Shrinks = shrinks,
        ShrinkLimit = shrinkLimit
    };

    /// <summary>
    /// The result of an explicit example failing, which ends the check before anything is generated.
    /// It carries no <see cref="PropertyResult{T}.Replay"/> because no seed and run number produced
    /// the value, and no shrinks because the value has no choices behind it to reduce.
    /// </summary>
    public static PropertyResult<T> FalsifiedByExample<T>(
        ulong seed,
        ExplicitExampleCounts explicitExamples,
        Counterexample<T> counterexample) => new()
    {
        Outcome = PropertyOutcome.Falsified,
        Seed = seed,
        TestsRun = 0,
        Discards = 0,
        ExplicitExamplesRun = explicitExamples.Run,
        ExplicitExamplesDiscarded = explicitExamples.Discarded,
        Statistics = PropertyStatistics.Empty,
        Original = counterexample,
        Minimal = counterexample
    };
}
