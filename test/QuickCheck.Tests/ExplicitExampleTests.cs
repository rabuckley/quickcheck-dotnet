namespace QuickCheck.Tests;

public sealed class ExplicitExampleTests
{
    [Fact]
    public void Check_WithExplicitExamples_ShouldCheckThemInOrderBeforeAnyGeneratedExample()
    {
        // Arrange
        var seen = new List<int>();

        var property = Property
            .ForAll(Generate.Between(100, 200), value =>
            {
                seen.Add(value);
                return true;
            })
            .Example(7)
            .Example(8);

        // Act
        var result = property.Check(new CheckOptions { RunCount = 3, Seed = 1 });

        // Assert
        Assert.Equal(PropertyOutcome.Passed, result.Outcome);
        Assert.Equal([7, 8], seen[..2]);
        Assert.All(seen[2..], value => Assert.InRange(value, 100, 200));
    }

    [Fact]
    public void Check_WithExplicitExamples_ShouldCheckThemOnTopOfRunCount()
    {
        // Arrange
        var invocations = 0;

        var property = Property
            .ForAll(Generate.Integer<int>(), _ =>
            {
                invocations++;
                return true;
            })
            .Example(1)
            .Example(2);

        // Act
        var result = property.Check(new CheckOptions { RunCount = 10, Seed = 1 });

        // Assert
        Assert.Equal(12, invocations);
        Assert.Equal(10, result.TestsRun);
        Assert.Equal(2, result.ExplicitExamplesRun);
    }

    [Fact]
    public void Check_WithFailingExplicitExample_ShouldReportItAsGivenWithNoShrinkingAndNoReplayToken()
    {
        // Arrange
        var property = Property
            .ForAll(Generate.Integer<int>(), static value => Math.Abs(value) >= 0)
            .Example(int.MinValue);

        // Act
        var result = property.Check(new CheckOptions { Seed = 5 });
        var report = result.ToString();

        // Assert
        Assert.True(result.IsFalsified);
        Assert.True(result.Original.IsExplicit);
        Assert.True(result.Minimal.IsExplicit);
        Assert.Null(result.Replay);
        Assert.Equal(0, result.TestsRun);
        Assert.Equal(0, result.Shrinks);
        Assert.Equal(int.MinValue, result.Minimal.Value);
        Assert.Equal(int.MinValue, result.Original.Value);
        Assert.Contains("Falsified by an explicit example (seed 5).", report, StringComparison.Ordinal);
        Assert.Contains("Counterexample: -2147483648", report, StringComparison.Ordinal);
        Assert.Contains("threw System.OverflowException", report, StringComparison.Ordinal);
        Assert.Contains("checked as given, so it was not shrunk", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Replay with", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_WithExplicitExampleTheGeneratorCannotProduce_ShouldStillCheckIt()
    {
        // Arrange
        var property = Property
            .ForAll(Generate.Between(1, 10), static value => value >= 1)
            .Example(-5);

        // Act
        var result = property.Check(new CheckOptions { RunCount = 20, Seed = 1 });

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal(-5, result.Minimal.Value);
    }

    [Fact]
    public void Check_WithExplicitExampleDiscardedByAnAssumption_ShouldSkipItAndSaySoInThePassedReport()
    {
        // Arrange
        var property = Property
            .ForAll(Generate.Between(1, 10), static value =>
            {
                Property.Assume(value > 0);
                return true;
            })
            .Example(0);

        // Act
        var result = property.Check(new CheckOptions { RunCount = 10, Seed = 1 });

        // Assert
        Assert.Equal(PropertyOutcome.Passed, result.Outcome);
        Assert.Equal(1, result.ExplicitExamplesDiscarded);
        Assert.Equal(0, result.ExplicitExamplesRun);

        Assert.Contains(
            "1 explicit example was discarded by an assumption and not checked.",
            result.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Check_WithExplicitExampleDiscardedByAnAssumption_ShouldSaySoInTheExhaustedReportToo()
    {
        // Arrange
        var property = Property
            .ForAll(Generate.Integer<int>(), static _ =>
            {
                Property.Assume(false);
                return true;
            })
            .Example(0);

        // Act
        var result = property.Check(new CheckOptions { RunCount = 5, Seed = 1 });

        // Assert
        Assert.Equal(PropertyOutcome.Exhausted, result.Outcome);
        Assert.Equal(1, result.ExplicitExamplesDiscarded);

        Assert.Contains(
            "1 explicit example was discarded by an assumption and not checked.",
            result.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Check_WithExplicitExamples_ShouldLeaveThemOutOfTheStatistics()
    {
        // Arrange
        var property = Property
            .ForAll(Generate.Between(1, 10), static value =>
            {
                Property.Classify(value == 0, "zero");
                return true;
            })
            .Example(0);

        // Act
        var result = property.Check(new CheckOptions { RunCount = 100, Seed = 1 });

        // Assert
        Assert.Equal(100, result.TestsRun);
        Assert.Equal(1, result.ExplicitExamplesRun);
        Assert.Equal(0, result.Statistics.Labels["zero"]);
        Assert.Contains("Passed 100 tests and 1 explicit example (seed 1).", result.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Check_WithReplayAndExplicitExamples_ShouldThrow()
    {
        // Arrange
        var invocations = 0;

        var property = Property
            .ForAll(Generate.Between(1, 10), _ =>
            {
                invocations++;
                return true;
            })
            .Example(0);

        // Act
        void Act() => property.Check(new CheckOptions { Replay = new Replay(5, 0) });

        // Assert
        var exception = Assert.Throws<ArgumentException>(Act);
        Assert.Equal("options", exception.ParamName);
        Assert.Equal(0, invocations);
    }

    [Fact]
    public void CheckAsync_WithReplayAndExplicitExamples_ShouldThrowBeforeReturningItsTask()
    {
        // Arrange
        var property = Property
            .ForAll(Generate.Between(1, 10), static async (int value) =>
            {
                await Task.Yield();
                Assert.InRange(value, 1, 10);
            })
            .Example(0);

        // Act
        void Act() => _ = property.CheckAsync(new CheckOptions { Replay = new Replay(5, 0) });

        // Assert
        var exception = Assert.Throws<ArgumentException>(Act);
        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public void Check_WithFailingGeneratedExample_ShouldNotMarkTheCounterexampleExplicit()
    {
        // Arrange
        var property = Property
            .ForAll(Generate.Between(1, 10), static value => value == 5)
            .Example(5);

        // Act
        var result = property.Check(new CheckOptions { RunCount = 10, Seed = 1 });

        // Assert
        Assert.True(result.IsFalsified);
        Assert.False(result.Original.IsExplicit);
        Assert.False(result.Minimal.IsExplicit);
    }

    [Fact]
    public void Example_ShouldReturnANewPropertyAndLeaveTheOriginalUnchanged()
    {
        // Arrange
        var seen = new List<int>();

        var original = Property.ForAll(Generate.Between(1, 10), value =>
        {
            seen.Add(value);
            return true;
        });

        // Act
        var pinned = original.Example(0);
        original.Check(new CheckOptions { RunCount = 5, Seed = 1 });
        var seenWithoutPin = seen.ToArray();
        pinned.Check(new CheckOptions { RunCount = 5, Seed = 1 });

        // Assert
        Assert.NotSame(original, pinned);
        Assert.DoesNotContain(0, seenWithoutPin);
        Assert.Equal(0, seen[seenWithoutPin.Length]);
    }

    [Fact]
    public void Example_WithTupleProperty_ShouldPinTheCounterexampleTheReportPrinted()
    {
        // Arrange
        var property = Property
            .ForAll(Generate.Integer<int>(), Generate.Integer<int>(), static (a, b) => _ = a / b)
            .Example((0, 0));

        // Act
        var result = property.Check(new CheckOptions { Seed = 1 });

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal((0, 0), result.Minimal.Value);
        Assert.IsType<DivideByZeroException>(result.Minimal.Exception);
        Assert.Contains("Counterexample: (0, 0)", result.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssertAsync_WithFailingExplicitExample_ShouldThrowNamingIt()
    {
        // Arrange
        var property = Property
            .ForAll(Generate.Between(1, 10), static async (int value) =>
            {
                await Task.Yield();
                Assert.NotEqual(0, value);
            })
            .Example(0);

        // Act
        var exception = await Assert.ThrowsAsync<PropertyFailedException>(() => property.AssertAsync());

        // Assert
        Assert.Contains("Falsified by an explicit example", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Counterexample: 0", exception.Message, StringComparison.Ordinal);
    }
}
