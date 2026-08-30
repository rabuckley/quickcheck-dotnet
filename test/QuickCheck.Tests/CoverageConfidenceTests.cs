namespace QuickCheck.Tests;

public sealed class CoverageConfidenceTests
{
    private static readonly CheckOptions Confident = new() { Seed = 1, CoverageConfidence = Confidence.Default };

    [Fact]
    public void Check_WithConfidenceAndNeverHitLabel_ShouldFailAtTheFirstLookBeforeRunCount()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>(), _ => Property.Cover(false, 50, "never"));

        // Act
        var result = property.Check(Confident with { RunCount = 1000 });
        var exception = Assert.Throws<PropertyFailedException>(result.ThrowIfFailed);

        // Assert
        Assert.Equal(PropertyOutcome.InsufficientCoverage, result.Outcome);
        Assert.Equal(100, result.TestsRun);

        Assert.StartsWith(
            "Insufficient coverage after 100 tests (seed 1).\n"
            + "  Only 0% never, but required 50% (the true rate is 0% to 2%)\n"
            + "  0% never (required 50%)",
            exception.Message.ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(150)]
    [InlineData(1000)]
    public void Check_WithConfidenceAndLabelAlwaysHit_ShouldPassAtExactlyRunCount(int runCount)
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>(), _ => Property.Cover(true, 50, "always"));

        // Act
        var result = property.Check(Confident with { RunCount = runCount });

        // Assert
        Assert.Equal(PropertyOutcome.Passed, result.Outcome);
        Assert.Equal(runCount, result.TestsRun);
    }

    [Fact]
    public void Check_WithConfidenceAndLabelInsideTheMargin_ShouldRunPastRunCountUntilSufficient()
    {
        // Arrange
        var n = 0;
        var property = Property.ForAll(Generate.Integer<int>(), _ => Property.Cover(n++ % 10 < 6, 50, "most"));

        // Act
        var result = property.Check(Confident with { RunCount = 100 });

        // Assert
        Assert.Equal(PropertyOutcome.Passed, result.Outcome);
        Assert.Equal(800, result.TestsRun);
        Assert.Equal(480, result.Statistics.Labels["most"]);
    }

    [Fact]
    public void Check_WithConfidenceAndRateJustUnderTheMinimum_ShouldPassWithTheRequirementMetAndNoShortfallPrinted()
    {
        // Arrange
        var n = 0;

        var property = Property.ForAll(Generate.Integer<int>(), _ =>
        {
            // Every other example, less one in 200: 49.5%.
            var i = n++;
            Property.Cover(i % 2 == 0 && i % 200 != 0, 50, "most");
        });

        // Act
        var result = property.Check(Confident);

        // Assert
        Assert.Equal(PropertyOutcome.Passed, result.Outcome);
        Assert.Equal(6400, result.TestsRun);

        Assert.Equal(new CoverageRequirement("most", MinimumPercent: 50, Count: 3168, IsMet: true),
            Assert.Single(result.Statistics.Coverage));

        Assert.DoesNotContain("Only", result.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Check_WithConfidenceAndOneHundredPercentMinimumMetByEveryExample_ShouldPassAtFourHundred()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>(), _ => Property.Cover(true, 100, "all"));

        // Act
        var result = property.Check(Confident);

        // Assert
        Assert.Equal(PropertyOutcome.Passed, result.Outcome);
        Assert.Equal(400, result.TestsRun);
    }

    [Fact]
    public void Check_WithConfidenceAndOneHundredPercentMinimumMissedOnce_ShouldFailAtTheFirstLook()
    {
        // Arrange
        var n = 0;
        var property = Property.ForAll(Generate.Integer<int>(), _ => Property.Cover(++n != 50, 100, "all"));

        // Act
        var result = property.Check(Confident with { RunCount = 1000 });

        // Assert
        Assert.Equal(PropertyOutcome.InsufficientCoverage, result.Outcome);
        Assert.Equal(100, result.TestsRun);
    }

    [Fact]
    public void Check_WithConfidenceAndZeroMinimum_ShouldPassAtRunCount()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>(), _ => Property.Cover(false, 0, "never"));

        // Act
        var result = property.Check(Confident);

        // Assert
        Assert.Equal(PropertyOutcome.Passed, result.Outcome);
        Assert.Equal(100, result.TestsRun);
    }

    [Fact]
    public void Check_WithConfidenceAndNoCoverCalls_ShouldPassAtRunCount()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>(), _ => Property.Label("seen"));

        // Act
        var result = property.Check(Confident with { RunCount = 250 });

        // Assert
        Assert.Equal(PropertyOutcome.Passed, result.Outcome);
        Assert.Equal(250, result.TestsRun);
    }

    [Fact]
    public void Check_WithConfidenceAndDiscards_ShouldScaleTheDiscardBudgetWithPasses()
    {
        // Arrange
        var m = 0;
        var n = 0;

        var property = Property.ForAll(Generate.Integer<int>(), _ =>
        {
            Property.Assume(m++ % 2 == 0);
            Property.Cover(n++ % 10 < 6, 50, "most");
        });

        // Act
        var result = property.Check(Confident with { RunCount = 100, MaxDiscardRatio = 2 });

        // Assert
        Assert.Equal(PropertyOutcome.Passed, result.Outcome);
        Assert.Equal(800, result.TestsRun);
        Assert.True(result.Discards > 200, "a fixed RunCount * MaxDiscardRatio budget would have been exhausted");
    }

    [Fact]
    public void Check_WithConfidenceAndFalsifiedBeforeTheFirstLook_ShouldReportFalsified()
    {
        // Arrange
        var n = 0;

        var property = Property.ForAll(Generate.Integer<int>(), _ =>
        {
            Property.Cover(false, 50, "never");
            return n++ < 50;
        });

        // Act
        var result = property.Check(Confident);

        // Assert
        Assert.Equal(PropertyOutcome.Falsified, result.Outcome);
        Assert.Equal(50, result.TestsRun);
    }

    [Theory]
    [InlineData(1L, 0.9)]
    [InlineData(0L, 0.9)]
    [InlineData(-5L, 0.9)]
    [InlineData(1_000L, 0.0)]
    [InlineData(1_000L, 1.0)]
    [InlineData(1_000L, -0.1)]
    [InlineData(1_000L, 1.5)]
    [InlineData(1_000L, double.NaN)]
    public void Confidence_WithCertaintyBelowTwoOrToleranceOutsideZeroToOne_ShouldThrowArgumentOutOfRangeException(
        long certainty,
        double tolerance)
    {
        // Arrange
        var construct = () => new Confidence { Certainty = certainty, Tolerance = tolerance };

        // Act
        var exception = Record.Exception(construct);

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    [Fact]
    public void Confidence_Default_ShouldBeOneInABillionAtNinetyPercentToleranceAndOffByDefault()
    {
        // Arrange
        var confidence = Confidence.Default;

        // Act
        var options = CheckOptions.Default;

        // Assert
        Assert.Equal(1_000_000_000, confidence.Certainty);
        Assert.Equal(0.9, confidence.Tolerance);
        Assert.Null(options.CoverageConfidence);
    }
}
