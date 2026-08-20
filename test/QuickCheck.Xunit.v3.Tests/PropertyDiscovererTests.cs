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
    public async Task Valid_methods_become_a_single_property_test_case_carrying_their_settings()
    {
        var attribute = new PropertyAttribute { RunCount = 7, Seed = 9, MaxShrinkAttempts = 3, MaxShrinkWork = 4, Generators = typeof(Samples) };

        var testCase = Assert.IsType<PropertyTestCase>(await TestHost.Discover(typeof(Samples), nameof(Samples.Fine), attribute));

        Assert.Equal(7, testCase.Options.RunCount);
        Assert.Equal(9UL, testCase.Options.Seed);
        Assert.Equal(3, testCase.Options.MaxShrinkAttempts);
        Assert.Equal(4, testCase.Options.MaxShrinkWork);
        Assert.Null(testCase.Options.Replay);
        Assert.Equal(typeof(Samples), testCase.Generators);
        Assert.Equal("QuickCheck.Xunit.Tests.PropertyDiscovererTests+Samples.Fine", testCase.TestCaseDisplayName);
    }

    [Fact]
    public async Task Unset_attribute_values_leave_the_library_defaults()
    {
        var testCase = Assert.IsType<PropertyTestCase>(await TestHost.Discover(typeof(Samples), nameof(Samples.Fine)));

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
    public async Task Invalid_methods_become_an_error_test_case_naming_the_problem(string method, string expectedMessage)
    {
        var testCase = Assert.IsType<PropertyTestCase>(await TestHost.Discover(typeof(Samples), method));

        Assert.StartsWith($"[Property] method Samples.{method}: ", testCase.Error);
        Assert.Contains(expectedMessage, testCase.Error);
    }

    [Theory]
    [InlineData("not-a-token", "not a valid replay token")]
    [InlineData("1:2:3", "not a valid replay token")]
    public async Task Invalid_replay_tokens_are_reported_at_discovery(string replay, string expectedMessage)
    {
        var testCase = Assert.IsType<PropertyTestCase>(
            await TestHost.Discover(typeof(Samples), nameof(Samples.Fine), new PropertyAttribute { Replay = replay }));

        Assert.Contains(expectedMessage, testCase.Error);
    }

    [Fact]
    public async Task Out_of_range_settings_are_reported_at_discovery()
    {
        var testCase = Assert.IsType<PropertyTestCase>(
            await TestHost.Discover(typeof(Samples), nameof(Samples.Fine), new PropertyAttribute { RunCount = -1 }));

        Assert.StartsWith($"[Property] method Samples.{nameof(Samples.Fine)}: ", testCase.Error);
        Assert.Contains("RunCount", testCase.Error);
    }

    [Fact]
    public async Task Error_test_cases_fail_when_run()
    {
        var messages = await TestHost.Run(typeof(Samples), nameof(Samples.Generic));

        var failed = Assert.Single(messages.OfType<global::Xunit.Sdk.ITestFailed>());
        Assert.Contains("generic methods are not supported", failed.Messages[0]);
    }

    [Fact]
    public async Task Error_test_cases_keep_their_traits_so_filters_can_still_exclude_them()
    {
        var testCase = Assert.IsType<PropertyTestCase>(
            await TestHost.Discover(typeof(Samples), nameof(Samples.Categorised_but_unsupported)));

        Assert.NotNull(testCase.Error);
        Assert.Equal(["Integration"], testCase.Traits["Category"]);
    }

    [Fact]
    public async Task A_skipped_property_is_skipped_even_when_it_could_not_be_run()
    {
        var messages = await TestHost.Run(
            typeof(Samples), nameof(Samples.Generic), new PropertyAttribute { Skip = "not yet" });

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
    public async Task Test_cases_round_trip_through_xunit_serialization_with_their_settings()
    {
        var attribute = new PropertyAttribute { RunCount = 7, Seed = 9, Replay = "9:3", MaxShrinkAttempts = 3, MaxShrinkWork = 4, Generators = typeof(Samples) };
        var original = Assert.IsType<PropertyTestCase>(await Harness.TestHost.Discover(typeof(Samples), nameof(Samples.Fine), attribute));

        var serialized = global::Xunit.Sdk.SerializationHelper.Instance.Serialize(original);
        var deserialized = Assert.IsType<PropertyTestCase>(global::Xunit.Sdk.SerializationHelper.Instance.Deserialize(serialized));

        Assert.Equal(original.UniqueID, deserialized.UniqueID);
        Assert.Equal(original.TestCaseDisplayName, deserialized.TestCaseDisplayName);
        Assert.Equal(original.Options, deserialized.Options);
        Assert.Equal(typeof(Samples), deserialized.Generators);
        Assert.Equal(nameof(Samples.Fine), deserialized.TestMethod.MethodName);
        Assert.Null(deserialized.Error);
    }

    [Fact]
    public async Task An_error_round_trips_so_the_execution_process_reports_it()
    {
        var original = Assert.IsType<PropertyTestCase>(
            await Harness.TestHost.Discover(typeof(Samples), nameof(Samples.Fine), new PropertyAttribute { RunCount = -1 }));

        var serialized = global::Xunit.Sdk.SerializationHelper.Instance.Serialize(original);
        var deserialized = Assert.IsType<PropertyTestCase>(global::Xunit.Sdk.SerializationHelper.Instance.Deserialize(serialized));

        Assert.Equal(original.Error, deserialized.Error);
        Assert.Contains("RunCount", deserialized.Error);
    }

    [Fact]
    public async Task Default_settings_round_trip()
    {
        var original = Assert.IsType<PropertyTestCase>(await Harness.TestHost.Discover(typeof(Samples), nameof(Samples.Fine)));

        var serialized = global::Xunit.Sdk.SerializationHelper.Instance.Serialize(original);
        var deserialized = Assert.IsType<PropertyTestCase>(global::Xunit.Sdk.SerializationHelper.Instance.Deserialize(serialized));

        Assert.Equal(CheckOptions.Default, deserialized.Options);
        Assert.Null(deserialized.Generators);
    }
}
