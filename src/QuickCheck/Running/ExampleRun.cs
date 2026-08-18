using QuickCheck.Choices;

namespace QuickCheck.Running;

/// <summary>
/// The result of generating one example and running the property on it, together with the choices
/// consumed so the example can be replayed.
/// </summary>
internal sealed class ExampleRun<T>
{
    private ExampleRun(
        ExampleStatus status,
        IReadOnlyList<Choice> choices,
        IReadOnlyList<ChoiceSpan> spans,
        T value,
        Exception? exception)
    {
        Status = status;
        Choices = choices;
        Spans = spans;
        Value = value;
        Exception = exception;
    }

    public ExampleStatus Status { get; }
    public IReadOnlyList<Choice> Choices { get; }
    public IReadOnlyList<ChoiceSpan> Spans { get; }
    public T Value { get; }
    public Exception? Exception { get; }

    public FailureKey Key => new(Exception?.GetType());

    public bool IsFailure => Status is ExampleStatus.Failed;

    public static async ValueTask<ExampleRun<T>> ExecuteAsync(
        ChoiceSource source,
        Generator<T> generator,
        Func<T, ValueTask<bool>> body)
    {
        T value;

        try
        {
            value = source.Draw(generator);
        }
        catch (DiscardException)
        {
            return new ExampleRun<T>(ExampleStatus.Discarded, source.Recorded, source.Spans, default!, null);
        }

        try
        {
            var holds = await body(value).ConfigureAwait(false);

            return holds
                ? new ExampleRun<T>(ExampleStatus.Passed, source.Recorded, source.Spans, value, null)
                : new ExampleRun<T>(ExampleStatus.Failed, source.Recorded, source.Spans, value, null);
        }
        catch (DiscardException)
        {
            return new ExampleRun<T>(ExampleStatus.Discarded, source.Recorded, source.Spans, value, null);
        }
        catch (Exception exception)
        {
            return new ExampleRun<T>(ExampleStatus.Failed, source.Recorded, source.Spans, value, exception);
        }
    }
}
