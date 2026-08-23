using QuickCheck.Choices;

namespace QuickCheck.Generators;

/// <summary>
/// Generates instants in [min, max] with an offset drawn independently of the bounds: any whole
/// minute in [-14:00, +14:00] that keeps the local clock time within <see cref="DateTime"/>'s
/// range, whole hours 3 draws in 4, then a local date and time in the range that offset leaves.
/// Shrinking moves towards midnight on 1 January 2000 UTC, or the nearest the bounds allow.
/// </summary>
internal sealed class DateTimeOffsetGenerator : Generator<DateTimeOffset>
{
    private const int MaxOffsetMinutes = 14 * 60;

    private readonly long _minUtcTicks;
    private readonly long _maxUtcTicks;
    private readonly IntegerRange<int> _offsetHours;
    private readonly IntegerRange<int> _offsetMinutes;

    public DateTimeOffsetGenerator(DateTimeOffset min, DateTimeOffset max)
    {
        if (min > max)
        {
            throw new ArgumentOutOfRangeException(
                nameof(min), $"min ({min:O}) must not exceed max ({max:O}).");
        }

        _minUtcTicks = min.UtcTicks;
        _maxUtcTicks = max.UtcTicks;

        // Both numerators are non-negative, so the divisions floor; 0 is always feasible.
        var minMinutes = (int)Math.Max(-MaxOffsetMinutes, -(_maxUtcTicks / TimeSpan.TicksPerMinute));
        var maxMinutes = (int)Math.Min(MaxOffsetMinutes, (DateTime.MaxValue.Ticks - _minUtcTicks) / TimeSpan.TicksPerMinute);

        _offsetHours = new IntegerRange<int>(minMinutes / 60, maxMinutes / 60);
        _offsetMinutes = new IntegerRange<int>(minMinutes, maxMinutes);
    }

    protected internal override DateTimeOffset Generate(ChoiceSource source)
    {
        var offsetMinutes = source.NextChoice(3) < 3
            ? _offsetHours.Draw(source) * 60
            : _offsetMinutes.Draw(source);
        var offsetTicks = offsetMinutes * TimeSpan.TicksPerMinute;

        // The local clock range that keeps the instant in [min, max] and the local time in
        // DateTime's range; every sum stays far below long.MaxValue.
        var minLocal = new DateTime(Math.Max(0, _minUtcTicks + offsetTicks));
        var maxLocal = new DateTime(Math.Min(DateTime.MaxValue.Ticks, _maxUtcTicks + offsetTicks));

        var local = TimeComponents.DrawDateTime(source, minLocal, maxLocal);

        return new DateTimeOffset(local, TimeSpan.FromMinutes(offsetMinutes));
    }
}
