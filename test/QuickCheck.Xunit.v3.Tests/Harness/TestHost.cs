using System.Reflection;
using Xunit.Sdk;
using Xunit.v3;

namespace QuickCheck.Xunit.Tests.Harness;

/// <summary>
/// Runs the adapter's discovery and execution in-process against sample
/// methods, capturing the messages xUnit would receive.
/// </summary>
internal static class TestHost
{
    public static IXunitTestMethod TestMethod(Type type, string methodName)
    {
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            ?? throw new ArgumentException($"{type.Name} has no method {methodName}.", nameof(methodName));

        var assembly = new XunitTestAssembly(type.Assembly, configFilePath: null);
        var collection = new XunitTestCollection(assembly, collectionDefinition: null, disableParallelization: false, "Test host");
        var testClass = new XunitTestClass(type, collection);
        return new XunitTestMethod(testClass, method, testMethodArguments: []);
    }

    public static async Task<IXunitTestCase> Discover(Type type, string methodName, PropertyAttribute? attribute = null)
    {
        var testCases = await new PropertyDiscoverer().Discover(
            new DiscoveryOptions(),
            TestMethod(type, methodName),
            attribute ?? new PropertyAttribute());

        return Assert.Single(testCases);
    }

    public static async Task<IReadOnlyList<IMessageSinkMessage>> Run(
        Type type, string methodName, PropertyAttribute? attribute = null, object?[]? constructorArguments = null)
    {
        var testCase = await Discover(type, methodName, attribute);
        using var bus = new SpyMessageBus();
        using var cancellation = new CancellationTokenSource();

        await using var scheduler = ExecutionScheduler.CreateUnlimited();
        await using var fixtures = new FixtureMappingManager("Test host");

        // The same dispatch XunitTestMethodRunnerBaseContext performs.
        if (testCase is ISelfExecutingXunitTestCase selfExecuting)
        {
            await selfExecuting.Run(
                ExplicitOption.Off, bus, constructorArguments ?? [], ExceptionAggregator.Create(), cancellation,
                ParallelMode.None, scheduler, fixtures);
        }
        else
        {
            await XunitRunnerHelper.RunXunitTestCase(
                testCase, bus, cancellation, ParallelMode.None, scheduler, ExceptionAggregator.Create(),
                ExplicitOption.Off, constructorArguments ?? [], fixtures);
        }

        return bus.Messages;
    }

    private sealed class DiscoveryOptions : ITestFrameworkDiscoveryOptions
    {
        private readonly Dictionary<string, object?> _values = [];

        public TValue? GetValue<TValue>(string name) => _values.TryGetValue(name, out var value) ? (TValue?)value : default;

        public void SetValue<TValue>(string name, TValue value) => _values[name] = value;

        public string ToJson() => "{}";
    }
}
