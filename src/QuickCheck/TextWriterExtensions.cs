using System.Collections;

namespace QuickCheck;

public static class TextWriterExtensions
{
    public static void WriteTestResult<T>(this TextWriter output, TestResult<T> testResult)
    {
        switch (testResult.Type)
        {
            case TestResultType.Discard:
                output.WriteLine($"Input '{testResult.Input}' was discarded.");
                break;
            case TestResultType.Success:
                output.WriteLine("Test succeeded.");
                break;
            case TestResultType.Error:
                if (testResult.Input is IEnumerable enumerable)
                {
                    output.Write("Test failed with input '");
                    var en = enumerable.GetEnumerator();

                    output.Write('[');

                    object? prev = null;

                    while (en.MoveNext())
                    {
                        if (prev is not null)
                        {
                            output.Write($"{prev}, ");
                        }

                        prev = en.Current;
                    }

                    if (prev is not null)
                    {
                        output.Write(prev);
                    }

                    output.Write(']');
                    output.WriteLine("'.");
                }
                else
                {
                    output.WriteLine($"Test failed with input '{testResult.Input}'.");
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(testResult));
        }
    }
}