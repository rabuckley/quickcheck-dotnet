namespace QuickCheck;

public sealed class TestResult<T>
{
    internal TestResult(T input, TestResultType type)
    {
        Type = type;
        Input = input;
    }

    internal TestResult(T input, Exception exception)
    {
        Type = TestResultType.Error;
        Input = input;
        Exception = exception;
    }

    /// <summary>
    /// If the test failed by throwing an exception, this property will contain
    /// the thrown exception. Otherwise, it will be <see langword="null"/>.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// The input that caused the test to fail, if any.
    /// </summary>
    public T Input { get; }

    public TestResultType Type { get; }

    public bool IsError => Type is TestResultType.Error;

    public bool IsSuccess => Type is TestResultType.Success;

    public bool IsDiscard => Type is TestResultType.Discard;
}

internal static class TestResult
{
    public static TestResult<T> CreateFailed<T>(T input)
    {
        return new TestResult<T>(input, TestResultType.Error);
    }

    public static TestResult<T> CreateFailed<T>(T input, Exception exception)
    {
        return new TestResult<T>(input, exception);
    }

    public static TestResult<T> CreateSuccess<T>(T input)
    {
        return new TestResult<T>(input, TestResultType.Success);
    }

    public static TestResult<T> CreateDiscard<T>(T input)
    {
        return new TestResult<T>(input, TestResultType.Discard);
    }
}

public enum TestResultType
{
    None,
    Discard,
    Success,
    Error
}
