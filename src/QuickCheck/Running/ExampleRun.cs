using QuickCheck.Choices;

namespace QuickCheck.Running;

/// <summary>
/// Runs a property's body on one example, generated or explicit, recording it as an
/// <see cref="ExampleRun{T}"/>. Every invocation of a property body goes through here, so the
/// statistics sink is installed and the discard, cancellation and failure ladder is applied in one
/// place.
/// </summary>
internal static class ExampleRun
{
    /// <summary>
    /// Generates one example and runs <paramref name="body"/> on it.
    /// </summary>
    /// <remarks>
    /// <paramref name="cancellationToken"/> is not passed to the generator or the body; it only
    /// distinguishes one that abandoned the check because that token was cancelled — which
    /// propagates — from one that threw, which is recorded as a run of its own.
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
            return WithoutValue(ExampleStatus.Discarded);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return WithoutValue(ExampleStatus.GenerationFailed, exception);
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

        // A run that ended inside the generator has no value to carry; its choices are the ones
        // drawn before the generator stopped, which are what replay it.
        ExampleRun<T> WithoutValue(ExampleStatus status, Exception? exception = null) => new()
        {
            Status = status,
            Choices = source.Recorded,
            Spans = source.Spans,
            Value = default!,
            Exception = exception
        };
    }

    /// <summary>
    /// Runs <paramref name="body"/> on an explicit example, a value the caller supplied rather than
    /// one a generator produced.
    /// </summary>
    /// <remarks>
    /// The run carries no choices, because no generator made any. That makes it unshrinkable — a
    /// <see cref="Shrinker{T}"/> given one would report convergence having tried nothing — so an
    /// explicit failure is reported as given instead of being shrunk.
    /// </remarks>
    public static async ValueTask<ExampleRun<T>> ExecuteAsync<T>(
        T value,
        Func<T, ValueTask<bool>> body,
        CancellationToken cancellationToken)
    {
        var outcome = await RunBodyAsync(value, body, cancellationToken).ConfigureAwait(false);

        return new ExampleRun<T>
        {
            Status = outcome.Status,
            Choices = [],
            Spans = [],
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
