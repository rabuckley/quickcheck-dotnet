using QuickCheck.Xunit.Tests.Harness;
using Xunit.Sdk;

namespace QuickCheck.Xunit.Tests;

/// <summary>
/// End-to-end runs of <see cref="PropertyTestCase"/> through the adapter's
/// runners, observed via the messages xUnit would receive.
/// </summary>
public sealed class PropertyTestCaseTests
{
    private sealed class Samples
    {
        public static int Invocations;

        public void Sum_is_small(int a, int b) => Assert.True((long)a + b < 100);

        public bool Strings_are_short(string s) => s.Length < 3;

        public async Task Async_bodies_fail_after_awaiting(List<int> items)
        {
            await Task.Yield();
            Assert.DoesNotContain(items, static x => x > 1000);
        }

        public void Passes(int x) => Invocations++;

        // Declared Task, not Task<bool>: the bool it happens to produce is not a verdict.
        public Task Awaits_a_bool_returning_call(int x) => Task.FromResult(false);

        public void Assumes_the_impossible(int x) => Property.Assume(false);

        public void Divides(int a, int b) => _ = a / b;

        public static void Static_and_cancels(int x)
        {
            TestContext.Current.CancelCurrentTest();
        }

        public void Classifies(int x) => Property.Classify(x >= 0, "non-negative");

        public void Covers_the_impossible(int x) => Property.Cover(false, 50, "never");
    }

    [Fact]
    public async Task Failing_property_fails_with_a_report_naming_the_minimal_counterexample_and_replay()
    {
        var messages = await TestHost.Run(typeof(Samples), nameof(Samples.Sum_is_small), new PropertyAttribute { Seed = 11 });

        var failed = Assert.Single(messages.OfType<ITestFailed>());
        var report = failed.Messages[0];

        Assert.Equal(typeof(PropertyFailedException).FullName, failed.ExceptionTypes[0]);
        Assert.Contains(failed.ExceptionTypes, static type => type == typeof(TrueException).FullName);
        Assert.Contains("Falsified after", report);
        Assert.Contains("Minimal counterexample: a = 0, b = 100", report);
        Assert.Contains("Replay with: [Property(Replay = \"11:", report);
        Assert.Empty(messages.OfType<ITestPassed>());
    }

    [Fact]
    public async Task False_return_and_async_bodies_are_falsified_and_shrunk()
    {
        var predicate = await TestHost.Run(typeof(Samples), nameof(Samples.Strings_are_short), new PropertyAttribute { Seed = 3 });
        var asynchronous = await TestHost.Run(typeof(Samples), nameof(Samples.Async_bodies_fail_after_awaiting), new PropertyAttribute { Seed = 3 });

        Assert.Contains("Minimal counterexample: s = \"aaa\"", Assert.Single(predicate.OfType<ITestFailed>()).Messages[0]);
        Assert.Contains("Minimal counterexample: items = [1001]", Assert.Single(asynchronous.OfType<ITestFailed>()).Messages[0]);
    }

    [Fact]
    public async Task A_Task_returning_body_has_no_verdict_even_when_it_hands_back_a_Task_of_bool()
    {
        var messages = await TestHost.Run(
            typeof(Samples), nameof(Samples.Awaits_a_bool_returning_call), new PropertyAttribute { Seed = 9 });

        Assert.Empty(messages.OfType<ITestFailed>());
        Assert.Single(messages.OfType<ITestPassed>());
    }

    [Fact]
    public async Task Passing_property_passes_and_reports_the_seed_in_the_test_output()
    {
        Samples.Invocations = 0;

        var messages = await TestHost.Run(typeof(Samples), nameof(Samples.Passes), new PropertyAttribute { RunCount = 37, Seed = 5 });

        var passed = Assert.Single(messages.OfType<ITestPassed>());
        Assert.Contains("Passed 37 tests (seed 5).", passed.Output);
        Assert.Equal(37, Samples.Invocations);
    }

    [Fact]
    public async Task PropertyTestCase_WithClassifiedExamples_ShouldWriteTheDistributionToTheTestOutput()
    {
        // Arrange
        var attribute = new PropertyAttribute { Seed = 5 };

        // Act
        var messages = await TestHost.Run(typeof(Samples), nameof(Samples.Classifies), attribute);

        // Assert
        var passed = Assert.Single(messages.OfType<ITestPassed>());
        Assert.Contains("Passed 100 tests (seed 5).", passed.Output);
        Assert.Contains("% non-negative", passed.Output);
    }

    [Fact]
    public async Task PropertyTestCase_WithUnmetCoverage_ShouldFailWithTheCoverageReport()
    {
        // Arrange
        var attribute = new PropertyAttribute { Seed = 5 };

        // Act
        var messages = await TestHost.Run(typeof(Samples), nameof(Samples.Covers_the_impossible), attribute);

        // Assert
        var failed = Assert.Single(messages.OfType<ITestFailed>());
        Assert.Equal(typeof(PropertyFailedException).FullName, failed.ExceptionTypes[0]);
        Assert.Contains("Insufficient coverage after 100 tests (seed 5).", failed.Messages[0]);
        Assert.Contains("Only 0% never, but required 50%", failed.Messages[0]);
        Assert.Empty(messages.OfType<ITestPassed>());
    }

    [Fact]
    public async Task Replay_token_checks_only_the_named_example()
    {
        var first = await TestHost.Run(typeof(Samples), nameof(Samples.Sum_is_small), new PropertyAttribute { Seed = 11 });
        var report = Assert.Single(first.OfType<ITestFailed>()).Messages[0];
        var token = report[(report.IndexOf("Replay = \"", StringComparison.Ordinal) + "Replay = \"".Length)..];
        token = token[..token.IndexOf('"')];

        var replayed = await TestHost.Run(typeof(Samples), nameof(Samples.Sum_is_small), new PropertyAttribute { Replay = token });

        var failed = Assert.Single(replayed.OfType<ITestFailed>());
        Assert.Contains("Falsified after 1 tests", failed.Messages[0]);
        Assert.Contains("Minimal counterexample: a = 0, b = 100", failed.Messages[0]);
    }

    [Fact]
    public async Task Exhausted_properties_fail()
    {
        var messages = await TestHost.Run(typeof(Samples), nameof(Samples.Assumes_the_impossible), new PropertyAttribute { RunCount = 5 });

        var failed = Assert.Single(messages.OfType<ITestFailed>());
        Assert.Contains("Gave up after 0 tests", failed.Messages[0]);
    }

    [Fact]
    public async Task Exceptions_from_the_body_are_reported_with_the_counterexample()
    {
        var messages = await TestHost.Run(typeof(Samples), nameof(Samples.Divides), new PropertyAttribute { Seed = 1, RunCount = 500 });

        var failed = Assert.Single(messages.OfType<ITestFailed>());
        Assert.Contains("Minimal counterexample: a = 0, b = 0", failed.Messages[0]);
        Assert.Contains("threw System.DivideByZeroException", failed.Messages[0]);
        Assert.Contains(typeof(DivideByZeroException).FullName, failed.ExceptionTypes);
    }

    [Fact]
    public async Task Cancelling_the_test_stops_the_check()
    {
        var messages = await TestHost.Run(typeof(Samples), nameof(Samples.Static_and_cancels), new PropertyAttribute { RunCount = 1000 });

        var failed = Assert.Single(messages.OfType<ITestFailed>());
        Assert.Contains(failed.ExceptionTypes, static type => type?.Contains("Cancel", StringComparison.Ordinal) == true);
    }
}
