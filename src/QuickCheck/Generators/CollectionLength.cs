namespace QuickCheck.Generators;

/// <summary>
/// The length heuristic the collection generators share.
/// </summary>
internal static class CollectionLength
{
    /// <summary>
    /// The probability that a collection holding at least <paramref name="minLength"/> elements
    /// draws another. Aims for a modest average length above the minimum (the same heuristic
    /// Hypothesis uses), so typical examples stay readable while long ones remain reachable.
    /// </summary>
    public static double ContinueProbability(int minLength, int maxLength)
    {
        var averageExtra = Math.Min(Math.Max(minLength * 2, minLength + 5), 0.5 * (minLength + maxLength)) - minLength;

        return averageExtra <= 0 ? 0 : 1 - 1 / (1 + averageExtra);
    }
}
