namespace QuickCheck;

/// <summary>
/// The exception that is thrown by <see cref="Property{T}.Assert"/> and
/// <see cref="PropertyResult{T}.ThrowIfFailed()"/> when a property is falsified, a check is
/// exhausted, or a coverage requirement is not met.
/// </summary>
/// <remarks>
/// <see cref="Exception.Message"/> contains the full report of the check, and
/// <see cref="Exception.InnerException"/> is the exception the minimal counterexample raised, if it
/// raised one.
/// </remarks>
public sealed class PropertyFailedException : Exception
{
    internal PropertyFailedException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
