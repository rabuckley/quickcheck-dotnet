namespace QuickCheck.Tests;

public sealed class PropertyTests
{
    [Fact]
    public void Passing_property_runs_the_requested_number_of_examples()
    {
        var result = Property
            .ForAll(Generate.Integer<int>().List(), static items => items.AsEnumerable().Reverse().Reverse().SequenceEqual(items))
            .Check(new CheckOptions { RunCount = 250 });

        Assert.Equal(PropertyOutcome.Passed, result.Outcome);
        Assert.Equal(250, result.TestsRun);
        Assert.False(result.IsFalsified);
    }

    [Fact]
    public void Assert_throws_with_a_report_naming_the_minimal_counterexample_and_replay()
    {
        var property = Property.ForAll(Generate.Integer<int>(), Generate.Integer<int>(), static (a, b) => _ = a / b);

        var exception = Assert.Throws<PropertyFailedException>(() => property.Assert(new CheckOptions { Seed = 1, RunCount = 1000 }));

        Assert.IsType<DivideByZeroException>(exception.InnerException);
        Assert.Contains("Minimal counterexample: (0, 0)", exception.Message);
        Assert.Contains("DivideByZeroException", exception.Message);
        Assert.Contains("Replay = Replay.Parse(\"1:", exception.Message);
    }

    [Fact]
    public void Report_shows_the_elements_of_a_memory_counterexample()
    {
        var property = Property.ForAll(Generate.Between(0, 9).Memory(minLength: 2), static m => m.Length < 2);
        var readOnly = Property.ForAll(
            Generate.Between(0, 9).Memory(minLength: 2).Select(static m => (ReadOnlyMemory<int>)m),
            static m => m.Length < 2);

        var exception = Assert.Throws<PropertyFailedException>(() => property.Assert(new CheckOptions { Seed = 2 }));

        Assert.Contains("Minimal counterexample: [0, 0]", exception.Message);
        Assert.Equal("[0, 0]", readOnly.Check(new CheckOptions { Seed = 2 }).Minimal!.ToString());
    }

    [Fact]
    public void Same_seed_reproduces_the_same_result()
    {
        var property = Property.ForAll(Generate.String(), static s => s.Length < 20);
        var options = new CheckOptions { Seed = 99 };

        var first = property.Check(options);
        var second = property.Check(options);

        Assert.True(first.IsFalsified);
        Assert.Equal(first.Original.Value, second.Original!.Value);
        Assert.Equal(first.Minimal.Value, second.Minimal!.Value);
        Assert.Equal(first.Replay, second.Replay);
    }

    [Fact]
    public void Replay_token_reproduces_the_original_failure_directly()
    {
        var property = Property.ForAll(Generate.Integer<long>().List(), static items => items.Count < 5);

        var first = property.Check(new CheckOptions { Seed = 5 });
        var replayed = property.Check(new CheckOptions { Replay = Replay.Parse(first.Replay!.ToString()) });

        Assert.True(first.IsFalsified);
        Assert.True(replayed.IsFalsified);
        Assert.Equal(first.Original.Value, replayed.Original.Value);
        Assert.Equal(first.Minimal.Value, replayed.Minimal.Value);
    }

    [Fact]
    public void Unseeded_checks_report_the_seed_they_used()
    {
        var property = Property.ForAll(Generate.Integer<int>(), static x => x != int.MinValue);

        var result = property.Check();
        var again = property.Check(new CheckOptions { Seed = result.Seed });

        Assert.Equal(result.Outcome, again.Outcome);
    }

    [Fact]
    public void Assume_discards_examples_and_exhausts_when_nothing_satisfies_it()
    {
        var discarding = Property.ForAll(Generate.Integer<int>(), static x =>
        {
            Property.Assume(x % 2 == 0);
            return x % 2 == 0;
        }).Check(new CheckOptions { Seed = 3, RunCount = 50 });

        var impossible = Property.ForAll(Generate.Integer<int>(), static x =>
        {
            Property.Assume(false);
            return true;
        }).Check(new CheckOptions { RunCount = 10, MaxDiscardRatio = 2 });

        Assert.Equal(PropertyOutcome.Passed, discarding.Outcome);
        Assert.Equal(50, discarding.TestsRun);
        Assert.True(discarding.Discards > 0);

        Assert.Equal(PropertyOutcome.Exhausted, impossible.Outcome);
        Assert.Throws<PropertyFailedException>(impossible.ThrowIfFailed);
    }

    [Fact]
    public void Where_that_never_matches_discards_rather_than_hanging()
    {
        var result = Property
            .ForAll(Generate.Integer<int>().Where(static _ => false), static _ => true)
            .Check(new CheckOptions { RunCount = 10, MaxDiscardRatio = 1 });

        Assert.Equal(PropertyOutcome.Exhausted, result.Outcome);
    }

    [Fact]
    public void Exceptions_from_generators_propagate_rather_than_failing_the_property()
    {
        var broken = Generate.From<int>(static _ => throw new InvalidOperationException("bad generator"));

        Assert.Throws<InvalidOperationException>(() => Property.ForAll(broken, static _ => true).Check());
    }
}
