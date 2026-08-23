using QuickCheck.Choices;

namespace QuickCheck.Generators;

/// <summary>
/// Generates instants in [min, max] with an offset drawn independently of the bounds: any whole
/// minute in [-14:00, +14:00] that keeps the local clock time within <see cref="DateTime"/>'s
/// range, whole hours three draws in four, then a local date and time in the range that offset
/// leaves. Shrinking moves towards midnight on 1 January 2000 UTC, or the nearest the bounds
/// allow. One draw in sixteen forces a bound, offset included, through the same choices.
/// </summary>
internal sealed class DateTimeOffsetGenerator : Generator<DateTimeOffset>
{
    private const int MaxOffsetMinutes = 14 * 60;

    private readonly DateTimeOffset _min;
    private readonly DateTimeOffset _max;
    private readonly IntegerRange<int> _offsetHours;
    private readonly IntegerRange<int> _offsetMinutes;

    public DateTimeOffsetGenerator(DateTimeOffset min, DateTimeOffset max)
    {
        if (min > max)
        {
            throw new ArgumentOutOfRangeException(
                nameof(min), $"min ({min:O}) must not exceed max ({max:O}).");
        }

        _min = min;
        _max = max;

        // Both numerators are non-negative, so the divisions floor; 0 is always feasible.
        var minMinutes = (int)Math.Max(-MaxOffsetMinutes, -(max.UtcTicks / TimeSpan.TicksPerMinute));
        var maxMinutes = (int)Math.Min(MaxOffsetMinutes, (DateTime.MaxValue.Ticks - min.UtcTicks) / TimeSpan.TicksPerMinute);

        _offsetHours = new IntegerRange<int>(minMinutes / 60, maxMinutes / 60);
        _offsetMinutes = new IntegerRange<int>(minMinutes, maxMinutes);
    }

    protected internal override DateTimeOffset Generate(ChoiceSource source)
    {
        // A bound's own offset keeps that bound's local clock time within DateTime's range, so it
        // always lies within the clamped offset ranges.
        var edge = source.SampleEdge([_min, _max]);
        int? edgeMinutes = edge is { } bound ? (int)(bound.Offset.Ticks / TimeSpan.TicksPerMinute) : null;

        var wholeHours = (edgeMinutes is { } forced
            ? source.ForceChoice(forced % 60 == 0 ? 0UL : 3UL, 3)
            : source.NextChoice(3)) < 3;
        var offsetMinutes = (wholeHours, edgeMinutes) switch
        {
            (true, null) => _offsetHours.Draw(source) * 60,
            (true, { } minutes) => _offsetHours.Force(source, minutes / 60) * 60,
            (false, null) => _offsetMinutes.Draw(source),
            (false, { } minutes) => _offsetMinutes.Force(source, minutes),
        };
        var offsetTicks = offsetMinutes * TimeSpan.TicksPerMinute;

        // The local clock range that keeps the instant in [min, max] and the local time in
        // DateTime's range; every sum stays far below long.MaxValue.
        var minLocal = new DateTime(Math.Max(0, _min.UtcTicks + offsetTicks));
        var maxLocal = new DateTime(Math.Min(DateTime.MaxValue.Ticks, _max.UtcTicks + offsetTicks));

        var local = TimeComponents.DrawDateTime(
            source, minLocal, maxLocal, edge is { } instant ? instant.DateTime : null);

        return new DateTimeOffset(local, TimeSpan.FromMinutes(offsetMinutes));
    }
}
