using Xunit.Sdk;
using Xunit.v3;

namespace QuickCheck.Xunit;

/// <summary>
/// Turns each <see cref="PropertyAttribute"/> method into one
/// <see cref="PropertyTestCase"/>: either with the settings to check it by, or
/// with the <see cref="PropertyTestCase.Error"/> describing why it cannot be
/// run as a property.
/// </summary>
public sealed class PropertyDiscoverer : IXunitTestCaseDiscoverer
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyCollection<IXunitTestCase>> Discover(
        ITestFrameworkDiscoveryOptions discoveryOptions,
        IXunitTestMethod testMethod,
        IFactAttribute factAttribute)
    {
        ArgumentNullException.ThrowIfNull(discoveryOptions);
        ArgumentNullException.ThrowIfNull(testMethod);
        ArgumentNullException.ThrowIfNull(factAttribute);

        var details = TestIntrospectionHelper.GetTestCaseDetails(discoveryOptions, testMethod, factAttribute);

        string? error = null;
        CheckOptions options = CheckOptions.Default;
        Type? generators = null;

        try
        {
            if (factAttribute is not PropertyAttribute attribute)
            {
                throw new PropertyDefinitionException(
                    $"{PropertyMethod.Describe(testMethod.Method)}: {nameof(PropertyDiscoverer)} requires a "
                    + $"[Property] attribute but was given {factAttribute.GetType().Name}.");
            }

            generators = attribute.Generators;
            options = attribute.ToCheckOptions();

            if (options.Replay is not null && testMethod.Method.IsDefined(typeof(ExampleAttribute), inherit: true))
            {
                throw new PropertyDefinitionException(
                    $"{PropertyMethod.Describe(testMethod.Method)}: Replay checks only the example its token "
                    + "names, so the [Example] pins on this method would never be checked. Keep one or the other.");
            }

            // Validation only: neither the resolved generators nor the explicit
            // examples can be carried on the test case, which has to survive
            // serialization into the execution process, so the runner reads the
            // method again.
            _ = PropertyMethod.Create(testMethod.Method, generators);
        }
        catch (PropertyDefinitionException exception)
        {
            // Already names the method.
            error = exception.Message;
        }
        catch (Exception exception) when (exception is ArgumentOutOfRangeException or FormatException)
        {
            error = $"{PropertyMethod.Describe(testMethod.Method)}: {exception.Message}";
        }
        catch (Exception exception)
        {
            error = $"{PropertyMethod.Describe(testMethod.Method)}: "
                + $"validating the property threw {exception.GetType().Name}: {exception.Message}";
        }

        var testCase = new PropertyTestCase(
            details.ResolvedTestMethod,
            details.TestCaseDisplayName,
            details.UniqueID,
            details.Explicit,
            details.SkipExceptions,
            details.SkipReason,
            details.SkipType,
            details.SkipUnless,
            details.SkipWhen,
            TestIntrospectionHelper.GetTraits(testMethod, dataRow: null),
            sourceFilePath: details.SourceFilePath,
            sourceLineNumber: details.SourceLineNumber,
            timeout: details.Timeout,
            options: options,
            generators: generators,
            error: error);

        return new([testCase]);
    }
}
