using System.Collections.ObjectModel;

namespace QuickCheck.Running;

/// <summary>
/// Accumulates the <see cref="ExampleStatistics"/> of the examples that passed during one check,
/// and produces the <see cref="PropertyStatistics"/> snapshot a result carries.
/// </summary>
internal sealed class RunStatistics
{
    private readonly Dictionary<string, int> _labels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, int>> _tables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _coverMinimums = new(StringComparer.Ordinal);

    /// <summary>
    /// Counts one passed example: each label it hit counts once, a label it registered without
    /// hitting still appears (at its existing count), each collected value counts once, and a covered
    /// label keeps the largest minimum required so far.
    /// </summary>
    public void Merge(ExampleStatistics example)
    {
        foreach (var (label, hit) in example.Labels)
        {
            _labels[label] = _labels.GetValueOrDefault(label) + (hit ? 1 : 0);
        }

        foreach (var (name, value) in example.Collected)
        {
            if (!_tables.TryGetValue(name, out var table))
            {
                table = new Dictionary<string, int>(StringComparer.Ordinal);
                _tables.Add(name, table);
            }

            table[value] = table.GetValueOrDefault(value) + 1;
        }

        foreach (var (label, minimumPercent) in example.CoverMinimums)
        {
            _coverMinimums[label] = _coverMinimums.TryGetValue(label, out var existing)
                ? Math.Max(existing, minimumPercent)
                : minimumPercent;
        }
    }

    /// <summary>
    /// Takes an immutable snapshot with percentages of <paramref name="testsRun"/>, the number of
    /// examples that passed.
    /// </summary>
    public PropertyStatistics ToPropertyStatistics(int testsRun)
    {
        var labels = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(_labels, StringComparer.Ordinal));

        var tables = new ReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>(
            _tables.ToDictionary(
                static table => table.Key,
                static IReadOnlyDictionary<string, int> (table) =>
                    new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(table.Value, StringComparer.Ordinal)),
                StringComparer.Ordinal));

        var coverage = _coverMinimums
            .OrderBy(static requirement => requirement.Key, StringComparer.Ordinal)
            .Select(requirement => new CoverageRequirement(
                requirement.Key,
                requirement.Value,
                _labels[requirement.Key],
                // Compared without dividing, so that 100 means every example and 0 is met even when
                // no example passed.
                IsMet: _labels[requirement.Key] * 100.0 >= requirement.Value * testsRun))
            .ToArray()
            .AsReadOnly();

        return new PropertyStatistics(labels, tables, coverage);
    }
}
