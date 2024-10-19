using System.Diagnostics.CodeAnalysis;

namespace QuickCheck.Exceptions;

/// <summary>
/// Thrown when an unexpected error occurs during the execution of a
/// <see cref="QuickChecker"/> test run.
/// </summary>
public sealed class QuickCheckRunException : Exception
{
    private QuickCheckRunException(string message) : base(message)
    {
    }

    private QuickCheckRunException(string message, Exception innerException) :
        base(message, innerException)
    {
    }

    [DoesNotReturn]
    internal static void Throw(string message)
    {
        throw new QuickCheckRunException(message);
    }

    [DoesNotReturn]
    internal static void Throw(string message, Exception innerException)
    {
        throw new QuickCheckRunException(message, innerException);
    }

    [DoesNotReturn]
    internal static void ThrowValidationFunctionThrew(
        string input,
        Exception exception)
    {
        Throw(
            $"The provided validation function threw on the output target method with input '{input}'.",
            exception);
    }
}
