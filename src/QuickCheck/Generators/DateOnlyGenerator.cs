using QuickCheck.Choices;

namespace QuickCheck.Generators;

/// <summary>
/// Generates dates in [min, max] component-wise, so that shrinking moves towards 1 January 2000,
/// or the nearest the bounds allow.
/// </summary>
internal sealed class DateOnlyGenerator : Generator<DateOnly>
{
    private readonly DateOnly _min;
    private readonly DateOnly _max;

    public DateOnlyGenerator(DateOnly min, DateOnly max)
    {
        if (min > max)
        {
            throw new ArgumentOutOfRangeException(
                nameof(min), $"min ({min:O}) must not exceed max ({max:O}).");
        }

        _min = min;
        _max = max;
    }

    protected internal override DateOnly Generate(ChoiceSource source)
    {
        return TimeComponents.DrawDate(source, _min, _max);
    }
}
