using QuickCheck.Xunit.Tests.Harness;

namespace QuickCheck.Xunit.Tests;

public sealed class PropertyDiscovererTests
{
    private sealed class Samples
    {
        public void Fine(int x, string s) => _ = (x, s);

        public void Generic<T>(T value) => _ = value;

        public void ByRef(ref int x) => x = 0;

        public int Returns_int(int x) => x;

        public async void Async_void(int x) => await Task.Yield();

        public void Unsupported_type(double d) => _ = d;

        public void Missing_named_generator([Generator("Nope")] int x) => _ = x;

        public void Wrongly_typed_generator([Generator(nameof(Text))] int x) => _ = x;

        [Trait("Category", "Integration")]
        public void Categorised_but_unsupported(double d) => _ = d;

        public static Generator<string> Text => Generate.String();
    }

    [Fact]
    public async Task Discover_WithValidMethod_ShouldCreateOnePropertyTestCaseCarryingItsSettings()
    {
        // Arrange
        var attribute = new PropertyAttribute
        {
            RunCount = 7,
            Seed = 9,
            MaxShrinkAttempts = 3,
            MaxShrinkWork = 4,
            CheckCoverage = true,
            Generators = typeof(Samples)
        };

        // Act
        var testCase = Assert.IsType<PropertyTestCase>(await TestHost.Discover(typeof(Samples), nameof(Samples.Fine), attribute));

        // Assert
        Assert.Equal(7, testCase.Options.RunCount);
        Assert.Equal(9UL, testCase.Options.Seed);
        Assert.Equal(3, testCase.Options.MaxShrinkAttempts);
        Assert.Equal(4, testCase.Options.MaxShrinkWork);
        Assert.Equal(Confidence.Default, testCase.Options.CoverageConfidence);
        Assert.Null(testCase.Options.Replay);
        Assert.Equal(typeof(Samples), testCase.Generators);
        Assert.Equal("QuickCheck.Xunit.Tests.PropertyDiscovererTests+Samples.Fine", testCase.TestCaseDisplayName);
    }

    [Fact]
    public async Task Discover_WithUnsetAttributeValues_ShouldLeaveTheLibraryDefaults()
    {
        // Act
        var testCase = Assert.IsType<PropertyTestCase>(await TestHost.Discover(typeof(Samples), nameof(Samples.Fine)));

        // Assert
        Assert.Equal(CheckOptions.Default, testCase.Options);
        Assert.Null(testCase.Generators);
    }

    [Theory]
    [InlineData(nameof(Samples.Generic), "generic methods are not supported")]
    [InlineData(nameof(Samples.ByRef), "parameter 'x' is passed by reference")]
    [InlineData(nameof(Samples.Returns_int), "return type Int32 is not supported")]
    [InlineData(nameof(Samples.Async_void), "'async void' is not supported")]
    [InlineData(nameof(Samples.Unsupported_type), "Parameter 'd' (Double): QuickCheck has no built-in generator for Double")]
    [InlineData(nameof(Samples.Missing_named_generator), "Parameter 'x' (Int32): no static generator member named 'Nope' was found on Samples")]
    [InlineData(nameof(Samples.Wrongly_typed_generator), "'Samples.Text' is a Generator<String>, not a Generator<Int32>")]
    public async Task Discover_WithInvalidMethod_ShouldCreateAnErrorTestCaseNamingTheProblem(string method, string expectedMessage)
    {
        // Act
        var testCase = Assert.IsType<PropertyTestCase>(await TestHost.Discover(typeof(Samples), method));

        // Assert
        Assert.StartsWith($"[Property] method Samples.{method}: ", testCase.Error);
        Assert.Contains(expectedMessage, testCase.Error);
    }

    [Theory]
    [InlineData("not-a-token", "not a valid replay token")]
    [InlineData("1:2:3", "not a valid replay token")]
    public async Task Discover_WithInvalidReplayToken_ShouldReportItAtDiscovery(string replay, string expectedMessage)
    {
        // Arrange
        var attribute = new PropertyAttribute { Replay = replay };

        // Act
        var testCase = Assert.IsType<PropertyTestCase>(await TestHost.Discover(typeof(Samples), nameof(Samples.Fine), attribute));

        // Assert
        Assert.Contains(expectedMessage, testCase.Error);
    }

    [Fact]
    public async Task Discover_WithOutOfRangeSettings_ShouldReportThemAtDiscovery()
    {
        // Arrange
        var attribute = new PropertyAttribute { RunCount = -1 };

        // Act
        var testCase = Assert.IsType<PropertyTestCase>(await TestHost.Discover(typeof(Samples), nameof(Samples.Fine), attribute));

        // Assert
        Assert.StartsWith($"[Property] method Samples.{nameof(Samples.Fine)}: ", testCase.Error);
        Assert.Contains("RunCount", testCase.Error);
    }

    [Fact]
    public async Task Run_WithErrorTestCase_ShouldFail()
    {
        // Act
        var messages = await TestHost.Run(typeof(Samples), nameof(Samples.Generic));

        // Assert
        var failed = Assert.Single(messages.OfType<global::Xunit.Sdk.ITestFailed>());
        Assert.Contains("generic methods are not supported", failed.Messages[0]);
    }

    [Fact]
    public async Task Discover_WithErrorTestCase_ShouldKeepItsTraitsSoFiltersCanStillExcludeIt()
    {
        // Act
        var testCase = Assert.IsType<PropertyTestCase>(
            await TestHost.Discover(typeof(Samples), nameof(Samples.Categorised_but_unsupported)));

        // Assert
        Assert.NotNull(testCase.Error);
        Assert.Equal(["Integration"], testCase.Traits["Category"]);
    }

    [Fact]
    public async Task Run_WithSkippedErrorTestCase_ShouldSkipItRatherThanFail()
    {
        // Arrange
        var attribute = new PropertyAttribute { Skip = "not yet" };

        // Act
        var messages = await TestHost.Run(typeof(Samples), nameof(Samples.Generic), attribute);

        // Assert
        var skipped = Assert.Single(messages.OfType<global::Xunit.Sdk.ITestSkipped>());
        Assert.Equal("not yet", skipped.Reason);
        Assert.Empty(messages.OfType<global::Xunit.Sdk.ITestFailed>());
    }
}

public sealed class PropertyTestCaseSerializationTests
{
    private sealed class Samples
    {
        public void Fine(int x) => _ = x;
    }

    [Fact]
    public async Task Serialization_WithEverySettingSet_ShouldRoundTripTheTestCase()
    {
        // Arrange
        var attribute = new PropertyAttribute { RunCount = 7, Seed = 9, Replay = "9:3", MaxShrinkAttempts = 3, MaxShrinkWork = 4, Generators = typeof(Samples) };
        var original = Assert.IsType<PropertyTestCase>(await Harness.TestHost.Discover(typeof(Samples), nameof(Samples.Fine), attribute));

        // Act
        var serialized = global::Xunit.Sdk.SerializationHelper.Instance.Serialize(original);
        var deserialized = Assert.IsType<PropertyTestCase>(global::Xunit.Sdk.SerializationHelper.Instance.Deserialize(serialized));

        // Assert
        Assert.Equal(original.UniqueID, deserialized.UniqueID);
        Assert.Equal(original.TestCaseDisplayName, deserialized.TestCaseDisplayName);
        Assert.Equal(original.Options, deserialized.Options);
        Assert.Equal(typeof(Samples), deserialized.Generators);
        Assert.Equal(nameof(Samples.Fine), deserialized.TestMethod.MethodName);
        Assert.Null(deserialized.Error);
    }

    [Fact]
    public void SerializedCheckOptions_WithEveryOptionNonDefault_ShouldRoundTripEveryOption()
    {
        // Arrange
        var options = new CheckOptions
        {
            RunCount = 7,
            Seed = 9,
            Replay = new Replay(9, 3),
            MaxDiscardRatio = 3,
            MaxShrinkAttempts = 5,
            MaxShrinkWork = 6,
            CoverageConfidence = new Confidence { Certainty = 1_000_000, Tolerance = 0.8 }
        };
        Assert.NotEqual(CheckOptions.Default, options);

        // Act
        var serialized = global::Xunit.Sdk.SerializationHelper.Instance.Serialize(new SerializedCheckOptions(options));
        var deserialized = Assert.IsType<SerializedCheckOptions>(global::Xunit.Sdk.SerializationHelper.Instance.Deserialize(serialized));

        // Assert
        Assert.Equal(options, deserialized.Options);
    }

    [Fact]
    public async Task Serialization_WithErrorTestCase_ShouldRoundTripTheErrorSoTheExecutionProcessReportsIt()
    {
        // Arrange
        var original = Assert.IsType<PropertyTestCase>(
            await Harness.TestHost.Discover(typeof(Samples), nameof(Samples.Fine), new PropertyAttribute { RunCount = -1 }));

        // Act
        var serialized = global::Xunit.Sdk.SerializationHelper.Instance.Serialize(original);
        var deserialized = Assert.IsType<PropertyTestCase>(global::Xunit.Sdk.SerializationHelper.Instance.Deserialize(serialized));

        // Assert
        Assert.Equal(original.Error, deserialized.Error);
        Assert.Contains("RunCount", deserialized.Error);
    }

    [Fact]
    public async Task Serialization_WithDefaultSettings_ShouldRoundTripTheDefaults()
    {
        // Arrange
        var original = Assert.IsType<PropertyTestCase>(await Harness.TestHost.Discover(typeof(Samples), nameof(Samples.Fine)));

        // Act
        var serialized = global::Xunit.Sdk.SerializationHelper.Instance.Serialize(original);
        var deserialized = Assert.IsType<PropertyTestCase>(global::Xunit.Sdk.SerializationHelper.Instance.Deserialize(serialized));

        // Assert
        Assert.Equal(CheckOptions.Default, deserialized.Options);
        Assert.Null(deserialized.Generators);
    }
}
