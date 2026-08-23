using QuickCheck.Choices;

namespace QuickCheck.Generators;

/// <summary>
/// Generates spans in [min, max] as a whole number of one unit: a unit is chosen uniformly among
/// ticks, milliseconds, seconds, minutes, hours and days (those the range admits), then a count of
/// that unit is drawn with the integer layout, so small round spans of every unit are common and
/// shrinking moves towards zero, or the bound nearest zero. Units are finest first, so lowering
/// the unit choice keeps a shrunk span exact rather than rounding it up to a coarser unit.
/// </summary>
internal sealed class TimeSpanGenerator : Generator<TimeSpan>
{
    private static readonly long[] AllUnits =
    [
        1,
        TimeSpan.TicksPerMillisecond,
        TimeSpan.TicksPerSecond,
        TimeSpan.TicksPerMinute,
        TimeSpan.TicksPerHour,
        TimeSpan.TicksPerDay,
    ];

    private readonly long[] _units;
    private readonly IntegerRange<long>[] _counts;

    public TimeSpanGenerator(TimeSpan min, TimeSpan max)
    {
        if (min > max)
        {
            throw new ArgumentOutOfRangeException(
                nameof(min), $"min ({min:c}) must not exceed max ({max:c}).");
        }

        var units = new List<long>(AllUnits.Length);
        var counts = new List<IntegerRange<long>>(AllUnits.Length);

        foreach (var unit in AllUnits)
        {
            var lowCount = DivideRoundingUp(min.Ticks, unit);
            var highCount = DivideRoundingDown(max.Ticks, unit);

            if (lowCount <= highCount)
            {
                units.Add(unit);
                counts.Add(new IntegerRange<long>(lowCount, highCount));
            }
        }

        // The tick unit always fits, so there is at least one.
        _units = [.. units];
        _counts = [.. counts];
    }

    protected internal override TimeSpan Generate(ChoiceSource source)
    {
        var index = (int)source.NextChoice((ulong)(_units.Length - 1));
        var count = _counts[index].Draw(source);
        return TimeSpan.FromTicks(count * _units[index]);
    }

    // Math.DivRem rather than (a + unit - 1) / unit or negation, which overflow at the long extremes.
    private static long DivideRoundingUp(long value, long unit)
    {
        var (quotient, remainder) = Math.DivRem(value, unit);
        return remainder > 0 ? quotient + 1 : quotient;
    }

    private static long DivideRoundingDown(long value, long unit)
    {
        var (quotient, remainder) = Math.DivRem(value, unit);
        return remainder < 0 ? quotient - 1 : quotient;
    }
}
