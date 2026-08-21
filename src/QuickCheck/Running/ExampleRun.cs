using QuickCheck.Choices;

namespace QuickCheck.Running;

/// <summary>
/// Runs a property's body on one generated example, recording it as an <see cref="ExampleRun{T}"/>.
/// </summary>
internal static class ExampleRun
{
    /// <summary>
    /// Generates one example and runs <paramref name="body"/> on it.
    /// </summary>
    /// <remarks>
    /// <paramref name="cancellationToken"/> is not passed to the body; it only distinguishes a body
    /// that abandoned the check because that token was cancelled — which propagates — from one that
    /// threw, which becomes a counterexample.
    /// </remarks>
    public static async ValueTask<ExampleRun<T>> ExecuteAsync<T>(
        ChoiceSource source,
        Generator<T> generator,
        Func<T, ValueTask<bool>> body,
        CancellationToken cancellationToken)
    {
        T value;

        try
        {
            value = source.Draw(generator);
        }
        catch (DiscardException)
        {
            return Run(ExampleStatus.Discarded, default!);
        }

        try
        {
            var holds = await body(value).ConfigureAwait(false);

            return Run(holds ? ExampleStatus.Passed : ExampleStatus.Failed, value);
        }
        catch (DiscardException)
        {
            return Run(ExampleStatus.Discarded, value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Run(ExampleStatus.Failed, value, exception);
        }

        ExampleRun<T> Run(ExampleStatus status, T example, Exception? failure = null) => new()
        {
            Status = status,
            Choices = source.Recorded,
            Spans = source.Spans,
            Value = example,
            Exception = failure
        };
    }
}
