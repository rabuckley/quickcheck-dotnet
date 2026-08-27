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
            return new ExampleRun<T>
            {
                Status = ExampleStatus.Discarded,
                Choices = source.Recorded,
                Spans = source.Spans,
                Value = default!
            };
        }

        var outcome = await RunBodyAsync(value, body, cancellationToken).ConfigureAwait(false);

        return new ExampleRun<T>
        {
            Status = outcome.Status,
            Choices = source.Recorded,
            Spans = source.Spans,
            Value = value,
            Statistics = outcome.Statistics,
            Exception = outcome.Exception
        };
    }

    private static async ValueTask<BodyOutcome> RunBodyAsync<T>(
        T value,
        Func<T, ValueTask<bool>> body,
        CancellationToken cancellationToken)
    {
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

            return new BodyOutcome(holds ? ExampleStatus.Passed : ExampleStatus.Failed, statistics);
        }
        catch (DiscardException)
        {
            return new BodyOutcome(ExampleStatus.Discarded, statistics);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new BodyOutcome(ExampleStatus.Failed, statistics, exception);
        }
    }

    private readonly record struct BodyOutcome(
        ExampleStatus Status,
        ExampleStatistics Statistics,
        Exception? Exception = null);
}
