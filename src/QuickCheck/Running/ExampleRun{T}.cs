using QuickCheck.Choices;

namespace QuickCheck.Running;

internal abstract record ExampleRun<T>
{
    private ExampleRun()
    {
    }

    public required IReadOnlyList<Choice> Choices { get; init; }

    public sealed record Passed : ExampleRun<T>
    {
        public required ExampleStatistics Statistics { get; init; }
    }

    public sealed record Discarded : ExampleRun<T>;

    public sealed record Failed : ExampleRun<T>
    {
        public required IReadOnlyList<ChoiceSpan> Spans { get; init; }

        public required Exception? Exception { get; init; }

        public FailureKey Key => FailureKey.For(Exception);
    }

    public sealed record GenerationFailed : ExampleRun<T>
    {
        public required Exception Exception { get; init; }
    }
}
