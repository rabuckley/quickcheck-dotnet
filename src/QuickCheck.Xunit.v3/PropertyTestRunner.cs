using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace QuickCheck.Xunit;

/// <summary>
/// The default xUnit test runner, except that invoking the test method means
/// checking it as a property: generating arguments, running the method on
/// each example, and shrinking any failure before reporting it.
/// </summary>
internal sealed class PropertyTestRunner : XunitTestRunnerBase<PropertyTestRunner.Context, IXunitTest>
{
    public static PropertyTestRunner Instance { get; } = new();

    public async ValueTask<RunSummary> Run(
        IXunitTest test,
        IMessageBus messageBus,
        object?[] constructorArguments,
        ExplicitOption explicitOption,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource,
        ParallelMode parallelMode,
        ExecutionScheduler scheduler,
        IReadOnlyCollection<IBeforeAfterTestAttribute> beforeAfterAttributes,
        FixtureMappingManager caseFixtureMappings)
    {
        await using var context = new Context(
            test, explicitOption, messageBus, aggregator, cancellationTokenSource, parallelMode, scheduler,
            beforeAfterAttributes, constructorArguments, caseFixtureMappings);
        await context.InitializeAsync().ConfigureAwait(false);

        return await Run(context).ConfigureAwait(false);
    }

    internal sealed class Context(
        IXunitTest test,
        ExplicitOption explicitOption,
        IMessageBus messageBus,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource,
        ParallelMode parallelMode,
        ExecutionScheduler scheduler,
        IReadOnlyCollection<IBeforeAfterTestAttribute> beforeAfterTestAttributes,
        object?[] constructorArguments,
        FixtureMappingManager caseFixtureMappings)
        : XunitTestRunnerContext(
            test, explicitOption, messageBus, aggregator, cancellationTokenSource, parallelMode, scheduler,
            beforeAfterTestAttributes, constructorArguments, caseFixtureMappings)
    {
        // The docs point at InvokeTestMethod as the seam for replacing the
        // invocation, which would save this timer and aggregator boilerplate,
        // but the base InvokeTest raises a TestPipelineException unless the
        // method's parameters match MethodArguments — and a property method has
        // N parameters with no xUnit-supplied arguments at all.
        public override ValueTask<TimeSpan> InvokeTest(object? testClassInstance) =>
            ExecutionTimer.MeasureAsync(() => Aggregator.RunAsync(async () =>
            {
                var testCase = Test.TestCase as PropertyTestCase
                    ?? throw new InvalidOperationException(
                        $"{nameof(PropertyTestRunner)} can only run {nameof(PropertyTestCase)}s.");

                if (testCase.Error is { } error)
                {
                    throw new TestPipelineException(error);
                }

                var property = PropertyMethod.Create(Method, testCase.Generators).ToProperty(testClassInstance);

                // The test context's token also observes TestContext.CancelCurrentTest().
                var result = await property
                    .CheckAsync(testCase.Options, TestContext.Current.CancellationToken)
                    .ConfigureAwait(false);
                var replayHint = result.Replay is { } replay ? $"[Property(Replay = \"{replay}\")]" : null;

                if (result.Outcome is PropertyOutcome.Passed)
                {
                    TestContext.Current.TestOutputHelper?.WriteLine(result.ToString(replayHint));
                }

                result.ThrowIfFailed(replayHint);
            }));
    }
}
