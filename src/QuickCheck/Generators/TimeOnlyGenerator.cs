using QuickCheck.Choices;

namespace QuickCheck.Generators;

/// <summary>
/// Generates times of day in [min, max] component-wise, so that shrinking moves towards midnight,
/// or the nearest the bounds allow, and drops detail before it shrinks it. One draw in sixteen
/// forces a bound through the same components.
/// </summary>
internal sealed class TimeOnlyGenerator : Generator<TimeOnly>
{
    private readonly TimeOnly _min;
    private readonly TimeOnly _max;

    public TimeOnlyGenerator(TimeOnly min, TimeOnly max)
    {
        if (min > max)
        {
            throw new ArgumentOutOfRangeException(
                nameof(min), $"min ({min:O}) must not exceed max ({max:O}).");
        }

        _min = min;
        _max = max;
    }

    protected internal override TimeOnly Generate(ChoiceSource source)
    {
        var edge = source.SampleEdge([_min, _max]);
        return TimeComponents.DrawTime(source, _min, _max, allowMidnight: false, edge);
    }
}
