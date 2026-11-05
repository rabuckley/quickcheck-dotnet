using System.Diagnostics;
using QuickCheck.Choices;

namespace QuickCheck.Running;

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
            return new ExampleRun<T>.Discarded { Choices = source.Recorded };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new ExampleRun<T>.GenerationFailed { Choices = source.Recorded, Exception = exception };
        }

        var outcome = await ExecuteAsync(value, body, cancellationToken).ConfigureAwait(false);

        return outcome switch
        {
            BodyOutcome.Passed passed => new ExampleRun<T>.Passed
            {
                Choices = source.Recorded,
                Statistics = passed.Statistics
            },
            BodyOutcome.Discarded => new ExampleRun<T>.Discarded { Choices = source.Recorded },
            BodyOutcome.Failed failed => new ExampleRun<T>.Failed
            {
                Choices = source.Recorded,
                Spans = source.Spans,
                Exception = failed.Exception
            },
            _ => throw new UnreachableException()
        };
    }

    public static async ValueTask<BodyOutcome> ExecuteAsync<T>(
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

            return holds
                ? new BodyOutcome.Passed { Statistics = statistics }
                : new BodyOutcome.Failed { Exception = null };
        }
        catch (DiscardException)
        {
            return new BodyOutcome.Discarded();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new BodyOutcome.Failed { Exception = exception };
        }
    }
}
