namespace QuickCheck;

public sealed class TestResult<T>
{
    internal TestResult(T input, TestResultType type)
    {
        Type = type;
        Input = input;
    }

    public T Input { get; }

    public TestResultType Type { get; }

    public bool IsError => Type is TestResultType.Error;
}

public static class TestResult
{
    public static TestResult<T> CreateFailed<T>(T input)
    {
        return new TestResult<T>(input, TestResultType.Error);
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