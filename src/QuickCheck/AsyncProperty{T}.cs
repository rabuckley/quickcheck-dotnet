using QuickCheck.Running;

namespace QuickCheck;

/// <summary>
/// Represents an assertion, checked by an asynchronous body, that is expected to hold for every
/// value a <see cref="Generator{T}"/> produces.
/// </summary>
/// <typeparam name="T">The type of value the property is checked over.</typeparam>
/// <remarks>
/// Create a property with <see cref="Property.ForAll{T}(Generator{T}, Func{T, Task})"/> or one of
/// its overloads, then check it with <see cref="AssertAsync"/> or <see cref="CheckAsync"/>. Examples
/// run one at a time, each body invocation awaited before the next example is generated, so the
/// sequence the shrinker sees stays deterministic.
/// </remarks>
public sealed class AsyncProperty<T>
{
    private readonly PropertyRunner<T> _runner;

    internal AsyncProperty(Generator<T> generator, Func<T, ValueTask<bool>> body)
    {
        _runner = new PropertyRunner<T>(generator, body);
    }

    /// <summary>
    /// Checks the property and throws if it does not pass.
    /// </summary>
    /// <param name="options">
    /// The options that control the check, or <see langword="null"/> to use
    /// <see cref="CheckOptions.Default"/>.
    /// </param>
    /// <returns>A task that completes when the check has finished.</returns>
    /// <exception cref="PropertyFailedException">
    /// The property was falsified, or too many examples were discarded. The message reports the
    /// minimal counterexample and how to replay it.
    /// </exception>
    /// <remarks>This method is intended to be awaited directly from a test method.</remarks>
    public async Task AssertAsync(CheckOptions? options = null) =>
        (await CheckAsync(options).ConfigureAwait(false)).ThrowIfFailed();

    /// <summary>
    /// Checks the property and returns its outcome instead of throwing.
    /// </summary>
    /// <param name="options">
    /// The options that control the check, or <see langword="null"/> to use
    /// <see cref="CheckOptions.Default"/>.
    /// </param>
    /// <returns>The result of the check, including any counterexample found.</returns>
    public Task<PropertyResult<T>> CheckAsync(CheckOptions? options = null) =>
        _runner.CheckAsync(options ?? CheckOptions.Default).AsTask();
}
