using Xunit.Sdk;

namespace QuickCheck.Xunit;

internal sealed class SerializedCheckOptions : IXunitSerializable
{
    private CheckOptions? _options;

    // Called by the deserializer via `Activator.CreateInstance`
    public SerializedCheckOptions()
    {
    }

    public SerializedCheckOptions(CheckOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public CheckOptions Options => _options
        ?? throw new InvalidOperationException("The options have not been initialized or deserialized.");

    public void Serialize(IXunitSerializationInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        info.AddValue("rc", Options.RunCount);
        info.AddValue("seed", Options.Seed);
        info.AddValue("replay", Options.Replay?.ToString());
        info.AddValue("mdr", Options.MaxDiscardRatio);
        info.AddValue("msa", Options.MaxShrinkAttempts);
        info.AddValue("msw", Options.MaxShrinkWork);
        info.AddValue("cc", Options.CoverageConfidence?.Certainty);
        info.AddValue("ct", Options.CoverageConfidence?.Tolerance);
    }

    // An absent key reads back as null, which a bare GetValue<int> would unbox with an exception, so
    // each value type is read as nullable and falls back to its default.
    public void Deserialize(IXunitSerializationInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        _options = new CheckOptions
        {
            RunCount = info.GetValue<int?>("rc") ?? CheckOptions.Default.RunCount,
            Seed = info.GetValue<ulong?>("seed"),
            Replay = info.GetValue<string>("replay") is { } replay ? Replay.Parse(replay) : null,
            MaxDiscardRatio = info.GetValue<int?>("mdr") ?? CheckOptions.Default.MaxDiscardRatio,
            MaxShrinkAttempts = info.GetValue<int?>("msa") ?? CheckOptions.Default.MaxShrinkAttempts,
            MaxShrinkWork = info.GetValue<int?>("msw") ?? CheckOptions.Default.MaxShrinkWork,
            CoverageConfidence = info.GetValue<long?>("cc") is { } certainty
                ? new Confidence { Certainty = certainty, Tolerance = info.GetValue<double?>("ct") ?? Confidence.Default.Tolerance }
                : null
        };
    }
}
