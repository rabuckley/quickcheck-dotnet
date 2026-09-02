using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using QuickCheck.Running;

namespace QuickCheck;

/// <summary>
/// Represents the result of checking a <see cref="Property{T}"/> or an <see cref="AsyncProperty{T}"/>.
/// </summary>
/// <typeparam name="T">The type of value the property was checked over.</typeparam>
public sealed class PropertyResult<T>
{
    internal PropertyResult()
    {
    }

    /// <summary>Gets the outcome of the check.</summary>
    public required PropertyOutcome Outcome { get; init; }

    /// <summary>Gets the seed the examples were generated from.</summary>
    public required ulong Seed { get; init; }

    /// <summary>
    /// Gets the number of examples that passed, or, when the check stopped on an example, the
    /// number that passed before that one.
    /// </summary>
    public required int TestsRun { get; init; }

    /// <summary>Gets the number of examples discarded by assumptions and filters.</summary>
    public required int Discards { get; init; }

    /// <summary>The number of explicit examples that were checked and passed.</summary>
    internal int ExplicitExamplesRun { get; init; }

    /// <summary>
    /// The number of explicit examples discarded by an assumption and so not checked.
    /// </summary>
    internal int ExplicitExamplesDiscarded { get; init; }

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
    /// Gets the token that reproduces the example the check stopped on through
    /// <see cref="CheckOptions.Replay"/>, or <see langword="null"/> when there is no such example.
    /// A check that passed or was exhausted stopped on none, and a pinned counterexample was never
    /// drawn from a seed and run number; see <see cref="Counterexample{T}.IsExplicit"/>.
    /// </summary>
    public Replay? Replay { get; init; }

    /// <summary>Gets the number of candidate examples the shrinker evaluated.</summary>
    public int ShrinkAttempts { get; init; }

    /// <summary>Gets the number of times the shrinker found a smaller failing example.</summary>
    public int Shrinks { get; init; }

    /// <summary>
    /// Gets the budget limit that ended shrinking before it converged, or
    /// <see cref="QuickCheck.ShrinkLimit.None"/> when no limit was reached, including when the
    /// property was not falsified. When a limit was reached, <see cref="Minimal"/> may not be the
    /// smallest example the shrinker could have found.
    /// </summary>
    public ShrinkLimit ShrinkLimit { get; init; }

    /// <summary>
    /// Gets what the passed examples reported through <see cref="Property.Classify"/>,
    /// <see cref="Property.Collect"/> and <see cref="Property.Cover"/>: whatever had accumulated by
    /// the time the check ended, so a falsified or exhausted check carries the statistics of the
    /// examples that passed before it stopped.
    /// </summary>
    public PropertyStatistics Statistics { get; init; } = PropertyStatistics.Empty;

    /// <summary>
    /// Gets the exception a generator threw while producing an example, or <see langword="null"/>
    /// when generation did not fail.
    /// </summary>
    public Exception? GenerationException { get; init; }

    /// <summary>Gets a value indicating whether an example falsified the property.</summary>
    [MemberNotNullWhen(true, nameof(Original), nameof(Minimal))]
    public bool IsFalsified => Outcome is PropertyOutcome.Falsified;

    /// <summary>
    /// Gets a value indicating whether a generator threw while producing an example.
    /// </summary>
    [MemberNotNullWhen(true, nameof(GenerationException))]
    public bool IsGenerationFailed => Outcome is PropertyOutcome.GenerationFailed;

    /// <summary>
    /// Throws a <see cref="PropertyFailedException"/> if <see cref="Outcome"/> is anything other
    /// than <see cref="PropertyOutcome.Passed"/>.
    /// </summary>
    /// <exception cref="PropertyFailedException">
    /// <see cref="Outcome"/> is not <see cref="PropertyOutcome.Passed"/>.
    /// </exception>
    public void ThrowIfFailed() => ThrowIfFailed(replayHint: null);

    /// <summary>
    /// Throws a <see cref="PropertyFailedException"/> whose report replaces the replay
    /// instruction, if <see cref="Outcome"/> is anything other than
    /// <see cref="PropertyOutcome.Passed"/>.
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
            throw new PropertyFailedException(
                ToString(replayHint), Minimal?.Exception ?? GenerationException);
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
                AppendExplicitExamples(report);
                AppendDiscards(report);
                report.Append($" (seed {Seed}).");
                AppendDiscardedExamples(report);
                AppendShortfalls(report);
                AppendStatistics(report);
                break;

            case PropertyOutcome.InsufficientCoverage:
                report.Append($"Insufficient coverage after {TestsRun} tests");
                AppendExplicitExamples(report);
                AppendDiscards(report);
                report.Append($" (seed {Seed}).");
                AppendDiscardedExamples(report);
                AppendShortfalls(report);
                AppendStatistics(report);
                break;

            case PropertyOutcome.Exhausted:
                report.Append($"Gave up after {TestsRun} tests");
                AppendExplicitExamples(report);
                AppendDiscards(report);
                report.Append($" (seed {Seed}). ");
                AppendDiscardRate(report);
                report.Append("prefer generators that only produce valid inputs over Assume/Where.");
                AppendDiscardedExamples(report);

                break;

            // An explicit failure needs its own headline because the generated one counts the tests
            // that led up to the failure, and an explicit example runs before any of them.
            case PropertyOutcome.Falsified when Minimal is { IsExplicit: true }:
                report.Append($"Falsified by an explicit example (seed {Seed}).");
                AppendDiscardedExamples(report);
                report.AppendLine();
                report.Append("  Counterexample: ");
                AppendIndented(report, Minimal!);
                AppendException(report, Minimal!.Exception);
                report.AppendLine();
                report.Append("  An explicit example is checked as given, so it was not shrunk.");
                break;

            case PropertyOutcome.Falsified:
                report.Append($"Falsified after {TestsRun + 1} tests and {Shrinks} shrinks (seed {Seed}).");
                AppendDiscardedExamples(report);
                report.AppendLine();
                report.Append("  Minimal counterexample: ");
                AppendIndented(report, Minimal!);
                AppendException(report, Minimal!.Exception);

                if (Shrinks > 0)
                {
                    report.AppendLine();
                    report.Append("  Original counterexample: ");
                    AppendIndented(report, Original!);
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

            case PropertyOutcome.GenerationFailed:
                report.Append($"A generator failed after {TestsRun} tests");
                AppendExplicitExamples(report);
                AppendDiscards(report);
                report.Append($" (seed {Seed}).");
                AppendDiscardedExamples(report);
                AppendException(report, GenerationException);
                report.AppendLine();

                report.Append("  A generator must return a value or discard the example with ")
                    .Append("Property.Assume or Where; any other exception ends the check.");

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

        void AppendExplicitExamples(StringBuilder builder)
        {
            if (ExplicitExamplesRun > 0)
            {
                builder.Append(" and ").Append(ExplicitExamplesRun).Append(Examples(ExplicitExamplesRun));
            }
        }

        // Called from every branch, not just the passing one, so a pin that has quietly stopped
        // being checked stays visible in the reports someone reads most closely.
        void AppendDiscardedExamples(StringBuilder builder)
        {
            if (ExplicitExamplesDiscarded > 0)
            {
                builder.AppendLine();

                builder.Append("  ")
                    .Append(ExplicitExamplesDiscarded)
                    .Append(Examples(ExplicitExamplesDiscarded))
                    .Append(Was(ExplicitExamplesDiscarded))
                    .Append(" discarded by an assumption and not checked.");
            }
        }

        static string Examples(int count) => count == 1 ? " explicit example" : " explicit examples";

        static string Was(int count) => count == 1 ? " was" : " were";

        void AppendDiscardRate(StringBuilder builder)
        {
            // A replay checks one example, and its discard says nothing about a rate.
            if (Discards <= 1)
            {
                builder.Append("Too many examples were discarded; ");
                return;
            }

            var attempts = TestsRun + Discards;

            // The mean of the same Jeffreys posterior the interval is cut from.
            var mean = (Discards + 0.5) / (attempts + 1);
            var (lower, upper) = JeffreysInterval.Bounds(Discards, attempts);

            builder.Append("About ")
                .Append(FormatRate(mean * 100, attempts))
                .Append("% of examples were discarded (the true rate is ")
                .Append(FormatRate(lower * 100, attempts))
                .Append("% to ")
                .Append(FormatRate(upper * 100, attempts))
                .Append("%); ");
        }

        void AppendShortfalls(StringBuilder builder)
        {
            foreach (var requirement in Statistics.Coverage.Where(static requirement => !requirement.IsMet))
            {
                builder.AppendLine();

                builder.Append("  Only ")
                    .Append(FormatPercent(requirement.Count))
                    .Append("% ")
                    .Append(requirement.Label)
                    .Append(", but required ")
                    .Append(FormatMinimum(requirement.MinimumPercent))
                    .Append('%');

                // The 95% equal-tailed Jeffreys credible interval; see JeffreysInterval.
                var (lower, upper) = JeffreysInterval.Bounds(requirement.Count, TestsRun);

                builder.Append(" (the true rate is ")
                    .Append(FormatRate(lower * 100))
                    .Append("% to ")
                    .Append(FormatRate(upper * 100))
                    .Append("%)");
            }
        }

        // A value that prints on several lines (a command sequence, say) keeps its later lines
        // under the first rather than flush with the headline; an exception message with several
        // lines (an assertion's expected/actual) is indented the same way below.
        static void AppendIndented(StringBuilder builder, Counterexample<T> counterexample) =>
            builder.Append(counterexample.ToString().Replace("\n", "\n    ", StringComparison.Ordinal));

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
                .Append(exception.Message.Replace("\n", "\n    ", StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Appends one line per label, then each <see cref="Property.Collect"/> table under its name,
    /// each ordered by count descending and then label ascending.
    /// </summary>
    private void AppendStatistics(StringBuilder report)
    {
        var requirements = Statistics.Coverage.ToDictionary(
            static requirement => requirement.Label,
            static requirement => requirement.MinimumPercent,
            StringComparer.Ordinal);

        foreach (var (label, count) in InReportOrder(Statistics.Labels))
        {
            report.AppendLine();
            report.Append("  ").Append(FormatPercent(count)).Append("% ").Append(label);

            if (requirements.TryGetValue(label, out var minimumPercent))
            {
                report.Append(" (required ").Append(FormatMinimum(minimumPercent)).Append("%)");
            }
        }

        foreach (var table in Statistics.Tables.OrderBy(static table => table.Key, StringComparer.Ordinal))
        {
            report.AppendLine();
            report.Append("  ").Append(table.Key).Append(':');

            foreach (var (value, count) in InReportOrder(table.Value))
            {
                report.AppendLine();
                report.Append("    ").Append(FormatPercent(count)).Append("% ").Append(value);
            }
        }

        static IEnumerable<KeyValuePair<string, int>> InReportOrder(IReadOnlyDictionary<string, int> counts) =>
            counts.OrderByDescending(static entry => entry.Value).ThenBy(static entry => entry.Key, StringComparer.Ordinal);
    }

    private string FormatPercent(int count) => FormatRate(TestsRun == 0 ? 0 : count * 100.0 / TestsRun);

    private string FormatRate(double percent) => FormatRate(percent, TestsRun);

    // QuickCheck's rule: enough decimals to tell one example from the next, so none up to 100 of
    // whatever the rate is out of, one up to 1000, two up to 10000, and so on.
    private static string FormatRate(double percent, int total)
    {
        var places = total == 0 ? 0 : Math.Max(0, (int)Math.Ceiling(Math.Log10(total) - 2));

        return percent.ToString("F" + places.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    private static string FormatMinimum(double minimumPercent) =>
        minimumPercent.ToString(CultureInfo.InvariantCulture);
}
