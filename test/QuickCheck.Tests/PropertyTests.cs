namespace QuickCheck.Tests;

public sealed class PropertyTests
{
    [Fact]
    public void Check_WithPassingProperty_ShouldRunTheRequestedNumberOfExamples()
    {
        // Arrange
        var property = Property.ForAll(
            Generate.Integer<int>().List(),
            static items => items.AsEnumerable().Reverse().Reverse().SequenceEqual(items));

        // Act
        var result = property.Check(new CheckOptions { RunCount = 250 });

        // Assert
        Assert.Equal(PropertyOutcome.Passed, result.Outcome);
        Assert.Equal(250, result.TestsRun);
        Assert.False(result.IsFalsified);
    }

    [Fact]
    public void Assert_WithFalsifiedProperty_ShouldThrowNamingTheMinimalCounterexampleAndReplay()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>(), Generate.Integer<int>(), static (a, b) => _ = a / b);

        // Act
        var exception = Assert.Throws<PropertyFailedException>(() => property.Assert(new CheckOptions { Seed = 1, RunCount = 1000 }));

        // Assert
        Assert.IsType<DivideByZeroException>(exception.InnerException);
        Assert.Contains("Minimal counterexample: (0, 0)", exception.Message);
        Assert.Contains("DivideByZeroException", exception.Message);
        Assert.Contains("Replay = Replay.Parse(\"1:", exception.Message);
    }

    [Fact]
    public void Report_WithMemoryCounterexample_ShouldShowItsElements()
    {
        // Arrange
        var property = Property.ForAll(Generate.Between(0, 9).Memory(minLength: 2), static m => m.Length < 2);
        var readOnly = Property.ForAll(
            Generate.Between(0, 9).Memory(minLength: 2).Select(static m => (ReadOnlyMemory<int>)m),
            static m => m.Length < 2);

        // Act
        var exception = Assert.Throws<PropertyFailedException>(() => property.Assert(new CheckOptions { Seed = 2 }));
        var readOnlyResult = readOnly.Check(new CheckOptions { Seed = 2 });

        // Assert
        Assert.Contains("Minimal counterexample: [0, 0]", exception.Message);
        Assert.Equal("[0, 0]", readOnlyResult.Minimal!.ToString());
    }

    [Fact]
    public void Check_WithTheSameSeed_ShouldReproduceTheSameResult()
    {
        // Arrange
        var property = Property.ForAll(Generate.String(), static s => s.Length < 20);
        var options = new CheckOptions { Seed = 99 };

        // Act
        var first = property.Check(options);
        var second = property.Check(options);

        // Assert
        Assert.True(first.IsFalsified);
        Assert.Equal(first.Original.Value, second.Original!.Value);
        Assert.Equal(first.Minimal.Value, second.Minimal!.Value);
        Assert.Equal(first.Replay, second.Replay);
    }

    [Fact]
    public void Check_WithReplayToken_ShouldReproduceTheOriginalFailureDirectly()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<long>().List(), static items => items.Count < 5);
        var first = property.Check(new CheckOptions { Seed = 5 });

        // Act
        var replayed = property.Check(new CheckOptions { Replay = Replay.Parse(first.Replay!.Value.ToString()) });

        // Assert
        Assert.True(first.IsFalsified);
        Assert.True(replayed.IsFalsified);
        Assert.Equal(first.Original.Value, replayed.Original.Value);
        Assert.Equal(first.Minimal.Value, replayed.Minimal.Value);
    }

    [Fact]
    public void Check_WithNoSeed_ShouldReportTheSeedItUsed()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>(), static x => x != int.MinValue);

        // Act
        var result = property.Check();
        var again = property.Check(new CheckOptions { Seed = result.Seed });

        // Assert
        Assert.Equal(result.Outcome, again.Outcome);
    }

    [Fact]
    public void Assume_WithFailingCondition_ShouldDiscardTheExampleAndExhaustWhenNothingSatisfiesIt()
    {
        // Arrange
        var discarding = Property.ForAll(Generate.Integer<int>(), static x =>
        {
            Property.Assume(x % 2 == 0);
            return x % 2 == 0;
        });
        var impossible = Property.ForAll(Generate.Integer<int>(), static x =>
        {
            Property.Assume(false);
            return true;
        });

        // Act
        var discardingResult = discarding.Check(new CheckOptions { Seed = 3, RunCount = 50 });
        var impossibleResult = impossible.Check(new CheckOptions { RunCount = 10, MaxDiscardRatio = 2 });

        // Assert
        Assert.Equal(PropertyOutcome.Passed, discardingResult.Outcome);
        Assert.Equal(50, discardingResult.TestsRun);
        Assert.True(discardingResult.Discards > 0);
        Assert.Equal(PropertyOutcome.Exhausted, impossibleResult.Outcome);
        Assert.Throws<PropertyFailedException>(impossibleResult.ThrowIfFailed);
    }

    [Fact]
    public void Check_WithExhaustion_ShouldReportTheEstimatedDiscardRate()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>(), static _ =>
        {
            Property.Assume(false);
            return true;
        });

        // Act
        var result = property.Check(new CheckOptions { Seed = 1 });

        // Assert
        Assert.Equal(
            "Gave up after 0 tests with 1001 discards (seed 1). "
            + "About 99.95% of examples were discarded (the true rate is 99.75% to 100.00%); "
            + "prefer generators that only produce valid inputs over Assume/Where.",
            result.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Where_WithPredicateThatNeverMatches_ShouldExhaustRatherThanHang()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>().Where(static _ => false), static _ => true);

        // Act
        var result = property.Check(new CheckOptions { RunCount = 10, MaxDiscardRatio = 1 });

        // Assert
        Assert.Equal(PropertyOutcome.Exhausted, result.Outcome);
    }

    [Fact]
    public void Check_WithThrowingGenerator_ShouldPropagateTheExceptionRatherThanFailTheProperty()
    {
        // Arrange
        var broken = Generate.From<int>(static _ => throw new InvalidOperationException("bad generator"));
        var property = Property.ForAll(broken, static _ => true);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => property.Check());
    }
}
