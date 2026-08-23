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
    /// millisecond five times in six, produces <see cref="System.DateTime.MinValue"/> and
    /// <see cref="System.DateTime.MaxValue"/> more often than chance, and shrinks towards midnight
    /// on 1 January 2000.
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
    /// produces the bounds more often than chance, and shrinks towards midnight on 1 January 2000,
    /// or towards the simplest value the bounds allow when the range excludes it.
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
    /// then a local date and time distributed as <see cref="DateTime(DateTimeKind)"/>, produces
    /// <see cref="System.DateTimeOffset.MinValue"/> and <see cref="System.DateTimeOffset.MaxValue"/>
    /// more often than chance, and shrinks towards midnight on 1 January 2000 UTC.
    /// </returns>
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
    /// <see cref="DateTimeOffset()"/> does, produces the bounds, exactly as given, more often than
    /// chance, and shrinks towards midnight on 1 January 2000 UTC, or towards the simplest value the
    /// bounds allow when the range excludes it.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="min"/> is a later instant than <paramref name="max"/>.
    /// </exception>
    /// <remarks>
    /// The bounds' offsets locate the ends of the range; they do not fix the offsets of the values,
    /// which vary from draw to draw. To keep every value at one offset, pass it explicitly:
    /// <c>Generate.DateTimeOffset(min, max, min.Offset)</c>.
    /// </remarks>
    public static Generator<System.DateTimeOffset> DateTimeOffset(System.DateTimeOffset min, System.DateTimeOffset max) =>
        new DateTimeOffsetGenerator(min, max);

    /// <summary>
    /// Creates a generator for instants at a fixed offset.
    /// </summary>
    /// <param name="offset">The offset from UTC of every value, as a whole number of minutes from −14:00 to +14:00.</param>
    /// <returns>
    /// A generator that produces every instant <paramref name="offset"/> can represent, distributed
    /// as <see cref="DateTime(DateTimeKind)"/> over the local clock time, produces the ends of that
    /// window more often than chance, and shrinks towards midnight on 1 January 2000 at
    /// <paramref name="offset"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="offset"/> is not a whole number of minutes from −14:00 to +14:00.
    /// </exception>
    /// <remarks>
    /// An offset trims whichever end of <see cref="System.DateTimeOffset"/>'s range would push the
    /// local clock time past <see cref="System.DateTime"/>'s, so the window is up to 14 hours short
    /// of the full range at one end.
    /// </remarks>
    public static Generator<System.DateTimeOffset> DateTimeOffset(System.TimeSpan offset) =>
        DateTimeOffset(System.DateTimeOffset.MinValue, System.DateTimeOffset.MaxValue, offset);

    /// <summary>
    /// Creates a generator for instants at a fixed offset within a specified range.
    /// </summary>
    /// <param name="min">The inclusive lower bound of the instants to generate.</param>
    /// <param name="max">The inclusive upper bound of the instants to generate.</param>
    /// <param name="offset">The offset from UTC of every value, as a whole number of minutes from −14:00 to +14:00.</param>
    /// <returns>
    /// A generator that produces the instants from <paramref name="min"/> to <paramref name="max"/>,
    /// compared as instants, that <paramref name="offset"/> can represent, distributed as
    /// <see cref="DateTime(DateTimeKind)"/> over the local clock time, produces the ends of that
    /// window more often than chance, and shrinks towards midnight on 1 January 2000 at
    /// <paramref name="offset"/>, or towards the simplest value the window allows when it excludes it.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="min"/> is a later instant than <paramref name="max"/>, <paramref name="offset"/>
    /// is not a whole number of minutes from −14:00 to +14:00, or no instant in the range is
    /// representable at <paramref name="offset"/>.
    /// </exception>
    /// <remarks>
    /// <paramref name="min"/> and <paramref name="max"/> are reachable only where
    /// <paramref name="offset"/> keeps the local clock time inside <see cref="System.DateTime"/>'s
    /// range, so a bound nearer <see cref="System.DateTimeOffset.MinValue"/> or
    /// <see cref="System.DateTimeOffset.MaxValue"/> than <paramref name="offset"/> can represent is
    /// trimmed to the nearest instant that it can, and never appears.
    /// </remarks>
    public static Generator<System.DateTimeOffset> DateTimeOffset(
        System.DateTimeOffset min,
        System.DateTimeOffset max,
        System.TimeSpan offset) =>
        new FixedOffsetGenerator(min, max, offset);

    /// <summary>
    /// Creates a generator for dates over the full range of <see cref="System.DateOnly"/>.
    /// </summary>
    /// <returns>
    /// A generator that draws the year in 1900..2100 three times in four and anywhere in 1..9999
    /// otherwise, draws the month and day uniformly, produces <see cref="System.DateOnly.MinValue"/>
    /// and <see cref="System.DateOnly.MaxValue"/> more often than chance, and shrinks towards
    /// 1 January 2000.
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
    /// distributed as <see cref="DateOnly()"/> within the range, produces the bounds more often than
    /// chance, and shrinks towards 1 January 2000, or towards the simplest date the bounds allow
    /// when the range excludes it.
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
    /// produces a whole hour, minute, second or millisecond four times in five, produces
    /// <see cref="System.TimeOnly.MinValue"/> and <see cref="System.TimeOnly.MaxValue"/> more often
    /// than chance, and shrinks towards midnight.
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
    /// distributed as <see cref="TimeOnly()"/> within the range, produces the bounds more often than
    /// chance, and shrinks towards midnight, or towards the simplest time the bounds allow when the
    /// range excludes it.
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
    /// <remarks>
    /// Sentinels are not forced boundaries: <see cref="Timeout.InfiniteTimeSpan"/> (−1 millisecond)
    /// appears as a small count of the millisecond unit, at roughly one draw in 700. A property
    /// that hinges on it should raise the rate itself:
    /// <c>Generate.Frequency((15, Generate.TimeSpan()), (1, Generate.Constant(Timeout.InfiniteTimeSpan)))</c>.
    /// </remarks>
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
