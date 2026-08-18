using QuickCheck.Choices;

namespace QuickCheck.Running;

/// <summary>
/// The check loop behind <see cref="Property{T}"/>: generates examples, runs the body on each, and
/// shrinks the first failure.
/// </summary>
internal sealed class PropertyRunner<T>
{
    private readonly Generator<T> _generator;
    private readonly Func<T, bool> _body;

    public PropertyRunner(Generator<T> generator, Func<T, bool> body)
    {
        _generator = generator;
        _body = body;
    }

    public PropertyResult<T> Check(CheckOptions options)
    {
        if (options.Replay is { } replay)
        {
            return CheckSingle(replay, options);
        }

        var seed = options.Seed ?? (ulong)Random.Shared.NextInt64();
        var maxDiscards = checked(options.RunCount * options.MaxDiscardRatio);
        var passed = 0;
        var discards = 0;

        for (var run = 0; passed < options.RunCount; run++)
        {
            var source = ChoiceSource.FromRandom(Xoshiro256StarStar.ForRun(seed, run));
            var example = ExampleRun<T>.Execute(source, _generator, _body);

            switch (example.Status)
            {
                case ExampleStatus.Passed:
                    passed++;
                    break;

                case ExampleStatus.Discarded:
                    discards++;

                    if (discards > maxDiscards)
                    {
                        return PropertyResult<T>.Exhausted(seed, passed, discards);
                    }

                    break;

                case ExampleStatus.Failed:
                    return Falsify(example, new Replay(seed, run), passed, discards, options);
            }
        }

        return PropertyResult<T>.Passed(seed, passed, discards);
    }

    private PropertyResult<T> CheckSingle(Replay replay, CheckOptions options)
    {
        var source = ChoiceSource.FromRandom(Xoshiro256StarStar.ForRun(replay.Seed, replay.Run));
        var example = ExampleRun<T>.Execute(source, _generator, _body);

        return example.Status switch
        {
            ExampleStatus.Failed => Falsify(example, replay, testsRun: 0, discards: 0, options),
            ExampleStatus.Passed => PropertyResult<T>.Passed(replay.Seed, testsRun: 1, discards: 0),
            _ => PropertyResult<T>.Exhausted(replay.Seed, testsRun: 0, discards: 1)
        };
    }

    private PropertyResult<T> Falsify(
        ExampleRun<T> failure,
        Replay replay,
        int testsRun,
        int discards,
        CheckOptions options)
    {
        var shrinker = new Shrinker<T>(_generator, _body, failure, options.MaxShrinkAttempts);
        var outcome = shrinker.Run();

        return PropertyResult<T>.Falsified(
            replay.Seed,
            testsRun,
            discards,
            new Counterexample<T>(failure.Value, failure.Exception),
            new Counterexample<T>(outcome.Minimal.Value, outcome.Minimal.Exception),
            replay,
            outcome.Attempts,
            outcome.Shrinks);
    }
}
