namespace QuickCheck;

/// <summary>
/// The exception that is thrown by <see cref="Property{T}.Assert"/> and
/// <see cref="PropertyResult{T}.ThrowIfFailed()"/> when a property is falsified, a check is
/// exhausted, a coverage requirement is not met, or a generator throws while producing an example.
/// </summary>
/// <remarks>
/// <see cref="Exception.Message"/> contains the full report of the check, and
/// <see cref="Exception.InnerException"/> is the exception the minimal counterexample raised, or
/// the one a generator threw, if there was one.
/// </remarks>
public sealed class PropertyFailedException : Exception
{
    internal PropertyFailedException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
