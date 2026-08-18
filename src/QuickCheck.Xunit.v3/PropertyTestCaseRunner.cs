using Xunit.Sdk;
using Xunit.v3;

namespace QuickCheck.Xunit;

/// <summary>
/// The default xUnit test case runner, except that each test is run by
/// <see cref="PropertyTestRunner"/>.
/// </summary>
internal sealed class PropertyTestCaseRunner
    : XunitTestCaseRunnerBase<PropertyTestCaseRunner.Context, IXunitTestCase, IXunitTest>
{
    public static PropertyTestCaseRunner Instance { get; } = new();

    public async ValueTask<RunSummary> Run(
        IXunitTestCase testCase,
        IReadOnlyCollection<IXunitTest> tests,
        IMessageBus messageBus,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource,
        ParallelMode parallelMode,
        ExecutionScheduler scheduler,
        string displayName,
        string? skipReason,
        ExplicitOption explicitOption,
        object?[] constructorArguments,
        FixtureMappingManager methodFixtureMappings)
    {
        await using var context = new Context(
            testCase, tests, explicitOption, messageBus, aggregator, displayName, skipReason,
            cancellationTokenSource, parallelMode, scheduler, constructorArguments, methodFixtureMappings);
        await context.InitializeAsync().ConfigureAwait(false);

        return await Run(context).ConfigureAwait(false);
    }

    internal sealed class Context(
        IXunitTestCase testCase,
        IReadOnlyCollection<IXunitTest> tests,
        ExplicitOption explicitOption,
        IMessageBus messageBus,
        ExceptionAggregator aggregator,
        string displayName,
        string? skipReason,
        CancellationTokenSource cancellationTokenSource,
        ParallelMode parallelMode,
        ExecutionScheduler scheduler,
        object?[] constructorArguments,
        FixtureMappingManager methodFixtureMappings)
        : XunitTestCaseRunnerContext(
            testCase, tests, explicitOption, messageBus, aggregator, displayName, skipReason,
            cancellationTokenSource, parallelMode, scheduler, constructorArguments, methodFixtureMappings)
    {
        public override ValueTask<RunSummary> RunTest(IXunitTest test) =>
            PropertyTestRunner.Instance.Run(
                test,
                MessageBus,
                ConstructorArguments,
                ExplicitOption,
                Aggregator.Clone(),
                CancellationTokenSource,
                ParallelMode,
                Scheduler,
                BeforeAfterTestAttributes,
                CaseFixtureMappings);
    }
}
