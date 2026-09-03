using QuickCheck.Choices;

namespace QuickCheck.Generators;

/// <summary>
/// Draws the date and time parts shared by the date and time generators, component-wise so that
/// each component keeps its own boundary snapping and shrinks on its own while the whole value
/// lands in [min, max]. The year and month are weighted by the days they cover, so the date is
/// uniform over its range; the time components are drawn uniformly. Either part can be forced to a
/// whole value while generating, which emits that value through the same components so it shrinks
/// like a drawn one.
/// </summary>
internal static class TimeComponents
{
    private const int TicksPerMillisecond = (int)TimeSpan.TicksPerMillisecond;

    /// <summary>
    /// Draws a date uniformly in [min, max] as a year, a month and a day, or forces each
    /// component to <paramref name="forced"/>'s while generating.
    /// </summary>
    public static DateOnly DrawDate(ChoiceSource source, DateOnly min, DateOnly max, DateOnly? forced = null) =>
        new CappedDraw(source).Date(min, max, forced);

    /// <summary>
    /// Draws a time in [min, max]: a precision level first (midnight, hour, minute, second,
    /// millisecond or tick), then the hour, minute, second, millisecond and remaining ticks, each
    /// drawn up to that level and fixed at its low end beyond it. Level 0 is the coarsest, so
    /// round times are common and shrinking drops detail before it shrinks it. A forced time takes
    /// the finest level and forces every component, so no component of it is skipped.
    /// </summary>
    public static TimeOnly DrawTime(
        ChoiceSource source, TimeOnly min, TimeOnly max, bool allowMidnight, TimeOnly? forced = null) =>
        new CappedDraw(source).Time(min, max, allowMidnight, forced);

    /// <summary>
    /// Draws a date and time in [min, max] of <paramref name="min"/>'s <c>Kind</c>, as
    /// <see cref="DrawDate"/> followed by <see cref="DrawTime"/> over one run of components, so the
    /// time is bounded by <paramref name="min"/>'s only on <paramref name="min"/>'s date and by
    /// <paramref name="max"/>'s only on <paramref name="max"/>'s.
    /// </summary>
    public static DateTime DrawDateTime(ChoiceSource source, DateTime min, DateTime max, DateTime? forced = null)
    {
        var draw = new CappedDraw(source);
        var date = draw.Date(
            DateOnly.FromDateTime(min),
            DateOnly.FromDateTime(max),
            forced is { } forcedDate ? DateOnly.FromDateTime(forcedDate) : null);
        var time = draw.Time(
            TimeOnly.FromDateTime(min),
            TimeOnly.FromDateTime(max),
            allowMidnight: true,
            forced is { } forcedTime ? TimeOnly.FromDateTime(forcedTime) : null);
        return date.ToDateTime(time, min.Kind);
    }

    /// <summary>
    /// One run of components, most significant first: each is bounded by its counterpart in
    /// <c>min</c> only while every earlier component has equalled <c>min</c>'s, and likewise for
    /// <c>max</c>; otherwise only the component's natural range applies. Components compare
    /// lexicographically, so the value lands in [min, max]. While replaying, the replayed choice
    /// wins and later caps follow the value it produced.
    /// </summary>
    private sealed class CappedDraw(ChoiceSource source)
    {
        private const int ModernLow = 1900;
        private const int ModernHigh = 2100;
        private static readonly int Epoch = new DateOnly(2000, 1, 1).DayNumber;

        private bool _lowActive = true;
        private bool _highActive = true;

        public DateOnly Date(DateOnly min, DateOnly max, DateOnly? forced)
        {
            var year = Year(min, max, forced);
            var month = Month(year, min, max, forced);
            var day = Next(min.Day, max.Day, naturalLow: 1, naturalHigh: DateTime.DaysInMonth(year, month), forced?.Day);
            return new DateOnly(year, month, day);
        }

        public TimeOnly Time(TimeOnly min, TimeOnly max, bool allowMidnight, TimeOnly? forced)
        {
            // The level is not a component of the bounds, so it is drawn outside the caps.
            var precision = (allowMidnight, forced) switch
            {
                (true, null) => (int)source.NextChoice(5),
                (true, not null) => (int)source.ForceChoice(5, 5),
                (false, null) => 1 + (int)source.NextChoice(4),
                (false, not null) => 1 + (int)source.ForceChoice(4, 4),
            };

            var hour = Component(precision > 0, min.Hour, max.Hour, 23, forced?.Hour);
            var minute = Component(precision > 1, min.Minute, max.Minute, 59, forced?.Minute);
            var second = Component(precision > 2, min.Second, max.Second, 59, forced?.Second);
            var millisecond = Component(precision > 3, min.Millisecond, max.Millisecond, 999, forced?.Millisecond);
            var ticks = Component(
                precision > 4,
                (int)(min.Ticks % TicksPerMillisecond),
                (int)(max.Ticks % TicksPerMillisecond),
                TicksPerMillisecond - 1,
                forced is { } time ? (int)(time.Ticks % TicksPerMillisecond) : null);

            return new TimeOnly(
                hour * TimeSpan.TicksPerHour
                + minute * TimeSpan.TicksPerMinute
                + second * TimeSpan.TicksPerSecond
                + millisecond * TimeSpan.TicksPerMillisecond
                + ticks);
        }

        private int Component(bool drawn, int minComponent, int maxComponent, int naturalHigh, int? forced) =>
            drawn
                ? Next(minComponent, maxComponent, naturalLow: 0, naturalHigh, forced)
                : Skip(minComponent, maxComponent, naturalLow: 0, naturalHigh);

        /// <summary>
        /// Draws the next component from its capped range, choice 0 being the low end, or forces it
        /// to <paramref name="forced"/>, clamped to the capped range, while generating.
        /// </summary>
        private int Next(int minComponent, int maxComponent, int naturalLow, int naturalHigh, int? forced)
        {
            var (low, high) = Bounds(minComponent, maxComponent, naturalLow, naturalHigh);
            var range = new IntegerRange<int>(low, high);
            var value = forced is int f ? range.Force(source, f) : range.Draw(source);
            Advance(value, minComponent, maxComponent);
            return value;
        }

        /// <summary>
        /// Fixes the next component at the low end of its capped range without consuming a choice.
        /// A component beyond the precision level still passes through here, or the caps stop
        /// advancing and the value leaves [min, max].
        /// </summary>
        private int Skip(int minComponent, int maxComponent, int naturalLow, int naturalHigh)
        {
            var (low, _) = Bounds(minComponent, maxComponent, naturalLow, naturalHigh);
            Advance(low, minComponent, maxComponent);
            return low;
        }

        /// <summary>
        /// Draws a year in 1..9999 weighted by the days of the capped range each year covers, 2000
        /// (or the bound nearest it) being the simplest, and three draws in four falling in
        /// 1900..2100 when the capped range is wider than that band, or forces it to
        /// <paramref name="forced"/>'s year, clamped to the capped range, while generating.
        /// </summary>
        private int Year(DateOnly min, DateOnly max, DateOnly? forced)
        {
            var (low, high) = Bounds(min.Year, max.Year, naturalLow: 1, naturalHigh: 9999);
            var bandLow = Math.Max(low, ModernLow);
            var bandHigh = Math.Min(high, ModernHigh);
            var bandIsNarrower = bandLow <= bandHigh && (bandLow != low || bandHigh != high);

            if (bandIsNarrower)
            {
                var bandChoice = forced is { } forcedDate
                    ? source.ForceChoice(forcedDate.Year >= bandLow && forcedDate.Year <= bandHigh ? 0UL : 3UL, 3)
                    : source.NextChoice(3);

                if (bandChoice < 3)
                {
                    (low, high) = (bandLow, bandHigh);
                }
            }

            // When the band has replaced a bound's year, the bound's day no longer caps the range.
            var dayLow = _lowActive && low == min.Year ? min.DayNumber : new DateOnly(low, 1, 1).DayNumber;
            var dayHigh = _highActive && high == max.Year ? max.DayNumber : new DateOnly(high, 12, 31).DayNumber;
            var range = new IntegerRange<int>(dayLow - Epoch, dayHigh - Epoch);
            var day = forced is { } f ? range.Force(source, f.DayNumber - Epoch) : range.Draw(source);
            var value = DateOnly.FromDayNumber(day + Epoch).Year;
            Advance(value, min.Year, max.Year);
            return value;
        }

        /// <summary>
        /// Draws a month of <paramref name="year"/> weighted by the days of the capped range each
        /// month covers, January (or the bound's month) being the simplest, or forces it to
        /// <paramref name="forced"/>'s month, clamped to the capped range, while generating.
        /// </summary>
        private int Month(int year, DateOnly min, DateOnly max, DateOnly? forced)
        {
            var january1 = new DateOnly(year, 1, 1).DayNumber;
            var dayLow = _lowActive ? min.DayNumber : january1;
            var dayHigh = _highActive ? max.DayNumber : new DateOnly(year, 12, 31).DayNumber;
            var range = new IntegerRange<int>(dayLow - january1, dayHigh - january1);
            var day = forced is { } f ? range.Force(source, f.DayNumber - january1) : range.Draw(source);
            var value = DateOnly.FromDayNumber(day + january1).Month;
            Advance(value, min.Month, max.Month);
            return value;
        }

        private (int Low, int High) Bounds(int minComponent, int maxComponent, int naturalLow, int naturalHigh) =>
            (_lowActive ? minComponent : naturalLow, _highActive ? maxComponent : naturalHigh);

        private void Advance(int value, int minComponent, int maxComponent)
        {
            _lowActive &= value == minComponent;
            _highActive &= value == maxComponent;
        }
    }
}
