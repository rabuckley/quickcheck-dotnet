namespace QuickCheck.Tests;

public sealed class GenerateTimeTests
{
    private static T Minimal<T>(Generator<T> generator, Func<T, bool>? property = null) =>
        Property.ForAll(generator, property ?? (static _ => false)).Check(new CheckOptions { Seed = 19 }).Minimal!.Value;

    [Fact]
    public void DateTime_WithFullRange_ShouldBiasTowardsModernYearsAndRoundTimes()
    {
        // Act
        var samples = Generate.DateTime().Sample(count: 2000, seed: 1);

        // Assert
        Assert.All(samples, static d => Assert.Equal(DateTimeKind.Unspecified, d.Kind));
        Assert.Contains(samples, static d => d.Year < 1900);
        Assert.Contains(samples, static d => d.Year > 2100);
        Assert.InRange(samples.Count(static d => d.Year is >= 1900 and <= 2100), 1300, 1700);
        Assert.Contains(samples, static d => d.TimeOfDay == TimeSpan.Zero);
        Assert.Contains(samples, static d => d.TimeOfDay != TimeSpan.Zero && d.TimeOfDay.Ticks % TimeSpan.TicksPerMinute == 0);
        Assert.Contains(samples, static d => d.Ticks % TimeSpan.TicksPerMillisecond != 0);
        Assert.Contains(samples, static d => d.Month == 2 && d.Day == 29);
        Assert.Contains(samples, static d => d.Day == 31);
    }

    [Fact]
    public void DateTime_WithBounds_ShouldStayInRangeAndReachBothBounds()
    {
        // Arrange
        var leapMin = new DateTime(2000, 2, 29, 23, 59, 59).AddTicks(9_999_999);
        var leapMax = new DateTime(2000, 3, 1);
        var hourMin = new DateTime(2024, 5, 5, 10, 0, 0);
        var hourMax = new DateTime(2024, 5, 5, 11, 0, 0);
        var point = new DateTime(2024, 5, 5, 10, 30, 0);

        // Act
        var acrossLeapDay = Generate.DateTime(leapMin, leapMax).Sample(count: 200, seed: 2);
        var withinHour = Generate.DateTime(hourMin, hourMax).Sample(count: 500, seed: 3);
        var single = Generate.DateTime(point, point).Sample(count: 20, seed: 4);
        var full = Generate.DateTime(DateTime.MinValue, DateTime.MaxValue).Sample(count: 500, seed: 5);

        // Assert
        Assert.Equal([leapMin, leapMax], acrossLeapDay.Distinct().Order());
        Assert.All(withinHour, d => Assert.InRange(d, hourMin, hourMax));
        Assert.Contains(hourMin, withinHour);
        Assert.Contains(hourMax, withinHour);
        Assert.Contains(withinHour, d => d > hourMin && d < hourMax);
        Assert.All(single, d => Assert.Equal(point, d));
        Assert.Equal(500, full.Count);
    }

    [Fact]
    public void DateTime_WithBoundsThatCarryAKind_ShouldGenerateThatKind()
    {
        // Arrange
        var min = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var max = new DateTime(2024, 6, 2, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var utc = Generate.DateTime(min, max).Sample(count: 300, seed: 6);
        var local = Generate.DateTime(DateTimeKind.Local).Sample(count: 500, seed: 7);
        var unspecified = Generate.DateTime(new DateTime(2020, 1, 1), new DateTime(2020, 12, 31)).Sample(count: 50, seed: 8);
        void MixedKinds() => Generate.DateTime(min, DateTime.SpecifyKind(max, DateTimeKind.Local));

        // Assert
        Assert.All(utc, static d => Assert.Equal(DateTimeKind.Utc, d.Kind));

        // The window the caller stated is the window the system under test sees, whatever the machine's zone.
        Assert.All(utc, d => Assert.InRange(d.ToUniversalTime(), min, max));

        Assert.All(local, static d => Assert.Equal(DateTimeKind.Local, d.Kind));
        Assert.All(unspecified, static d => Assert.Equal(DateTimeKind.Unspecified, d.Kind));
        Assert.Throws<ArgumentException>(MixedKinds);
    }

    [Fact]
    public void TimeGenerators_WithFullRange_ShouldProduceTheBounds()
    {
        // Act
        var dateTimes = Generate.DateTime().Sample(count: 2000, seed: 20);
        var dates = Generate.DateOnly().Sample(count: 2000, seed: 21);
        var times = Generate.TimeOnly().Sample(count: 2000, seed: 22);
        var instants = Generate.DateTimeOffset().Sample(count: 2000, seed: 23);

        // Assert
        Assert.Contains(DateTime.MinValue, dateTimes);
        Assert.Contains(DateTime.MaxValue, dateTimes);
        Assert.Contains(DateOnly.MinValue, dates);
        Assert.Contains(DateOnly.MaxValue, dates);
        Assert.Contains(TimeOnly.MinValue, times);
        Assert.Contains(TimeOnly.MaxValue, times);
        Assert.Contains(DateTimeOffset.MinValue, instants);
        Assert.Contains(DateTimeOffset.MaxValue, instants);
        Assert.All(instants.Where(static d => d.UtcTicks == 0 || d.UtcTicks == DateTime.MaxValue.Ticks), static d => Assert.Equal(TimeSpan.Zero, d.Offset));
    }

    [Fact]
    public void DateTime_WithBounds_ShouldProduceBothBounds()
    {
        // Arrange
        var min = new DateTime(2024, 1, 1);
        var max = new DateTime(2024, 12, 31, 23, 59, 59);

        // Act
        var year = Generate.DateTime(min, max).Sample(count: 500, seed: 24);

        // Assert
        Assert.All(year, d => Assert.InRange(d, min, max));
        Assert.Contains(min, year);
        Assert.Contains(max, year);
    }

    [Fact]
    public void DateTime_WithEdgeExamples_ShouldShrinkLikeAnyOtherExample()
    {
        // Arrange
        var alwaysFalse = Enumerable.Range(1, 64)
            .Select(seed => Property.ForAll(Generate.DateTime(), static _ => false).Check(new CheckOptions { Seed = (ulong)seed }).Minimal!.Value);

        // Act
        var notMinValue = Property.ForAll(Generate.DateTime(), static d => d != DateTime.MinValue)
            .Check(new CheckOptions { Seed = 1, RunCount = 2000 });
        var firstExample = Generate.DateTime().Sample(count: 1, seed: 82).Single();
        var pastThreshold = Property.ForAll(Generate.DateTime(), static d => d.Year <= 2000)
            .Check(new CheckOptions { Seed = 82 });

        // Assert
        Assert.All(alwaysFalse, static minimal => Assert.Equal(new DateTime(2000, 1, 1), minimal));
        Assert.Equal(DateTime.MinValue, notMinValue.Minimal!.Value);
        Assert.Equal(DateTime.MaxValue, firstExample);
        Assert.Equal(new DateTime(2001, 1, 1), pastThreshold.Minimal!.Value);
    }

    [Fact]
    public void TimeGenerators_WithInvalidArguments_ShouldThrowArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => Generate.DateTime(new DateTime(2001, 1, 1), new DateTime(2000, 1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Generate.DateTime((DateTimeKind)7));
        Assert.Throws<ArgumentOutOfRangeException>(() => Generate.DateTimeOffset(DateTimeOffset.MaxValue, DateTimeOffset.MinValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => Generate.DateTimeOffset(TimeSpan.FromSeconds(90)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Generate.DateTimeOffset(TimeSpan.FromHours(15)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Generate.DateTimeOffset(DateTimeOffset.MaxValue, DateTimeOffset.MaxValue, TimeSpan.FromHours(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Generate.DateOnly(new DateOnly(2001, 1, 1), new DateOnly(2000, 1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Generate.TimeOnly(new TimeOnly(11, 0), new TimeOnly(10, 30)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Generate.TimeSpan(TimeSpan.FromHours(1), TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void TimeGenerators_WithFalsifiedProperty_ShouldShrinkToTheSimplestValueInRange()
    {
        // Act & Assert
        Assert.Equal(new DateTime(2000, 1, 1), Minimal(Generate.DateTime()));
        Assert.Equal(new DateTime(1995, 1, 1), Minimal(Generate.DateTime(new DateTime(1990, 3, 4), new DateTime(1995, 5, 6))));
        Assert.Equal(
            new DateTime(2001, 6, 15, 8, 0, 0),
            Minimal(Generate.DateTime(new DateTime(2001, 6, 15, 8, 0, 0), new DateTime(2010, 1, 1))));
        Assert.Equal(new DateOnly(2000, 1, 1), Minimal(Generate.DateOnly()));
        Assert.Equal(TimeOnly.MinValue, Minimal(Generate.TimeOnly()));
        Assert.Equal(new TimeOnly(10, 30), Minimal(Generate.TimeOnly(new TimeOnly(10, 30), new TimeOnly(11, 0))));

        var offset = Minimal(Generate.DateTimeOffset());
        Assert.Equal(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), offset);
        Assert.Equal(TimeSpan.Zero, offset.Offset);

        Assert.Equal(TimeSpan.Zero, Minimal(Generate.TimeSpan()));
        Assert.Equal(TimeSpan.FromSeconds(1), Minimal(Generate.TimeSpan(TimeSpan.FromSeconds(1), TimeSpan.FromHours(1))));
        Assert.Equal(TimeSpan.FromSeconds(-1), Minimal(Generate.TimeSpan(TimeSpan.FromHours(-1), TimeSpan.FromSeconds(-1))));
        Assert.Equal(
            TimeSpan.MaxValue - TimeSpan.FromTicks(5),
            Minimal(Generate.TimeSpan(TimeSpan.MaxValue - TimeSpan.FromTicks(5), TimeSpan.MaxValue)));
    }

    [Fact]
    public void TimeGenerators_WithThresholdProperty_ShouldShrinkToTheThreshold()
    {
        // Act & Assert
        Assert.Equal(TimeSpan.FromMinutes(90), Minimal(Generate.TimeSpan(), static s => s < TimeSpan.FromMinutes(90)));
        Assert.Equal(new DateTime(2001, 1, 1), Minimal(Generate.DateTime(), static d => d.Year <= 2000));
        Assert.Equal(new DateOnly(2000, 6, 1), Minimal(Generate.DateOnly(), static d => d < new DateOnly(2000, 6, 1)));
    }

    [Fact]
    public void DateGenerators_WithABoundOnAYearOrMonthBoundary_ShouldGiveItItsShareOfTheDays()
    {
        // Arrange
        var yearMin = new DateTime(2024, 1, 1);
        var yearMax = new DateTime(2025, 1, 1);
        var monthMin = new DateTime(2024, 1, 1);
        var monthMax = new DateTime(2024, 6, 1);

        // Act
        var acrossYear = Generate.DateTime(yearMin, yearMax).Sample(count: 4000, seed: 40);
        var acrossMonth = Generate.DateTime(monthMin, monthMax).Sample(count: 3000, seed: 41);

        // Assert
        Assert.All(acrossYear, d => Assert.InRange(d, yearMin, yearMax));
        Assert.InRange(acrossYear.Count(static d => d.Year == 2025), 150, 350);
        Assert.InRange(acrossMonth.Count(static d => d.Month == 6), 120, 300);
    }

    [Fact]
    public void DateOnly_WithinOneMonth_ShouldDrawEachDayEvenly()
    {
        // Arrange
        var min = new DateOnly(2024, 3, 10);
        var max = new DateOnly(2024, 3, 20);

        // Act
        var samples = Generate.DateOnly(min, max).Sample(count: 2200, seed: 42);

        // Assert
        Assert.Equal(11, samples.Distinct().Count());
        foreach (var day in Enumerable.Range(11, 9))
        {
            Assert.InRange(samples.Count(d => d.Day == day), 100, 260);
        }
    }

    [Fact]
    public void DateOnly_WithBoundsAcrossTheModernBandEdges_ShouldStayInRangeAndReachEveryDay()
    {
        // Arrange
        var lowMin = new DateOnly(1899, 12, 25);
        var lowMax = new DateOnly(1900, 1, 5);
        var highMin = new DateOnly(2100, 12, 25);
        var highMax = new DateOnly(2101, 1, 5);

        // Act
        var low = Generate.DateOnly(lowMin, lowMax).Sample(count: 2000, seed: 43);
        var high = Generate.DateOnly(highMin, highMax).Sample(count: 2000, seed: 44);

        // Assert
        Assert.All(low, d => Assert.InRange(d, lowMin, lowMax));
        Assert.All(high, d => Assert.InRange(d, highMin, highMax));
        Assert.Equal(12, low.Distinct().Count());
        Assert.Equal(12, high.Distinct().Count());
    }

    [Fact]
    public void DateOnly_WithManySamples_ShouldReachMonthEndsAndRespectBounds()
    {
        // Arrange
        var min = new DateOnly(2024, 2, 28);
        var max = new DateOnly(2024, 3, 1);

        // Act
        var samples = Generate.DateOnly().Sample(count: 1000, seed: 9);
        var bounded = Generate.DateOnly(min, max).Sample(count: 200, seed: 10);

        // Assert
        Assert.Contains(samples, static d => d.Day == 31);
        Assert.Contains(samples, static d => d.Year < 1900);
        Assert.Contains(samples, static d => d.Year > 2100);
        Assert.Equal([min, new DateOnly(2024, 2, 29), max], bounded.Distinct().Order());
    }

    [Fact]
    public void TimeOnly_WithManySamples_ShouldMixRoundAndPreciseTimesAndRespectBounds()
    {
        // Arrange
        var min = new TimeOnly(10, 30);
        var max = new TimeOnly(10, 31);

        // Act
        var samples = Generate.TimeOnly().Sample(count: 1000, seed: 11);
        var bounded = Generate.TimeOnly(min, max).Sample(count: 300, seed: 12);

        // Assert
        Assert.Contains(samples, static t => t != TimeOnly.MinValue && t.Ticks % TimeSpan.TicksPerHour == 0);
        Assert.Contains(samples, static t => t.Ticks % TimeSpan.TicksPerMillisecond != 0);
        Assert.Contains(samples, static t => t.Hour == 23);
        Assert.All(bounded, t => Assert.InRange(t, min, max));
        Assert.Contains(min, bounded);
        Assert.Contains(max, bounded);
        Assert.Contains(bounded, t => t > min && t < max);
    }

    [Fact]
    public void DateTimeOffset_WithFullRange_ShouldDrawWholeMinuteOffsetsFavouringWholeHours()
    {
        // Arrange
        var maxOffset = TimeSpan.FromHours(14);
        var nearMax = new DateTimeOffset(9999, 12, 31, 23, 0, 0, maxOffset);

        // Act
        var samples = Generate.DateTimeOffset().Sample(count: 2000, seed: 13);
        var nearMin = Generate.DateTimeOffset(DateTimeOffset.MinValue, DateTimeOffset.MinValue + TimeSpan.FromHours(1)).Sample(count: 300, seed: 14);
        var nearEnd = Generate.DateTimeOffset(nearMax, DateTimeOffset.MaxValue).Sample(count: 300, seed: 15);
        var single = Generate.DateTimeOffset(DateTimeOffset.MaxValue, DateTimeOffset.MaxValue).Sample(count: 20, seed: 16);

        // Assert
        Assert.All(samples, d => Assert.InRange(d.Offset, -maxOffset, maxOffset));
        Assert.All(samples, static d => Assert.Equal(0, d.Offset.Ticks % TimeSpan.TicksPerMinute));
        Assert.InRange(samples.Count(static d => d.Offset.Ticks % TimeSpan.TicksPerHour == 0), 1350, 1650);
        Assert.Contains(samples, static d => d.Offset < TimeSpan.Zero);
        Assert.Contains(samples, static d => d.Offset > TimeSpan.Zero);
        Assert.Contains(samples, static d => d.Offset == TimeSpan.Zero);
        Assert.Contains(samples, static d => d.Offset.Minutes != 0);
        Assert.All(nearMin, d => Assert.InRange(d.UtcTicks, 0, TimeSpan.TicksPerHour));
        Assert.All(nearEnd, d => Assert.InRange(d.UtcTicks, nearMax.UtcTicks, DateTime.MaxValue.Ticks));
        Assert.All(single, d => Assert.Equal(DateTimeOffset.MaxValue.UtcTicks, d.UtcTicks));
    }

    [Fact]
    public void DateTimeOffset_WithBoundsAtAnOffset_ShouldProduceTheBoundsVerbatimAndVaryOtherOffsets()
    {
        // Arrange
        var offset = TimeSpan.FromHours(5.5);
        var min = new DateTimeOffset(2024, 1, 1, 0, 0, 0, offset);
        var max = new DateTimeOffset(2024, 12, 31, 0, 0, 0, offset);
        var westMin = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.FromHours(-9.5));

        // Act
        var samples = Generate.DateTimeOffset(min, max).Sample(count: 500, seed: 32);
        var west = Generate.DateTimeOffset(westMin, max).Sample(count: 500, seed: 33);

        // Assert
        Assert.All(samples, d => Assert.InRange(d.UtcTicks, min.UtcTicks, max.UtcTicks));
        Assert.Contains(samples, d => d.EqualsExact(min));
        Assert.Contains(samples, d => d.EqualsExact(max));
        Assert.Contains(samples, d => d.Offset != offset);
        Assert.Contains(west, d => d.EqualsExact(westMin));
    }

    [Fact]
    public void DateTimeOffset_WithAFixedOffset_ShouldTrimTheRangeToWhatThatOffsetRepresents()
    {
        // Arrange
        var offset = TimeSpan.FromHours(5.5);
        var min = new DateTimeOffset(2024, 1, 1, 0, 0, 0, offset);
        var max = new DateTimeOffset(2024, 12, 31, 0, 0, 0, offset);

        // Act
        var full = Generate.DateTimeOffset(offset).Sample(count: 500, seed: 30);
        var bounded = Generate.DateTimeOffset(min, max, offset).Sample(count: 300, seed: 31);

        // Assert
        Assert.All(full, d => Assert.Equal(offset, d.Offset));
        Assert.All(full, d => Assert.InRange(d.UtcTicks, 0, DateTime.MaxValue.Ticks - offset.Ticks));
        Assert.Contains(full, static d => d.UtcTicks == 0);
        Assert.Contains(full, d => d.UtcTicks == DateTime.MaxValue.Ticks - offset.Ticks);
        Assert.All(bounded, d => Assert.Equal(offset, d.Offset));
        Assert.All(bounded, d => Assert.InRange(d, min, max));
        Assert.Contains(min, bounded);
        Assert.Contains(max, bounded);
    }

    [Fact]
    public void TimeSpan_WithFullRange_ShouldProduceRoundSpansOfBothSignsAndTheExtremes()
    {
        // Act
        var samples = Generate.TimeSpan().Sample(count: 3000, seed: 17);
        var bounded = Generate.TimeSpan(TimeSpan.FromSeconds(1), TimeSpan.FromHours(1)).Sample(count: 500, seed: 18);

        // Assert
        Assert.Contains(TimeSpan.Zero, samples);
        Assert.Contains(samples, static s => s < TimeSpan.Zero);
        Assert.Contains(samples, static s => s > TimeSpan.Zero);
        Assert.Contains(samples, static s => s != TimeSpan.Zero && s.Ticks % TimeSpan.TicksPerDay == 0);
        Assert.Contains(samples, static s => s.Ticks % TimeSpan.TicksPerHour == 0 && s.Ticks % TimeSpan.TicksPerDay != 0);
        Assert.Contains(samples, static s => s.Ticks % TimeSpan.TicksPerMillisecond != 0);
        Assert.Contains(TimeSpan.MaxValue, samples);
        Assert.Contains(TimeSpan.MinValue, samples);
        Assert.All(bounded, static s => Assert.InRange(s, TimeSpan.FromSeconds(1), TimeSpan.FromHours(1)));
        Assert.Contains(TimeSpan.FromSeconds(1), bounded);
        Assert.Contains(TimeSpan.FromHours(1), bounded);
        Assert.Contains(bounded, static s => s.Ticks % TimeSpan.TicksPerMinute == 0 && s.Ticks % TimeSpan.TicksPerHour != 0);
    }
}
