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
    private readonly PropertyRunner<T> _runner;

    internal Property(Generator<T> generator, Func<T, bool> body)
    {
        _runner = new PropertyRunner<T>(generator, value => new ValueTask<bool>(body(value)));
    }

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
    /// The property was falsified, or too many examples were discarded. The message reports the
    /// minimal counterexample and how to replay it.
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
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled.
    /// </exception>
    public PropertyResult<T> Check(
        CheckOptions? options = null, CancellationToken cancellationToken = default) =>
        _runner.Check(options ?? CheckOptions.Default, cancellationToken);
}
