using System.Diagnostics;
using QuickCheck.Choices;

namespace QuickCheck.Running;

/// <summary>
/// The check loop shared by <see cref="Property{T}"/> and <see cref="AsyncProperty{T}"/>:
/// generates examples, runs the body on each, and shrinks the first failure. It is written against
/// an asynchronous body so that a synchronous property completes without ever yielding.
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
    public PropertyResult<T> Check(CheckOptions options, CancellationToken cancellationToken)
    {
        var check = CheckAsync(options, cancellationToken);

        if (!check.IsCompleted)
        {
            throw new UnreachableException("A synchronous property check yielded.");
        }

        return check.GetAwaiter().GetResult();
    }

    public async ValueTask<PropertyResult<T>> CheckAsync(CheckOptions options, CancellationToken cancellationToken)
    {
        if (options.Replay is { } replay)
        {
            return await CheckSingleAsync(replay, options, cancellationToken).ConfigureAwait(false);
        }

        var seed = options.Seed ?? (ulong)Random.Shared.NextInt64();
        var maxDiscards = checked(options.RunCount * options.MaxDiscardRatio);
        var passed = 0;
        var discards = 0;
        var statistics = new RunStatistics();

        for (var run = 0; passed < options.RunCount; run++)
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
                    break;

                case ExampleStatus.Discarded:
                    discards++;

                    if (discards > maxDiscards)
                    {
                        return PropertyResult.Exhausted<T>(
                            seed, passed, discards, statistics.ToPropertyStatistics(passed));
                    }

                    break;

                case ExampleStatus.Failed:
                    return await FalsifyAsync(
                        example,
                        new Replay(seed, run),
                        passed,
                        discards,
                        statistics.ToPropertyStatistics(passed),
                        options,
                        cancellationToken).ConfigureAwait(false);
            }
        }

        var snapshot = statistics.ToPropertyStatistics(passed);

        return snapshot.Coverage.All(static requirement => requirement.IsMet)
            ? PropertyResult.Passed<T>(seed, passed, discards, snapshot)
            : PropertyResult.InsufficientCoverage<T>(seed, passed, discards, snapshot);
    }

    private async ValueTask<PropertyResult<T>> CheckSingleAsync(
        Replay replay,
        CheckOptions options,
        CancellationToken cancellationToken)
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
                PropertyStatistics.Empty,
                options,
                cancellationToken).ConfigureAwait(false),
            ExampleStatus.Passed => PropertyResult.Passed<T>(
                replay.Seed, testsRun: 1, discards: 0, SingleExampleStatistics(example.Statistics!)),
            _ => PropertyResult.Exhausted<T>(replay.Seed, testsRun: 0, discards: 1, PropertyStatistics.Empty)
        };

        static PropertyStatistics SingleExampleStatistics(ExampleStatistics example)
        {
            var statistics = new RunStatistics();
            statistics.Merge(example);
            return statistics.ToPropertyStatistics(testsRun: 1);
        }
    }

    private async ValueTask<PropertyResult<T>> FalsifyAsync(
        ExampleRun<T> failure,
        Replay replay,
        int testsRun,
        int discards,
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
            new Counterexample<T>(failure.Value, failure.Exception),
            new Counterexample<T>(outcome.Minimal.Value, outcome.Minimal.Exception),
            replay,
            outcome.Attempts,
            outcome.Shrinks,
            outcome.Limit,
            statistics);
    }
}
