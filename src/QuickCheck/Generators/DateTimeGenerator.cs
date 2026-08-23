using QuickCheck.Choices;

namespace QuickCheck.Generators;

/// <summary>
/// Generates dates and times in [min, max] component-wise: year, month and day, then a precision
/// level and the time components, so that shrinking moves towards midnight on 1 January 2000, or
/// the nearest the bounds allow, and drops time detail before it shrinks it. Every value takes the
/// <see cref="DateTimeKind"/> the bounds share.
/// </summary>
internal sealed class DateTimeGenerator : Generator<DateTime>
{
    private readonly DateTime _min;
    private readonly DateTime _max;

    public DateTimeGenerator(DateTime min, DateTime max)
    {
        if (min.Kind != max.Kind)
        {
            throw new ArgumentException(
                $"min and max must have the same Kind; min is {min.Kind} and max is {max.Kind}.", nameof(max));
        }

        if (min > max)
        {
            throw new ArgumentOutOfRangeException(
                nameof(min), $"min ({min:O}) must not exceed max ({max:O}).");
        }

        _min = min;
        _max = max;
    }

    protected internal override DateTime Generate(ChoiceSource source)
    {
        return TimeComponents.DrawDateTime(source, _min, _max);
    }
}
