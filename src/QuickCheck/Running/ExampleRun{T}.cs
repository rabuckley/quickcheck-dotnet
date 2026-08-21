using QuickCheck.Choices;

namespace QuickCheck.Running;

/// <summary>
/// The result of generating one example and running the property on it, together with the choices
/// consumed so the example can be replayed.
/// </summary>
internal sealed class ExampleRun<T>
{
    internal ExampleRun()
    {
    }

    public required ExampleStatus Status { get; init; }

    public required IReadOnlyList<Choice> Choices { get; init; }

    public required IReadOnlyList<ChoiceSpan> Spans { get; init; }

    public required T Value { get; init; }

    public Exception? Exception { get; init; }

    public FailureKey Key => new(Exception?.GetType());

    public bool IsFailure => Status is ExampleStatus.Failed;
}
