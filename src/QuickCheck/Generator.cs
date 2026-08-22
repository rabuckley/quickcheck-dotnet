using QuickCheck.Choices;

namespace QuickCheck;

/// <summary>
/// Represents a source of values of type <typeparamref name="T"/>, produced from a sequence of
/// recorded choices.
/// </summary>
/// <typeparam name="T">The type of value generated.</typeparam>
public abstract class Generator<T>
{
    /// <summary>
    /// When overridden in a derived class, produces one value by drawing choices from
    /// <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The source of choices the value is produced from.</param>
    /// <returns>The generated value.</returns>
    /// <remarks>
    /// An implementation must be deterministic in the choices it draws: the same sequence of choices
    /// must yield an equal value, and all randomness must come from <paramref name="source"/>. Draw
    /// from a nested generator with <see cref="ChoiceSource.Draw{T}"/> rather than calling its
    /// <see cref="Generate"/> method directly, so that the shrinker can see the structure of the
    /// value. A generator instance is shared between runs and may be drawn from concurrently, for
    /// example by test classes running in parallel, so keep per-draw state in locals or on
    /// <paramref name="source"/> rather than in fields.
    /// </remarks>
    protected internal abstract T Generate(ChoiceSource source);

    /// <summary>
    /// Generates a number of sample values, for inspecting a generator's distribution during
    /// development.
    /// </summary>
    /// <param name="count">The number of values to generate. The default is 10.</param>
    /// <param name="seed">The seed to generate from; the same seed yields the same samples.</param>
    /// <returns>A list of <paramref name="count"/> generated values.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">
    /// The generator discarded too many values to produce <paramref name="count"/> samples.
    /// </exception>
    public IReadOnlyList<T> Sample(int count = 10, ulong seed = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var samples = new List<T>(count);
        var maxRuns = checked(count * 100 + 100);

        for (var run = 0; samples.Count < count; run++)
        {
            if (run >= maxRuns)
            {
                throw new InvalidOperationException(
                    $"Only {samples.Count} of {count} samples could be generated; the generator discards too many values.");
            }

            var source = ChoiceSource.FromRandom(Xoshiro256StarStar.ForRun(seed, run));

            try
            {
                samples.Add(source.Draw(this));
            }
            catch (DiscardException)
            {
                // A filtered-out sample; try the next run.
            }
        }

        return samples;
    }
}
