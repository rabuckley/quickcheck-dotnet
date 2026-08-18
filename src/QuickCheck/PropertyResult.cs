using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace QuickCheck;

/// <summary>
/// Represents the result of checking a <see cref="Property{T}"/>.
/// </summary>
/// <typeparam name="T">The type of value the property was checked over.</typeparam>
public sealed class PropertyResult<T>
{
    private PropertyResult(
        PropertyOutcome outcome,
        ulong seed,
        int testsRun,
        int discards,
        Counterexample<T>? original,
        Counterexample<T>? minimal,
        Replay? replay,
        int shrinkAttempts,
        int shrinks)
    {
        Outcome = outcome;
        Seed = seed;
        TestsRun = testsRun;
        Discards = discards;
        Original = original;
        Minimal = minimal;
        Replay = replay;
        ShrinkAttempts = shrinkAttempts;
        Shrinks = shrinks;
    }

    internal static PropertyResult<T> Passed(ulong seed, int testsRun, int discards) =>
        new(PropertyOutcome.Passed, seed, testsRun, discards,
            original: null, minimal: null, replay: null, shrinkAttempts: 0, shrinks: 0);

    internal static PropertyResult<T> Exhausted(ulong seed, int testsRun, int discards) =>
        new(PropertyOutcome.Exhausted, seed, testsRun, discards,
            original: null, minimal: null, replay: null, shrinkAttempts: 0, shrinks: 0);

    internal static PropertyResult<T> Falsified(
        ulong seed,
        int testsRun,
        int discards,
        Counterexample<T> original,
        Counterexample<T> minimal,
        Replay replay,
        int shrinkAttempts,
        int shrinks) =>
        new(PropertyOutcome.Falsified, seed, testsRun, discards,
            original, minimal, replay, shrinkAttempts, shrinks);

    /// <summary>Gets the outcome of the check.</summary>
    public PropertyOutcome Outcome { get; }

    /// <summary>Gets the seed the examples were generated from.</summary>
    public ulong Seed { get; }

    /// <summary>
    /// Gets the number of examples that passed, or, when the property was falsified, the number that
    /// passed before the failing one.
    /// </summary>
    public int TestsRun { get; }

    /// <summary>Gets the number of examples discarded by assumptions and filters.</summary>
    public int Discards { get; }

    /// <summary>
    /// Gets the first falsifying example found, before shrinking, or <see langword="null"/> if the
    /// property was not falsified.
    /// </summary>
    public Counterexample<T>? Original { get; }

    /// <summary>
    /// Gets the smallest falsifying example the shrinker found, which is <see cref="Original"/> if it
    /// could not be shrunk, or <see langword="null"/> if the property was not falsified.
    /// </summary>
    public Counterexample<T>? Minimal { get; }

    /// <summary>
    /// Gets the token that reproduces the failing example through <see cref="CheckOptions.Replay"/>, or
    /// <see langword="null"/> if the property was not falsified.
    /// </summary>
    public Replay? Replay { get; }

    /// <summary>Gets the number of candidate examples the shrinker evaluated.</summary>
    public int ShrinkAttempts { get; }

    /// <summary>Gets the number of times the shrinker found a smaller failing example.</summary>
    public int Shrinks { get; }

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
    public void ThrowIfFailed()
    {
        if (Outcome is not PropertyOutcome.Passed)
        {
            throw new PropertyFailedException(ToString(), Minimal?.Exception);
        }
    }

    /// <summary>Returns a report of the check.</summary>
    /// <returns>
    /// A human-readable report of the result, including any falsifying example and how to replay it.
    /// </returns>
    public override string ToString()
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

                report.AppendLine();
                report.Append($"  Replay with: new CheckOptions {{ Replay = Replay.Parse(\"{Replay}\") }}");
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
            if (exception is not null)
            {
                builder.AppendLine();
                builder.Append("    threw ").Append(exception.GetType().FullName)
                    .Append(": ").Append(exception.Message);
            }
        }
    }
}