namespace QuickCheck.Tests;

public sealed class StatisticsTests
{
    private static readonly CheckOptions Hundred = new() { Seed = 1, RunCount = 100 };

    [Fact]
    public void Label_WithRepeatedCallsInOneExample_ShouldCountOnce()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>(), _ =>
        {
            Property.Label("every");
            Property.Label("every");
        });

        // Act
        var result = property.Check(Hundred);

        // Assert
        Assert.Equal(PropertyOutcome.Passed, result.Outcome);
        Assert.Equal(100, result.Statistics.Labels["every"]);
        Assert.Contains("100% every", result.ToString());
    }

    [Fact]
    public void Classify_WithConditionNeverTrue_ShouldReportTheLabelAtZeroPercent()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>(), _ => Property.Classify(false, "never"));

        // Act
        var result = property.Check(Hundred);

        // Assert
        Assert.Equal(0, result.Statistics.Labels["never"]);
        Assert.Contains("0% never", result.ToString());
    }

    [Fact]
    public void Collect_WithRepeatedAndDistinctValues_ShouldCountEachValueOncePerExampleAndPrintItVerbatim()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>(), _ =>
        {
            Property.Collect("command", "Put");
            Property.Collect("command", "Get");
            Property.Collect("command", "Put");
        });

        // Act
        var result = property.Check(Hundred);

        // Assert
        Assert.Equal(100, result.Statistics.Tables["command"]["Put"]);
        Assert.Equal(100, result.Statistics.Tables["command"]["Get"]);
        Assert.Contains("\n  command:\n    100% Get\n    100% Put", result.ToString().ReplaceLineEndings("\n"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void StatisticsCalls_WithNullOrEmptyText_ShouldThrowArgumentException(string? text)
    {
        // Arrange
        Action[] calls =
        [
            () => Property.Classify(true, text!),
            () => Property.Label(text!),
            () => Property.Collect(text!, "value"),
            () => Property.Collect("name", text!),
            () => Property.Cover(true, 10, text!)
        ];

        // Act
        var exceptions = calls.Select(BodyException);

        // Assert
        Assert.All(exceptions, static exception => Assert.IsAssignableFrom<ArgumentException>(exception));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Cover_WithMinimumOutsideZeroToOneHundred_ShouldThrowArgumentOutOfRangeException(double minimumPercent)
    {
        // Arrange
        var call = () => Property.Cover(true, minimumPercent, "label");

        // Act
        var exception = BodyException(call);

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    [Fact]
    public void Label_WithDiscardedExamples_ShouldNotCountTheDiscards()
    {
        // Arrange
        var property = Property.ForAll(Generate.Between(0, 9), x =>
        {
            Property.Label("all");
            Property.Assume(x < 5);
        });

        // Act
        var result = property.Check(Hundred);

        // Assert
        Assert.Equal(PropertyOutcome.Passed, result.Outcome);
        Assert.True(result.Discards > 0);
        Assert.Equal(100, result.Statistics.Labels["all"]);
    }

    [Fact]
    public void Label_WithFalsifiedProperty_ShouldNotCountShrinkCandidatesAndShouldKeepTheStatisticsSoFar()
    {
        // Arrange
        var invocations = 0;

        var property = Property.ForAll(Generate.Between(0, 1000), x =>
        {
            invocations++;
            Property.Label("seen");
            return x < 500;
        });

        // Act
        var result = property.Check(Hundred);

        // Assert
        Assert.Equal(PropertyOutcome.Falsified, result.Outcome);
        Assert.True(invocations > result.TestsRun + 1);
        Assert.Equal(result.TestsRun, result.Statistics.Labels["seen"]);
        Assert.DoesNotContain("% seen", result.ToString());
    }

    [Fact]
    public void Cover_WithRequirementMet_ShouldPassAndStateTheRequirement()
    {
        // Arrange
        var n = 0;
        var property = Property.ForAll(Generate.Integer<int>(), _ => Property.Cover(n++ % 2 == 0, 20, "even"));

        // Act
        var result = property.Check(Hundred);

        // Assert
        Assert.Equal(PropertyOutcome.Passed, result.Outcome);
        var requirement = Assert.Single(result.Statistics.Coverage);
        Assert.Equal(new CoverageRequirement("even", MinimumPercent: 20, Count: 50, IsMet: true), requirement);
        Assert.Contains("50% even (required 20%)", result.ToString());
    }

    [Fact]
    public void Cover_WithRequirementUnmet_ShouldPassAndWarnWithTheShortfallFirstAndTheDistributionAfter()
    {
        // Arrange
        var n = 0;

        var property = Property.ForAll(Generate.Integer<int>(), _ =>
        {
            Property.Cover(n++ % 10 == 0, 50, "tenth");
            Property.Label("all");
        });

        // Act
        var result = property.Check(Hundred);
        var exception = Record.Exception(result.ThrowIfFailed);

        // Assert
        Assert.Equal(PropertyOutcome.Passed, result.Outcome);
        Assert.Null(exception);
        Assert.False(Assert.Single(result.Statistics.Coverage).IsMet);

        Assert.Equal(
            "Passed 100 tests (seed 1).\n"
            + "  Only 10% tenth, but required 50%\n"
            + "  100% all\n"
            + "  10% tenth (required 50%)",
            result.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Cover_WithRepeatedLabel_ShouldTakeTheLargerMinimum()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>(), _ =>
        {
            Property.Cover(true, 10, "label");
            Property.Cover(true, 30, "label");
        });

        // Act
        var result = property.Check(Hundred);

        // Assert
        Assert.Equal(30, Assert.Single(result.Statistics.Coverage).MinimumPercent);
        Assert.Equal(100, result.Statistics.Labels["label"]);
    }

    [Fact]
    public void Cover_WithZeroMinimumAndNoHits_ShouldPass()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>(), _ => Property.Cover(false, 0, "never"));

        // Act
        var result = property.Check(Hundred);

        // Assert
        Assert.Equal(PropertyOutcome.Passed, result.Outcome);
        Assert.Contains("0% never (required 0%)", result.ToString());
    }

    [Fact]
    public void Cover_WithOneHundredPercentMinimum_ShouldRequireEveryExample()
    {
        // Arrange
        var n = 0;
        var everyExample = Property.ForAll(Generate.Integer<int>(), _ => Property.Cover(true, 100, "all"));
        var allButOne = Property.ForAll(Generate.Integer<int>(), _ => Property.Cover(++n != 50, 100, "all"));

        // Act
        var everyExampleResult = everyExample.Check(Hundred);
        var allButOneResult = allButOne.Check(Hundred);

        // Assert
        Assert.True(Assert.Single(everyExampleResult.Statistics.Coverage).IsMet);
        Assert.DoesNotContain("Only", everyExampleResult.ToString());
        Assert.Equal(PropertyOutcome.Passed, allButOneResult.Outcome);
        Assert.False(Assert.Single(allButOneResult.Statistics.Coverage).IsMet);
        Assert.Contains("\n  Only 99% all, but required 100%", allButOneResult.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Check_WithReplay_ShouldReportLabelsWithoutCheckingCoverage()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>(), _ =>
        {
            Property.Label("seen");
            Property.Cover(false, 50, "never");
        });

        // Act
        var result = property.Check(new CheckOptions
            { Replay = new Replay(1, 0), CoverageConfidence = Confidence.Default });

        // Assert
        Assert.Equal(PropertyOutcome.Passed, result.Outcome);
        Assert.Equal(1, result.TestsRun);
        Assert.Equal(1, result.Statistics.Labels["seen"]);
        Assert.False(Assert.Single(result.Statistics.Coverage).IsMet);
        Assert.Contains("100% seen", result.ToString());
    }

    [Fact]
    public void Check_WithExhaustionAndUnmetCoverage_ShouldReportExhausted()
    {
        // Arrange
        var n = 0;

        var property = Property.ForAll(Generate.Integer<int>(), _ =>
        {
            Property.Cover(false, 50, "never");
            Property.Assume(n++ % 3 == 0);
        });

        // Act
        var result = property.Check(
            new CheckOptions { Seed = 1, RunCount = 10, MaxDiscardRatio = 1, CoverageConfidence = Confidence.Default });

        // Assert
        Assert.Equal(PropertyOutcome.Exhausted, result.Outcome);
        Assert.True(result.TestsRun > 0);
        Assert.Equal(0, result.Statistics.Labels["never"]);
        Assert.Equal(0, Assert.Single(result.Statistics.Coverage).Count);
    }

    [Fact]
    public void Check_WithFailureOnTheFirstExample_ShouldHaveEmptyStatistics()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>(), _ =>
        {
            Property.Cover(true, 10, "seen");
            return false;
        });

        // Act
        var result = property.Check(Hundred);

        // Assert
        Assert.Equal(PropertyOutcome.Falsified, result.Outcome);
        Assert.Equal(0, result.TestsRun);
        Assert.Empty(result.Statistics.Labels);
        Assert.Empty(result.Statistics.Coverage);
    }

    [Fact]
    public async Task Label_WithCallAfterAnAwait_ShouldCount()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>(), async _ =>
        {
            await Task.Yield();
            Property.Label("after");
        });

        // Act
        var result = await property.CheckAsync(Hundred);

        // Assert
        Assert.Equal(PropertyOutcome.Passed, result.Outcome);
        Assert.Equal(100, result.Statistics.Labels["after"]);
    }

    [Fact]
    public void Label_WithParallelWorkInsideTheBody_ShouldCountOncePerExample()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>(),
            _ => { Parallel.For(0, 64, i => Property.Label($"worker {i % 4}")); });

        // Act
        var result = property.Check(Hundred);

        // Assert
        Assert.Equal(PropertyOutcome.Passed, result.Outcome);
        Assert.Equal(4, result.Statistics.Labels.Count);
        Assert.All(result.Statistics.Labels.Values, static count => Assert.Equal(100, count));
    }

    [Fact]
    public void StatisticsCalls_WithNoPropertyBodyRunning_ShouldThrowInvalidOperationException()
    {
        // Arrange
        Action[] calls =
        [
            () => Property.Classify(true, "label"),
            () => Property.Label("label"),
            () => Property.Collect("name", "value"),
            () => Property.Cover(true, 10, "label")
        ];

        // Act
        var exceptions = calls.Select(static call => Record.Exception(call));

        // Assert
        Assert.All(exceptions, static exception => Assert.IsType<InvalidOperationException>(exception));
    }

    [Fact]
    public void Report_WithSeveralLabels_ShouldOrderByCountThenName()
    {
        // Arrange
        var n = 0;

        var property = Property.ForAll(Generate.Integer<int>(), _ =>
        {
            Property.Label("b");
            Property.Label("a");
            Property.Classify(n++ % 2 == 0, "c");
        });

        // Act
        var result = property.Check(Hundred);

        // Assert
        Assert.Equal(
            "Passed 100 tests (seed 1).\n  100% a\n  100% b\n  50% c",
            result.ToString().ReplaceLineEndings("\n"));
    }

    [Theory]
    [InlineData(100, "25% quarter")]
    [InlineData(1000, "25.0% quarter")]
    [InlineData(10000, "25.00% quarter")]
    public void Report_WithLargerRunCount_ShouldShowMoreDecimals(int runCount, string expected)
    {
        // Arrange
        var n = 0;
        var property = Property.ForAll(Generate.Integer<int>(), _ => Property.Classify(n++ % 4 == 0, "quarter"));

        // Act
        var result = property.Check(new CheckOptions { Seed = 1, RunCount = runCount });

        // Assert
        Assert.Contains(expected, result.ToString());
    }

    /// <summary>
    /// Runs <paramref name="call"/> inside a one-example body and returns the exception it threw,
    /// which the runner records as the counterexample rather than letting escape.
    /// </summary>
    private static Exception BodyException(Action call)
    {
        var result = Property.ForAll(Generate.Integer<int>(), _ => call())
            .Check(new CheckOptions { Seed = 1, RunCount = 1 });

        Assert.Equal(PropertyOutcome.Falsified, result.Outcome);
        return result.Minimal!.Exception!;
    }
}
