using System.Diagnostics;
using QuickCheck.Choices;

namespace QuickCheck.Running;

/// <summary>
/// The check loop shared by <see cref="Property{T}"/> and <see cref="AsyncProperty{T}"/>: drains
/// the explicit examples, then generates examples, runs the body on each, and shrinks the first
/// generated failure. It is written against an asynchronous body so that a synchronous property
/// completes without ever yielding.
/// </summary>
internal sealed class PropertyRunner<T>
{
    private readonly Generator<T> _generator;
    private readonly Func<T, ValueTask<bool>> _body;

    public PropertyRunner(Generator<T> generator, Func<T, ValueTask<bool>> body)
    {
        _generator = generator;
        _body = body;
    }

    /// <summary>
    /// Runs the check to completion without yielding, for a body that completes synchronously.
    /// </summary>
    /// <remarks>
    /// The loop awaits nothing but the body, so a body that completes synchronously leaves the whole
    /// check complete by the time <see cref="CheckAsync"/> returns.
    /// </remarks>
    public PropertyResult<T> Check(
        CheckOptions options, IReadOnlyList<T> examples, CancellationToken cancellationToken)
    {
        var check = CheckAsync(options, examples, cancellationToken);

        if (!check.IsCompleted)
        {
            throw new UnreachableException("A synchronous property check yielded.");
        }

        return check.GetAwaiter().GetResult();
    }

    /// <exception cref="ArgumentException">
    /// <paramref name="options"/> sets <see cref="CheckOptions.Replay"/> and
    /// <paramref name="examples"/> is not empty.
    /// </exception>
    public ValueTask<PropertyResult<T>> CheckAsync(
        CheckOptions options, IReadOnlyList<T> examples, CancellationToken cancellationToken)
    {
        if (options.Replay is not null && examples.Count > 0)
        {
            throw new ArgumentException(
                "Replay checks only the example its token names, so the property's pinned examples "
                + "would never be checked. Keep one or the other.",
                nameof(options));
        }

        return RunAsync(options, examples, cancellationToken);
    }

    private async ValueTask<PropertyResult<T>> RunAsync(
        CheckOptions options, IReadOnlyList<T> examples, CancellationToken cancellationToken)
    {
        if (options.Replay is { } replay)
        {
            return await CheckSingleAsync(replay, options, cancellationToken).ConfigureAwait(false);
        }

        var seed = options.Seed ?? (ulong)Random.Shared.NextInt64();
        var confidence = options.CoverageConfidence;
        var passed = 0;
        var discards = 0;
        var looks = 0;
        var explicitExamples = ExplicitExampleCounts.None;
        var statistics = new RunStatistics();

        foreach (var value in examples)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pinned = await ExampleRun.ExecuteAsync(value, _body, cancellationToken).ConfigureAwait(false);

            switch (pinned.Status)
            {
                case ExampleStatus.Passed:
                    // Counted apart from `passed`, so a pin neither shortens the generated run nor
                    // contributes to the percentages, whose denominator is TestsRun.
                    explicitExamples = explicitExamples with { Run = explicitExamples.Run + 1 };
                    break;

                case ExampleStatus.Discarded:
                    explicitExamples = explicitExamples with { Discarded = explicitExamples.Discarded + 1 };
                    break;

                case ExampleStatus.Failed:
                    // Built here rather than through FalsifyAsync: an explicit example carries no
                    // choices, so there is nothing for the shrinker to reduce.
                    return PropertyResult.FalsifiedByExample(
                        seed,
                        explicitExamples,
                        new Counterexample<T>(pinned.Value, pinned.Exception, isExplicit: true));
            }
        }

        // With a coverage confidence the loop has no fixed length: it ends when the coverage is
        // decided, and is bounded by cancellation rather than by the counters.
        for (var run = 0;; run++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var source = ChoiceSource.FromRandom(Xoshiro256StarStar.ForRun(seed, run));

            var example = await ExampleRun.ExecuteAsync(source, _generator, _body, cancellationToken)
                .ConfigureAwait(false);

            switch (example.Status)
            {
                case ExampleStatus.Passed:
                    passed++;
                    statistics.Merge(example.Statistics!);

                    if (confidence is null)
                    {
                        if (passed == options.RunCount)
                        {
                            return PropertyResult.Passed<T>(seed, passed, discards, explicitExamples, Snapshot());
                        }

                        break;
                    }

                    if (!CoverageLook.IsDue(passed, options.RunCount))
                    {
                        break;
                    }

                    var look = new CoverageLook(confidence, passed, looks++);

                    var verdicts = statistics.CoverRequirements
                        .Select(requirement => look.Verdict(requirement.MinimumPercent, requirement.Count))
                        .ToArray();

                    // A rate known to lie within the tolerance is met, so a requirement the check
                    // has settled never fails it; a shortfall ends the check before RunCount, but a
                    // check whose requirements are all met still waits for it.
                    if (passed >= options.RunCount
                        && Array.TrueForAll(verdicts, static verdict => verdict == CoverageVerdict.Met))
                    {
                        return PropertyResult.Passed<T>(
                            seed, passed, discards, explicitExamples, statistics.ToPropertyStatistics(look));
                    }

                    if (Array.Exists(verdicts, static verdict => verdict == CoverageVerdict.Unmet))
                    {
                        return PropertyResult.InsufficientCoverage<T>(
                            seed, passed, discards, explicitExamples, statistics.ToPropertyStatistics(look));
                    }

                    break;

                case ExampleStatus.Discarded:
                    discards++;

                    // The budget grows with the run so that a long coverage check is not exhausted
                    // by a discard rate the RunCount examples would have tolerated.
                    if (discards > (long)options.MaxDiscardRatio * Math.Max(passed, options.RunCount))
                    {
                        return PropertyResult.Exhausted<T>(seed, passed, discards, explicitExamples, Snapshot());
                    }

                    break;

                case ExampleStatus.Failed:
                    return await FalsifyAsync(
                        example,
                        new Replay(seed, run),
                        passed,
                        discards,
                        explicitExamples,
                        Snapshot(),
                        options,
                        cancellationToken).ConfigureAwait(false);
            }
        }

        // Between looks a requirement is held to the standard of the look that would come next.
        PropertyStatistics Snapshot() => confidence is null
            ? statistics.ToPropertyStatistics(passed)
            : statistics.ToPropertyStatistics(new CoverageLook(confidence, passed, looks));
    }

    private async ValueTask<PropertyResult<T>> CheckSingleAsync(
        Replay replay, CheckOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var source = ChoiceSource.FromRandom(Xoshiro256StarStar.ForRun(replay.Seed, replay.Run));

        var example = await ExampleRun.ExecuteAsync(source, _generator, _body, cancellationToken)
            .ConfigureAwait(false);

        // A replayed example reports its labels but is never held to a coverage requirement: one
        // example cannot meet a distribution.
        return example.Status switch
        {
            ExampleStatus.Failed => await FalsifyAsync(
                example,
                replay,
                testsRun: 0,
                discards: 0,
                ExplicitExampleCounts.None,
                PropertyStatistics.Empty,
                options,
                cancellationToken).ConfigureAwait(false),
            ExampleStatus.Passed => PropertyResult.Passed<T>(
                replay.Seed,
                testsRun: 1,
                discards: 0,
                ExplicitExampleCounts.None,
                SingleExampleStatistics(example.Statistics!)),
            _ => PropertyResult.Exhausted<T>(
                replay.Seed, testsRun: 0, discards: 1, ExplicitExampleCounts.None, PropertyStatistics.Empty)
        };

        static PropertyStatistics SingleExampleStatistics(ExampleStatistics example)
        {
            var statistics = new RunStatistics();
            statistics.Merge(example);
            return statistics.ToPropertyStatistics();
        }
    }

    private async ValueTask<PropertyResult<T>> FalsifyAsync(
        ExampleRun<T> failure,
        Replay replay,
        int testsRun,
        int discards,
        ExplicitExampleCounts explicitExamples,
        PropertyStatistics statistics,
        CheckOptions options,
        CancellationToken cancellationToken)
    {
        var shrinker = new Shrinker<T>(_generator, _body, failure, options, cancellationToken);
        var outcome = await shrinker.RunAsync().ConfigureAwait(false);

        return PropertyResult.Falsified(
            replay.Seed,
            testsRun,
            discards,
            explicitExamples,
            new Counterexample<T>(failure.Value, failure.Exception, isExplicit: false),
            new Counterexample<T>(outcome.Minimal.Value, outcome.Minimal.Exception, isExplicit: false),
            replay,
            outcome.Attempts,
            outcome.Shrinks,
            outcome.Limit,
            statistics);
    }
}
