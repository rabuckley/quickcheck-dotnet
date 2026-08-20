using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace QuickCheck;

/// <summary>
/// Represents the result of checking a <see cref="Property{T}"/> or an <see cref="AsyncProperty{T}"/>.
/// </summary>
/// <typeparam name="T">The type of value the property was checked over.</typeparam>
public sealed class PropertyResult<T>
{
    private PropertyResult()
    {
    }

    internal static PropertyResult<T> Passed(ulong seed, int testsRun, int discards) => new()
    {
        Outcome = PropertyOutcome.Passed,
        Seed = seed,
        TestsRun = testsRun,
        Discards = discards
    };

    internal static PropertyResult<T> Exhausted(ulong seed, int testsRun, int discards) => new()
    {
        Outcome = PropertyOutcome.Exhausted,
        Seed = seed,
        TestsRun = testsRun,
        Discards = discards
    };

    internal static PropertyResult<T> Falsified(
        ulong seed,
        int testsRun,
        int discards,
        Counterexample<T> original,
        Counterexample<T> minimal,
        Replay replay,
        int shrinkAttempts,
        int shrinks,
        ShrinkLimit shrinkLimit) => new()
    {
        Outcome = PropertyOutcome.Falsified,
        Seed = seed,
        TestsRun = testsRun,
        Discards = discards,
        Original = original,
        Minimal = minimal,
        Replay = replay,
        ShrinkAttempts = shrinkAttempts,
        Shrinks = shrinks,
        ShrinkLimit = shrinkLimit
    };

    /// <summary>Gets the outcome of the check.</summary>
    public required PropertyOutcome Outcome { get; init; }

    /// <summary>Gets the seed the examples were generated from.</summary>
    public required ulong Seed { get; init; }

    /// <summary>
    /// Gets the number of examples that passed, or, when the property was falsified, the number that
    /// passed before the failing one.
    /// </summary>
    public required int TestsRun { get; init; }

    /// <summary>Gets the number of examples discarded by assumptions and filters.</summary>
    public required int Discards { get; init; }

    /// <summary>
    /// Gets the first falsifying example found, before shrinking, or <see langword="null"/> if the
    /// property was not falsified.
    /// </summary>
    public Counterexample<T>? Original { get; init; }

    /// <summary>
    /// Gets the smallest falsifying example the shrinker found, which is <see cref="Original"/> if it
    /// could not be shrunk, or <see langword="null"/> if the property was not falsified.
    /// </summary>
    public Counterexample<T>? Minimal { get; init; }

    /// <summary>
    /// Gets the token that reproduces the failing example through <see cref="CheckOptions.Replay"/>, or
    /// <see langword="null"/> if the property was not falsified.
    /// </summary>
    public Replay? Replay { get; init; }

    /// <summary>Gets the number of candidate examples the shrinker evaluated.</summary>
    public int ShrinkAttempts { get; init; }

    /// <summary>Gets the number of times the shrinker found a smaller failing example.</summary>
    public int Shrinks { get; init; }

    /// <summary>
    /// Gets the budget limit that ended shrinking before it converged, or
    /// <see cref="QuickCheck.ShrinkLimit.None"/> when shrinking ran until no candidate improved (or
    /// the property was not falsified). When a limit was reached, <see cref="Minimal"/> may not be
    /// the smallest example the shrinker could have found.
    /// </summary>
    public ShrinkLimit ShrinkLimit { get; init; }

    /// <summary>Gets a value indicating whether an example falsified the property.</summary>
    [MemberNotNullWhen(true, nameof(Original), nameof(Minimal), nameof(Replay))]
    public bool IsFalsified => Outcome is PropertyOutcome.Falsified;

    /// <summary>
    /// Throws a <see cref="PropertyFailedException"/> if the property was falsified or the check was
    /// exhausted.
    /// </summary>
    /// <exception cref="PropertyFailedException">
    /// <see cref="Outcome"/> is not <see cref="PropertyOutcome.Passed"/>.
    /// </exception>
    public void ThrowIfFailed() => ThrowIfFailed(replayHint: null);

    /// <summary>
    /// Throws a <see cref="PropertyFailedException"/> whose report replaces the replay
    /// instruction, if the property was falsified or the check was exhausted.
    /// </summary>
    /// <param name="replayHint">
    /// The instruction the report gives for reproducing a failure; see
    /// <see cref="ToString(string?)"/>.
    /// </param>
    /// <exception cref="PropertyFailedException">
    /// <see cref="Outcome"/> is not <see cref="PropertyOutcome.Passed"/>.
    /// </exception>
    public void ThrowIfFailed(string? replayHint)
    {
        if (Outcome is not PropertyOutcome.Passed)
        {
            throw new PropertyFailedException(ToString(replayHint), Minimal?.Exception);
        }
    }

    /// <summary>Returns a report of the check.</summary>
    /// <returns>
    /// A human-readable report of the result, including any falsifying example and how to replay it.
    /// </returns>
    public override string ToString() => ToString(replayHint: null);

    /// <summary>Returns a report of the check that replaces the replay instruction.</summary>
    /// <param name="replayHint">
    /// The instruction the report gives for reproducing a failure, for a caller that runs
    /// properties by some means other than <see cref="CheckOptions"/> — a test framework adapter,
    /// say, where the seed belongs in an attribute on the test method. When <see langword="null"/>,
    /// the report shows the <see cref="CheckOptions.Replay"/> snippet.
    /// </param>
    /// <returns>
    /// A human-readable report of the result, including any falsifying example and how to replay it.
    /// </returns>
    public string ToString(string? replayHint)
    {
        var report = new StringBuilder();

        switch (Outcome)
        {
            case PropertyOutcome.Passed:
                report.Append($"Passed {TestsRun} tests");
                AppendDiscards(report);
                report.Append($" (seed {Seed}).");
                break;

            case PropertyOutcome.Exhausted:
                report.Append($"Gave up after {TestsRun} tests");
                AppendDiscards(report);

                report.Append($" (seed {Seed}). Too many examples were discarded; ")
                    .Append("prefer generators that only produce valid inputs over Assume/Where.");

                break;

            case PropertyOutcome.Falsified:
                report.Append($"Falsified after {TestsRun + 1} tests and {Shrinks} shrinks (seed {Seed}).");
                report.AppendLine();
                report.Append("  Minimal counterexample: ").Append(Minimal);
                AppendException(report, Minimal!.Exception);

                if (Shrinks > 0)
                {
                    report.AppendLine();
                    report.Append("  Original counterexample: ").Append(Original);
                    AppendException(report, Original!.Exception);
                }

                if (ShrinkLimit is not ShrinkLimit.None)
                {
                    report.AppendLine();

                    report.Append("  Shrinking stopped early: ")
                        .Append(ShrinkLimit is ShrinkLimit.Attempts ? "MaxShrinkAttempts" : "MaxShrinkWork")
                        .Append(" ran out, so a smaller counterexample may exist.");
                }

                report.AppendLine();

                report.Append("  Replay with: ")
                    .Append(replayHint ?? $"new CheckOptions {{ Replay = Replay.Parse(\"{Replay}\") }}");

                break;
        }

        return report.ToString();

        void AppendDiscards(StringBuilder builder)
        {
            if (Discards > 0)
            {
                builder.Append($" with {Discards} discards");
            }
        }

        static void AppendException(StringBuilder builder, Exception? exception)
        {
            if (exception is null)
            {
                return;
            }

            builder.AppendLine();

            builder.Append("    threw ")
                .Append(exception.GetType().FullName)
                .Append(": ")
                .Append(exception.Message);
        }
    }
}
