using Xunit.Sdk;
using Xunit.v3;

namespace QuickCheck.Xunit;

/// <summary>
/// The test case for a <see cref="PropertyAttribute"/> method. It runs
/// through the standard xUnit pipeline (fixtures, before/after attributes,
/// timeouts, output capture) but replaces the final method invocation with a
/// property check over generated arguments, or, when the method cannot be run
/// as a property, with the <see cref="Error"/> describing why.
/// </summary>
public sealed class PropertyTestCase : XunitTestCase, ISelfExecutingXunitTestCase
{
    private CheckOptions? _options;

    /// <summary>
    /// Called by the de-serializer; should only be called by deriving classes
    /// for de-serialization purposes.
    /// </summary>
    [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
    public PropertyTestCase()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyTestCase"/> class.
    /// The leading parameters are those of <see cref="XunitTestCase"/>;
    /// <paramref name="options"/> is the check configuration for the property,
    /// <paramref name="generators"/> its
    /// <see cref="PropertyAttribute.Generators"/> type, if any, and
    /// <paramref name="error"/> the reason the method cannot be run as a
    /// property, if it cannot.
    /// </summary>
    public PropertyTestCase(
        IXunitTestMethod testMethod,
        string testCaseDisplayName,
        string uniqueID,
        bool @explicit,
        Type[]? skipExceptions = null,
        string? skipReason = null,
        Type? skipType = null,
        string? skipUnless = null,
        string? skipWhen = null,
        Dictionary<string, HashSet<string>>? traits = null,
        string? sourceFilePath = null,
        int? sourceLineNumber = null,
        int? timeout = null,
        CheckOptions? options = null,
        Type? generators = null,
        string? error = null)
        : base(
            testMethod,
            testCaseDisplayName,
            uniqueID,
            @explicit,
            skipExceptions,
            skipReason,
            skipType,
            skipUnless,
            skipWhen,
            traits,
            testMethodArguments: null,
            sourceFilePath,
            sourceLineNumber,
            timeout)
    {
        _options = options ?? CheckOptions.Default;
        Generators = generators;
        Error = error;
    }

    /// <summary>
    /// The check configuration for the property, as declared by its
    /// <see cref="PropertyAttribute"/>. The runner supplies the cancellation
    /// token.
    /// </summary>
    public CheckOptions Options =>
        _options ?? throw new InvalidOperationException("The test case has not been initialized or deserialized.");

    /// <summary>
    /// The <see cref="PropertyAttribute.Generators"/> type, if any.
    /// </summary>
    public Type? Generators { get; private set; }

    /// <summary>
    /// Why the method cannot be run as a property, or <see langword="null"/>
    /// when it can. Running the test reports this instead of checking
    /// anything; discovery, skipping, traits, and filtering are unaffected, so
    /// a broken property can still be skipped or filtered out like any test.
    /// </summary>
    public string? Error { get; private set; }

    /// <inheritdoc />
    public async ValueTask<RunSummary> Run(
        ExplicitOption explicitOption,
        IMessageBus messageBus,
        object?[] constructorArguments,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource,
        ParallelMode parallelMode,
        ExecutionScheduler scheduler,
        FixtureMappingManager methodFixtureMappings)
    {
        var tests = await CreateTests().ConfigureAwait(false);

        return await PropertyTestCaseRunner.Instance.Run(
            this,
            tests,
            messageBus,
            aggregator,
            cancellationTokenSource,
            parallelMode,
            scheduler,
            TestCaseDisplayName,
            SkipReason,
            explicitOption,
            constructorArguments,
            methodFixtureMappings).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override void Serialize(IXunitSerializationInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        base.Serialize(info);

        info.AddValue("qc.rc", Options.RunCount);
        info.AddValue("qc.seed", Options.Seed);
        info.AddValue("qc.replay", Options.Replay?.ToString());
        info.AddValue("qc.mdr", Options.MaxDiscardRatio);
        info.AddValue("qc.msa", Options.MaxShrinkAttempts);
        info.AddValue("qc.generators", Generators);
        info.AddValue("qc.error", Error);
    }

    /// <inheritdoc />
    protected override void Deserialize(IXunitSerializationInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        base.Deserialize(info);

        _options = new CheckOptions
        {
            RunCount = info.GetValue<int>("qc.rc"),
            Seed = info.GetValue<ulong?>("qc.seed"),
            Replay = info.GetValue<string>("qc.replay") is { } replay ? QuickCheck.Replay.Parse(replay) : null,
            MaxDiscardRatio = info.GetValue<int>("qc.mdr"),
            MaxShrinkAttempts = info.GetValue<int>("qc.msa")
        };
        Generators = info.GetValue<Type>("qc.generators");
        Error = info.GetValue<string>("qc.error");
    }
}
