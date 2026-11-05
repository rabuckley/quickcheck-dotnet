namespace QuickCheck.Running;

internal abstract record BodyOutcome
{
    private BodyOutcome()
    {
    }

    public sealed record Passed : BodyOutcome
    {
        public required ExampleStatistics Statistics { get; init; }
    }

    public sealed record Discarded : BodyOutcome;

    public sealed record Failed : BodyOutcome
    {
        public required Exception? Exception { get; init; }
    }
}
