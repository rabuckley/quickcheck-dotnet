using QuickCheck.Choices;

namespace QuickCheck.Generators;

/// <summary>
/// Draws the date and time parts shared by the date and time generators, component-wise so that
/// each component keeps its own uniform draw and boundary snapping while the whole value lands in
/// [min, max].
/// </summary>
internal static class TimeComponents
{
    private const int TicksPerMillisecond = (int)TimeSpan.TicksPerMillisecond;

    /// <summary>
    /// Draws a date in [min, max]: the year, then a uniform month, then a uniform day of that month.
    /// </summary>
    public static DateOnly DrawDate(ChoiceSource source, DateOnly min, DateOnly max) =>
        new CappedDraw(source).Date(min, max);

    /// <summary>
    /// Draws a time in [min, max]: a precision level first (midnight, hour, minute, second,
    /// millisecond or tick), then the hour, minute, second, millisecond and remaining ticks, each
    /// drawn up to that level and fixed at its low end beyond it. Level 0 is the coarsest, so
    /// round times are common and shrinking drops detail before it shrinks it.
    /// </summary>
    public static TimeOnly DrawTime(ChoiceSource source, TimeOnly min, TimeOnly max, bool allowMidnight) =>
        new CappedDraw(source).Time(min, max, allowMidnight);

    /// <summary>
    /// Draws a date and time in [min, max] of <paramref name="min"/>'s <c>Kind</c>, as
    /// <see cref="DrawDate"/> followed by <see cref="DrawTime"/> over one run of components, so the
    /// time is bounded by <paramref name="min"/>'s only on <paramref name="min"/>'s date and by
    /// <paramref name="max"/>'s only on <paramref name="max"/>'s.
    /// </summary>
    public static DateTime DrawDateTime(ChoiceSource source, DateTime min, DateTime max)
    {
        var draw = new CappedDraw(source);
        var date = draw.Date(DateOnly.FromDateTime(min), DateOnly.FromDateTime(max));
        var time = draw.Time(TimeOnly.FromDateTime(min), TimeOnly.FromDateTime(max), allowMidnight: true);
        return date.ToDateTime(time, min.Kind);
    }

    /// <summary>
    /// One run of components, most significant first: each is bounded by its counterpart in
    /// <c>min</c> only while every earlier component has equalled <c>min</c>'s, and likewise for
    /// <c>max</c>; otherwise only the component's natural range applies. Components compare
    /// lexicographically, so the value lands in [min, max].
    /// </summary>
    private sealed class CappedDraw(ChoiceSource source)
    {
        private const int ModernLow = 1900;
        private const int ModernHigh = 2100;
        private const int TargetYear = 2000;

        private bool _lowActive = true;
        private bool _highActive = true;

        public DateOnly Date(DateOnly min, DateOnly max)
        {
            var year = Year(min.Year, max.Year);
            var month = Next(min.Month, max.Month, naturalLow: 1, naturalHigh: 12);
            var day = Next(min.Day, max.Day, naturalLow: 1, naturalHigh: DateTime.DaysInMonth(year, month));
            return new DateOnly(year, month, day);
        }

        public TimeOnly Time(TimeOnly min, TimeOnly max, bool allowMidnight)
        {
            // The level is not a component of the bounds, so it is drawn outside the caps.
            var precision = allowMidnight
                ? (int)source.NextChoice(5)
                : 1 + (int)source.NextChoice(4);

            var hour = Component(precision > 0, min.Hour, max.Hour, 23);
            var minute = Component(precision > 1, min.Minute, max.Minute, 59);
            var second = Component(precision > 2, min.Second, max.Second, 59);
            var millisecond = Component(precision > 3, min.Millisecond, max.Millisecond, 999);
            var ticks = Component(
                precision > 4,
                (int)(min.Ticks % TicksPerMillisecond),
                (int)(max.Ticks % TicksPerMillisecond),
                TicksPerMillisecond - 1);

            return new TimeOnly(
                hour * TimeSpan.TicksPerHour
                + minute * TimeSpan.TicksPerMinute
                + second * TimeSpan.TicksPerSecond
                + millisecond * TimeSpan.TicksPerMillisecond
                + ticks);
        }

        private int Component(bool drawn, int minComponent, int maxComponent, int naturalHigh) =>
            drawn
                ? Next(minComponent, maxComponent, naturalLow: 0, naturalHigh)
                : Skip(minComponent, maxComponent, naturalLow: 0, naturalHigh);

        /// <summary>
        /// Draws the next component from its capped range, choice 0 being the low end.
        /// </summary>
        private int Next(int minComponent, int maxComponent, int naturalLow, int naturalHigh)
        {
            var (low, high) = Bounds(minComponent, maxComponent, naturalLow, naturalHigh);
            var value = new IntegerRange<int>(low, high).Draw(source);
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
        /// Draws a year in 1..9999, 2000 (or the bound nearest it) being the simplest, and 3 draws
        /// in 4 falling in 1900..2100 when the capped range is wider than that band.
        /// </summary>
        private int Year(int minYear, int maxYear)
        {
            var (low, high) = Bounds(minYear, maxYear, naturalLow: 1, naturalHigh: 9999);
            var bandLow = Math.Max(low, ModernLow);
            var bandHigh = Math.Min(high, ModernHigh);
            var bandIsNarrower = bandLow <= bandHigh && (bandLow != low || bandHigh != high);

            if (bandIsNarrower && source.NextChoice(3) < 3)
            {
                (low, high) = (bandLow, bandHigh);
            }

            var value = new IntegerRange<int>(low - TargetYear, high - TargetYear).Draw(source) + TargetYear;
            Advance(value, minYear, maxYear);
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
