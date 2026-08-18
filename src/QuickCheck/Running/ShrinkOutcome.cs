namespace QuickCheck.Running;

/// <summary>
/// The smallest failing example a <see cref="Shrinker{T}"/> reached, and what it cost to get there.
/// </summary>
internal readonly record struct ShrinkOutcome<T>(ExampleRun<T> Minimal, int Attempts, int Shrinks);
