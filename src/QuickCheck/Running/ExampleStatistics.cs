namespace QuickCheck.Running;

/// <summary>
/// The sink for the labels, collected values and coverage requirements one example reports through
/// <see cref="Property.Classify"/>, <see cref="Property.Collect"/> and <see cref="Property.Cover"/>.
/// A body may report from several threads at once (a <c>Parallel.For</c> inside the body reaches
/// the same sink through the flowed execution context), so every update takes the lock.
/// </summary>
internal sealed class ExampleStatistics
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, bool> _labels = new(StringComparer.Ordinal);
    private readonly HashSet<(string Name, string Value)> _collected = [];
    private readonly Dictionary<string, double> _coverMinimums = new(StringComparer.Ordinal);

    /// <summary>Each label the example registered, and whether the example hit it.</summary>
    public IReadOnlyDictionary<string, bool> Labels => _labels;

    /// <summary>Each (table, value) pair the example collected.</summary>
    public IReadOnlyCollection<(string Name, string Value)> Collected => _collected;

    /// <summary>The largest minimum percentage required for each covered label.</summary>
    public IReadOnlyDictionary<string, double> CoverMinimums => _coverMinimums;

    public void Label(string label, bool hit)
    {
        lock (_lock)
        {
            RegisterLabel(label, hit);
        }
    }

    public void Collect(string name, string value)
    {
        lock (_lock)
        {
            _collected.Add((name, value));
        }
    }

    public void Cover(string label, bool hit, double minimumPercent)
    {
        lock (_lock)
        {
            RegisterLabel(label, hit);

            _coverMinimums[label] = _coverMinimums.TryGetValue(label, out var existing)
                ? Math.Max(existing, minimumPercent)
                : minimumPercent;
        }
    }

    private void RegisterLabel(string label, bool hit) =>
        _labels[label] = hit || (_labels.TryGetValue(label, out var wasHit) && wasHit);
}
