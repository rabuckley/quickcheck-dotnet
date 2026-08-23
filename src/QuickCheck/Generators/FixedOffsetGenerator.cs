using QuickCheck.Choices;

namespace QuickCheck.Generators;

/// <summary>
/// Generates instants in [min, max] that all carry one offset, by drawing the local clock time over
/// the window where that offset keeps the instant both within the bounds and within
/// <see cref="DateTime"/>'s range. Distribution, shrinking and edge forcing are the local time's.
/// </summary>
internal sealed class FixedOffsetGenerator : Generator<DateTimeOffset>
{
    private const long MaxOffsetTicks = 14 * TimeSpan.TicksPerHour;

    private readonly DateTimeGenerator _local;
    private readonly TimeSpan _offset;

    public FixedOffsetGenerator(DateTimeOffset min, DateTimeOffset max, TimeSpan offset)
    {
        if (min > max)
        {
            throw new ArgumentOutOfRangeException(
                nameof(min), $"min ({min:O}) must not exceed max ({max:O}).");
        }

        if (offset.Ticks % TimeSpan.TicksPerMinute != 0 || Math.Abs(offset.Ticks) > MaxOffsetTicks)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset), offset, "offset must be a whole number of minutes from -14:00 to +14:00.");
        }

        // The local clock time is the instant plus the offset, so the offset trims whichever end of
        // the range would push it past DateTime's. Every sum stays far below long.MaxValue.
        var minUtcTicks = Math.Max(min.UtcTicks, Math.Max(0, -offset.Ticks));
        var maxUtcTicks = Math.Min(max.UtcTicks, DateTime.MaxValue.Ticks - offset.Ticks);

        if (minUtcTicks > maxUtcTicks)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                offset,
                $"no instant from min ({min:O}) to max ({max:O}) is representable at this offset.");
        }

        _local = new DateTimeGenerator(
            new DateTime(minUtcTicks + offset.Ticks),
            new DateTime(maxUtcTicks + offset.Ticks));
        _offset = offset;
    }

    protected internal override DateTimeOffset Generate(ChoiceSource source) =>
        new(source.Draw(_local), _offset);
}
