using QuickCheck.Running;

namespace QuickCheck;

/// <summary>
/// Represents an assertion that is expected to hold for every value a <see cref="Generator{T}"/>
/// produces, and for every value pinned with <see cref="Example"/>.
/// </summary>
/// <typeparam name="T">The type of value the property is checked over.</typeparam>
/// <remarks>
/// Create a property with <see cref="Property.ForAll{T}(Generator{T}, Action{T})"/> or one of its
/// overloads, then check it with <see cref="Assert"/> or <see cref="Check"/>.
/// </remarks>
public sealed class Property<T>
{
    private readonly PropertyRunner<T> _runner;
    private readonly T[] _examples;

    internal Property(Generator<T> generator, Func<T, bool> body)
    {
        _runner = new PropertyRunner<T>(generator, value => new ValueTask<bool>(body(value)));
        _examples = [];
    }

    private Property(PropertyRunner<T> runner, T[] examples)
    {
        _runner = runner;
        _examples = examples;
    }

    /// <summary>
    /// Returns a property that also checks <paramref name="value"/>, whatever the generator
    /// produces.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>
    /// A new property with <paramref name="value"/> added to the end of its explicit examples; this
    /// property is unchanged.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Explicit examples are checked first, in the order they were added, before anything is
    /// generated, and the first one to fail ends the check. They are checked on top of
    /// <see cref="CheckOptions.RunCount"/> rather than out of it, they contribute nothing to the
    /// <see cref="Property.Classify"/> statistics, and one the body discards with
    /// <see cref="Property.Assume"/> is skipped and reported in the check's report.
    /// </para>
    /// <para>
    /// A failing explicit example is reported as it was given: there are no choices behind a value
    /// the caller supplied, so there is nothing for the shrinker to reduce and no
    /// <see cref="PropertyResult{T}.Replay"/> token that reproduces it. That is the point of a pin.
    /// A replay token means "example number n of the stream seeded by s", so it drifts to a
    /// different input as soon as a generator changes shape; a pinned value keeps testing the
    /// input the failure was found on.
    /// </para>
    /// <para>
    /// Nothing checks <paramref name="value"/> against the generator, so a pin may be a value the
    /// generator's range excludes. A property with pinned examples cannot be replayed: because
    /// <see cref="CheckOptions.Replay"/> checks only the example its token names,
    /// <see cref="Check"/> throws rather than leave the pins unchecked.
    /// </para>
    /// </remarks>
    public Property<T> Example(T value) => new(_runner, [.. _examples, value]);

    /// <summary>
    /// Checks the property and throws if it does not pass.
    /// </summary>
    /// <param name="options">
    /// The options that control the check, or <see langword="null"/> to use
    /// <see cref="CheckOptions.Default"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// The token that aborts the check between examples and between shrink attempts. The body does
    /// not receive it, so a long-running body runs to completion; a body that throws
    /// <see cref="OperationCanceledException"/> while this token is cancelled aborts the check
    /// rather than being recorded as a counterexample.
    /// </param>
    /// <exception cref="PropertyFailedException">
    /// The property was falsified, too many examples were discarded, or a coverage requirement was
    /// not met. The message is the report of the check; a property falsified by a generated example
    /// reports the minimal counterexample and how to replay it, and one falsified by an explicit
    /// example reports that example as it was given.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="options"/> sets <see cref="CheckOptions.Replay"/> and the property has
    /// examples pinned with <see cref="Example"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled.
    /// </exception>
    /// <remarks>This method is intended to be called directly from a test method.</remarks>
    public void Assert(CheckOptions? options = null, CancellationToken cancellationToken = default) =>
        Check(options, cancellationToken).ThrowIfFailed();

    /// <summary>
    /// Checks the property and returns its outcome instead of throwing.
    /// </summary>
    /// <param name="options">
    /// The options that control the check, or <see langword="null"/> to use
    /// <see cref="CheckOptions.Default"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// The token that aborts the check between examples and between shrink attempts. The body does
    /// not receive it, so a long-running body runs to completion; a body that throws
    /// <see cref="OperationCanceledException"/> while this token is cancelled aborts the check
    /// rather than being recorded as a counterexample.
    /// </param>
    /// <returns>The result of the check, including any counterexample found.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="options"/> sets <see cref="CheckOptions.Replay"/> and the property has
    /// examples pinned with <see cref="Example"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled.
    /// </exception>
    public PropertyResult<T> Check(
        CheckOptions? options = null, CancellationToken cancellationToken = default) =>
        _runner.Check(options ?? CheckOptions.Default, _examples, cancellationToken);
}
