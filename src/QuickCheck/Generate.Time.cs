using QuickCheck.Generators;

namespace QuickCheck;

public static partial class Generate
{
    /// <summary>
    /// Creates a generator for dates and times over the full range of <see cref="System.DateTime"/>.
    /// </summary>
    /// <param name="kind">
    /// The <see cref="DateTimeKind"/> of every value. The default is
    /// <see cref="DateTimeKind.Unspecified"/>.
    /// </param>
    /// <returns>
    /// A generator that produces values of <paramref name="kind"/>, draws the year in 1900..2100
    /// three times in four and anywhere in 1..9999 otherwise, draws the month, day and time
    /// components uniformly, produces a time that is midnight or a whole hour, minute, second or
    /// millisecond about as often as not, and shrinks towards midnight on 1 January 2000.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="kind"/> is not a defined <see cref="DateTimeKind"/>.
    /// </exception>
    /// <remarks>
    /// For a mix of kinds, draw the kind first:
    /// <c>Generate.Enum&lt;DateTimeKind&gt;().SelectMany(kind =&gt; Generate.DateTime(kind))</c>.
    /// Shrinking minimises the more significant components first, so a failure that depends on a
    /// threshold can end on the round value just past it rather than on the threshold itself.
    /// </remarks>
    public static Generator<System.DateTime> DateTime(DateTimeKind kind = DateTimeKind.Unspecified)
    {
        if (kind is not (DateTimeKind.Unspecified or DateTimeKind.Utc or DateTimeKind.Local))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind must be a defined DateTimeKind.");
        }

        return DateTime(
            new System.DateTime(System.DateTime.MinValue.Ticks, kind),
            new System.DateTime(System.DateTime.MaxValue.Ticks, kind));
    }

    /// <summary>
    /// Creates a generator for dates and times within a specified range.
    /// </summary>
    /// <param name="min">The inclusive lower bound of the values to generate.</param>
    /// <param name="max">
    /// The inclusive upper bound of the values to generate, of the same <c>Kind</c> as
    /// <paramref name="min"/>.
    /// </param>
    /// <returns>
    /// A generator that produces values from <paramref name="min"/> to <paramref name="max"/> of the
    /// bounds' <c>Kind</c>, distributed as <see cref="DateTime(DateTimeKind)"/> within the range,
    /// and shrinks towards midnight on 1 January 2000, or towards the simplest value the bounds
    /// allow when the range excludes it.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="min"/> and <paramref name="max"/> have different <c>Kind</c>s.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="min"/> is greater than <paramref name="max"/>.
    /// </exception>
    public static Generator<System.DateTime> DateTime(System.DateTime min, System.DateTime max) =>
        new DateTimeGenerator(min, max);

    /// <summary>
    /// Creates a generator for instants with an offset over the full range of
    /// <see cref="System.DateTimeOffset"/>.
    /// </summary>
    /// <returns>
    /// A generator that draws the offset as any whole minute in −14:00..+14:00 that keeps the local
    /// clock time within <see cref="System.DateTime"/>'s range, whole hours three times in four,
    /// then a local date and time distributed as <see cref="DateTime(DateTimeKind)"/>, and shrinks
    /// towards midnight on 1 January 2000 UTC.
    /// </returns>
    /// <remarks>
    /// For a fixed offset, generate a <see cref="System.DateTime"/> and attach it:
    /// <c>Generate.DateTime(min, max).Select(d =&gt; new DateTimeOffset(d, offset))</c>, with bounds
    /// that keep the instant in range.
    /// </remarks>
    public static Generator<System.DateTimeOffset> DateTimeOffset() =>
        DateTimeOffset(System.DateTimeOffset.MinValue, System.DateTimeOffset.MaxValue);

    /// <summary>
    /// Creates a generator for instants with an offset within a specified range.
    /// </summary>
    /// <param name="min">The inclusive lower bound of the instants to generate.</param>
    /// <param name="max">The inclusive upper bound of the instants to generate.</param>
    /// <returns>
    /// A generator that produces instants from <paramref name="min"/> to <paramref name="max"/>,
    /// compared as instants, with an offset drawn independently of the bounds' offsets as
    /// <see cref="DateTimeOffset()"/> does, and shrinks towards midnight on 1 January 2000 UTC, or
    /// towards the simplest value the bounds allow when the range excludes it.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="min"/> is a later instant than <paramref name="max"/>.
    /// </exception>
    public static Generator<System.DateTimeOffset> DateTimeOffset(System.DateTimeOffset min, System.DateTimeOffset max) =>
        new DateTimeOffsetGenerator(min, max);

    /// <summary>
    /// Creates a generator for dates over the full range of <see cref="System.DateOnly"/>.
    /// </summary>
    /// <returns>
    /// A generator that draws the year in 1900..2100 three times in four and anywhere in 1..9999
    /// otherwise, draws the month and day uniformly, and shrinks towards 1 January 2000.
    /// </returns>
    public static Generator<System.DateOnly> DateOnly() =>
        DateOnly(System.DateOnly.MinValue, System.DateOnly.MaxValue);

    /// <summary>
    /// Creates a generator for dates within a specified range.
    /// </summary>
    /// <param name="min">The inclusive lower bound of the dates to generate.</param>
    /// <param name="max">The inclusive upper bound of the dates to generate.</param>
    /// <returns>
    /// A generator that produces dates from <paramref name="min"/> to <paramref name="max"/>,
    /// distributed as <see cref="DateOnly()"/> within the range, and shrinks towards 1 January
    /// 2000, or towards the simplest date the bounds allow when the range excludes it.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="min"/> is greater than <paramref name="max"/>.
    /// </exception>
    public static Generator<System.DateOnly> DateOnly(System.DateOnly min, System.DateOnly max) =>
        new DateOnlyGenerator(min, max);

    /// <summary>
    /// Creates a generator for times of day over the full range of <see cref="System.TimeOnly"/>.
    /// </summary>
    /// <returns>
    /// A generator that draws the hour, minute, second, millisecond and remaining ticks uniformly,
    /// produces a whole hour, minute, second or millisecond about as often as not, and shrinks
    /// towards midnight.
    /// </returns>
    /// <remarks>
    /// Shrinking minimises the more significant components first, so a failure that depends on a
    /// threshold can end on the round value just past it rather than on the threshold itself.
    /// </remarks>
    public static Generator<System.TimeOnly> TimeOnly() =>
        TimeOnly(System.TimeOnly.MinValue, System.TimeOnly.MaxValue);

    /// <summary>
    /// Creates a generator for times of day within a specified range.
    /// </summary>
    /// <param name="min">The inclusive lower bound of the times to generate.</param>
    /// <param name="max">The inclusive upper bound of the times to generate, not before <paramref name="min"/>.</param>
    /// <returns>
    /// A generator that produces times from <paramref name="min"/> to <paramref name="max"/>,
    /// distributed as <see cref="TimeOnly()"/> within the range, and shrinks towards midnight, or
    /// towards the simplest time the bounds allow when the range excludes it.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="min"/> is greater than <paramref name="max"/>. The range does not wrap past
    /// midnight.
    /// </exception>
    public static Generator<System.TimeOnly> TimeOnly(System.TimeOnly min, System.TimeOnly max) =>
        new TimeOnlyGenerator(min, max);

    /// <summary>
    /// Creates a generator for time spans over the full range of <see cref="System.TimeSpan"/>.
    /// </summary>
    /// <returns>
    /// A generator that picks a unit uniformly among ticks, milliseconds, seconds, minutes, hours
    /// and days, then a whole number of that unit of either sign biased towards small counts and
    /// the extremes, and shrinks towards <see cref="System.TimeSpan.Zero"/>.
    /// </returns>
    public static Generator<System.TimeSpan> TimeSpan() =>
        TimeSpan(System.TimeSpan.MinValue, System.TimeSpan.MaxValue);

    /// <summary>
    /// Creates a generator for time spans within a specified range.
    /// </summary>
    /// <param name="min">The inclusive lower bound of the spans to generate.</param>
    /// <param name="max">The inclusive upper bound of the spans to generate.</param>
    /// <returns>
    /// A generator that produces spans from <paramref name="min"/> to <paramref name="max"/> as a
    /// whole number of one of the units the range admits, distributed as <see cref="TimeSpan()"/>
    /// within the range, and shrinks towards zero, or towards the bound nearest zero when the range
    /// excludes it.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="min"/> is greater than <paramref name="max"/>.
    /// </exception>
    public static Generator<System.TimeSpan> TimeSpan(System.TimeSpan min, System.TimeSpan max) =>
        new TimeSpanGenerator(min, max);
}
