using QuickCheck.Choices;
using QuickCheck.Running;

namespace QuickCheck;

/// <summary>
/// Represents an assertion that is expected to hold for every value a <see cref="Generator{T}"/>
/// produces.
/// </summary>
/// <typeparam name="T">The type of value the property is checked over.</typeparam>
/// <remarks>
/// Create a property with <see cref="Property.ForAll{T}(Generator{T}, Action{T})"/> or one of its
/// overloads, then check it with <see cref="Assert"/> or <see cref="Check"/>.
/// </remarks>
public sealed class Property<T>
{
    private readonly Generator<T> _generator;
    private readonly Func<T, bool> _body;

    internal Property(Generator<T> generator, Func<T, bool> body)
    {
        _generator = generator;
        _body = body;
    }

    /// <summary>
    /// Checks the property and throws if it does not pass.
    /// </summary>
    /// <param name="options">
    /// The options that control the check, or <see langword="null"/> to use
    /// <see cref="CheckOptions.Default"/>.
    /// </param>
    /// <exception cref="PropertyFailedException">
    /// The property was falsified, or too many examples were discarded. The message reports the
    /// minimal counterexample and how to replay it.
    /// </exception>
    /// <remarks>This method is intended to be called directly from a test method.</remarks>
    public void Assert(CheckOptions? options = null) => Check(options).ThrowIfFailed();

    /// <summary>
    /// Checks the property and returns its outcome instead of throwing.
    /// </summary>
    /// <param name="options">
    /// The options that control the check, or <see langword="null"/> to use
    /// <see cref="CheckOptions.Default"/>.
    /// </param>
    /// <returns>The result of the check, including any counterexample found.</returns>
    public PropertyResult<T> Check(CheckOptions? options = null)
    {
        options ??= CheckOptions.Default;

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
        var minimal = shrinker.Run();

        return PropertyResult<T>.Falsified(
            replay.Seed,
            testsRun,
            discards,
            new Counterexample<T>(failure.Value, failure.Exception),
            new Counterexample<T>(minimal.Value, minimal.Exception),
            replay,
            shrinker.Attempts,
            shrinker.Shrinks);
    }
}
