namespace QuickCheck.Running;

/// <summary>
/// The smallest failing example a <see cref="Shrinker{T}"/> reached, and what it cost to get there.
/// </summary>
internal sealed record ShrinkOutcome<T>
{
    public required ExampleRun<T> Minimal { get; init; }

    public required int Attempts { get; init; }

    public required int Shrinks { get; init; }

    public required ShrinkLimit Limit { get; init; }
}
