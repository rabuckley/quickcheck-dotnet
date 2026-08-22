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

        var statistics = new ExampleStatistics();

        try
        {
            bool holds;
            Property.CurrentStatistics.Value = statistics;

            try
            {
                holds = await body(value).ConfigureAwait(false);
            }
            finally
            {
                // Belt and braces: an async method restores the caller's execution context on exit,
                // so the sink cannot leak out of this method; clearing it here only bounds it to the
                // body call within this method as well.
                Property.CurrentStatistics.Value = null;
            }

            return Run(holds ? ExampleStatus.Passed : ExampleStatus.Failed, value, statistics);
        }
        catch (DiscardException)
        {
            return Run(ExampleStatus.Discarded, value, statistics);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Run(ExampleStatus.Failed, value, statistics, exception);
        }

        ExampleRun<T> Run(
            ExampleStatus status,
            T example,
            ExampleStatistics? bodyStatistics = null,
            Exception? failure = null) => new()
        {
            Status = status,
            Choices = source.Recorded,
            Spans = source.Spans,
            Value = example,
            Statistics = bodyStatistics,
            Exception = failure
        };
    }
}
